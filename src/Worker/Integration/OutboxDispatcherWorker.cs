using IUMP.Modules.Integration.Contracts;

namespace IUMP.Worker.Integration;

public sealed class OutboxDispatcherWorker(IIntegrationDeliveryRepository repository, RequiredConsumerRegistry registry)
{
    public static IReadOnlyList<TimeSpan> RetrySchedule { get; } = new[]
    {
        TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)
    };

    public async Task<int> DispatchOnceAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var claimed = await repository.ClaimAsync(nowUtc, "dispatcher", TimeSpan.FromSeconds(30), ct);
        if (claimed is null) return 0;
        var consumers = registry.RequiredConsumersFor(claimed.EventType);
        if (consumers.Count == 0)
        {
            await repository.MarkFailedAsync(claimed.EventId, "NO_REQUIRED_CONSUMER", nowUtc, ct);
            return 0;
        }
        try
        {
            var completedConsumers = 0;
            foreach (var consumer in consumers)
            {
                // Each required consumer has an independent inbox lease and completion record.
                // A completed consumer is skipped after restart; incomplete consumers are invoked.
                var inbox = await repository.ClaimAsync(consumer.Name, claimed.EventId, claimed.PayloadHash,
                    nowUtc, "dispatcher", TimeSpan.FromSeconds(30), ct);
                if (inbox is null)
                {
                    if (repository is IInboxStateRepository stateRepository)
                    {
                        var state = await stateRepository.GetInboxAsync(consumer.Name, claimed.EventId, ct);
                        if (state?.Status == DeliveryStatus.Completed) { completedConsumers++; continue; }
                        if (state?.Status == DeliveryStatus.Claimed && state.LeaseUntilUtc > nowUtc)
                            throw new InvalidOperationException("CONSUMER_LEASE_LIVE");
                    }
                    throw new InvalidOperationException("CONSUMER_CLAIM_UNAVAILABLE");
                }
                var handled = await consumer.Handler(claimed);
                if (!handled) throw new InvalidOperationException("CONSUMER_NOT_COMPLETE");
                await repository.CompleteAsync(inbox, ct);
                completedConsumers++;
            }
            if (completedConsumers != consumers.Count) throw new InvalidOperationException("CONSUMER_COMPLETION_INCOMPLETE");
            await repository.MarkPublishedAsync(claimed.EventId, ct);
            return 1;
        }
        catch (Exception)
        {
            var attempt = claimed.AttemptCount;
            if (attempt >= 10) await repository.MarkFailedAsync(claimed.EventId, "DELIVERY_EXHAUSTED", nowUtc, ct);
            else await repository.RescheduleAsync(claimed.EventId, nowUtc.Add(NextRetry(attempt)), "DELIVERY_RETRY", ct);
            return 0;
        }
    }

    public static TimeSpan NextRetry(int attemptCount) =>
        RetrySchedule[Math.Clamp(attemptCount - 1, 0, RetrySchedule.Count - 1)];
}
