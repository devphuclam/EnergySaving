using IUMP.Modules.Integration.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Integration;

public static class DeliveryRepositoryContractTests
{
    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        var repository = new FakeIntegrationDeliveryRepositories();
        var id = Guid.NewGuid();
        await repository.AddOutboxAsync(new OutboxDeliveryRecord(id, "Audit.v1", 1, "hash", DateTime.UtcNow));
        var claim = await repository.ClaimAsync(DateTime.UtcNow, "worker", TimeSpan.FromSeconds(30));
        if (claim is null) failures.Add("due outbox must be claimable");
        await repository.MarkFailedAsync(id, "redacted", DateTime.UtcNow);
        if ((await repository.GetAsync(id))?.Status != DeliveryStatus.Failed) failures.Add("failed state required");
        return failures;
    }
}
