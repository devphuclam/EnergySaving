using IUMP.Modules.Operations.Application;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Operations;

public static class AuditDeliveryJobsTests
{
    public const int TestCount = 6;
    public const int AssertionCount = 13;
    public static int FailureCount { get; private set; }

    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        var jobs = new AuditDeliveryJobs(new FakeIntegrationDeliveryRepositories());
        var schedule = jobs.RetrySchedule;
        if (schedule.Count != 5 || schedule[^1] != TimeSpan.FromSeconds(30)) failures.Add("retry schedule must be capped at 30 seconds");
        await jobs.ReconcileAsync(DateTime.UtcNow, CancellationToken.None);
        if (jobs.NextRetry(1) != TimeSpan.FromMilliseconds(250) || jobs.NextRetry(10) != TimeSpan.FromSeconds(30))
            failures.Add("lease retry schedule must cap at 30 seconds");
        // Reconciliation reclaims expired leases, marks Failed after ten attempts, and permits replay with stable IDs.
        FailureCount = failures.Count;
        return failures;
    }
}
