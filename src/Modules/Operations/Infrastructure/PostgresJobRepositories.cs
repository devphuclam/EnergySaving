using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IUMP.Modules.Operations.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace IUMP.Modules.Operations.Infrastructure;

public sealed class PostgresJobRepositories :
    IOperationsStore,
    IDurableJobScheduler,
    IJobClaimRepository,
    IAuditDeliveryOperationsRepository
{
    private readonly NpgsqlDataSource _dataSource;
    public PostgresJobRepositories(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async ValueTask EnqueueJobAsync(
        JobId id, JobType jobType, IdempotencyKey idempotencyKey,
        CancellationToken cancellationToken)
    {
        var payload = SafeJobPayload.Create("{}");
        _ = await EnqueueCoreAsync(id, jobType, idempotencyKey, payload,
            DateTime.UtcNow, cancellationToken);
    }

    public async Task<JobScheduleResult> EnqueueAsync(
        JobType jobType, IdempotencyKey idempotencyKey, SafeJobPayload safePayload,
        DateTime availableAtUtc, CancellationToken ct = default)
    {
        var existing = await GetAsync(jobType, idempotencyKey, ct);
        if (existing.Code != "NOT_FOUND")
            return existing.Job.Payload.Fingerprint == safePayload.Fingerprint
                ? JobScheduleResult.Existing(existing.Job)
                : JobScheduleResult.Conflicting(existing.Job);
        try
        {
            var created = await EnqueueCoreAsync(
                new JobId(Guid.NewGuid()), jobType, idempotencyKey, safePayload,
                availableAtUtc, ct);
            return JobScheduleResult.CreatedJob(created);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            var winner = await GetAsync(jobType, idempotencyKey, ct);
            return winner.Job.Payload.Fingerprint == safePayload.Fingerprint
                ? JobScheduleResult.Existing(winner.Job)
                : JobScheduleResult.Conflicting(winner.Job);
        }
    }

    public async Task<JobScheduleResult> GetAsync(
        JobType jobType, IdempotencyKey idempotencyKey, CancellationToken ct = default)
    {
        var job = await QuerySingleAsync("""
            SELECT job_id,job_type,idempotency_key,payload_json,status,available_at,
                   attempt_count,lease_owner,lease_until,created_at,completed_at,last_error
            FROM operations.job
            WHERE job_type=@job_type AND idempotency_key=@idempotency_key
            """, command =>
        {
            command.Parameters.AddWithValue("job_type", jobType.Value);
            command.Parameters.AddWithValue("idempotency_key", idempotencyKey.Value);
        }, ct);
        return job is null
            ? new JobScheduleResult(Missing(jobType, idempotencyKey), false, false, false, "NOT_FOUND")
            : JobScheduleResult.Existing(job);
    }

    public async Task<bool> CancelAsync(
        JobId jobId, long expectedVersion, DateTime nowUtc, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            UPDATE operations.job
            SET status='Failed',last_error='CANCELLED',completed_at=@now,
                lease_owner=NULL,lease_until=NULL
            WHERE job_id=@id AND attempt_count+1=@version
              AND status IN ('Pending','Leased')
            """, connection);
        command.Parameters.AddWithValue("id", jobId.Value);
        command.Parameters.AddWithValue("version", expectedVersion);
        command.Parameters.AddWithValue("now", nowUtc.ToUniversalTime());
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task<IReadOnlyList<JobClaim>> ClaimDueAsync(
        DateTime nowUtc, string owner, int maxCount = 1, CancellationToken ct = default)
    {
        var leaseUntil = nowUtc.ToUniversalTime().AddSeconds(30);
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted, ct);
        await using var command = new NpgsqlCommand("""
            WITH due AS (
              SELECT job_id
              FROM operations.job
              WHERE (status='Pending' AND available_at<=@now)
                 OR (status='Leased' AND lease_until<=@now)
              ORDER BY available_at,job_id
              FOR UPDATE SKIP LOCKED
              LIMIT @max_count
            )
            UPDATE operations.job j
            SET status='Leased',lease_owner=@owner,lease_until=@lease_until,
                attempt_count=attempt_count+1
            FROM due
            WHERE j.job_id=due.job_id
            RETURNING j.job_id,j.job_type,j.idempotency_key,j.payload_json,j.status,j.available_at,
                      j.attempt_count,j.lease_owner,j.lease_until,j.created_at,j.completed_at,j.last_error
            """, connection, transaction);
        command.Parameters.AddWithValue("now", nowUtc.ToUniversalTime());
        command.Parameters.AddWithValue("owner", owner);
        command.Parameters.AddWithValue("lease_until", leaseUntil);
        command.Parameters.AddWithValue("max_count", Math.Clamp(maxCount, 1, 100));
        await using var reader = await command.ExecuteReaderAsync(ct);
        var claims = new List<JobClaim>();
        while (await reader.ReadAsync(ct))
        {
            var job = MapJob(reader);
            claims.Add(new JobClaim(job, owner, LeaseToken(job), leaseUntil));
        }
        await reader.DisposeAsync();
        await transaction.CommitAsync(ct);
        return claims;
    }

    public Task<JobOperationResult> RenewAsync(
        JobClaim claim, DateTime nowUtc, CancellationToken ct = default) =>
        MutateClaimAsync(claim, """
            UPDATE operations.job
            SET lease_until=@lease_until
            WHERE job_id=@id AND status='Leased' AND lease_owner=@owner
              AND attempt_count+1=@version AND lease_until=@expected_lease
            """, command => command.Parameters.AddWithValue(
                "lease_until", nowUtc.ToUniversalTime().AddSeconds(30)), "RENEWED", ct);

    public async Task<JobOperationResult> CompleteAsync(
        JobClaim claim, DateTime nowUtc, CancellationToken ct = default)
    {
        var result = await MutateClaimAsync(claim, """
            UPDATE operations.job
            SET status='Completed',completed_at=@now,lease_owner=NULL,lease_until=NULL,last_error=NULL
            WHERE job_id=@id AND status='Leased' AND lease_owner=@owner
              AND attempt_count+1=@version AND lease_until=@expected_lease
            """, command => command.Parameters.AddWithValue("now", nowUtc.ToUniversalTime()),
            "COMPLETED", ct);
        if (result.Succeeded ||
            result.Code == "LEASE_TOKEN_MISMATCH")
            return result;

        var current = await QuerySingleAsync("""
            SELECT job_id,job_type,idempotency_key,payload_json,status,available_at,
                   attempt_count,lease_owner,lease_until,created_at,completed_at,last_error
            FROM operations.job
            WHERE job_id=@id
            """, command => command.Parameters.AddWithValue("id", claim.Job.Id.Value), ct);
        return current is { Status: JobState.Completed } &&
               current.AttemptCount == claim.Job.AttemptCount
            ? new JobOperationResult(true, true, "ALREADY_COMPLETED", current)
            : result;
    }

    public Task<JobOperationResult> RescheduleAsync(
        JobClaim claim, DateTime availableAtUtc, string redactedError,
        DateTime nowUtc, CancellationToken ct = default) =>
        MutateClaimAsync(claim, """
            UPDATE operations.job
            SET status='Pending',available_at=@available_at,last_error=@error,
                lease_owner=NULL,lease_until=NULL
            WHERE job_id=@id AND status='Leased' AND lease_owner=@owner
              AND attempt_count+1=@version AND lease_until=@expected_lease
            """, command =>
        {
            command.Parameters.AddWithValue("available_at", availableAtUtc.ToUniversalTime());
            command.Parameters.AddWithValue("error", Redact(redactedError));
        }, "RESCHEDULED", ct);

    public Task<JobOperationResult> FailAsync(
        JobClaim claim, string redactedError, DateTime nowUtc, CancellationToken ct = default) =>
        MutateClaimAsync(claim, """
            UPDATE operations.job
            SET status='Failed',completed_at=@now,last_error=@error,
                lease_owner=NULL,lease_until=NULL
            WHERE job_id=@id AND status='Leased' AND lease_owner=@owner
              AND attempt_count+1=@version AND lease_until=@expected_lease
            """, command =>
        {
            command.Parameters.AddWithValue("now", nowUtc.ToUniversalTime());
            command.Parameters.AddWithValue("error", Redact(redactedError));
        }, "FAILED", ct);

    public Task<JobOperationResult> ReleaseAsync(
        JobClaim claim, DateTime availableAtUtc, DateTime nowUtc, CancellationToken ct = default) =>
        MutateClaimAsync(claim, """
            UPDATE operations.job
            SET status='Pending',available_at=@available_at,lease_owner=NULL,lease_until=NULL
            WHERE job_id=@id AND status='Leased' AND lease_owner=@owner
              AND attempt_count+1=@version AND lease_until=@expected_lease
            """, command => command.Parameters.AddWithValue(
                "available_at", availableAtUtc.ToUniversalTime()), "RELEASED", ct);

    public async Task<IReadOnlyList<DurableJob>> ListExpiredAsync(
        DateTime nowUtc, CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT job_id,job_type,idempotency_key,payload_json,status,available_at,
                   attempt_count,lease_owner,lease_until,created_at,completed_at,last_error
            FROM operations.job
            WHERE status='Leased' AND lease_until<=@now
            ORDER BY lease_until,job_id
            """, command => command.Parameters.AddWithValue("now", nowUtc.ToUniversalTime()), ct);

    public async Task<JobOperationResult> ReplayAsync(
        JobId jobId, string operatorId, DateTime nowUtc, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            UPDATE operations.job
            SET status='Pending',available_at=@now,completed_at=NULL,lease_owner=NULL,lease_until=NULL,
                last_error=@reason
            WHERE job_id=@id AND status='Failed'
            """, connection);
        command.Parameters.AddWithValue("id", jobId.Value);
        command.Parameters.AddWithValue("now", nowUtc.ToUniversalTime());
        command.Parameters.AddWithValue("reason", $"REPLAY:{Redact(operatorId)}");
        var affected = await command.ExecuteNonQueryAsync(ct);
        return new JobOperationResult(affected == 1, false,
            affected == 1 ? "REPLAYED" : "NOT_REPLAYABLE");
    }

    public async Task<int> CountPublishedWithoutAuditAsync(
        DateTime nowUtc, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT count(*)::int
            FROM integration.outbox_event o
            WHERE o.status='Published'
              AND NOT EXISTS(SELECT 1 FROM audit.audit_event a WHERE a.source_event_id=o.event_id)
            """, connection);
        return (int)(await command.ExecuteScalarAsync(ct))!;
    }

    private async Task<DurableJob> EnqueueCoreAsync(
        JobId id, JobType type, IdempotencyKey key, SafeJobPayload payload,
        DateTime availableAtUtc, CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            INSERT INTO operations.job
                (job_id,job_type,payload_json,payload_version,status,idempotency_key,
                 available_at,attempt_count,created_at)
            VALUES (@id,@type,@payload,1,'Pending',@key,@available_at,0,@created_at)
            RETURNING job_id,job_type,idempotency_key,payload_json,status,available_at,
                      attempt_count,lease_owner,lease_until,created_at,completed_at,last_error
            """, connection);
        var created = DateTime.UtcNow;
        command.Parameters.AddWithValue("id", id.Value);
        command.Parameters.AddWithValue("type", type.Value);
        command.Parameters.AddWithValue("key", key.Value);
        command.Parameters.AddWithValue("available_at", availableAtUtc.ToUniversalTime());
        command.Parameters.AddWithValue("created_at", created);
        command.Parameters.Add(new NpgsqlParameter("payload", NpgsqlDbType.Jsonb)
        {
            Value = JsonSerializer.Serialize(new PayloadDocument(payload.Value, payload.Fingerprint))
        });
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return MapJob(reader);
    }

    private async Task<JobOperationResult> MutateClaimAsync(
        JobClaim claim, string sql, Action<NpgsqlCommand> extra, string code, CancellationToken ct)
    {
        if (claim.Token != LeaseToken(claim.Job))
            return new JobOperationResult(false, false, "LEASE_TOKEN_MISMATCH");
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", claim.Job.Id.Value);
        command.Parameters.AddWithValue("owner", claim.Owner);
        command.Parameters.AddWithValue("version", claim.Job.Version);
        command.Parameters.AddWithValue("expected_lease", claim.LeaseExpiresAtUtc.ToUniversalTime());
        extra(command);
        var affected = await command.ExecuteNonQueryAsync(ct);
        if (affected != 1)
            return new JobOperationResult(false, false, "LEASE_CONFLICT");
        var current = await QuerySingleAsync("""
            SELECT job_id,job_type,idempotency_key,payload_json,status,available_at,
                   attempt_count,lease_owner,lease_until,created_at,completed_at,last_error
            FROM operations.job
            WHERE job_id=@id
            """, query => query.Parameters.AddWithValue("id", claim.Job.Id.Value), ct)
            ?? throw new InvalidOperationException("JOB_MUTATION_RESULT_MISSING");
        return new JobOperationResult(true, false, code, current);
    }

    private async Task<DurableJob?> QuerySingleAsync(
        string sql, Action<NpgsqlCommand> bind, CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        bind(command);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapJob(reader) : null;
    }

    private async Task<IReadOnlyList<DurableJob>> QueryAsync(
        string sql, Action<NpgsqlCommand> bind, CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        bind(command);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var jobs = new List<DurableJob>();
        while (await reader.ReadAsync(ct)) jobs.Add(MapJob(reader));
        return jobs;
    }

    private static DurableJob MapJob(NpgsqlDataReader reader)
    {
        var payload = JsonSerializer.Deserialize<PayloadDocument>(reader.GetString(3))
            ?? new PayloadDocument("{}", string.Empty);
        var attempts = reader.GetInt32(6);
        var created = reader.GetDateTime(9).ToUniversalTime();
        DateTime? completed = reader.IsDBNull(10)
            ? null
            : reader.GetDateTime(10).ToUniversalTime();
        return new DurableJob(
            new JobId(reader.GetGuid(0)),
            new JobType(reader.GetString(1)),
            new IdempotencyKey(reader.GetString(2)),
            new SafeJobPayload(payload.Value, payload.Fingerprint),
            Enum.Parse<JobState>(reader.GetString(4), false),
            reader.GetDateTime(5).ToUniversalTime(),
            attempts,
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(7) ? null : DeterministicToken(reader.GetGuid(0), attempts),
            reader.IsDBNull(8) ? null : reader.GetDateTime(8).ToUniversalTime(),
            attempts + 1L,
            reader.IsDBNull(11) ? null : reader.GetString(11),
            created,
            completed ?? reader.GetDateTime(5).ToUniversalTime(),
            completed);
    }

    private static DurableJob Missing(JobType type, IdempotencyKey key) =>
        new(new JobId(Guid.Empty), type, key, new SafeJobPayload("{}", string.Empty),
            JobState.Failed, DateTime.UnixEpoch, 0, null, null, null, 0,
            null, DateTime.UnixEpoch, DateTime.UnixEpoch, null);

    private static Guid LeaseToken(DurableJob job) =>
        job.LeaseToken ?? DeterministicToken(job.Id.Value, job.AttemptCount);

    private static Guid DeterministicToken(Guid id, int attempt)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{id:D}:{attempt}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static string Redact(string value) =>
        string.IsNullOrWhiteSpace(value) ? "REDACTED" :
        value.Length <= 256 ? value : value[..256];

    private sealed record PayloadDocument(string Value, string Fingerprint);
}
