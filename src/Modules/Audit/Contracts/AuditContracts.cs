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
    string CorrelationId,
    string? ActorId,
    string? ActorUsername,
    IReadOnlyDictionary<string, object?> Before,
    IReadOnlyDictionary<string, object?> After,
    string? SiteId,
    string? AreaId,
    string? CausationId);

public interface IAuditEventConsumer
{
    Task<AuditEventRecord> ConsumeAsync(AuditEventEnvelope envelope, CancellationToken ct = default);
}

public interface IAuditAppendRepository
{
    Task<AuditEventRecord?> AppendIfAbsentAsync(AuditEventRecord record, CancellationToken ct = default);
}

public interface IAuditQueryRepository
{
    Task<IReadOnlyList<AuditEventRecord>> QueryAsync(AuditQueryRequest request, CancellationToken ct = default);
}

public sealed record AuditQueryRequest(string? ObjectType, string? Action, string? ActorId, string? CorrelationId,
    DateTime? FromUtc, int Page, int PageSize);

public sealed record AuditQueryResult(IReadOnlyList<AuditEventRecord> Items, string? ErrorCode = null,
    int TotalCount = 0);

public sealed record AuditCaller(bool IsAdministrator, bool HasAuditRead, IReadOnlySet<string> SiteIds,
    IReadOnlySet<string> AreaIds, bool IsActive = true)
{
    public static AuditCaller Administrator() => new(true, true, new HashSet<string>(), new HashSet<string>());
    public static AuditCaller Viewer() => new(false, false, new HashSet<string>(), new HashSet<string>());
}
