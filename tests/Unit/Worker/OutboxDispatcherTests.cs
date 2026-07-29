using IUMP.Worker.Integration;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Worker;

public static class OutboxDispatcherTests
{
    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        var registry = new RequiredConsumerRegistry();
        registry.Register("Audit.v1", _ => Task.CompletedTask);
        var dispatcher = new OutboxDispatcherWorker(new FakeIntegrationDeliveryRepositories(), registry);
        if (registry.RequiredFor("Audit.v1").Count != 1) failures.Add("consumer registry must resolve required consumers");
        await dispatcher.DispatchOnceAsync(DateTime.UtcNow, CancellationToken.None);
        return failures;
    }
}
