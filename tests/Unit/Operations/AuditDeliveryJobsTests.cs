using IUMP.Modules.Operations.Application;
using IUMP.Modules.Operations.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Operations;

public static class AuditDeliveryJobsTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }
    public static int FailureCount { get; private set; }

    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        var assertions = 0;
        void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) failures.Add(message);
        }

        var now = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);
        var repository = new FakeOperationsRepositories();
        var jobs = new AuditDeliveryJobs(repository);
        Check(jobs.RetrySchedule.Count == 5 && jobs.RetrySchedule[^1] == TimeSpan.FromSeconds(30),
            "retry schedule must be capped at 30 seconds");
        Check(jobs.NextRetry(1) == TimeSpan.FromMilliseconds(250) && jobs.NextRetry(10) == TimeSpan.FromSeconds(30),
            "retry schedule must select the bounded attempt delay");

        var scheduled = await repository.EnqueueAsync(new JobType("AuditDelivery"), new IdempotencyKey("event-1"),
            SafeJobPayload.Create("eventId=one;purpose=audit"), now);
        var claimed = (await repository.ClaimDueAsync(now, "worker", 1)).Single();
        var expired = await jobs.ReconcileAsync(now.AddSeconds(31));
        Check(expired.Released == 1 && repository.Snapshot().Single().Status == JobState.Pending,
            "reconciliation must release expired leases for retry");

        var attemptTime = now.AddSeconds(32);
        for (var attempt = 2; attempt <= 10; attempt++)
        {
            var poisonClaim = (await repository.ClaimDueAsync(attemptTime, "worker", 1)).Single();
            if (attempt == 10)
                await repository.FailAsync(poisonClaim, "poison payload", attemptTime);
            else
                await repository.RescheduleAsync(poisonClaim, attemptTime.AddSeconds(1), "poison payload", attemptTime);
            attemptTime = attemptTime.AddSeconds(2);
        }
        var replay = await jobs.ReplayAsync(scheduled.Job.Id, "operator-1", now.AddSeconds(33));
        Check(replay.Succeeded && replay.Code == "REPLAYED" && replay.Job!.Status == JobState.Pending,
            "operator replay must reopen a failed job with stable identity");
        var replayAgain = await jobs.ReplayAsync(scheduled.Job.Id, "operator-1", now.AddSeconds(33));
        Check(replayAgain.Succeeded && replayAgain.Idempotent && replayAgain.Code == "ALREADY_PENDING",
            "repeated operator replay must be idempotent");

        repository.PublishedWithoutAuditCount = 1;
        var diagnostics = await jobs.ReconcileAsync(now.AddSeconds(34));
        Check(diagnostics.PublishedWithoutAudit == 1, "published-without-audit diagnostics must be explicit, not inferred");
        Check(repository.Snapshot().Single().AttemptCount >= 10, "poison exhaustion must retain all delivery attempts");

        TestCount = 6;
        AssertionCount = assertions;
        FailureCount = failures.Count;
        return failures;
    }
}
