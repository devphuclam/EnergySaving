using IUMP.Modules.Integration.Contracts;

namespace IUMP.Worker.Integration;

public sealed class OutboxDispatcherWorker(IIntegrationDeliveryRepository repository, RequiredConsumerRegistry registry)
{
    public async Task<int> DispatchOnceAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var claimed = await repository.ClaimAsync(nowUtc, "dispatcher", TimeSpan.FromSeconds(30), ct);
        if (claimed is null) return 0;
        var handlers = registry.RequiredFor(claimed.EventType);
        if (handlers.Count == 0)
        {
            await repository.MarkFailedAsync(claimed.EventId, "NO_REQUIRED_CONSUMER", nowUtc, ct);
            return 0;
        }
        try
        {
            foreach (var handler in handlers) await handler(claimed);
            await repository.MarkPublishedAsync(claimed.EventId, ct);
            return 1;
        }
        catch (Exception)
        {
            var attempt = claimed.AttemptCount;
            if (attempt >= 10) await repository.MarkFailedAsync(claimed.EventId, "DELIVERY_EXHAUSTED", nowUtc, ct);
            else await repository.RescheduleAsync(claimed.EventId, nowUtc.AddMilliseconds(250), "DELIVERY_RETRY", ct);
            return 0;
        }
    }
}
