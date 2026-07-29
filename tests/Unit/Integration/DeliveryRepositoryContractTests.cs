using IUMP.Modules.Integration.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Integration;

public static class DeliveryRepositoryContractTests
{
    public const int TestCount = 7;
    public const int AssertionCount = 14;
    public static int FailureCount { get; private set; }

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
        var inbox = await repository.ClaimAsync("Audit.v1", id, "hash", DateTime.UtcNow, "worker", TimeSpan.FromSeconds(30));
        if (inbox is null || inbox.LeaseUntilUtc is null || inbox.AttemptCount != 1)
            failures.Add("inbox claim must persist payload hash, lease and retry attempt");
        await repository.CompleteAsync(inbox!, CancellationToken.None);
        if (await repository.ClaimAsync("Audit.v1", id, "hash", DateTime.UtcNow, "worker-2", TimeSpan.FromSeconds(30)) is not null)
            failures.Add("completed inbox must deduplicate after restart");
        var conflict = false;
        try { await repository.ClaimAsync("Audit.v1", id, "different-hash", DateTime.UtcNow, "worker", TimeSpan.FromSeconds(30)); }
        catch (InvalidOperationException ex) { conflict = ex.Message.Contains("hash", StringComparison.OrdinalIgnoreCase); }
        if (!conflict) failures.Add("inbox hash conflict must be typed and fail closed");
        FailureCount = failures.Count;
        return failures;
    }
}
