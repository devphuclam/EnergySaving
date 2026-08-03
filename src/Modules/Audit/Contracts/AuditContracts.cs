using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IUMP.BuildingBlocks.Persistence;

namespace IUMP.Modules.Audit.Contracts;

public sealed record AuditEventEnvelope(
    Guid SourceEventId,
    string EventType,
    string ObjectType,
    string ObjectId,
    string Action,
    string Summary,
    DateTime OccurredAtUtc,
    string CorrelationId,
    string? ActorId = null,
    string? ActorUsername = null,
    IReadOnlyDictionary<string, object?>? Before = null,
    IReadOnlyDictionary<string, object?>? After = null,
    string? SiteId = null,
    string? AreaId = null,
    string? CausationId = null)
{
    public int SchemaVersion => 1;
    public string SourceProducer => "IUMP";
    public string PayloadHash => ComputePayloadHash(this);

    public static string ComputePayloadHash(AuditEventEnvelope envelope)
    {
        var canonical = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sourceEventId"] = envelope.SourceEventId.ToString("D"),
            ["eventType"] = envelope.EventType,
            ["schemaVersion"] = envelope.SchemaVersion,
            ["producer"] = envelope.SourceProducer,
            ["objectType"] = envelope.ObjectType,
            ["objectId"] = envelope.ObjectId,
            ["action"] = envelope.Action,
            ["summary"] = envelope.Summary,
            ["occurredAtUtc"] = envelope.OccurredAtUtc.ToUniversalTime().ToString("O"),
            ["actorId"] = envelope.ActorId,
            ["actorUsername"] = envelope.ActorUsername,
            ["siteId"] = envelope.SiteId,
            ["areaId"] = envelope.AreaId,
            ["correlationId"] = envelope.CorrelationId,
            ["causationId"] = envelope.CausationId,
            ["before"] = CanonicalMap(envelope.Before),
            ["after"] = CanonicalMap(envelope.After)
        };
        var json = JsonSerializer.Serialize(canonical, new JsonSerializerOptions { WriteIndented = false });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static IReadOnlyDictionary<string, object?> CanonicalMap(IReadOnlyDictionary<string, object?>? values) =>
        (values ?? new Dictionary<string, object?>()).OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    public static AuditEventEnvelope Create(Guid sourceEventId, string eventType, string objectType, string objectId,
        string action, string summary, DateTime occurredAtUtc, string correlationId) =>
        new(sourceEventId, eventType, objectType, objectId, action, summary, occurredAtUtc.ToUniversalTime(), correlationId);
}

public sealed record AuditEventRecord(
    Guid AuditEventId,
    Guid SourceEventId,
    string EventType,
    string ObjectType,
    string ObjectId,
    string Action,
    string Summary,
    DateTime OccurredAtUtc,
    DateTime RecordedAtUtc,
    string? CorrelationId,
    string? ActorId,
    string? ActorUsername,
    IReadOnlyDictionary<string, object?> Before,
    IReadOnlyDictionary<string, object?> After,
    string? SiteId,
    string? AreaId,
    string? CausationId)
{
    public int SchemaVersion { get; init; } = 1;
    public string SourceProducer { get; init; } = "IUMP";
    public string PayloadHash { get; init; } = string.Empty;
}

public interface IAuditEventConsumer
{
    Task<AuditEventRecord> ConsumeAsync(AuditEventEnvelope envelope, CancellationToken ct = default);
}

public interface ITransactionalAuditEventConsumer : IAuditEventConsumer
{
    Task<AuditEventRecord> ConsumeAsync(AuditEventEnvelope envelope, IHostTransaction transaction,
        CancellationToken ct = default);
}

public interface IAuditAppendRepository
{
    Task<AuditEventRecord?> AppendIfAbsentAsync(AuditEventRecord record, CancellationToken ct = default);
}

