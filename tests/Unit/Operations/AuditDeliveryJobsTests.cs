using IUMP.Modules.Operations.Application;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Operations;

public static class AuditDeliveryJobsTests
{
    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        var jobs = new AuditDeliveryJobs(new FakeIntegrationDeliveryRepositories());
        var schedule = jobs.RetrySchedule;
        if (schedule.Count != 5 || schedule[^1] != TimeSpan.FromSeconds(30)) failures.Add("retry schedule must be capped at 30 seconds");
        await jobs.ReconcileAsync(DateTime.UtcNow, CancellationToken.None);
        return failures;
    }
}
