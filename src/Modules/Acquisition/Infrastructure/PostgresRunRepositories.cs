using IUMP.Modules.Acquisition.Contracts;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Infrastructure.Postgres;
using Npgsql;

namespace IUMP.Modules.Acquisition.Infrastructure;

public sealed class PostgresSimulatorRunUnitOfWork(
    PostgresTransactionContext hostTransactions,
    IHostTransactionFactory hostTransactionFactory) : ISimulatorRunUnitOfWork
{
    public async ValueTask<ISimulatorRunTransaction> BeginAsync(CancellationToken ct = default)
    {
        if (hostTransactions.Current is { IsCompleted: false } ambient)
            return new PostgresSimulatorRunTransaction(
                ambient.Connection, ambient.Transaction, ambient, ownsTransaction: false);
        var host = await hostTransactionFactory.BeginAsync(ct);
        var postgres = PostgresTransactionResolver.Require(host);
        return new PostgresSimulatorRunTransaction(
            postgres.Connection, postgres.Transaction, postgres, ownsTransaction: true);
    }
}

public sealed class PostgresSimulatorRunTransaction(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    PostgresHostTransaction hostTransaction,
    bool ownsTransaction) : ISimulatorRunTransaction, IHostTransactionAccessor
{
    private readonly List<SimulatorStartLock> _locks = [];
    private bool _completed;
    private bool _disposed;

    public Guid TransactionId { get; } = Guid.NewGuid();
    public string IsolationIntent => "REPEATABLE READ";
    public bool IsCompleted => _completed;
    public IReadOnlyList<SimulatorStartLock> LockTrace => _locks;
    public IHostTransaction InnerTransaction => hostTransaction;
    internal NpgsqlConnection Connection => connection;
    internal NpgsqlTransaction Transaction => transaction;

    public async ValueTask LockAsync(
        SimulatorStartLockTarget target,
        string key,
        CancellationToken ct = default)
    {
        if (_locks.Count > 0 && (int)target < (int)_locks[^1].Target)
            throw new InvalidOperationException("LOCK_ORDER_VIOLATION");
        var (table, column) = target switch
        {
            SimulatorStartLockTarget.OrganizationSite => ("organization.sites", "id"),
            SimulatorStartLockTarget.OrganizationArea => ("organization.areas", "id"),
            SimulatorStartLockTarget.OrganizationAsset => ("organization.assets", "id"),
            SimulatorStartLockTarget.OrganizationPoint => ("organization.measurement_points", "id"),
            SimulatorStartLockTarget.CatalogSourceMapping => ("catalog.source_point_mapping", "mapping_id"),
            SimulatorStartLockTarget.AcquisitionRun => ("acquisition.simulator_run", "run_id"),
            SimulatorStartLockTarget.IntegrationOutbox => ("integration.outbox_event", "event_id"),
            _ => throw new InvalidOperationException("LOCK_TARGET_INVALID")
        };
        var canonicalKey = key.Split('/', 2)[0];
        if (!Guid.TryParse(canonicalKey, out var id))
            throw new InvalidOperationException("LOCK_KEY_INVALID");
        await using var command = new NpgsqlCommand(
            $"SELECT {column} FROM {table} WHERE {column}=@id FOR UPDATE",
            connection, transaction);
        command.Parameters.AddWithValue("id", id);
        _ = await command.ExecuteScalarAsync(ct);
        _locks.Add(new SimulatorStartLock(target, key));
    }

    public async ValueTask CommitAsync(CancellationToken ct = default)
    {
        if (_completed) return;
        if (!ownsTransaction)
        {
            _completed = true;
            return;
        }
        try { await hostTransaction.CommitAsync(ct); }
        catch
        {
            try { await hostTransaction.RollbackAsync(CancellationToken.None); } catch { }
            throw;
        }
        finally { _completed = true; }
    }

    public async ValueTask RollbackAsync(CancellationToken ct = default)
    {
        if (_completed) return;
        try { await hostTransaction.RollbackAsync(ct); }
        finally { _completed = true; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (!_completed && ownsTransaction) await RollbackAsync();
        if (ownsTransaction) await hostTransaction.DisposeAsync();
        _disposed = true;
    }
}

public sealed class PostgresAcquisitionRunRepository : IAcquisitionRunRepository
{
    private readonly NpgsqlDataSource _dataSource;
    public PostgresAcquisitionRunRepository(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public Task<SimulatorRun?> GetAsync(Guid runId, CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT run_id,source_id,source_version,configuration_id,configuration_version,
                   algorithm_id,algorithm_version,status,version,generated_count,accepted_count,
                   rejected_count,latest_error_code,latest_error_message,created_at_utc,started_at_utc,
                   paused_at_utc,resumed_at_utc,stopped_at_utc,actor_id,actor_username,correlation_id,causation_id
            FROM acquisition.simulator_run WHERE run_id=@id
            """, command => command.Parameters.AddWithValue("id", runId), MapRun, ct);

    public Task<SimulatorRun?> GetCurrentBySourceAsync(Guid sourceId, CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT run_id,source_id,source_version,configuration_id,configuration_version,
                   algorithm_id,algorithm_version,status,version,generated_count,accepted_count,
                   rejected_count,latest_error_code,latest_error_message,created_at_utc,started_at_utc,
                   paused_at_utc,resumed_at_utc,stopped_at_utc,actor_id,actor_username,correlation_id,causation_id
            FROM acquisition.simulator_run
            WHERE source_id=@id AND status IN ('Running','Paused')
            ORDER BY created_at_utc DESC LIMIT 1
            """, command => command.Parameters.AddWithValue("id", sourceId), MapRun, ct);

    public async Task<IReadOnlyList<SimulatorRun>> ListRunningAsync(CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT run_id,source_id,source_version,configuration_id,configuration_version,
                   algorithm_id,algorithm_version,status,version,generated_count,accepted_count,
                   rejected_count,latest_error_code,latest_error_message,created_at_utc,started_at_utc,
                   paused_at_utc,resumed_at_utc,stopped_at_utc,actor_id,actor_username,correlation_id,causation_id
            FROM acquisition.simulator_run WHERE status='Running' ORDER BY run_id
            """, null, MapRun, ct);

    public async Task<IReadOnlyList<SimulatorRunPointState>> ListPointStatesAsync(
        Guid runId, CancellationToken ct = default) =>
        await QueryAsync(PointSelect + " WHERE run_id=@run_id ORDER BY point_id",
            command => command.Parameters.AddWithValue("run_id", runId), MapPoint, ct);

    public Task<SimulatorRunPointState?> GetPointStateAsync(
        Guid runId, Guid pointId, CancellationToken ct = default) =>
        QuerySingleAsync(PointSelect + " WHERE run_id=@run_id AND point_id=@point_id",
            command =>
        {
            command.Parameters.AddWithValue("run_id", runId);
            command.Parameters.AddWithValue("point_id", pointId);
        }, MapPoint, ct);

    public async Task CreateAsync(
        SimulatorRun run,
        IReadOnlyList<SimulatorRunPointState> points,
        ISimulatorRunTransaction transaction,
        CancellationToken ct = default)
    {
        var postgres = Require(transaction);
        try
        {
            await ExecuteAsync(postgres, """
                INSERT INTO acquisition.simulator_run
                    (run_id,source_id,source_version,configuration_id,configuration_version,
                     algorithm_id,algorithm_version,status,version,generated_count,accepted_count,
                     rejected_count,latest_error_code,latest_error_message,created_at_utc,started_at_utc,
                     paused_at_utc,resumed_at_utc,stopped_at_utc,actor_id,actor_username,correlation_id,causation_id)
                VALUES
                    (@run_id,@source_id,@source_version,@configuration_id,@configuration_version,
                     @algorithm_id,@algorithm_version,@status,@version,@generated,@accepted,@rejected,
                     @error_code,@error_message,@created,@started,@paused,@resumed,@stopped,
                     @actor_id,@actor_name,@correlation_id,@causation_id)
                """, command => BindRun(command, run), ct);
            foreach (var point in points)
                await ExecuteAsync(postgres, """
                    INSERT INTO acquisition.simulator_run_point_state
                        (run_id,point_id,point_version_at_start,mapping_id,mapping_version,metric_id,unit_id,
                         unit_code,source_version,next_source_sequence,prng_state,next_due_at_utc,site_id,area_id,
                         lease_owner,lease_token,lease_version,lease_expires_at_utc,version)
                    VALUES
                        (@run_id,@point_id,@point_version,@mapping_id,@mapping_version,@metric_id,@unit_id,
                         @unit_code,@source_version,@sequence,@prng,@due,@site_id,@area_id,@lease_owner,
                         @lease_token,@lease_version,@lease_expires,@version)
                    """, command => BindPoint(command, point), ct);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException("SIMULATOR_RUN_CONFLICT", exception);
        }
    }

    public async Task<SimulatorRun> ChangeStatusAsync(
        Guid runId, long expectedVersion, SimulatorRunStatus targetStatus,
        DateTime nowUtc, string? errorCode, string? errorMessage,
        ISimulatorRunTransaction transaction, CancellationToken ct = default)
    {
        var postgres = Require(transaction);
        var affected = await ExecuteAsync(postgres, """
            UPDATE acquisition.simulator_run
            SET status=@status,version=version+1,
                paused_at_utc=CASE WHEN @status='Paused' THEN @now ELSE paused_at_utc END,
                resumed_at_utc=CASE WHEN @status='Running' THEN @now ELSE resumed_at_utc END,
                stopped_at_utc=CASE WHEN @status='Stopped' THEN @now ELSE stopped_at_utc END,
                latest_error_code=@error_code,latest_error_message=@error_message
            WHERE run_id=@id AND version=@expected_version
            """, command =>
        {
            command.Parameters.AddWithValue("id", runId);
            command.Parameters.AddWithValue("expected_version", expectedVersion);
            command.Parameters.AddWithValue("status", targetStatus.ToString());
            command.Parameters.AddWithValue("now", nowUtc.ToUniversalTime());
            command.Parameters.AddWithValue("error_code", (object?)errorCode ?? DBNull.Value);
            command.Parameters.AddWithValue("error_message", (object?)errorMessage ?? DBNull.Value);
        }, ct);
        if (affected != 1) throw new InvalidOperationException("RUN_VERSION_CONFLICT");
        return await QuerySingleAsync(postgres, RunSelect + " WHERE run_id=@id",
            command => command.Parameters.AddWithValue("id", runId), MapRun, ct)
            ?? throw new InvalidOperationException("RUN_NOT_FOUND");
    }

    public async Task<SimulatorRunLease?> ClaimDuePointAsync(
        Guid runId, Guid pointId, string owner, DateTime nowUtc,
        DateTime leaseUntilUtc, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted, ct);
        await using var command = new NpgsqlCommand("""
            UPDATE acquisition.simulator_run_point_state p
            SET lease_owner=@owner,lease_token=@token,lease_version=lease_version+1,
                lease_expires_at_utc=@lease_until
            FROM acquisition.simulator_run r
            WHERE p.run_id=r.run_id AND p.run_id=@run_id AND p.point_id=@point_id
              AND r.status='Running' AND p.next_due_at_utc<=@now
              AND (p.lease_expires_at_utc IS NULL OR p.lease_expires_at_utc<=@now)
            RETURNING p.run_id,p.point_id,p.lease_owner,p.lease_token,p.lease_version,p.lease_expires_at_utc
            """, connection, transaction);
        var token = Guid.NewGuid();
        command.Parameters.AddWithValue("run_id", runId);
        command.Parameters.AddWithValue("point_id", pointId);
        command.Parameters.AddWithValue("owner", owner);
        command.Parameters.AddWithValue("token", token);
        command.Parameters.AddWithValue("now", nowUtc.ToUniversalTime());
        command.Parameters.AddWithValue("lease_until", leaseUntilUtc.ToUniversalTime());
        await using var reader = await command.ExecuteReaderAsync(ct);
        SimulatorRunLease? lease = null;
        if (await reader.ReadAsync(ct)) lease = MapLease(reader);
        await reader.DisposeAsync();
        await transaction.CommitAsync(ct);
        return lease;
    }

    public async Task<SimulatorRunLease?> RenewLeaseAsync(
        SimulatorRunLease lease, DateTime leaseUntilUtc, CancellationToken ct = default) =>
        await UpdateLeaseAsync("""
            UPDATE acquisition.simulator_run_point_state
            SET lease_version=lease_version+1,lease_expires_at_utc=@lease_until
            WHERE run_id=@run_id AND point_id=@point_id AND lease_owner=@owner
              AND lease_token=@token AND lease_version=@version
            RETURNING run_id,point_id,lease_owner,lease_token,lease_version,lease_expires_at_utc
            """, lease, leaseUntilUtc, ct);

    public async Task ReleaseLeaseAsync(SimulatorRunLease lease, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            UPDATE acquisition.simulator_run_point_state
            SET lease_owner=NULL,lease_token=NULL,lease_expires_at_utc=NULL,
                lease_version=lease_version+1
            WHERE run_id=@run_id AND point_id=@point_id AND lease_owner=@owner
              AND lease_token=@token AND lease_version=@version
            """, connection);
        BindLease(command, lease);
        _ = await command.ExecuteNonQueryAsync(ct);
    }

    public async Task StageReservationAsync(
        SimulatorRunPointReservationTransition transition,
        ISimulatorRunTransaction transaction,
        CancellationToken ct = default)
    {
        var postgres = Require(transaction);
        var pointAffected = await ExecuteAsync(postgres, """
            UPDATE acquisition.simulator_run_point_state
            SET prng_state=@prng,next_source_sequence=@next_sequence,next_due_at_utc=@next_due,
                version=version+1
            WHERE run_id=@run_id AND point_id=@point_id
              AND version=@point_version AND next_source_sequence=@expected_sequence
            """, command =>
        {
            command.Parameters.AddWithValue("run_id", transition.RunId);
            command.Parameters.AddWithValue("point_id", transition.PointId);
            command.Parameters.AddWithValue("point_version", transition.ExpectedPointStateVersion);
            command.Parameters.AddWithValue("expected_sequence", transition.ExpectedNextSourceSequence);
            command.Parameters.AddWithValue("prng", transition.ResultingPrngState);
            command.Parameters.AddWithValue("next_sequence", transition.NextSourceSequence);
            command.Parameters.AddWithValue("next_due", transition.NextDueAtUtc.ToUniversalTime());
        }, ct);
        var runAffected = await ExecuteAsync(postgres, """
            UPDATE acquisition.simulator_run
            SET generated_count=generated_count+1,version=version+1
            WHERE run_id=@run_id AND version=@expected_version AND status='Running'
            """, command =>
        {
            command.Parameters.AddWithValue("run_id", transition.RunId);
            command.Parameters.AddWithValue("expected_version", transition.ExpectedRunVersion);
        }, ct);
        if (pointAffected != 1 || runAffected != 1)
            throw new InvalidOperationException("RUN_POINT_VERSION_CONFLICT");
    }

    public async Task StageFinalCounterAsync(
        Guid runId, long expectedRunVersion, ProductionFinalClassification classification,
        ISimulatorRunTransaction transaction, CancellationToken ct = default)
    {
        var postgres = Require(transaction);
        var affected = await ExecuteAsync(postgres, """
            UPDATE acquisition.simulator_run
            SET accepted_count=accepted_count+CASE WHEN @classification='Accepted' THEN 1 ELSE 0 END,
                rejected_count=rejected_count+CASE WHEN @classification='Rejected' THEN 1 ELSE 0 END,
                version=version+1
            WHERE run_id=@run_id AND version=@expected_version
            """, command =>
        {
            command.Parameters.AddWithValue("run_id", runId);
            command.Parameters.AddWithValue("expected_version", expectedRunVersion);
            command.Parameters.AddWithValue("classification", classification.ToString());
        }, ct);
        if (affected != 1) throw new InvalidOperationException("RUN_VERSION_CONFLICT");
    }

    private async Task<SimulatorRunLease?> UpdateLeaseAsync(
        string sql, SimulatorRunLease lease, DateTime leaseUntilUtc, CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        BindLease(command, lease);
        command.Parameters.AddWithValue("lease_until", leaseUntilUtc.ToUniversalTime());
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapLease(reader) : null;
    }

    private const string RunSelect = """
        SELECT run_id,source_id,source_version,configuration_id,configuration_version,
               algorithm_id,algorithm_version,status,version,generated_count,accepted_count,
               rejected_count,latest_error_code,latest_error_message,created_at_utc,started_at_utc,
               paused_at_utc,resumed_at_utc,stopped_at_utc,actor_id,actor_username,correlation_id,causation_id
        FROM acquisition.simulator_run
        """;

    private const string PointSelect = """
        SELECT run_id,point_id,point_version_at_start,mapping_id,mapping_version,metric_id,unit_id,
               unit_code,source_version,next_source_sequence,prng_state,next_due_at_utc,site_id,area_id,
               lease_owner,lease_token,lease_version,lease_expires_at_utc,version
        FROM acquisition.simulator_run_point_state
        """;

    private static SimulatorRun MapRun(NpgsqlDataReader r) => new(
        r.GetGuid(0),r.GetGuid(1),r.GetInt64(2),r.GetGuid(3),r.GetInt64(4),r.GetString(5),
        r.GetInt32(6),Enum.Parse<SimulatorRunStatus>(r.GetString(7),false),r.GetInt64(8),
        r.GetInt64(9),r.GetInt64(10),r.GetInt64(11),
        r.IsDBNull(12)?null:r.GetString(12),r.IsDBNull(13)?null:r.GetString(13),
        Utc(r,14),Utc(r,15),UtcNullable(r,16),UtcNullable(r,17),UtcNullable(r,18),
        r.GetString(19),r.GetString(20),r.GetString(21),r.IsDBNull(22)?null:r.GetString(22));

    private static SimulatorRunPointState MapPoint(NpgsqlDataReader r) => new(
        r.GetGuid(0),r.GetGuid(1),r.GetInt64(2),r.GetGuid(3),r.GetInt64(4),r.GetGuid(5),r.GetGuid(6),
        r.GetString(7),r.GetInt64(8),r.GetInt64(9),(byte[])r[10],Utc(r,11),r.GetString(12),
        r.IsDBNull(13)?null:r.GetString(13),r.IsDBNull(14)?null:r.GetString(14),
        r.IsDBNull(15)?null:r.GetGuid(15),r.GetInt64(16),UtcNullable(r,17),r.GetInt64(18));

    private static SimulatorRunLease MapLease(NpgsqlDataReader r) => new(
        r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetGuid(3),r.GetInt64(4),Utc(r,5));

    private static void BindRun(NpgsqlCommand c,SimulatorRun v)
    {
        c.Parameters.AddWithValue("run_id",v.RunId);c.Parameters.AddWithValue("source_id",v.SourceId);
        c.Parameters.AddWithValue("source_version",v.SourceVersion);c.Parameters.AddWithValue("configuration_id",v.ConfigurationId);
        c.Parameters.AddWithValue("configuration_version",v.ConfigurationVersion);c.Parameters.AddWithValue("algorithm_id",v.AlgorithmId);
        c.Parameters.AddWithValue("algorithm_version",v.AlgorithmVersion);c.Parameters.AddWithValue("status",v.Status.ToString());
        c.Parameters.AddWithValue("version",v.Version);c.Parameters.AddWithValue("generated",v.GeneratedCount);
        c.Parameters.AddWithValue("accepted",v.AcceptedCount);c.Parameters.AddWithValue("rejected",v.RejectedCount);
        c.Parameters.AddWithValue("error_code",(object?)v.LatestErrorCode??DBNull.Value);
        c.Parameters.AddWithValue("error_message",(object?)v.LatestErrorMessage??DBNull.Value);
        c.Parameters.AddWithValue("created",v.CreatedAtUtc);c.Parameters.AddWithValue("started",v.StartedAtUtc);
        c.Parameters.AddWithValue("paused",(object?)v.PausedAtUtc??DBNull.Value);
        c.Parameters.AddWithValue("resumed",(object?)v.ResumedAtUtc??DBNull.Value);
        c.Parameters.AddWithValue("stopped",(object?)v.StoppedAtUtc??DBNull.Value);
        c.Parameters.AddWithValue("actor_id",v.ActorId);c.Parameters.AddWithValue("actor_name",v.ActorUsername);
        c.Parameters.AddWithValue("correlation_id",v.CorrelationId);
        c.Parameters.AddWithValue("causation_id",(object?)v.CausationId??DBNull.Value);
    }

    private static void BindPoint(NpgsqlCommand c,SimulatorRunPointState v)
    {
        c.Parameters.AddWithValue("run_id",v.RunId);c.Parameters.AddWithValue("point_id",v.PointId);
        c.Parameters.AddWithValue("point_version",v.PointVersionAtStart);c.Parameters.AddWithValue("mapping_id",v.MappingId);
        c.Parameters.AddWithValue("mapping_version",v.MappingVersion);c.Parameters.AddWithValue("metric_id",v.MetricId);
        c.Parameters.AddWithValue("unit_id",v.UnitId);c.Parameters.AddWithValue("unit_code",v.UnitCode);
        c.Parameters.AddWithValue("source_version",v.SourceVersion);c.Parameters.AddWithValue("sequence",v.NextSourceSequence);
        c.Parameters.AddWithValue("prng",v.PrngState);c.Parameters.AddWithValue("due",v.NextDueAtUtc);
        c.Parameters.AddWithValue("site_id",v.SiteId);c.Parameters.AddWithValue("area_id",(object?)v.AreaId??DBNull.Value);
        c.Parameters.AddWithValue("lease_owner",(object?)v.LeaseOwner??DBNull.Value);
        c.Parameters.AddWithValue("lease_token",(object?)v.LeaseToken??DBNull.Value);
        c.Parameters.AddWithValue("lease_version",v.LeaseVersion);
        c.Parameters.AddWithValue("lease_expires",(object?)v.LeaseExpiresAtUtc??DBNull.Value);
        c.Parameters.AddWithValue("version",v.Version);
    }

    private static void BindLease(NpgsqlCommand c,SimulatorRunLease v)
    {
        c.Parameters.AddWithValue("run_id",v.RunId);c.Parameters.AddWithValue("point_id",v.PointId);
        c.Parameters.AddWithValue("owner",v.Owner);c.Parameters.AddWithValue("token",v.Token);
        c.Parameters.AddWithValue("version",v.Version);
    }

    private static DateTime Utc(NpgsqlDataReader r,int i)=>r.GetDateTime(i).ToUniversalTime();
    private static DateTime? UtcNullable(NpgsqlDataReader r,int i)=>r.IsDBNull(i)?null:Utc(r,i);
    private static PostgresSimulatorRunTransaction Require(ISimulatorRunTransaction transaction)=>
        transaction as PostgresSimulatorRunTransaction ??
        throw new InvalidOperationException("POSTGRES_RUN_TRANSACTION_REQUIRED");

    private static async Task<int> ExecuteAsync(
        PostgresSimulatorRunTransaction tx,string sql,Action<NpgsqlCommand> bind,CancellationToken ct)
    {
        await using var command=new NpgsqlCommand(sql,tx.Connection,tx.Transaction);
        bind(command);return await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<T?> QuerySingleAsync<T>(
        string sql,Action<NpgsqlCommand> bind,Func<NpgsqlDataReader,T> map,CancellationToken ct)
    {
        await using var connection=await _dataSource.OpenConnectionAsync(ct);
        await using var command=new NpgsqlCommand(sql,connection);bind(command);
        await using var reader=await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)?map(reader):default;
    }

    private static async Task<T?> QuerySingleAsync<T>(
        PostgresSimulatorRunTransaction tx,string sql,Action<NpgsqlCommand> bind,
        Func<NpgsqlDataReader,T> map,CancellationToken ct)
    {
        await using var command=new NpgsqlCommand(sql,tx.Connection,tx.Transaction);bind(command);
        await using var reader=await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)?map(reader):default;
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,Action<NpgsqlCommand>? bind,Func<NpgsqlDataReader,T> map,CancellationToken ct)
    {
        await using var connection=await _dataSource.OpenConnectionAsync(ct);
        await using var command=new NpgsqlCommand(sql,connection);bind?.Invoke(command);
        await using var reader=await command.ExecuteReaderAsync(ct);
        var values=new List<T>();while(await reader.ReadAsync(ct))values.Add(map(reader));return values;
    }
}

