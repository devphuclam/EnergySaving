using System.Text.Json;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Audit.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace IUMP.Modules.Audit.Infrastructure;

public sealed class PostgresAuditRepositories :
    IAuditAppendRepository,
    ITransactionalAuditAppendRepository,
    IAuditConflictRepository,
    IAuditQueryRepository
{
    private readonly NpgsqlDataSource _dataSource;
    public PostgresAuditRepositories(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<AuditEventRecord?> AppendIfAbsentAsync(
        AuditEventRecord record, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        return await AppendCoreAsync(record, connection, null, ct);
    }

    public Task<AuditEventRecord?> AppendIfAbsentAsync(
        AuditEventRecord record, IHostTransaction transaction, CancellationToken ct = default)
    {
        var postgres = PostgresTransactionResolver.Require(transaction);
        return AppendCoreAsync(record, postgres.Connection, postgres.Transaction, ct);
    }

    public async Task<bool> IsSourceHashConflictAsync(
        Guid sourceEventId, string payloadHash, CancellationToken ct = default)
    {
        var hash = ParseHash(payloadHash);
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS(
              SELECT 1 FROM audit.audit_event
              WHERE source_event_id=@id AND payload_hash<>@hash)
            """, connection);
        command.Parameters.AddWithValue("id", sourceEventId);
        command.Parameters.AddWithValue("hash", hash);
        return (bool)(await command.ExecuteScalarAsync(ct))!;
    }

    public async Task<IReadOnlyList<AuditEventRecord>> QueryAsync(
        AuditQueryRequest request, CancellationToken ct = default)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        AuditKeysetCursor.TryDecode(request.KeysetCursor, out var cursor);
        var hasCursor = !string.IsNullOrWhiteSpace(request.KeysetCursor);
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT audit_event_id,source_event_id,event_type,object_type,object_id,action,summary,
                   occurred_at_utc,recorded_at_utc,correlation_id,actor_id,actor_username,
                   before_json::text,after_json::text,site_id,area_id,causation_id,
                   schema_version,source_producer,payload_hash
            FROM audit.audit_event
            WHERE (@object_type IS NULL OR object_type=@object_type)
              AND (@action IS NULL OR action=@action)
              AND (@actor_id IS NULL OR actor_id=@actor_id)
              AND (@correlation_id IS NULL OR correlation_id=@correlation_id)
              AND (@from_utc IS NULL OR occurred_at_utc>=@from_utc)
              AND (
                cardinality(@site_ids)=0 AND cardinality(@area_ids)=0
                OR site_id=ANY(@site_ids) OR area_id=ANY(@area_ids))
              AND (NOT @has_cursor OR (occurred_at_utc,audit_event_id)<(@cursor_time,@cursor_id))
            ORDER BY occurred_at_utc DESC,audit_event_id DESC
            OFFSET @offset LIMIT @limit
            """, connection);
        command.Parameters.Add(new NpgsqlParameter("object_type", NpgsqlDbType.Text)
            { Value = (object?)request.ObjectType ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("action", NpgsqlDbType.Text)
            { Value = (object?)request.Action ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("actor_id", NpgsqlDbType.Text)
            { Value = (object?)request.ActorId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("correlation_id", NpgsqlDbType.Text)
            { Value = (object?)request.CorrelationId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("from_utc", NpgsqlDbType.TimestampTz)
            { Value = (object?)request.FromUtc?.ToUniversalTime() ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter(
            "site_ids", NpgsqlDbType.Array | NpgsqlDbType.Text)
            { Value = request.ScopeSiteIds.ToArray() });
        command.Parameters.Add(new NpgsqlParameter(
            "area_ids", NpgsqlDbType.Array | NpgsqlDbType.Text)
            { Value = request.ScopeAreaIds.ToArray() });
        command.Parameters.Add(new NpgsqlParameter("has_cursor", NpgsqlDbType.Boolean)
            { Value = hasCursor });
        command.Parameters.Add(new NpgsqlParameter("cursor_time", NpgsqlDbType.TimestampTz)
            { Value = hasCursor ? cursor.OccurredAtUtc : DateTime.UnixEpoch });
        command.Parameters.Add(new NpgsqlParameter("cursor_id", NpgsqlDbType.Uuid)
            { Value = hasCursor ? cursor.AuditEventId : Guid.Empty });
        command.Parameters.Add(new NpgsqlParameter("offset", NpgsqlDbType.Integer)
            { Value = (page - 1) * size });
        command.Parameters.Add(new NpgsqlParameter("limit", NpgsqlDbType.Integer)
            { Value = size });
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<AuditEventRecord>();
        while (await reader.ReadAsync(ct)) rows.Add(Map(reader));
        return rows;
    }

    private static async Task<AuditEventRecord?> AppendCoreAsync(
        AuditEventRecord record, NpgsqlConnection connection,
        NpgsqlTransaction? transaction, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO audit.audit_event
                (audit_event_id,source_event_id,event_type,schema_version,source_producer,payload_hash,
                 object_type,object_id,action,actor_id,actor_username,before_json,after_json,summary,
                 site_id,area_id,occurred_at_utc,recorded_at_utc,correlation_id,causation_id)
            VALUES
                (@audit_id,@source_id,@event_type,@schema_version,@producer,@hash,@object_type,
                 @object_id,@action,@actor_id,@actor_username,@before,@after,@summary,@site_id,
                 @area_id,@occurred,@recorded,@correlation_id,@causation_id)
            ON CONFLICT (source_event_id) DO NOTHING
            RETURNING audit_event_id,source_event_id,event_type,object_type,object_id,action,summary,
                      occurred_at_utc,recorded_at_utc,correlation_id,actor_id,actor_username,
                      before_json::text,after_json::text,site_id,area_id,causation_id,
                      schema_version,source_producer,payload_hash
            """, connection, transaction);
        Bind(command, record);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct)) return Map(reader);
        await reader.DisposeAsync();

        await using var existing = new NpgsqlCommand("""
            SELECT audit_event_id,source_event_id,event_type,object_type,object_id,action,summary,
                   occurred_at_utc,recorded_at_utc,correlation_id,actor_id,actor_username,
                   before_json::text,after_json::text,site_id,area_id,causation_id,
                   schema_version,source_producer,payload_hash
            FROM audit.audit_event WHERE source_event_id=@source_id
            """, connection, transaction);
        existing.Parameters.AddWithValue("source_id", record.SourceEventId);
        await using var existingReader = await existing.ExecuteReaderAsync(ct);
        if (!await existingReader.ReadAsync(ct)) return null;
        var prior = Map(existingReader);
        if (!string.Equals(prior.PayloadHash, record.PayloadHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AUDIT_SOURCE_HASH_CONFLICT");
        return prior;
    }

    private static void Bind(NpgsqlCommand command, AuditEventRecord value)
    {
        command.Parameters.AddWithValue("audit_id", value.AuditEventId);
        command.Parameters.AddWithValue("source_id", value.SourceEventId);
        command.Parameters.AddWithValue("event_type", value.EventType);
        command.Parameters.AddWithValue("schema_version", value.SchemaVersion);
        command.Parameters.AddWithValue("producer", value.SourceProducer);
        command.Parameters.AddWithValue("hash", ParseHash(value.PayloadHash));
        command.Parameters.AddWithValue("object_type", value.ObjectType);
        command.Parameters.AddWithValue("object_id", value.ObjectId);
        command.Parameters.AddWithValue("action", value.Action);
        command.Parameters.AddWithValue("actor_id", (object?)value.ActorId ?? DBNull.Value);
        command.Parameters.AddWithValue("actor_username", (object?)value.ActorUsername ?? DBNull.Value);
        command.Parameters.Add(new NpgsqlParameter("before", NpgsqlDbType.Jsonb)
        {
            Value = JsonSerializer.Serialize(value.Before)
        });
        command.Parameters.Add(new NpgsqlParameter("after", NpgsqlDbType.Jsonb)
        {
            Value = JsonSerializer.Serialize(value.After)
        });
        command.Parameters.AddWithValue("summary", value.Summary);
        command.Parameters.AddWithValue("site_id", (object?)value.SiteId ?? DBNull.Value);
        command.Parameters.AddWithValue("area_id", (object?)value.AreaId ?? DBNull.Value);
        command.Parameters.AddWithValue("occurred", value.OccurredAtUtc.ToUniversalTime());
        command.Parameters.AddWithValue("recorded", value.RecordedAtUtc.ToUniversalTime());
        command.Parameters.AddWithValue("correlation_id", value.CorrelationId);
        command.Parameters.AddWithValue("causation_id", (object?)value.CausationId ?? DBNull.Value);
    }

    private static AuditEventRecord Map(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
        reader.GetString(4), reader.GetString(5), reader.GetString(6),
        reader.GetDateTime(7).ToUniversalTime(), reader.GetDateTime(8).ToUniversalTime(),
        reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10),
        reader.IsDBNull(11) ? null : reader.GetString(11),
        ParseMap(reader.GetString(12)), ParseMap(reader.GetString(13)),
        reader.IsDBNull(14) ? null : reader.GetString(14),
        reader.IsDBNull(15) ? null : reader.GetString(15),
        reader.IsDBNull(16) ? null : reader.GetString(16))
    {
        SchemaVersion = reader.GetInt32(17),
        SourceProducer = reader.GetString(18),
        PayloadHash = Convert.ToHexString((byte[])reader[19]).ToLowerInvariant()
    };

    private static IReadOnlyDictionary<string, object?> ParseMap(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
        ?? new Dictionary<string, object?>();

    private static byte[] ParseHash(string hash)
    {
        try
        {
            var bytes = Convert.FromHexString(hash);
            if (bytes.Length == 32) return bytes;
        }
        catch (FormatException) { }
        throw new InvalidOperationException("AUDIT_PAYLOAD_HASH_INVALID");
    }
}
