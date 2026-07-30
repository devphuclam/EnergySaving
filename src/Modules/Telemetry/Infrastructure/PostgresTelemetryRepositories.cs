using System.Text.Json;
using IUMP.Modules.Telemetry.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace IUMP.Modules.Telemetry.Infrastructure;

public sealed class PostgresTelemetryFlowUnitOfWork(NpgsqlDataSource dataSource) : ITelemetryFlowUnitOfWork
{
    public async ValueTask<ITelemetryFlowTransaction> BeginRepeatableReadAsync(
        CancellationToken ct = default)
    {
        var connection = await dataSource.OpenConnectionAsync(ct);
        try
        {
            var transaction = await connection.BeginTransactionAsync(
                System.Data.IsolationLevel.RepeatableRead, ct);
            return new PostgresTelemetryFlowTransaction(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}

public sealed class PostgresTelemetryFlowTransaction(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction) : ITelemetryFlowTransaction
{
    private readonly List<TelemetryFlowLock> _locks = [];
    private bool _completed;
    private bool _disposed;

    public Guid TransactionId { get; } = Guid.NewGuid();
    public string IsolationIntent => "REPEATABLE READ";
    public bool IsCompleted => _completed;
    public IReadOnlyList<TelemetryFlowLock> LockTrace => _locks;
    internal NpgsqlConnection Connection => connection;
    internal NpgsqlTransaction Transaction => transaction;

    public async ValueTask AcquireLockAsync(
        TelemetryFlowLockTarget target,
        string key,
        CancellationToken ct = default)
    {
        if ((int)target != _locks.Count + 1)
            throw new InvalidOperationException("LOCK_ORDER_VIOLATION");
        await using var command = new NpgsqlCommand("""
            SELECT pg_advisory_xact_lock(hashtextextended(@key,@target))
            """, connection, transaction);
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("target", (long)target);
        _ = await command.ExecuteScalarAsync(ct);
        _locks.Add(new TelemetryFlowLock(target, key));
    }

    public async ValueTask CommitAsync(CancellationToken ct = default)
    {
        if (_completed) return;
        try { await transaction.CommitAsync(ct); }
        catch
        {
            try { await transaction.RollbackAsync(CancellationToken.None); } catch { }
            throw;
        }
        finally { _completed = true; }
    }

    public async ValueTask RollbackAsync(CancellationToken ct = default)
    {
        if (_completed) return;
        try { await transaction.RollbackAsync(ct); }
        finally { _completed = true; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (!_completed) await RollbackAsync();
        await transaction.DisposeAsync();
        await connection.DisposeAsync();
        _disposed = true;
    }
}

public sealed class PostgresTelemetryRepositories :
    ITelemetryIngestionRepository,
    IPointLatestProjectionRepository,
    ISourceHealthProjectionRepository,
    ISourceHealthRepository,
    ITelemetryQueryRepository
{
    private readonly NpgsqlDataSource _dataSource;
    public PostgresTelemetryRepositories(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public Task<TelemetryTerminalResult?> GetTerminalAsync(
        Guid measurementId, CancellationToken ct = default) =>
        QueryTerminalAsync("measurement_id=@measurement_id",
            command => command.Parameters.AddWithValue("measurement_id", measurementId), ct);

    public Task<TelemetryTerminalResult?> GetTerminalBySlotAsync(
        Guid runId, Guid pointId, long sourceSequence, CancellationToken ct = default) =>
        QueryTerminalAsync("""
            simulator_run_id=@run_id AND point_id=@point_id AND source_sequence=@sequence
            """, command =>
        {
            command.Parameters.AddWithValue("run_id", runId);
            command.Parameters.AddWithValue("point_id", pointId);
            command.Parameters.AddWithValue("sequence", sourceSequence);
        }, ct);

    public async Task<TelemetryTerminalResult?> RecheckTerminalAsync(
        Guid measurementId,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default)
    {
        var tx = Require(transaction);
        await using var command = new NpgsqlCommand(
            TerminalSelect + " WHERE measurement_id=@id FOR UPDATE",
            tx.Connection, tx.Transaction);
        command.Parameters.AddWithValue("id", measurementId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapTerminal(reader) : null;
    }

    public async Task StageTerminalAsync(
        TelemetryTerminalResult result,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default)
    {
        var tx = Require(transaction);
        try
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO telemetry.measurement_identity
                    (measurement_id,source_id,simulator_run_id,point_id,mapping_id,mapping_version,
                     source_sequence,algorithm_id,algorithm_version,simulator_configuration_id,
                     configuration_version,request_fingerprint,final_classification,measurement_persisted,
                     persisted_measurement_id,quality_code,reason_code,rejection_code,latest_advanced,
                     completed_at_utc,original_correlation_id,original_lineage_id)
                VALUES
                    (@measurement_id,@source_id,@run_id,@point_id,@mapping_id,@mapping_version,
                     @sequence,@algorithm_id,@algorithm_version,@configuration_id,@configuration_version,
                     @fingerprint,@classification,@persisted,@persisted_id,@quality,@reason,@rejection,
                     @latest,@completed,@correlation_id,@lineage_id)
                """, tx.Connection, tx.Transaction);
            BindTerminal(command, result);
            await command.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new TelemetryUniqueRaceException();
        }
    }

    public async Task StageRawAsync(
        RawMeasurement measurement,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default)
    {
        var tx = Require(transaction);
        await using var command = new NpgsqlCommand("""
            INSERT INTO telemetry.measurement_raw
                (measurement_id,source_id,simulator_run_id,point_id,mapping_id,mapping_version,
                 source_sequence,source_timestamp_utc,received_at_utc,processing_at_utc,numeric_value,
                 unit_code,quality_code,reason_code,correlation_id,lineage_id)
            VALUES
                (@measurement_id,@source_id,@run_id,@point_id,@mapping_id,@mapping_version,
                 @sequence,@source_timestamp,@received,@processing,@value,@unit_code,@quality,
                 @reason,@correlation_id,@lineage_id)
            """, tx.Connection, tx.Transaction);
        BindRaw(command, measurement);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<TelemetryTerminalResult>> ListCommittedTerminalsAsync(
        CancellationToken ct = default) =>
        await QueryAsync(TerminalSelect + " ORDER BY completed_at_utc,measurement_id",
            null, MapTerminal, ct);

    public async Task<IReadOnlyList<RawMeasurement>> ListCommittedRawAsync(
        CancellationToken ct = default) =>
        await QueryAsync(RawSelect + " ORDER BY processing_at_utc,measurement_id",
            null, MapRaw, ct);

    public async Task<bool> EvaluateAdvanceAsync(
        LatestProjectionCandidate candidate,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default)
    {
        var current = await GetCurrentAsync(candidate.PointId, transaction, ct);
        return current is null ||
            LatestOrdering.Compare(candidate.ToOrdering(), current.Ordering) > 0;
    }

    public async Task StageAdvanceAsync(
        LatestProjectionCandidate candidate,
        bool latestAdvanced,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default)
    {
        if (!latestAdvanced) return;
        _ = await CompareAndSetAsync(candidate, transaction, ct);
    }

    public Task<PointLatestProjection?> GetCurrentAsync(
        Guid pointId, CancellationToken ct = default) =>
        QueryLatestAsync("p.point_id=@point_id",
            command => command.Parameters.AddWithValue("point_id", pointId), ct);

    public async Task<PointLatestAdvanceResult> CompareAndSetAsync(
        LatestProjectionCandidate candidate,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default)
    {
        if (candidate.QualityCode == MeasurementQuality.Bad)
            return new PointLatestAdvanceResult(false,
                await GetCurrentAsync(candidate.PointId, transaction, ct),
                await GetCurrentAsync(candidate.PointId, transaction, ct));

        var tx = Require(transaction);
        var previous = await GetCurrentAsync(candidate.PointId, transaction, ct);
        await using var command = new NpgsqlCommand("""
            INSERT INTO telemetry.point_latest
                (point_id,measurement_id,source_id,simulator_run_id,mapping_id,mapping_version,
                 numeric_value,unit_code,quality_code,reason_code,source_timestamp_utc,source_sequence,
                 received_at_utc,processing_at_utc,ordering_source_timestamp_utc,
                 ordering_source_sequence,ordering_processing_at_utc,version,updated_at_utc)
            SELECT r.point_id,r.measurement_id,r.source_id,r.simulator_run_id,r.mapping_id,r.mapping_version,
                   r.numeric_value,r.unit_code,r.quality_code,r.reason_code,r.source_timestamp_utc,
                   r.source_sequence,r.received_at_utc,r.processing_at_utc,r.source_timestamp_utc,
                   r.source_sequence,r.processing_at_utc,1,now()
            FROM telemetry.measurement_raw r
            WHERE r.measurement_id=@measurement_id
            ON CONFLICT (point_id) DO UPDATE
            SET measurement_id=EXCLUDED.measurement_id,source_id=EXCLUDED.source_id,
                simulator_run_id=EXCLUDED.simulator_run_id,mapping_id=EXCLUDED.mapping_id,
                mapping_version=EXCLUDED.mapping_version,numeric_value=EXCLUDED.numeric_value,
                unit_code=EXCLUDED.unit_code,quality_code=EXCLUDED.quality_code,
                reason_code=EXCLUDED.reason_code,source_timestamp_utc=EXCLUDED.source_timestamp_utc,
                source_sequence=EXCLUDED.source_sequence,received_at_utc=EXCLUDED.received_at_utc,
                processing_at_utc=EXCLUDED.processing_at_utc,
                ordering_source_timestamp_utc=EXCLUDED.ordering_source_timestamp_utc,
                ordering_source_sequence=EXCLUDED.ordering_source_sequence,
                ordering_processing_at_utc=EXCLUDED.ordering_processing_at_utc,
                version=telemetry.point_latest.version+1,updated_at_utc=now()
            WHERE (telemetry.point_latest.ordering_source_timestamp_utc,
                   telemetry.point_latest.ordering_source_sequence,
                   telemetry.point_latest.ordering_processing_at_utc,
                   telemetry.point_latest.measurement_id)
                < (EXCLUDED.ordering_source_timestamp_utc,
                   EXCLUDED.ordering_source_sequence,
                   EXCLUDED.ordering_processing_at_utc,
                   EXCLUDED.measurement_id)
            """, tx.Connection, tx.Transaction);
        command.Parameters.AddWithValue("measurement_id", candidate.MeasurementId);
        var affected = await command.ExecuteNonQueryAsync(ct);
        var current = await GetCurrentAsync(candidate.PointId, transaction, ct);
        return new PointLatestAdvanceResult(affected == 1, previous, current);
    }

    public ValueTask StageAdvancedEventAsync(
        PointLatestAdvancedEvent latestEvent,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default) =>
        StageEventAsync(latestEvent.EventId, latestEvent.EventType, latestEvent.SchemaVersion,
            latestEvent.OccurredAtUtc, latestEvent, transaction, ct);

    async Task<PointSourceHealthProjection?> ISourceHealthProjectionRepository.GetCurrentAsync(
        Guid pointId, CancellationToken ct) =>
        await QueryHealthAsync(pointId, null, ct);

    public async Task<SourceHealthEvaluationResult> CompareAndSetAsync(
        SourceHealthEvaluationInput input,
        SourceHealthStatus status,
        DateTime evaluatedAtUtc,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default)
    {
        var tx = Require(transaction);
        var previous = await QueryHealthAsync(input.PointId, transaction, ct);
        await using var command = new NpgsqlCommand("""
            INSERT INTO telemetry.point_source_status
                (point_id,source_id,health_status,last_accepted_received_at_utc,
                 expected_interval_seconds,no_data_after_seconds,source_version,point_version,
                 provider_version,run_status,generated_count,accepted_count,rejected_count,
                 version,evaluated_at_utc,updated_at_utc)
            VALUES
                (@point_id,@source_id,@status,@last_received,@expected_interval,@no_data_after,
                 @source_version,@point_version,@provider_version,@run_status,@generated,@accepted,
                 @rejected,1,@evaluated,now())
            ON CONFLICT (point_id) DO UPDATE
            SET source_id=EXCLUDED.source_id,health_status=EXCLUDED.health_status,
                last_accepted_received_at_utc=EXCLUDED.last_accepted_received_at_utc,
                expected_interval_seconds=EXCLUDED.expected_interval_seconds,
                no_data_after_seconds=EXCLUDED.no_data_after_seconds,
                source_version=EXCLUDED.source_version,point_version=EXCLUDED.point_version,
                provider_version=EXCLUDED.provider_version,run_status=EXCLUDED.run_status,
                generated_count=EXCLUDED.generated_count,accepted_count=EXCLUDED.accepted_count,
                rejected_count=EXCLUDED.rejected_count,version=telemetry.point_source_status.version+1,
                evaluated_at_utc=EXCLUDED.evaluated_at_utc,updated_at_utc=now()
            WHERE EXCLUDED.provider_version>=telemetry.point_source_status.provider_version
            """, tx.Connection, tx.Transaction);
        command.Parameters.AddWithValue("point_id", input.PointId);
        command.Parameters.AddWithValue("source_id", input.SourceId);
        command.Parameters.AddWithValue("status", status.ToString());
        command.Parameters.AddWithValue("last_received", (object?)input.LastAcceptedReceivedAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("expected_interval", input.ExpectedIntervalSeconds);
        command.Parameters.AddWithValue("no_data_after", input.NoDataAfterSeconds);
        command.Parameters.AddWithValue("source_version", input.SourceVersion);
        command.Parameters.AddWithValue("point_version", input.PointVersion);
        command.Parameters.AddWithValue("provider_version", input.ProviderVersion);
        command.Parameters.AddWithValue("run_status", (object?)input.RunStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("generated", input.GeneratedCount);
        command.Parameters.AddWithValue("accepted", input.AcceptedCount);
        command.Parameters.AddWithValue("rejected", input.RejectedCount);
        command.Parameters.AddWithValue("evaluated", evaluatedAtUtc.ToUniversalTime());
        var affected = await command.ExecuteNonQueryAsync(ct);
        var current = await QueryHealthAsync(input.PointId, transaction, ct)
            ?? throw new InvalidOperationException("SOURCE_HEALTH_WRITE_FAILED");
        return new SourceHealthEvaluationResult(
            affected == 1 && (previous is null || previous.Status != current.Status),
            current, previous);
    }

    public ValueTask StageChangedEventAsync(
        PointSourceHealthChangedEvent healthEvent,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default) =>
        StageEventAsync(healthEvent.EventId, healthEvent.EventType, healthEvent.SchemaVersion,
            healthEvent.OccurredAtUtc, healthEvent, transaction, ct);

    public async Task<SourceHealthSnapshot?> GetSourceHealthAsync(
        Guid pointId, CancellationToken ct = default)
    {
        var current = await QueryHealthAsync(pointId, null, ct);
        return current is null ? null : new SourceHealthSnapshot(
            current.PointId, current.Status.ToString(), current.LastAcceptedReceivedAtUtc,
            current.EvaluatedAtUtc, current.ProviderVersion);
    }

    public async Task<RawMeasurement?> GetMeasurementAsync(
        Guid measurementId, CancellationToken ct = default)
    {
        var values = await QueryAsync(RawSelect + " WHERE measurement_id=@id",
            command => command.Parameters.AddWithValue("id", measurementId), MapRaw, ct);
        return values.SingleOrDefault();
    }

    private async ValueTask StageEventAsync<T>(
        Guid eventId, string eventType, int schemaVersion, DateTime occurredAt,
        T payload, ITelemetryFlowTransaction transaction, CancellationToken ct)
    {
        var tx = Require(transaction);
        await using var command = new NpgsqlCommand("""
            INSERT INTO integration.outbox_event
                (event_id,event_type,schema_version,occurred_at,payload_json,status,next_attempt_at)
            VALUES (@id,@type,@version,@occurred,@payload,'Pending',@occurred)
            ON CONFLICT (event_id) DO NOTHING
            """, tx.Connection, tx.Transaction);
        command.Parameters.AddWithValue("id", eventId);
        command.Parameters.AddWithValue("type", eventType);
        command.Parameters.AddWithValue("version", schemaVersion);
        command.Parameters.AddWithValue("occurred", occurredAt.ToUniversalTime());
        command.Parameters.Add(new NpgsqlParameter("payload", NpgsqlDbType.Jsonb)
        {
            Value = JsonSerializer.Serialize(payload)
        });
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<PointLatestProjection?> GetCurrentAsync(
        Guid pointId, ITelemetryFlowTransaction transaction, CancellationToken ct)
    {
        var tx = Require(transaction);
        await using var command = new NpgsqlCommand(LatestSelect + " WHERE p.point_id=@point_id FOR UPDATE",
            tx.Connection, tx.Transaction);
        command.Parameters.AddWithValue("point_id", pointId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapLatest(reader) : null;
    }

    private Task<PointLatestProjection?> QueryLatestAsync(
        string predicate, Action<NpgsqlCommand> bind, CancellationToken ct) =>
        QuerySingleAsync(LatestSelect + " WHERE " + predicate, bind, MapLatest, ct);

    private async Task<PointSourceHealthProjection?> QueryHealthAsync(
        Guid pointId, ITelemetryFlowTransaction? transaction, CancellationToken ct)
    {
        const string sql = """
            SELECT h.point_id,h.source_id,h.health_status,h.last_accepted_received_at_utc,
                   h.expected_interval_seconds,h.no_data_after_seconds,h.run_status,h.generated_count,
                   h.accepted_count,h.rejected_count,h.point_version,h.source_version,h.provider_version,
                   h.version,h.evaluated_at_utc,
                   COALESCE(p.site_id::text,''),p.area_id::text
            FROM telemetry.point_source_status h
            LEFT JOIN organization.measurement_points p ON p.id=h.point_id
            WHERE h.point_id=@point_id
            """;
        if (transaction is null)
            return await QuerySingleAsync(sql,
                command => command.Parameters.AddWithValue("point_id", pointId), MapHealth, ct);
        var tx = Require(transaction);
        await using var command = new NpgsqlCommand(sql + " FOR UPDATE OF h", tx.Connection, tx.Transaction);
        command.Parameters.AddWithValue("point_id", pointId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapHealth(reader) : null;
    }

    private Task<TelemetryTerminalResult?> QueryTerminalAsync(
        string predicate, Action<NpgsqlCommand> bind, CancellationToken ct) =>
        QuerySingleAsync(TerminalSelect + " WHERE " + predicate, bind, MapTerminal, ct);

    private const string TerminalSelect = """
        SELECT measurement_id,source_id,simulator_run_id,point_id,mapping_id,mapping_version,
               source_sequence,algorithm_id,algorithm_version,simulator_configuration_id,
               configuration_version,final_classification,measurement_persisted,persisted_measurement_id,
               quality_code,reason_code,rejection_code,latest_advanced,completed_at_utc,
               original_correlation_id,original_lineage_id,request_fingerprint
        FROM telemetry.measurement_identity
        """;

    private const string RawSelect = """
        SELECT measurement_id,source_id,simulator_run_id,point_id,mapping_id,mapping_version,
               source_sequence,source_timestamp_utc,received_at_utc,processing_at_utc,numeric_value,
               unit_code,quality_code,reason_code,correlation_id,lineage_id
        FROM telemetry.measurement_raw
        """;

    private const string LatestSelect = """
        SELECT p.point_id,p.measurement_id,p.source_id,p.simulator_run_id,p.mapping_id,p.mapping_version,
               p.numeric_value,p.unit_code,p.quality_code,p.reason_code,p.source_timestamp_utc,
               p.source_sequence,p.received_at_utc,p.processing_at_utc,p.version
        FROM telemetry.point_latest p
        """;

    private static TelemetryTerminalResult MapTerminal(NpgsqlDataReader r) => new(
        r.GetGuid(0),r.GetGuid(1),r.GetGuid(2),r.GetGuid(3),r.GetGuid(4),r.GetInt64(5),r.GetInt64(6),
        r.GetString(7),r.GetInt32(8),r.GetGuid(9),r.GetInt64(10),
        Enum.Parse<TelemetryFinalClassification>(r.GetString(11),false),r.GetBoolean(12),
        r.IsDBNull(13)?null:r.GetGuid(13),
        r.IsDBNull(14)?null:Enum.Parse<MeasurementQuality>(r.GetString(14),false),
        r.IsDBNull(15)?null:r.GetString(15),r.IsDBNull(16)?null:r.GetString(16),
        r.IsDBNull(17)?null:r.GetBoolean(17),r.GetDateTime(18).ToUniversalTime(),
        r.GetString(19),r.GetString(20),(byte[])r[21]);

    private static RawMeasurement MapRaw(NpgsqlDataReader r) => new(
        r.GetGuid(0),r.GetGuid(1),r.GetGuid(2),r.GetGuid(3),r.GetGuid(4),r.GetInt64(5),r.GetInt64(6),
        r.GetDateTime(7).ToUniversalTime(),r.GetDateTime(8).ToUniversalTime(),
        r.GetDateTime(9).ToUniversalTime(),r.GetDouble(10),r.GetString(11),
        Enum.Parse<MeasurementQuality>(r.GetString(12),false),
        r.IsDBNull(13)?null:r.GetString(13),r.GetString(14),r.GetString(15));

    private static PointLatestProjection MapLatest(NpgsqlDataReader r) => new(
        r.GetGuid(0),r.GetGuid(1),r.GetGuid(2),r.GetGuid(3),r.GetGuid(4),r.GetInt64(5),
        r.GetDouble(6),r.GetString(7),Enum.Parse<MeasurementQuality>(r.GetString(8),false),
        r.IsDBNull(9)?null:r.GetString(9),r.GetDateTime(10).ToUniversalTime(),r.GetInt64(11),
        r.GetDateTime(12).ToUniversalTime(),r.GetDateTime(13).ToUniversalTime(),r.GetInt64(14));

    private static PointSourceHealthProjection MapHealth(NpgsqlDataReader r) => new(
        r.GetGuid(0),r.GetGuid(1),Enum.Parse<SourceHealthStatus>(r.GetString(2),false),
        r.IsDBNull(3)?null:r.GetDateTime(3).ToUniversalTime(),r.GetInt32(4),r.GetInt32(5),
        r.IsDBNull(6)?null:r.GetString(6),r.GetInt64(7),r.GetInt64(8),r.GetInt64(9),
        r.GetInt64(10),r.GetInt64(11),r.GetInt64(12),r.GetInt64(13),
        r.GetDateTime(14).ToUniversalTime(),r.GetString(15),r.IsDBNull(16)?null:r.GetString(16));

    private static void BindTerminal(NpgsqlCommand c, TelemetryTerminalResult v)
    {
        c.Parameters.AddWithValue("measurement_id",v.MeasurementId);c.Parameters.AddWithValue("source_id",v.SourceId);
        c.Parameters.AddWithValue("run_id",v.SimulatorRunId);c.Parameters.AddWithValue("point_id",v.PointId);
        c.Parameters.AddWithValue("mapping_id",v.MappingId);c.Parameters.AddWithValue("mapping_version",v.MappingVersion);
        c.Parameters.AddWithValue("sequence",v.SourceSequence);c.Parameters.AddWithValue("algorithm_id",v.AlgorithmId);
        c.Parameters.AddWithValue("algorithm_version",v.AlgorithmVersion);c.Parameters.AddWithValue("configuration_id",v.SimulatorConfigurationId);
        c.Parameters.AddWithValue("configuration_version",v.ConfigurationVersion);
        c.Parameters.AddWithValue("fingerprint",v.RequestFingerprint);
        c.Parameters.AddWithValue("classification",v.FinalClassification.ToString());
        c.Parameters.AddWithValue("persisted",v.MeasurementPersisted);
        c.Parameters.AddWithValue("persisted_id",(object?)v.PersistedMeasurementId??DBNull.Value);
        c.Parameters.AddWithValue("quality",(object?)v.QualityCode?.ToString()??DBNull.Value);
        c.Parameters.AddWithValue("reason",(object?)v.ReasonCode??DBNull.Value);
        c.Parameters.AddWithValue("rejection",(object?)v.RejectionCode??DBNull.Value);
        c.Parameters.AddWithValue("latest",(object?)v.LatestAdvanced??DBNull.Value);
        c.Parameters.AddWithValue("completed",v.CompletedAtUtc);c.Parameters.AddWithValue("correlation_id",v.OriginalCorrelationId);
        c.Parameters.AddWithValue("lineage_id",v.OriginalLineageId);
    }

    private static void BindRaw(NpgsqlCommand c, RawMeasurement v)
    {
        c.Parameters.AddWithValue("measurement_id",v.MeasurementId);c.Parameters.AddWithValue("source_id",v.SourceId);
        c.Parameters.AddWithValue("run_id",v.SimulatorRunId);c.Parameters.AddWithValue("point_id",v.PointId);
        c.Parameters.AddWithValue("mapping_id",v.MappingId);c.Parameters.AddWithValue("mapping_version",v.MappingVersion);
        c.Parameters.AddWithValue("sequence",v.SourceSequence);c.Parameters.AddWithValue("source_timestamp",v.SourceTimestampUtc);
        c.Parameters.AddWithValue("received",v.ReceivedAtUtc);c.Parameters.AddWithValue("processing",v.ProcessingAtUtc);
        c.Parameters.AddWithValue("value",v.NumericValue);c.Parameters.AddWithValue("unit_code",v.UnitCode);
        c.Parameters.AddWithValue("quality",v.QualityCode.ToString());c.Parameters.AddWithValue("reason",(object?)v.ReasonCode??DBNull.Value);
        c.Parameters.AddWithValue("correlation_id",v.CorrelationId);c.Parameters.AddWithValue("lineage_id",v.LineageId);
    }

    private static PostgresTelemetryFlowTransaction Require(ITelemetryFlowTransaction tx) =>
        tx as PostgresTelemetryFlowTransaction ??
        throw new InvalidOperationException("POSTGRES_TELEMETRY_TRANSACTION_REQUIRED");

    private async Task<T?> QuerySingleAsync<T>(
        string sql,Action<NpgsqlCommand> bind,Func<NpgsqlDataReader,T> map,CancellationToken ct)
    {
        await using var connection=await _dataSource.OpenConnectionAsync(ct);
        await using var command=new NpgsqlCommand(sql,connection);bind(command);
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

public sealed class PostgresMeasurementAcceptedEventWriter(
    NpgsqlDataSource dataSource) : IMeasurementAcceptedEventWriter
{
    public async ValueTask StageAsync(
        TelemetryOwnerEvent ownerEvent,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default)
    {
        var tx = transaction as PostgresTelemetryFlowTransaction ??
            throw new InvalidOperationException("POSTGRES_TELEMETRY_TRANSACTION_REQUIRED");
        await using var command = new NpgsqlCommand("""
            INSERT INTO integration.outbox_event
                (event_id,event_type,schema_version,occurred_at,payload_json,status,next_attempt_at)
            VALUES (@id,@type,@version,@occurred,@payload,'Pending',@occurred)
            ON CONFLICT (event_id) DO NOTHING
            """, tx.Connection, tx.Transaction);
        command.Parameters.AddWithValue("id", ownerEvent.EventId);
        command.Parameters.AddWithValue("type", ownerEvent.EventType);
        command.Parameters.AddWithValue("version", ownerEvent.SchemaVersion);
        command.Parameters.AddWithValue("occurred", ownerEvent.OccurredAtUtc);
        command.Parameters.Add(new NpgsqlParameter("payload", NpgsqlDbType.Jsonb)
        {
            Value = JsonSerializer.Serialize(ownerEvent)
        });
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<TelemetryOwnerEvent>> ListCommittedAsync(
        CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT payload_json::text
            FROM integration.outbox_event
            WHERE event_type='MeasurementAccepted.v1'
            ORDER BY occurred_at,event_id
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var events = new List<TelemetryOwnerEvent>();
        while (await reader.ReadAsync(ct))
        {
            var value = JsonSerializer.Deserialize<TelemetryOwnerEvent>(reader.GetString(0));
            if (value is not null) events.Add(value);
        }
        return events;
    }
}

internal static class LatestCandidateExtensions
{
    public static LatestOrderingTuple ToOrdering(this LatestProjectionCandidate value) =>
        new(value.SourceTimestampUtc,value.SourceSequence,value.ProcessingAtUtc,value.MeasurementId);
}