public sealed class PostgresSimulatorProductionAttemptRepository : ISimulatorProductionAttemptRepository
{
    private readonly NpgsqlDataSource _dataSource;
    public PostgresSimulatorProductionAttemptRepository(NpgsqlDataSource dataSource)=>_dataSource=dataSource;

    public Task<SimulatorProductionAttempt?> GetPendingAsync(
        Guid runId,Guid pointId,CancellationToken ct=default)=>
        QuerySingleAsync(AttemptSelect+"""
             WHERE run_id=@run_id AND point_id=@point_id AND status='Pending'
             ORDER BY source_sequence LIMIT 1
            """,c=>{c.Parameters.AddWithValue("run_id",runId);c.Parameters.AddWithValue("point_id",pointId);},ct);

    public Task<SimulatorProductionAttempt?> GetAsync(
        Guid runId,Guid pointId,long sourceSequence,CancellationToken ct=default)=>
        QuerySingleAsync(AttemptSelect+"""
             WHERE run_id=@run_id AND point_id=@point_id AND source_sequence=@sequence
            """,c=>{c.Parameters.AddWithValue("run_id",runId);c.Parameters.AddWithValue("point_id",pointId);
                c.Parameters.AddWithValue("sequence",sourceSequence);},ct);

    public async Task<IReadOnlyList<SimulatorProductionAttempt>> ListPendingAsync(CancellationToken ct=default)
    {
        await using var connection=await _dataSource.OpenConnectionAsync(ct);
        await using var command=new NpgsqlCommand(AttemptSelect+" WHERE status='Pending' ORDER BY created_at_utc,run_id,point_id",connection);
        await using var reader=await command.ExecuteReaderAsync(ct);
        var values=new List<SimulatorProductionAttempt>();while(await reader.ReadAsync(ct))values.Add(MapAttempt(reader));return values;
    }

