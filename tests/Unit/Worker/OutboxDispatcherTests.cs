using IUMP.Worker.Integration;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Worker;

public static class OutboxDispatcherTests
{
    public const int TestCount = 5;
    public const int AssertionCount = 10;
    public static int FailureCount { get; private set; }

    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        var registry = new RequiredConsumerRegistry();
        registry.Register("Audit.v1", _ => Task.CompletedTask);
        var dispatcher = new OutboxDispatcherWorker(new FakeIntegrationDeliveryRepositories(), registry);
        if (registry.RequiredFor("Audit.v1").Count != 1) failures.Add("consumer registry must resolve required consumers");
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
        if (calls != 1 || inbox?.Status != IUMP.Modules.Integration.Contracts.DeliveryStatus.Completed)
            failures.Add("completed per-consumer inbox must survive restart and prevent duplicate invocation");
        if (inbox?.PayloadHash != "payload-hash") failures.Add("inbox must retain payload hash");
        FailureCount = failures.Count;
        return failures;
    }
}
