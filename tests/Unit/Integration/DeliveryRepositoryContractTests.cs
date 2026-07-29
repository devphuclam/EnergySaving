using IUMP.Modules.Integration.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Integration;

public static class DeliveryRepositoryContractTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }
    public static int FailureCount { get; private set; }

    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        var assertions = 0;
        var repository = new FakeIntegrationDeliveryRepositories();
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await repository.AddOutboxAsync(new OutboxDeliveryRecord(id, "Audit.v1", 1, "hash", now));
        var claim = await repository.ClaimAsync(now, "worker", TimeSpan.FromSeconds(30));
        assertions++; if (claim is null) failures.Add("due outbox must be claimable");
        await repository.MarkFailedAsync(id, "redacted", now);
        assertions++; if ((await repository.GetAsync(id))?.Status != DeliveryStatus.Failed) failures.Add("failed state required");
        var inbox = await repository.ClaimAsync("Audit.v1", id, "hash", now, "worker", TimeSpan.FromSeconds(30));
        assertions++; if (inbox is null || inbox.LeaseUntilUtc is null || inbox.AttemptCount != 1)
            failures.Add("inbox claim must persist payload hash, lease and retry attempt");
        assertions++; if (await repository.ClaimAsync("Audit.v1", id, "hash", now.AddSeconds(1), "worker-2", TimeSpan.FromSeconds(30)) is not null)
            failures.Add("a live Claimed inbox lease must not be stolen");
        var reclaimed = await repository.ClaimAsync("Audit.v1", id, "hash", now.AddSeconds(31), "worker-2", TimeSpan.FromSeconds(30));
        assertions++; if (reclaimed is null || reclaimed.AttemptCount != 2) failures.Add("an expired inbox lease must be reclaimed");
        await repository.CompleteAsync(reclaimed!, CancellationToken.None);
        assertions++; if (await repository.ClaimAsync("Audit.v1", id, "hash", now.AddSeconds(32), "worker-3", TimeSpan.FromSeconds(30)) is not null)
            failures.Add("completed inbox must deduplicate after restart");
        var conflict = false;
        try { await repository.ClaimAsync("Audit.v1", id, "different-hash", DateTime.UtcNow, "worker", TimeSpan.FromSeconds(30)); }
        catch (InvalidOperationException ex) { conflict = ex.Message.Contains("hash", StringComparison.OrdinalIgnoreCase); }
        assertions++; if (!conflict) failures.Add("inbox hash conflict must be typed and fail closed");
        TestCount = 7; AssertionCount = assertions; FailureCount = failures.Count;
        return failures;
    }
}
