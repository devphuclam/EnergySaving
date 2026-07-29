using IUMP.Modules.Integration.Contracts;

namespace IUMP.Worker.Integration;

public sealed class RequiredConsumerRegistry
{
    private readonly Dictionary<string, List<RegisteredConsumer>> _consumers = new(StringComparer.Ordinal);

    public sealed record RegisteredConsumer(string Name, Func<OutboxDeliveryRecord, Task<bool>> Handler);

    public void Register(string eventType, Func<OutboxDeliveryRecord, Task> consumer)
    {
        if (string.IsNullOrWhiteSpace(eventType) || consumer is null) throw new ArgumentNullException(nameof(consumer));
        if (!_consumers.TryGetValue(eventType, out var handlers)) _consumers[eventType] = handlers = new();
        handlers.Add(new RegisteredConsumer($"{eventType}:{handlers.Count + 1}", async record =>
        {
            await consumer(record);
            return true;
        }));
    }

    public IReadOnlyList<Func<OutboxDeliveryRecord, Task>> RequiredFor(string eventType) =>
        _consumers.TryGetValue(eventType, out var handlers)
            ? handlers.Select(item => new Func<OutboxDeliveryRecord, Task>(async record => { await item.Handler(record); })).ToArray()
            : Array.Empty<Func<OutboxDeliveryRecord, Task>>();

    public void Register(string eventType, string consumerName, Func<OutboxDeliveryRecord, Task<bool>> consumer)
    {
        if (string.IsNullOrWhiteSpace(eventType) || string.IsNullOrWhiteSpace(consumerName) || consumer is null)
            throw new ArgumentException("eventType, consumerName and handler are required");
        if (!_consumers.TryGetValue(eventType, out var handlers)) _consumers[eventType] = handlers = new();
        handlers.Add(new RegisteredConsumer(consumerName, consumer));
    }

    public IReadOnlyList<RegisteredConsumer> RequiredConsumersFor(string eventType) =>
        _consumers.TryGetValue(eventType, out var handlers) ? handlers : Array.Empty<RegisteredConsumer>();
}
