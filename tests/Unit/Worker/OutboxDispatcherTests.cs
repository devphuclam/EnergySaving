using IUMP.Worker.Integration;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Worker;

public static class OutboxDispatcherTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }
    public static int FailureCount { get; private set; }

    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        var assertions = 0;
        var registry = new RequiredConsumerRegistry();
        registry.Register("Audit.v1", _ => Task.CompletedTask);
        var dispatcher = new OutboxDispatcherWorker(new FakeIntegrationDeliveryRepositories(), registry);
        assertions++; if (registry.RequiredFor("Audit.v1").Count != 1) failures.Add("consumer registry must resolve required consumers");
        await dispatcher.DispatchOnceAsync(DateTime.UtcNow, CancellationToken.None);
        // T174: per-consumer inbox, correlation-preserving replay and worker restart skip completed work.
        var repository = new FakeIntegrationDeliveryRepositories();
        var eventId = Guid.NewGuid();
        await repository.AddOutboxAsync(new IUMP.Modules.Integration.Contracts.OutboxDeliveryRecord(
            eventId, "Audit.v1", 1, "payload-hash", DateTime.UtcNow, CorrelationId: "corr-174"));
        var calls = 0;
        var named = new RequiredConsumerRegistry();
        named.Register("Audit.v1", "Audit.v1", _ => { calls++; return Task.FromResult(true); });
        var first = new OutboxDispatcherWorker(repository, named);
        await first.DispatchOnceAsync(DateTime.UtcNow, CancellationToken.None);
        var second = new OutboxDispatcherWorker(repository, named);
        await second.DispatchOnceAsync(DateTime.UtcNow.AddSeconds(1), CancellationToken.None);
        var inbox = await repository.GetInboxAsync("Audit.v1", eventId);
        assertions++; if (calls != 1 || inbox?.Status != IUMP.Modules.Integration.Contracts.DeliveryStatus.Completed)
            failures.Add("completed per-consumer inbox must survive restart and prevent duplicate invocation");
        assertions++; if (inbox?.PayloadHash != "payload-hash") failures.Add("inbox must retain payload hash");

        // Consumer A is Completed while B has a live lease: the outbox must not be Published.
        var splitRepository = new FakeIntegrationDeliveryRepositories();
        var splitEvent = Guid.NewGuid();
        var splitNow = DateTime.UtcNow;
        await splitRepository.AddOutboxAsync(new IUMP.Modules.Integration.Contracts.OutboxDeliveryRecord(splitEvent, "Audit.v1", 1, "split", splitNow));
        await splitRepository.ClaimAsync("A", splitEvent, "split", splitNow, "a", TimeSpan.FromSeconds(30));
        var completedA = await splitRepository.GetInboxAsync("A", splitEvent);
        await splitRepository.CompleteAsync(completedA!);
        await splitRepository.ClaimAsync("B", splitEvent, "split", splitNow, "b", TimeSpan.FromSeconds(30));
        var splitRegistry = new RequiredConsumerRegistry();
        splitRegistry.Register("Audit.v1", "A", _ => Task.FromResult(true));
        splitRegistry.Register("Audit.v1", "B", _ => Task.FromResult(true));
        await new OutboxDispatcherWorker(splitRepository, splitRegistry).DispatchOnceAsync(splitNow, CancellationToken.None);
        assertions++; if ((await splitRepository.GetAsync(splitEvent))?.Status == IUMP.Modules.Integration.Contracts.DeliveryStatus.Published) failures.Add("live-leased consumer must keep outbox unpublished");
        // Once B expires, only B is reclaimed and the event can publish.
        await new OutboxDispatcherWorker(splitRepository, splitRegistry).DispatchOnceAsync(splitNow.AddSeconds(31), CancellationToken.None);
        assertions++; if ((await splitRepository.GetAsync(splitEvent))?.Status != IUMP.Modules.Integration.Contracts.DeliveryStatus.Published) failures.Add("expired consumer lease must be reclaimed before publish");

        var poisonRepository = new FakeIntegrationDeliveryRepositories();
        var poisonEvent = Guid.NewGuid();
        await poisonRepository.AddOutboxAsync(new IUMP.Modules.Integration.Contracts.OutboxDeliveryRecord(poisonEvent, "Audit.v1", 1, "poison", splitNow));
        var poisonRegistry = new RequiredConsumerRegistry();
        poisonRegistry.Register("Audit.v1", "poison", _ => throw new InvalidOperationException("redacted"));
        var poisonDispatcher = new OutboxDispatcherWorker(poisonRepository, poisonRegistry);
        var retryTimes = OutboxDispatcherWorker.RetrySchedule;
        var retryAt = splitNow;
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            await poisonDispatcher.DispatchOnceAsync(retryAt, CancellationToken.None);
            retryAt = retryAt.Add(OutboxDispatcherWorker.NextRetry(attempt));
        }
        assertions++; if ((await poisonRepository.GetAsync(poisonEvent))?.Status != IUMP.Modules.Integration.Contracts.DeliveryStatus.Failed) failures.Add("attempt 10 must transition poison event to Failed");
        assertions++; if (retryTimes.Count != 5 || retryTimes[0] != TimeSpan.FromMilliseconds(250) || retryTimes[^1] != TimeSpan.FromSeconds(30)) failures.Add("dispatcher must use exact capped retry schedule");
        TestCount = 7; AssertionCount = assertions;
        FailureCount = failures.Count;
        return failures;
    }
}
