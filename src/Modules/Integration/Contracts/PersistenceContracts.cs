namespace IUMP.Modules.Integration.Contracts;

public readonly record struct OutboxMessageId(Guid Value);

public readonly record struct InboxMessageId(string ConsumerName, Guid EventId);

public readonly record struct IntegrationEventType(string Value);

public interface IIntegrationStore
{
    ValueTask<bool> TryRecordInboxAsync(InboxMessageId id, CancellationToken cancellationToken);

    ValueTask EnqueueOutboxAsync(
        OutboxMessageId id,
        IntegrationEventType eventType,
        int schemaVersion,
        CancellationToken cancellationToken);
}
