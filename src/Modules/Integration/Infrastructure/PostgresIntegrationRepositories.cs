using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Integration.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace IUMP.Modules.Integration.Infrastructure;

public sealed class PostgresCommandIdempotencyStore :
    ICommandIdempotencyStore,
    ITransactionalCommandIdempotencyStore
{
    private readonly NpgsqlDataSource _dataSource;
    public PostgresCommandIdempotencyStore(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<CommandRegistrationResult> RegisterOrReadAsync(
        CommandIdentity identity, byte[] fingerprint, string? target, TimeSpan lease,
        CancellationToken ct = default)
    {
        if (fingerprint.Length != 32)
            throw new ArgumentException("Fingerprint must be SHA-256.", nameof(fingerprint));
        var now = DateTime.UtcNow;
        var owner = string.IsNullOrWhiteSpace(target) ? "server-principal" : target;
        var pendingUntil = now.Add(lease);
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted, ct);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO integration.command_idempotency
                (command_idempotency_id,caller_user_id,operation_code,idempotency_key,
                 request_fingerprint,status,pending_owner,pending_until,attempt_count,
                 created_at,updated_at,expires_at,version)
            VALUES
                (@id,@caller,@operation,@key,@fingerprint,'Pending',@owner,@pending_until,
                 0,@now,@now,@expires_at,1)
            ON CONFLICT (caller_user_id,operation_code,idempotency_key) DO NOTHING
            """, connection, transaction);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("caller", identity.CallerUserId);
        insert.Parameters.AddWithValue("operation", identity.OperationCode);
        insert.Parameters.AddWithValue("key", identity.IdempotencyKey);
        insert.Parameters.AddWithValue("fingerprint", fingerprint);
        insert.Parameters.AddWithValue("owner", owner);
        insert.Parameters.AddWithValue("pending_until", pendingUntil);
        insert.Parameters.AddWithValue("now", now);
        insert.Parameters.AddWithValue("expires_at", now.AddHours(24));
        var created = await insert.ExecuteNonQueryAsync(ct) == 1;
        var record = await GetByIdentityAsync(connection, transaction, identity, ct)
            ?? throw new InvalidOperationException("COMMAND_REGISTRATION_FAILED");
        await transaction.CommitAsync(ct);
        if (!record.Fingerprint.SequenceEqual(fingerprint))
            return new CommandRegistrationResult(record, false, false, true, false);
        var inProgress = !created && record.Status == CommandIdempotencyStatus.Pending &&
            record.IsLeaseLive(now);
        return new CommandRegistrationResult(record, created,
            !created && record.Status == CommandIdempotencyStatus.Completed,
            false, inProgress);
    }

    public Task<CommandIdempotencyRecord?> GetAsync(Guid id, CancellationToken ct = default) =>
        QuerySingleAsync(CommandSelect + " WHERE command_idempotency_id=@id",
            command => command.Parameters.AddWithValue("id", id), ct);

    public async Task<CommandIdempotencyRecord?> TryReclaimExpiredAsync(
        Guid id, long expectedVersion, string owner, DateTime leaseUntilUtc,
        CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            UPDATE integration.command_idempotency
            SET pending_owner=@owner,pending_until=@lease_until,
                attempt_count=attempt_count+1,version=version+1,updated_at=now()
            WHERE command_idempotency_id=@id AND status='Pending'
              AND version=@version AND pending_until<=now()
            RETURNING command_idempotency_id,caller_user_id,operation_code,idempotency_key,
                      request_fingerprint,status,pending_owner,pending_until,attempt_count,
                      original_http_status,original_result_payload::text,stable_result_reference,
                      original_location,original_etag,created_at,updated_at,completed_at,expires_at,version
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("version", expectedVersion);
        command.Parameters.AddWithValue("owner", owner);
        command.Parameters.AddWithValue("lease_until", leaseUntilUtc.ToUniversalTime());
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapCommand(reader) : null;
    }

    public Task<CommandIdempotencyRecord?> CompleteAsync(
        Guid id, long expectedVersion, StoredHttpResult result, DateTime expiresAtUtc,
        CancellationToken ct = default) =>
        CompleteCoreAsync(id, expectedVersion, result, expiresAtUtc, null, ct);

    public Task<CommandIdempotencyRecord?> CompleteInTransactionAsync(
        Guid id, long expectedVersion, StoredHttpResult result, DateTime expiresAtUtc,
        IHostTransaction transaction, CancellationToken ct = default) =>
        CompleteCoreAsync(id, expectedVersion, result, expiresAtUtc,
            PostgresTransactionResolver.Require(transaction), ct);

    public async Task<int> RemoveExpiredAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            DELETE FROM integration.command_idempotency
            WHERE status='Pending' AND expires_at<=@now
            """, connection);
        command.Parameters.AddWithValue("now", nowUtc.ToUniversalTime());
        return await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<CommandIdempotencyRecord?> CompleteCoreAsync(
        Guid id, long expectedVersion, StoredHttpResult result, DateTime expiresAtUtc,
        PostgresHostTransaction? transaction, CancellationToken ct)
    {
        var owns = transaction is null;
        var connection = transaction?.Connection ?? await _dataSource.OpenConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand("""
                UPDATE integration.command_idempotency
                SET status='Completed',pending_owner=NULL,pending_until=NULL,
                    original_http_status=@status,original_result_payload=@payload,
                    stable_result_reference=@reference,original_location=@location,
                    original_etag=@etag,completed_at=now(),updated_at=now(),
                    expires_at=@expires_at,version=version+1
                WHERE command_idempotency_id=@id AND status='Pending' AND version=@version
                RETURNING command_idempotency_id,caller_user_id,operation_code,idempotency_key,
                          request_fingerprint,status,pending_owner,pending_until,attempt_count,
                          original_http_status,original_result_payload::text,stable_result_reference,
                          original_location,original_etag,created_at,updated_at,completed_at,expires_at,version
                """, connection, transaction?.Transaction);
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("version", expectedVersion);
            command.Parameters.AddWithValue("status", result.StatusCode);
            command.Parameters.Add(new NpgsqlParameter("payload", NpgsqlDbType.Jsonb)
            {
                Value = JsonSerializer.Serialize(new ResultDocument(
                    result.Body, result.OriginalCorrelationId))
            });
            command.Parameters.AddWithValue("reference", (object?)result.ResourceReference ?? DBNull.Value);
            command.Parameters.AddWithValue("location", (object?)result.Location ?? DBNull.Value);
            command.Parameters.AddWithValue("etag", (object?)result.ETag ?? DBNull.Value);
            command.Parameters.AddWithValue("expires_at", expiresAtUtc.ToUniversalTime());
            await using var reader = await command.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? MapCommand(reader) : null;
        }
        finally { if (owns) await connection.DisposeAsync(); }
    }

    private async Task<CommandIdempotencyRecord?> QuerySingleAsync(
        string sql, Action<NpgsqlCommand> bind, CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        bind(command);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapCommand(reader) : null;
    }

    private static async Task<CommandIdempotencyRecord?> GetByIdentityAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        CommandIdentity identity, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(CommandSelect + """
             WHERE caller_user_id=@caller AND operation_code=@operation AND idempotency_key=@key
             FOR UPDATE
            """, connection, transaction);
        command.Parameters.AddWithValue("caller", identity.CallerUserId);
        command.Parameters.AddWithValue("operation", identity.OperationCode);
        command.Parameters.AddWithValue("key", identity.IdempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapCommand(reader) : null;
    }

    private const string CommandSelect = """
        SELECT command_idempotency_id,caller_user_id,operation_code,idempotency_key,
               request_fingerprint,status,pending_owner,pending_until,attempt_count,
               original_http_status,original_result_payload::text,stable_result_reference,
               original_location,original_etag,created_at,updated_at,completed_at,expires_at,version
        FROM integration.command_idempotency
        """;

    private static CommandIdempotencyRecord MapCommand(NpgsqlDataReader reader)
    {
        StoredHttpResult? result = null;
        if (!reader.IsDBNull(9))
        {
            var document = JsonSerializer.Deserialize<ResultDocument>(reader.GetString(10))
                ?? new ResultDocument("{}", null);
            result = new StoredHttpResult(
                reader.GetInt32(9), document.Body,
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.IsDBNull(13) ? null : reader.GetString(13),
                document.OriginalCorrelationId);
        }
        return new CommandIdempotencyRecord(
            reader.GetGuid(0),
            new CommandIdentity(reader.GetGuid(1), reader.GetString(2), reader.GetString(3)),
            (byte[])reader[4],
            Enum.Parse<CommandIdempotencyStatus>(reader.GetString(5), false),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetDateTime(7).ToUniversalTime(),
            reader.GetInt32(8),
            result,
            reader.GetDateTime(14).ToUniversalTime(),
            reader.GetDateTime(15).ToUniversalTime(),
            reader.IsDBNull(16) ? null : reader.GetDateTime(16).ToUniversalTime(),
            reader.GetDateTime(17).ToUniversalTime(),
            reader.GetInt64(18));
    }

    private sealed record ResultDocument(string Body, string? OriginalCorrelationId);
}

