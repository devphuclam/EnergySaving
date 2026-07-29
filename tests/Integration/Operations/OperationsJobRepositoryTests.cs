using IUMP.Modules.Operations.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Integration.Operations;

public interface IOperationsJobRepositoryTestProviderFactory
{
    OperationsJobRepositoryFixture Create();
}

public sealed record OperationsJobRepositoryFixture(
    IDurableJobScheduler Scheduler,
    IJobClaimRepository Claims);

/// <summary>
/// Provider-neutral contract runner.  It intentionally knows only the
/// Operations ports; the PostgreSQL adapter is a later package-policy task.
/// </summary>
public sealed class OperationsJobRepositoryContractRunner
{
    public int TestCount { get; private set; }
    public int AssertionCount { get; private set; }
    public List<string> Failures { get; } = [];

    public async Task RunAllAsync(IOperationsJobRepositoryTestProviderFactory factory)
    {
        TestCount = 0;
        AssertionCount = 0;
        Failures.Clear();
        var fixture = factory.Create();
        var now = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);
        var type = new JobType("ContractHealth");
        var key = new IdempotencyKey("point:contract");
        var payload = SafeJobPayload.Create("pointId=50000000-0000-0000-0000-000000000001;purpose=health");

        TestCount++;
        var first = await fixture.Scheduler.EnqueueAsync(type, key, payload, now);
        var same = await fixture.Scheduler.EnqueueAsync(type, key, payload, now);
        var conflict = await fixture.Scheduler.EnqueueAsync(type, key, SafeJobPayload.Create("pointId=other"), now);
        Check(first.Created, "contract enqueue creates", Failures);
        Check(same.Equivalent, "contract equivalent replay", Failures);
        Check(conflict.Conflict, "contract conflicting payload", Failures);

        TestCount++;
        var claims = await fixture.Claims.ClaimDueAsync(now, "contract-worker", 1);
        Check(claims.Count == 1, "contract claim due", Failures);
        Check(claims[0].LeaseExpiresAtUtc == now.AddSeconds(30), "contract 30 second lease", Failures);
        var wrong = claims[0] with { Token = Guid.NewGuid() };
        Check(!(await fixture.Claims.RenewAsync(wrong, now)).Succeeded, "contract wrong token rejected", Failures);
        var renewed = await fixture.Claims.RenewAsync(claims[0], now);
        Check(renewed.Succeeded, "contract renew", Failures);

        TestCount++;
        var renewedClaim = claims[0] with
        {
            Job = renewed.Job!,
            LeaseExpiresAtUtc = renewed.Job!.LeaseExpiresAtUtc!.Value
        };
        var retry = await fixture.Claims.RescheduleAsync(renewedClaim, now.AddSeconds(30), "temporary", now);
        Check(retry.Succeeded && retry.Job!.Status == JobState.Pending, "contract retry", Failures);
        var claimAgain = (await fixture.Claims.ClaimDueAsync(now.AddSeconds(30), "contract-worker", 1)).Single();
        var complete = await fixture.Claims.CompleteAsync(claimAgain, now.AddSeconds(31));
        var replay = await fixture.Claims.CompleteAsync(claimAgain, now.AddSeconds(31));
        Check(complete.Succeeded && complete.Job!.Status == JobState.Completed, "contract complete", Failures);
        Check(replay.Succeeded && replay.Idempotent, "contract completion replay", Failures);

        TestCount++;
        Check((await fixture.Claims.ListExpiredAsync(now.AddMinutes(1))).Count == 0,
            "contract reconciliation is idempotent", Failures);
    }

    private void Check(bool condition, string message, List<string> failures)
    {
        AssertionCount++;
        if (!condition) failures.Add(message);
    }
}

public sealed class FakeOperationsJobRepositoryTestProviderFactory : IOperationsJobRepositoryTestProviderFactory
{
    public OperationsJobRepositoryFixture Create()
    {
        var fake = new FakeOperationsRepositories();
        return new OperationsJobRepositoryFixture(fake, fake);
    }
}
