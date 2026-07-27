using System.Collections.ObjectModel;

namespace IUMP.Modules.Integration.Contracts;

public sealed record OwnerEventEnvelope
{
    public Guid EventId { get; }
    public string EventType { get; }
    public int SchemaVersion { get; }
    public string Producer { get; }
    public string AggregateType { get; }
    public string AggregateId { get; }
    public long AggregateVersion { get; }
    public string ActorId { get; }
    public string ActorUsername { get; }
    public IReadOnlyDictionary<string, object?> Before { get; }
    public IReadOnlyDictionary<string, object?> After { get; }
    public string Action { get; }
    public string Summary { get; }
    public DateTime OccurredAt { get; }
    public string CorrelationId { get; }
    public string CausationId { get; }
    public string? SiteId { get; }
    public string? AreaId { get; }

    public OwnerEventEnvelope(Guid eventId, string eventType, int schemaVersion, string producer,
        string aggregateType, string aggregateId, long aggregateVersion, string actorId, string actorUsername,
        IReadOnlyDictionary<string, object?> before, IReadOnlyDictionary<string, object?> after,
        string action, string summary, DateTime occurredAt, string correlationId, string causationId,
        string? siteId, string? areaId)
    {
        if (eventId == Guid.Empty) throw new ArgumentException("EventId is required.", nameof(eventId));
        if (string.IsNullOrWhiteSpace(eventType) || !eventType.EndsWith(".v1", StringComparison.Ordinal)) throw new ArgumentException("EventType must be versioned.", nameof(eventType));
        if (schemaVersion != 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        if (aggregateVersion <= 0) throw new ArgumentOutOfRangeException(nameof(aggregateVersion));
        if (occurredAt.Kind != DateTimeKind.Utc) throw new ArgumentException("OccurredAt must be UTC.", nameof(occurredAt));
        if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentException("CorrelationId is required.", nameof(correlationId));
        EventId = eventId; EventType = eventType; SchemaVersion = schemaVersion; Producer = producer;
        AggregateType = aggregateType; AggregateId = aggregateId; AggregateVersion = aggregateVersion;
        ActorId = actorId; ActorUsername = actorUsername;
        Before = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(before, StringComparer.Ordinal));
        After = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(after, StringComparer.Ordinal));
        Action = action; Summary = summary; OccurredAt = occurredAt; CorrelationId = correlationId; CausationId = causationId;
        SiteId = siteId; AreaId = areaId;
    }
}

public interface ITransactionalOutboxWriter
{
    ValueTask EnqueueAsync(OwnerEventEnvelope envelope, object hostTransaction, CancellationToken ct = default);
}

// Optional adapter seam: the host coordinator owns commit ordering; the writer only participates
// in that transaction and never publishes independently.
public interface IOutboxTransactionParticipant
{
    ValueTask CommitAsync(object hostTransaction, CancellationToken ct = default);
    ValueTask RollbackAsync(object hostTransaction, CancellationToken ct = default);
}