public sealed class PostgresIntegrationRepositories :
    IIntegrationStore,
    IIntegrationDeliveryRepository,
    IInboxStateRepository,
    ITransactionalInboxRepository,
    ITransactionalOutboxWriter
{
    private readonly NpgsqlDataSource _dataSource;
    public PostgresIntegrationRepositories(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async ValueTask<bool> TryRecordInboxAsync(
        InboxMessageId id, CancellationToken cancellationToken)
    {
        var record = await ClaimAsync(id.ConsumerName, id.EventId, "unspecified",
            DateTime.UtcNow, "integration-store", TimeSpan.FromSeconds(30), cancellationToken);
        return record is not null;
    }

    public async ValueTask EnqueueOutboxAsync(
        OutboxMessageId id, IntegrationEventType eventType, int schemaVersion,
        CancellationToken cancellationToken)
    {
        await AddOutboxAsync(new OutboxDeliveryRecord(
            id.Value, eventType.Value, schemaVersion, "unspecified", DateTime.UtcNow),
            cancellationToken);
    }

    public async Task AddOutboxAsync(
        OutboxDeliveryRecord record, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await InsertOutboxAsync(record, connection, null, ct);
    }

    public async Task<OutboxDeliveryRecord?> ClaimAsync(
        DateTime nowUtc, string owner, TimeSpan lease, CancellationToken ct = default)
    {
        var leaseUntil = nowUtc.ToUniversalTime().Add(lease);
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted, ct);
        await using var command = new NpgsqlCommand("""
            WITH due AS (
              SELECT event_id FROM integration.outbox_event
              WHERE (status='Pending' AND next_attempt_at<=@now)
                 OR (status='Leased' AND lease_until<=@now)
              ORDER BY next_attempt_at,event_id
              FOR UPDATE SKIP LOCKED LIMIT 1
            )
            UPDATE integration.outbox_event o
            SET status='Leased',lease_owner=@owner,lease_until=@lease_until,
                attempt_count=attempt_count+1
            FROM due WHERE o.event_id=due.event_id
            RETURNING o.event_id,o.event_type,o.schema_version,o.payload_json::text,o.next_attempt_at,
                      o.status,o.attempt_count,o.lease_owner,o.lease_until,o.last_error
            """, connection, transaction);
        command.Parameters.AddWithValue("now", nowUtc.ToUniversalTime());
        command.Parameters.AddWithValue("owner", owner);
        command.Parameters.AddWithValue("lease_until", leaseUntil);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = await reader.ReadAsync(ct) ? MapOutbox(reader) : null;
        await reader.DisposeAsync();
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<OutboxDeliveryRecord?> RenewAsync(
        OutboxDeliveryRecord record, DateTime leaseUntilUtc, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            UPDATE integration.outbox_event
            SET lease_until=@lease_until
            WHERE event_id=@id AND status='Leased' AND lease_owner=@owner
              AND attempt_count=@attempt AND lease_until=@expected_lease
            RETURNING event_id,event_type,schema_version,payload_json::text,next_attempt_at,
                      status,attempt_count,lease_owner,lease_until,last_error
            """, connection);
        BindOutboxLease(command, record);
        command.Parameters.AddWithValue("lease_until", leaseUntilUtc.ToUniversalTime());
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapOutbox(reader) : null;
    }

    public Task MarkPublishedAsync(Guid eventId, CancellationToken ct = default) =>
        UpdateOutboxAsync("""
            UPDATE integration.outbox_event
            SET status='Published',published_at=now(),lease_owner=NULL,lease_until=NULL,last_error=NULL
            WHERE event_id=@id AND status IN ('Pending','Leased')
            """, eventId, null, null, ct);

    public Task RescheduleAsync(
        Guid eventId, DateTime availableAtUtc, string redactedError,
        CancellationToken ct = default) =>
        UpdateOutboxAsync("""
            UPDATE integration.outbox_event
            SET status='Pending',next_attempt_at=@at,last_error=@error,
                lease_owner=NULL,lease_until=NULL
            WHERE event_id=@id AND status='Leased'
            """, eventId, availableAtUtc, redactedError, ct);

    public Task MarkFailedAsync(
        Guid eventId, string redactedError, DateTime nowUtc, CancellationToken ct = default) =>
        UpdateOutboxAsync("""
            UPDATE integration.outbox_event
            SET status='Failed',last_error=@error,next_attempt_at=@at,
                lease_owner=NULL,lease_until=NULL
            WHERE event_id=@id AND status IN ('Pending','Leased')
            """, eventId, nowUtc, redactedError, ct);

    public async Task<OutboxDeliveryRecord?> GetAsync(
        Guid eventId, CancellationToken ct = default) =>
        await QuerySingleOutboxAsync("""
            SELECT event_id,event_type,schema_version,payload_json::text,next_attempt_at,
                   status,attempt_count,lease_owner,lease_until,last_error
            FROM integration.outbox_event WHERE event_id=@id
            """, command => command.Parameters.AddWithValue("id", eventId), ct);

    public async Task<InboxDeliveryRecord?> ClaimAsync(
        string consumerName, Guid eventId, string payloadHash, DateTime nowUtc,
        string owner, TimeSpan lease, CancellationToken ct = default)
    {
        var now = nowUtc.ToUniversalTime();
        var leaseUntil = now.Add(lease);
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted, ct);
        await using var select = new NpgsqlCommand("""
            SELECT consumer_name,event_id,payload_hash,status,attempt_count,pending_owner,
                   pending_until,last_error,received_at
            FROM integration.inbox_message
            WHERE consumer_name=@consumer AND event_id=@event_id
            FOR UPDATE
            """, connection, transaction);
        select.Parameters.AddWithValue("consumer", consumerName);
        select.Parameters.AddWithValue("event_id", eventId);
        await using var reader = await select.ExecuteReaderAsync(ct);
        InboxDeliveryRecord? existing = await reader.ReadAsync(ct) ? MapInbox(reader) : null;
        await reader.DisposeAsync();
        if (existing is not null)
        {
            if (!string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal))
                throw new InvalidOperationException("INBOX_PAYLOAD_HASH_CONFLICT");
            if (existing.Status is DeliveryStatus.Completed or DeliveryStatus.Failed ||
                (existing.LeaseUntilUtc.HasValue && existing.LeaseUntilUtc > now))
            {
                await transaction.CommitAsync(ct);
                return null;
            }
            await using var reclaim = new NpgsqlCommand("""
                UPDATE integration.inbox_message
                SET status='Processing',pending_owner=@owner,pending_until=@lease_until,
                    attempt_count=attempt_count+1,next_attempt_at=@now
                WHERE consumer_name=@consumer AND event_id=@event_id
                RETURNING consumer_name,event_id,payload_hash,status,attempt_count,pending_owner,
                          pending_until,last_error,received_at
                """, connection, transaction);
            BindInboxIdentity(reclaim, consumerName, eventId);
            reclaim.Parameters.AddWithValue("owner", owner);
            reclaim.Parameters.AddWithValue("lease_until", leaseUntil);
            reclaim.Parameters.AddWithValue("now", now);
            await using var reclaimedReader = await reclaim.ExecuteReaderAsync(ct);
            var reclaimed = await reclaimedReader.ReadAsync(ct) ? MapInbox(reclaimedReader) : null;
            await reclaimedReader.DisposeAsync();
            await transaction.CommitAsync(ct);
            return reclaimed;
        }
        await using var insert = new NpgsqlCommand("""
            INSERT INTO integration.inbox_message
                (consumer_name,event_id,payload_hash,status,received_at,pending_owner,
                 pending_until,attempt_count,next_attempt_at)
            VALUES (@consumer,@event_id,@hash,'Processing',@now,@owner,@lease_until,1,@now)
            RETURNING consumer_name,event_id,payload_hash,status,attempt_count,pending_owner,
                      pending_until,last_error,received_at
            """, connection, transaction);
        BindInboxIdentity(insert, consumerName, eventId);
        insert.Parameters.AddWithValue("hash", payloadHash);
        insert.Parameters.AddWithValue("now", now);
        insert.Parameters.AddWithValue("owner", owner);
        insert.Parameters.AddWithValue("lease_until", leaseUntil);
        await using var insertedReader = await insert.ExecuteReaderAsync(ct);
        var inserted = await insertedReader.ReadAsync(ct) ? MapInbox(insertedReader) : null;
        await insertedReader.DisposeAsync();
        await transaction.CommitAsync(ct);
        return inserted;
    }

    public Task CompleteAsync(InboxDeliveryRecord record, CancellationToken ct = default) =>
        UpdateInboxAsync(record, """
            UPDATE integration.inbox_message
            SET status='Completed',completed_at=now(),pending_owner=NULL,pending_until=NULL,
                retention_until=now()+interval '24 hours',last_error=NULL
            WHERE consumer_name=@consumer AND event_id=@event_id AND status='Processing'
              AND pending_owner=@owner AND pending_until=@expected_lease
            """, null, null, ct);

    public Task RescheduleAsync(
        InboxDeliveryRecord record, DateTime availableAtUtc, string redactedError,
        CancellationToken ct = default) =>
        UpdateInboxAsync(record, """
            UPDATE integration.inbox_message
            SET status='Processing',next_attempt_at=@at,last_error=@error,
                pending_owner=NULL,pending_until=NULL
            WHERE consumer_name=@consumer AND event_id=@event_id AND status='Processing'
              AND pending_owner=@owner AND pending_until=@expected_lease
            """, availableAtUtc, redactedError, ct);

    public Task MarkFailedAsync(
        InboxDeliveryRecord record, string redactedError, DateTime nowUtc,
        CancellationToken ct = default) =>
        UpdateInboxAsync(record, """
            UPDATE integration.inbox_message
            SET status='Failed',last_error=@error,next_attempt_at=@at,
                pending_owner=NULL,pending_until=NULL
            WHERE consumer_name=@consumer AND event_id=@event_id AND status='Processing'
              AND pending_owner=@owner AND pending_until=@expected_lease
            """, nowUtc, redactedError, ct);

    public async Task<InboxDeliveryRecord?> GetInboxAsync(
        string consumerName, Guid eventId, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT consumer_name,event_id,payload_hash,status,attempt_count,pending_owner,
                   pending_until,last_error,received_at
            FROM integration.inbox_message
            WHERE consumer_name=@consumer AND event_id=@event_id
            """, connection);
        BindInboxIdentity(command, consumerName, eventId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapInbox(reader) : null;
    }

    async Task ITransactionalInboxRepository.CompleteAsync(
        InboxDeliveryRecord record, IHostTransaction transaction, CancellationToken ct)
    {
        var tx = PostgresTransactionResolver.Require(transaction);
        await using var command = new NpgsqlCommand("""
            UPDATE integration.inbox_message
            SET status='Completed',completed_at=now(),pending_owner=NULL,pending_until=NULL,
                retention_until=now()+interval '24 hours',last_error=NULL
            WHERE consumer_name=@consumer AND event_id=@event_id AND status='Processing'
              AND pending_owner=@owner AND pending_until=@expected_lease
            """, tx.Connection, tx.Transaction);
        BindInboxRecord(command, record);
        if (await command.ExecuteNonQueryAsync(ct) != 1)
            throw new InvalidOperationException("INBOX_COMPLETION_CONFLICT");
    }

    public async ValueTask AcquireLockAsync(
        IHostTransaction transaction, LockRequest request, CancellationToken ct = default)
    {
        if (request.Target != LockTarget.IntegrationOutbox)
            throw new InvalidOperationException("INTEGRATION_LOCK_TARGET_INVALID");
        var tx = PostgresTransactionResolver.Require(transaction);
        await using var command = new NpgsqlCommand("""
            SELECT pg_advisory_xact_lock(hashtextextended(@key,@target))
            """, tx.Connection, tx.Transaction);
        command.Parameters.AddWithValue("key", request.Id);
        command.Parameters.AddWithValue("target", (long)request.Target);
        _ = await command.ExecuteScalarAsync(ct);
    }

    public async ValueTask EnqueueAsync(
        OwnerEventEnvelope envelope, IHostTransaction hostTransaction,
        CancellationToken ct = default)
    {
        var tx = PostgresTransactionResolver.Require(hostTransaction);
        var payload = JsonSerializer.Serialize(envelope);
        await using var command = new NpgsqlCommand("""
            INSERT INTO integration.outbox_event
                (event_id,event_type,schema_version,occurred_at,payload_json,status,next_attempt_at)
            VALUES (@id,@type,@version,@occurred,@payload,'Pending',@occurred)
            ON CONFLICT (event_id) DO NOTHING
            """, tx.Connection, tx.Transaction);
        command.Parameters.AddWithValue("id", envelope.EventId);
        command.Parameters.AddWithValue("type", envelope.EventType);
        command.Parameters.AddWithValue("version", envelope.SchemaVersion);
        command.Parameters.AddWithValue("occurred", envelope.OccurredAt);
        command.Parameters.Add(new NpgsqlParameter("payload", NpgsqlDbType.Jsonb) { Value = payload });
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task InsertOutboxAsync(
        OutboxDeliveryRecord record, NpgsqlConnection connection,
        NpgsqlTransaction? transaction, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO integration.outbox_event
                (event_id,event_type,schema_version,occurred_at,payload_json,status,attempt_count,
                 next_attempt_at,lease_owner,lease_until,last_error)
            VALUES (@id,@type,@version,@occurred,@payload,@status,@attempt_count,@available,
                    @lease_owner,@lease_until,@error)
            ON CONFLICT (event_id) DO NOTHING
            """, connection, transaction);
        command.Parameters.AddWithValue("id", record.EventId);
        command.Parameters.AddWithValue("type", record.EventType);
        command.Parameters.AddWithValue("version", record.SchemaVersion);
        command.Parameters.AddWithValue("occurred", record.AvailableAtUtc.ToUniversalTime());
        command.Parameters.Add(new NpgsqlParameter("payload", NpgsqlDbType.Jsonb)
        {
            Value = JsonSerializer.Serialize(new OutboxDocument(
                record.PayloadHash, record.CorrelationId, record.CausationId))
        });
        command.Parameters.AddWithValue("status", ToDatabaseStatus(record.Status));
        command.Parameters.AddWithValue("attempt_count", record.AttemptCount);
        command.Parameters.AddWithValue("available", record.AvailableAtUtc.ToUniversalTime());
        command.Parameters.AddWithValue("lease_owner", (object?)record.LeaseOwner ?? DBNull.Value);
        command.Parameters.AddWithValue("lease_until", (object?)record.LeaseUntilUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("error", (object?)record.Error ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task UpdateOutboxAsync(
        string sql, Guid id, DateTime? at, string? error, CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        if (at.HasValue) command.Parameters.AddWithValue("at", at.Value.ToUniversalTime());
        if (error is not null) command.Parameters.AddWithValue("error", Redact(error));
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task UpdateInboxAsync(
        InboxDeliveryRecord record, string sql, DateTime? at, string? error, CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        BindInboxRecord(command, record);
        if (at.HasValue) command.Parameters.AddWithValue("at", at.Value.ToUniversalTime());
        if (error is not null) command.Parameters.AddWithValue("error", Redact(error));
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<OutboxDeliveryRecord?> QuerySingleOutboxAsync(
        string sql, Action<NpgsqlCommand> bind, CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        bind(command);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapOutbox(reader) : null;
    }

    private static OutboxDeliveryRecord MapOutbox(NpgsqlDataReader reader)
    {
        var payload = reader.GetString(3);
        var document = JsonSerializer.Deserialize<OutboxDocument>(payload);
        var payloadHash = string.IsNullOrWhiteSpace(document?.PayloadHash)
            ? Hash(payload)
            : document.PayloadHash;
        return new OutboxDeliveryRecord(
            reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2),
            payloadHash, reader.GetDateTime(4).ToUniversalTime(),
            FromDatabaseStatus(reader.GetString(5)), reader.GetInt32(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(7) ? null : LeaseToken(reader.GetGuid(0), reader.GetInt32(6)),
            reader.IsDBNull(8) ? null : reader.GetDateTime(8).ToUniversalTime(),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            document?.CorrelationId, document?.CausationId, reader.GetInt32(6) + 1L);
    }

    private static InboxDeliveryRecord MapInbox(NpgsqlDataReader reader) => new(
        reader.GetString(0), reader.GetGuid(1), reader.GetString(2),
        reader.GetDateTime(8).ToUniversalTime(),
        FromInboxStatus(reader.GetString(3)), reader.GetInt32(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.IsDBNull(5) ? null : LeaseToken(reader.GetGuid(1), reader.GetInt32(4)),
        reader.IsDBNull(6) ? null : reader.GetDateTime(6).ToUniversalTime(),
        reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetInt32(4) + 1L);

    private static void BindOutboxLease(NpgsqlCommand command, OutboxDeliveryRecord record)
    {
        command.Parameters.AddWithValue("id", record.EventId);
        command.Parameters.AddWithValue("owner", record.LeaseOwner!);
        command.Parameters.AddWithValue("attempt", record.AttemptCount);
        command.Parameters.AddWithValue("expected_lease", record.LeaseUntilUtc!.Value);
    }

    private static void BindInboxIdentity(NpgsqlCommand command, string consumerName, Guid eventId)
    {
        command.Parameters.AddWithValue("consumer", consumerName);
        command.Parameters.AddWithValue("event_id", eventId);
    }

    private static void BindInboxRecord(NpgsqlCommand command, InboxDeliveryRecord record)
    {
        BindInboxIdentity(command, record.ConsumerName, record.EventId);
        command.Parameters.AddWithValue("owner", record.LeaseOwner!);
        command.Parameters.AddWithValue("expected_lease", record.LeaseUntilUtc!.Value);
    }

    private static string ToDatabaseStatus(DeliveryStatus status) => status switch
    {
        DeliveryStatus.Claimed => "Leased",
        DeliveryStatus.Completed => "Published",
        _ => status.ToString()
    };

    private static DeliveryStatus FromDatabaseStatus(string status) => status switch
    {
        "Leased" => DeliveryStatus.Claimed,
        _ => Enum.Parse<DeliveryStatus>(status, false)
    };

    private static DeliveryStatus FromInboxStatus(string status) => status switch
    {
        "Processing" => DeliveryStatus.Claimed,
        _ => Enum.Parse<DeliveryStatus>(status, false)
    };

    private static Guid LeaseToken(Guid id, int attempt)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{id:D}:{attempt}"));
        return new Guid(digest.AsSpan(0, 16));
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Redact(string value) =>
        value.Length <= 256 ? value : value[..256];

    private sealed record OutboxDocument(
        string PayloadHash, string? CorrelationId, string? CausationId);
}
