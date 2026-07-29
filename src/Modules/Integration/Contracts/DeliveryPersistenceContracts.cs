namespace IUMP.Modules.Integration.Contracts;

public enum DeliveryStatus { Pending, Claimed, Published, Completed, Failed }

public sealed record OutboxDeliveryRecord(
    Guid EventId, string EventType, int SchemaVersion, string PayloadHash, DateTime AvailableAtUtc,
    DeliveryStatus Status = DeliveryStatus.Pending, int AttemptCount = 0, string? LeaseOwner = null,
    Guid? LeaseToken = null, DateTime? LeaseUntilUtc = null, string? Error = null, string? CorrelationId = null,
    string? CausationId = null, long Version = 1);

public sealed record InboxDeliveryRecord(
    string ConsumerName, Guid EventId, string PayloadHash, DateTime AvailableAtUtc,
    DeliveryStatus Status = DeliveryStatus.Pending, int AttemptCount = 0, string? LeaseOwner = null,
    Guid? LeaseToken = null, DateTime? LeaseUntilUtc = null, string? Error = null, long Version = 1);

public interface IOutboxClaimRepository
{
    Task<OutboxDeliveryRecord?> ClaimAsync(DateTime nowUtc, string owner, TimeSpan lease, CancellationToken ct = default);
    Task<OutboxDeliveryRecord?> RenewAsync(OutboxDeliveryRecord record, DateTime leaseUntilUtc, CancellationToken ct = default);
    Task MarkPublishedAsync(Guid eventId, CancellationToken ct = default);
    Task RescheduleAsync(Guid eventId, DateTime availableAtUtc, string redactedError, CancellationToken ct = default);
    Task MarkFailedAsync(Guid eventId, string redactedError, DateTime nowUtc, CancellationToken ct = default);
    Task<OutboxDeliveryRecord?> GetAsync(Guid eventId, CancellationToken ct = default);
}

public interface IInboxDeduplicationRepository
{
    Task<InboxDeliveryRecord?> ClaimAsync(string consumerName, Guid eventId, string payloadHash, DateTime nowUtc,
        string owner, TimeSpan lease, CancellationToken ct = default);
    Task CompleteAsync(InboxDeliveryRecord record, CancellationToken ct = default);
    Task RescheduleAsync(InboxDeliveryRecord record, DateTime availableAtUtc, string redactedError, CancellationToken ct = default);
    Task MarkFailedAsync(InboxDeliveryRecord record, string redactedError, DateTime nowUtc, CancellationToken ct = default);
}

public interface IIntegrationDeliveryRepository : IOutboxClaimRepository, IInboxDeduplicationRepository
{
    Task AddOutboxAsync(OutboxDeliveryRecord record, CancellationToken ct = default);
}