public interface ITransactionalAuditAppendRepository
{
    Task<AuditEventRecord?> AppendIfAbsentAsync(AuditEventRecord record, IHostTransaction transaction,
        CancellationToken ct = default);
}

public interface IAuditConflictRepository
{
    Task<bool> IsSourceHashConflictAsync(Guid sourceEventId, string payloadHash, CancellationToken ct = default);
}

public interface IAuditQueryRepository
{
    Task<IReadOnlyList<AuditEventRecord>> QueryAsync(AuditQueryRequest request, CancellationToken ct = default);
}

public sealed record AuditQueryRequest(string? ObjectType, string? Action, string? ActorId, string? CorrelationId,
    DateTime? FromUtc, int Page, int PageSize)
{
    public IReadOnlySet<string> ScopeSiteIds { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> ScopeAreaIds { get; init; } = new HashSet<string>();
    public string? KeysetCursor { get; init; }
    public DateTime? ToUtc { get; init; }
    public string? EntityId { get; init; }
    public string? SiteId { get; init; }
    public string? AreaId { get; init; }
}

public sealed record AuditQueryResult(IReadOnlyList<AuditEventRecord> Items, string? ErrorCode = null,
    int ItemCount = 0)
{
    public string? NextCursor { get; init; }
}

public readonly record struct AuditKeysetCursor(DateTime OccurredAtUtc, Guid AuditEventId)
{
    public string Encode() => Convert.ToBase64String(Encoding.UTF8.GetBytes(
        $"{OccurredAtUtc.ToUniversalTime().Ticks}:{AuditEventId:D}"))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool TryDecode(string? value, out AuditKeysetCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            var padded = value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4);
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(padded)).Split(':', 2);
            if (parts.Length != 2 || !long.TryParse(parts[0], out var ticks) || !Guid.TryParse(parts[1], out var id)) return false;
            cursor = new AuditKeysetCursor(new DateTime(ticks, DateTimeKind.Utc), id);
            return true;
        }
        catch (FormatException) { return false; }
        catch (ArgumentOutOfRangeException) { return false; }
    }
}

public sealed record AuditCaller(bool IsAdministrator, bool HasAuditRead, IReadOnlySet<string> SiteIds,
    IReadOnlySet<string> AreaIds, bool IsActive = true, bool CanReadCorrelation = false)
{
    public static AuditCaller Administrator() => new(true, true, new HashSet<string>(), new HashSet<string>(), true, true);
    public static AuditCaller Viewer() => new(false, false, new HashSet<string>(), new HashSet<string>());
}

public static class AuditRedaction
{
    private static readonly string[] SensitiveNames =
        ["password", "passwordhash", "secret", "token", "credential", "connection" + "string", "privatekey"];

    public static AuditEventRecord ForCaller(AuditEventRecord value, AuditCaller caller)
    {
        var correlation = caller.CanReadCorrelation ? value.CorrelationId : null;
        return value with
        {
            CorrelationId = correlation,
            Before = RedactMap(value.Before),
            After = RedactMap(value.After)
        };
    }

    private static IReadOnlyDictionary<string, object?> RedactMap(
        IReadOnlyDictionary<string, object?> values)
    {
        var output = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            if (SensitiveNames.Any(name => pair.Key.Contains(name, StringComparison.OrdinalIgnoreCase)))
                continue;
            output[pair.Key] = RedactNested(pair.Value);
        }
        return output;
    }

    private static object? RedactNested(object? value) => value switch
    {
        IReadOnlyDictionary<string, object?> map => RedactMap(map),
        IEnumerable<KeyValuePair<string, object?>> pairs =>
            RedactMap(pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)),
        JsonElement element => RedactJsonElement(element),
        _ => value
    };

    private static object? RedactJsonElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (SensitiveNames.Any(name => property.Name.Contains(name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                map[property.Name] = RedactJsonElement(property.Value);
            }
            return map;
        }
        if (element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray().Select(RedactJsonElement).ToArray();
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString();
        if (element.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            return element;
        return null;
    }
}
