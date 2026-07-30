using IUMP.Modules.Integration.Contracts;
using IUMP.BuildingBlocks.Persistence;

namespace IUMP.Worker.Integration;

public sealed class RequiredConsumerRegistry
{
    private readonly Dictionary<string, List<RegisteredConsumer>> _consumers = new(StringComparer.Ordinal);

    public sealed record RegisteredConsumer(
        string Name,
        Func<OutboxDeliveryRecord, IHostTransaction?, CancellationToken, Task<bool>> Handler);

    public void Register(string eventType, Func<OutboxDeliveryRecord, Task> consumer)
    {
        if (string.IsNullOrWhiteSpace(eventType) || consumer is null) throw new ArgumentNullException(nameof(consumer));
        if (!_consumers.TryGetValue(eventType, out var handlers)) _consumers[eventType] = handlers = new();
        handlers.Add(new RegisteredConsumer($"{eventType}:{handlers.Count + 1}", async (record, _, _) =>
        {
            await consumer(record);
            return true;
        }));
    }

    public IReadOnlyList<Func<OutboxDeliveryRecord, Task>> RequiredFor(string eventType) =>
        _consumers.TryGetValue(eventType, out var handlers)
            ? handlers.Select(item => new Func<OutboxDeliveryRecord, Task>(
                async record => { await item.Handler(record, null, CancellationToken.None); })).ToArray()
            : Array.Empty<Func<OutboxDeliveryRecord, Task>>();

    public void Register(string eventType, string consumerName, Func<OutboxDeliveryRecord, Task<bool>> consumer)
    {
        if (string.IsNullOrWhiteSpace(eventType) || string.IsNullOrWhiteSpace(consumerName) || consumer is null)
            throw new ArgumentException("eventType, consumerName and handler are required");
        if (!_consumers.TryGetValue(eventType, out var handlers)) _consumers[eventType] = handlers = new();
        handlers.Add(new RegisteredConsumer(
            consumerName, (record, _, _) => consumer(record)));
    }

    public void RegisterTransactional(
        string eventType,
        string consumerName,
        Func<OutboxDeliveryRecord, IHostTransaction, CancellationToken, Task<bool>> consumer)
    {
        if (string.IsNullOrWhiteSpace(eventType) ||
            string.IsNullOrWhiteSpace(consumerName) ||
            consumer is null)
            throw new ArgumentException(
                "eventType, consumerName and handler are required");
        if (!_consumers.TryGetValue(eventType, out var handlers))
            _consumers[eventType] = handlers = new();
        handlers.Add(new RegisteredConsumer(
            consumerName,
            (record, transaction, ct) =>
                transaction is null
                    ? throw new InvalidOperationException(
                        "TRANSACTIONAL_CONSUMER_REQUIRES_HOST_TRANSACTION")
                    : consumer(record, transaction, ct)));
    }

    public IReadOnlyList<RegisteredConsumer> RequiredConsumersFor(string eventType) =>
        _consumers.TryGetValue(eventType, out var handlers) ? handlers :
        _consumers.TryGetValue("*", out var fallback) ? fallback :
        Array.Empty<RegisteredConsumer>();
}
