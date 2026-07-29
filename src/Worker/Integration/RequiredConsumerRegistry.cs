using IUMP.Modules.Integration.Contracts;

namespace IUMP.Worker.Integration;

public sealed class RequiredConsumerRegistry
{
    private readonly Dictionary<string, List<Func<OutboxDeliveryRecord, Task>>> _consumers = new(StringComparer.Ordinal);

    public void Register(string eventType, Func<OutboxDeliveryRecord, Task> consumer)
    {
        if (string.IsNullOrWhiteSpace(eventType) || consumer is null) throw new ArgumentNullException(nameof(consumer));
        if (!_consumers.TryGetValue(eventType, out var handlers)) _consumers[eventType] = handlers = new();
        handlers.Add(consumer);
    }

    public IReadOnlyList<Func<OutboxDeliveryRecord, Task>> RequiredFor(string eventType) =>
        _consumers.TryGetValue(eventType, out var handlers) ? handlers : Array.Empty<Func<OutboxDeliveryRecord, Task>>();
}