    public async Task<bool> TryReserveAsync(
        SimulatorProductionAttempt attempt,SimulatorRunPointReservationTransition transition,
        ISimulatorRunTransaction transaction,CancellationToken ct=default)
    {
        var tx=Require(transaction);
        await using var command=new NpgsqlCommand("""
            INSERT INTO acquisition.simulator_production_attempt
                (run_id,point_id,source_sequence,measurement_id,source_id,mapping_id,mapping_version,
                 algorithm_id,algorithm_version,configuration_id,configuration_version,source_timestamp_utc,
                 numeric_value,unit_code,producer_identity,correlation_id,lineage_id,status,created_at_utc,version)
            VALUES
                (@run_id,@point_id,@sequence,@measurement_id,@source_id,@mapping_id,@mapping_version,
                 @algorithm_id,@algorithm_version,@configuration_id,@configuration_version,@timestamp,
                 @value,@unit_code,@producer,@correlation_id,@lineage_id,'Pending',@created_at,@version)
            ON CONFLICT (run_id,point_id,source_sequence) DO NOTHING
            """,tx.Connection,tx.Transaction);
        BindAttempt(command,attempt);
        return await command.ExecuteNonQueryAsync(ct)==1;
    }

    public async Task<AttemptFinalizeResult> FinalizeAsync(
        Guid runId,Guid pointId,long sourceSequence,TelemetryDispatchResult result,
        DateTime completedAtUtc,ISimulatorRunTransaction transaction,CancellationToken ct=default)
    {
        var tx=Require(transaction);
        var existing=await QuerySingleAsync(tx,runId,pointId,sourceSequence,ct)
            ?? throw new InvalidOperationException("ATTEMPT_NOT_FOUND");
        if(existing.Status==SimulatorProductionAttemptStatus.Completed)
        {
            if(!Equivalent(existing,result))throw new InvalidOperationException("TERMINAL_RESULT_CONFLICT");
            return new AttemptFinalizeResult(existing,false,true);
        }
        var normalized=result with
        {
            CompletedAtUtc=result.CompletedAtUtc??completedAtUtc.ToUniversalTime(),
            OriginalCorrelationId=result.OriginalCorrelationId??existing.Payload.CorrelationId,
            OriginalLineageId=result.OriginalLineageId??existing.Payload.LineageId
        };
        TelemetryDispatchResultValidator.EnsureValid(existing.Payload,normalized);
        await using var command=new NpgsqlCommand("""
            UPDATE acquisition.simulator_production_attempt
            SET status='Completed',telemetry_outcome=@outcome,final_classification=@classification,
                measurement_persisted=@persisted,persisted_measurement_id=@persisted_id,
                quality_code=@quality,reason_code=@reason,latest_advanced=@latest,
                error_code=@error,rejection_code=@rejection,completed_at_utc=@completed,
                original_correlation_id=@original_correlation,original_lineage_id=@original_lineage,
                version=version+1
            WHERE run_id=@run_id AND point_id=@point_id AND source_sequence=@sequence
              AND status='Pending' AND version=@version
            """,tx.Connection,tx.Transaction);
        command.Parameters.AddWithValue("run_id",runId);command.Parameters.AddWithValue("point_id",pointId);
        command.Parameters.AddWithValue("sequence",sourceSequence);command.Parameters.AddWithValue("version",existing.Version);
        command.Parameters.AddWithValue("outcome",normalized.Outcome.ToString());
        command.Parameters.AddWithValue("classification",normalized.FinalClassification.ToString());
        command.Parameters.AddWithValue("persisted",(object?)normalized.MeasurementPersisted??DBNull.Value);
        command.Parameters.AddWithValue("persisted_id",(object?)normalized.PersistedMeasurementId??DBNull.Value);
        command.Parameters.AddWithValue("quality",(object?)normalized.QualityCode??DBNull.Value);
        command.Parameters.AddWithValue("reason",(object?)normalized.ReasonCode??DBNull.Value);
        command.Parameters.AddWithValue("latest",(object?)normalized.LatestAdvanced??DBNull.Value);
        command.Parameters.AddWithValue("error",(object?)normalized.ErrorCode??DBNull.Value);
        command.Parameters.AddWithValue("rejection",(object?)normalized.RejectionCode??DBNull.Value);
        command.Parameters.AddWithValue("completed",normalized.CompletedAtUtc!.Value);
        command.Parameters.AddWithValue("original_correlation",normalized.OriginalCorrelationId!);
        command.Parameters.AddWithValue("original_lineage",normalized.OriginalLineageId!);
        if(await command.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException("ATTEMPT_VERSION_CONFLICT");
        var completed=await QuerySingleAsync(tx,runId,pointId,sourceSequence,ct)
            ?? throw new InvalidOperationException("ATTEMPT_NOT_FOUND");
        return new AttemptFinalizeResult(completed,true,false);
    }

    private const string AttemptSelect="""
        SELECT run_id,point_id,source_sequence,measurement_id,source_id,mapping_id,mapping_version,
               algorithm_id,algorithm_version,configuration_id,configuration_version,source_timestamp_utc,
               numeric_value,unit_code,producer_identity,correlation_id,lineage_id,status,telemetry_outcome,
               final_classification,measurement_persisted,persisted_measurement_id,quality_code,reason_code,
               latest_advanced,error_code,rejection_code,created_at_utc,completed_at_utc,
               original_correlation_id,original_lineage_id,version
        FROM acquisition.simulator_production_attempt
        """;

    private static SimulatorProductionAttempt MapAttempt(NpgsqlDataReader r)
    {
        var payload=new SimulatorProductionPayload(r.GetGuid(3),r.GetGuid(4),r.GetGuid(0),r.GetGuid(1),
            r.GetGuid(5),r.GetInt64(6),r.GetInt64(2),r.GetString(7),r.GetInt32(8),r.GetGuid(9),
            r.GetInt64(10),r.GetDateTime(11).ToUniversalTime(),r.GetDouble(12),r.GetString(13),
            r.GetString(14),r.GetString(15),r.GetString(16));
        return new SimulatorProductionAttempt(r.GetGuid(0),r.GetGuid(1),r.GetInt64(2),payload,
            Enum.Parse<SimulatorProductionAttemptStatus>(r.GetString(17),false),
            r.IsDBNull(18)?null:Enum.Parse<TelemetryAttemptOutcome>(r.GetString(18),false),
            r.IsDBNull(19)?null:Enum.Parse<ProductionFinalClassification>(r.GetString(19),false),
            r.IsDBNull(20)?null:r.GetBoolean(20),r.IsDBNull(21)?null:r.GetGuid(21),
            r.IsDBNull(22)?null:r.GetString(22),r.IsDBNull(23)?null:r.GetString(23),
            r.IsDBNull(24)?null:r.GetBoolean(24),r.IsDBNull(25)?null:r.GetString(25),
            r.IsDBNull(26)?null:r.GetString(26),r.GetDateTime(27).ToUniversalTime(),
            r.IsDBNull(28)?null:r.GetDateTime(28).ToUniversalTime(),
            r.IsDBNull(29)?null:r.GetString(29),r.IsDBNull(30)?null:r.GetString(30),r.GetInt64(31));
    }

    private static void BindAttempt(NpgsqlCommand c,SimulatorProductionAttempt v)
    {
        var p=v.Payload;c.Parameters.AddWithValue("run_id",v.RunId);c.Parameters.AddWithValue("point_id",v.PointId);
        c.Parameters.AddWithValue("sequence",v.SourceSequence);c.Parameters.AddWithValue("measurement_id",p.MeasurementId);
        c.Parameters.AddWithValue("source_id",p.SourceId);c.Parameters.AddWithValue("mapping_id",p.MappingId);
        c.Parameters.AddWithValue("mapping_version",p.MappingVersion);c.Parameters.AddWithValue("algorithm_id",p.AlgorithmId);
        c.Parameters.AddWithValue("algorithm_version",p.AlgorithmVersion);c.Parameters.AddWithValue("configuration_id",p.ConfigurationId);
        c.Parameters.AddWithValue("configuration_version",p.ConfigurationVersion);c.Parameters.AddWithValue("timestamp",p.SourceTimestampUtc);
        c.Parameters.AddWithValue("value",p.NumericValue);c.Parameters.AddWithValue("unit_code",p.UnitCode);
        c.Parameters.AddWithValue("producer",p.ProducerIdentity);c.Parameters.AddWithValue("correlation_id",p.CorrelationId);
        c.Parameters.AddWithValue("lineage_id",p.LineageId);c.Parameters.AddWithValue("created_at",v.CreatedAtUtc);
        c.Parameters.AddWithValue("version",v.Version);
    }

    private static bool Equivalent(SimulatorProductionAttempt a,TelemetryDispatchResult r)=>
        a.TelemetryOutcome==r.Outcome&&a.FinalClassification==r.FinalClassification&&
        a.MeasurementPersisted==r.MeasurementPersisted&&a.PersistedMeasurementId==r.PersistedMeasurementId&&
        a.QualityCode==r.QualityCode&&a.ReasonCode==r.ReasonCode&&a.LatestAdvanced==r.LatestAdvanced&&
        a.ErrorCode==r.ErrorCode&&a.RejectionCode==r.RejectionCode;

    private async Task<SimulatorProductionAttempt?> QuerySingleAsync(
        string sql,Action<NpgsqlCommand> bind,CancellationToken ct)
    {
        await using var connection=await _dataSource.OpenConnectionAsync(ct);
        await using var command=new NpgsqlCommand(sql,connection);bind(command);
        await using var reader=await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)?MapAttempt(reader):null;
    }
    private static async Task<SimulatorProductionAttempt?> QuerySingleAsync(
        PostgresSimulatorRunTransaction tx,Guid runId,Guid pointId,long sourceSequence,CancellationToken ct)
    {
        await using var command=new NpgsqlCommand(AttemptSelect+
            " WHERE run_id=@run_id AND point_id=@point_id AND source_sequence=@sequence",
            tx.Connection,tx.Transaction);
        command.Parameters.AddWithValue("run_id",runId);command.Parameters.AddWithValue("point_id",pointId);
        command.Parameters.AddWithValue("sequence",sourceSequence);
        await using var reader=await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)?MapAttempt(reader):null;
    }
    private static PostgresSimulatorRunTransaction Require(ISimulatorRunTransaction tx)=>
        tx as PostgresSimulatorRunTransaction??throw new InvalidOperationException("POSTGRES_RUN_TRANSACTION_REQUIRED");
}
