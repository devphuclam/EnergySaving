using IUMP.Modules.Operations.Application;
using IUMP.Modules.Operations.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Operations;

public static class DurableJobTests
{
    public static int TestCount { get; private set; }
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        TestCount = 0;
        CheckCount = 0;
        var failures = new List<string>();
        Case("unique scheduling and safe payload conflicts", failures, () =>
        {
            var fake = new FakeOperationsRepositories();
            var type = new JobType("Health");
            var key = new IdempotencyKey("point:1:health");
            var at = Utc(2026, 7, 29, 8, 0, 0);
            var payload = SafeJobPayload.Create("pointId=10000000-0000-0000-0000-000000000001;purpose=health");
            var created = fake.EnqueueAsync(type, key, payload, at).Result;
            var equivalent = fake.EnqueueAsync(type, key, payload, at).Result;
            Check(created.Created && fake.Count == 1, "unique job created once", failures);
            Check(equivalent.Equivalent && !equivalent.Created, "equivalent enqueue returns existing", failures);
            var conflict = fake.EnqueueAsync(type, key, SafeJobPayload.Create("pointId=other;purpose=health"), at).Result;
            Check(conflict.Conflict && conflict.Code == "JOB_IDEMPOTENCY_CONFLICT", "payload conflict fails closed", failures);
            try { SafeJobPayload.Create("credential=never-store"); failures.Add("sensitive payload accepted"); }
            catch (InvalidOperationException ex) { Check(ex.Message == "JOB_PAYLOAD_SENSITIVE", "sensitive payload rejected", failures); }
        });
        Case("claim ordering, lease, renewal, and expiry reclaim", failures, () =>
        {
            var fake = new FakeOperationsRepositories();
            var now = Utc(2026, 7, 29, 9, 0, 0);
            for (var i = 0; i < 3; i++)
                fake.EnqueueAsync(new JobType("Health"), new IdempotencyKey($"point:{i}"),
                    SafeJobPayload.Create($"pointId=00000000-0000-0000-0000-{i + 1:000000000000}"), now).Wait();
            var claims = fake.ClaimDueAsync(now, "worker-a", 2).Result;
            Check(claims.Count == 2 && claims[0].Job.AvailableAtUtc <= claims[1].Job.AvailableAtUtc,
                "due jobs claim deterministically", failures);
            Check(claims[0].LeaseExpiresAtUtc == now.AddSeconds(30), "lease is exactly 30 seconds", failures);
            Check(fake.ClaimDueAsync(now, "worker-b", 3).Result.Count == 1, "live leases excluded", failures);
            var wrongOwner = claims[0] with { Owner = "worker-b" };
            Check(!fake.RenewAsync(wrongOwner, now.AddSeconds(1)).Result.Succeeded, "wrong owner cannot renew", failures);
            var renewed = fake.RenewAsync(claims[0], now.AddSeconds(1)).Result;
            Check(renewed.Succeeded && renewed.Job!.LeaseExpiresAtUtc == now.AddSeconds(31), "current owner renews", failures);
            var expiredAt = now.AddSeconds(30.5);
            var expired = fake.ListExpiredAsync(expiredAt).Result;
            Check(expired.Count == 2, "expired leases are listed for reclaim", failures);
            var expiredClaim = new JobClaim(expired[0], expired[0].LeaseOwner!, expired[0].LeaseToken!.Value, expired[0].LeaseExpiresAtUtc!.Value);
            Check(fake.ReleaseAsync(expiredClaim, expiredAt, expiredAt).Result.Succeeded,
                "expired claim releases", failures);
        });
        Case("completion replay, retry, and terminal failure", failures, () =>
        {
            var fake = new FakeOperationsRepositories();
            var now = Utc(2026, 7, 29, 10, 0, 0);
            fake.EnqueueAsync(new JobType("Work"), new IdempotencyKey("one"), SafeJobPayload.Create("pointId=one"), now).Wait();
            var claim = fake.ClaimDueAsync(now, "worker", 1).Result.Single();
            var completed = fake.CompleteAsync(claim, now.AddSeconds(1)).Result;
            var replay = fake.CompleteAsync(claim, now.AddSeconds(1)).Result;
            Check(completed.Succeeded && completed.Job!.Status == JobState.Completed, "completion succeeds once", failures);
            Check(replay.Succeeded && replay.Idempotent, "completion replay is no-op", failures);

            fake.EnqueueAsync(new JobType("Work"), new IdempotencyKey("retry"), SafeJobPayload.Create("pointId=retry"), now).Wait();
            var retryClaim = fake.ClaimDueAsync(now, "worker", 1).Result.Single();
            Check(fake.RescheduleAsync(retryClaim, now.AddSeconds(30), "credential=redacted", now).Result.Succeeded,
                "retry reschedules", failures);
            var retryAgain = fake.ClaimDueAsync(now.AddSeconds(30), "worker", 1).Result.Single();
            Check(retryAgain.Job.AttemptCount == 2, "attempt count increments once per claim", failures);
            Check(fake.FailAsync(retryAgain, "credential=redacted", now.AddSeconds(31)).Result.Succeeded,
                "terminal failure succeeds", failures);
            Check(fake.Snapshot().Single(job => job.IdempotencyKey.Value == "retry").RedactedError!.Contains("[redacted]"),
                "only redacted error is retained", failures);
        });
        Case("health scheduling and reconciliation are idempotent", failures, () =>
        {
            var fake = new FakeOperationsRepositories();
            var clock = new Clock(Utc(2026, 7, 29, 11, 0, 0));
            var handler = new Handler();
            var jobs = new SourceHealthJobs(fake, fake, handler, clock);
            var pointId = Guid.Parse("40000000-0000-0000-0000-000000000001");
            var first = jobs.ScheduleAsync(pointId, clock.UtcNow).Result;
            var second = jobs.ScheduleAsync(pointId, clock.UtcNow).Result;
            Check(first.Created && second.Equivalent && fake.Count == 1, "one stable health job per point", failures);
            var cycle = jobs.RunDueAsync("health-worker").Result;
            Check(cycle.Completed == 1 && handler.Points.SequenceEqual(new[] { pointId }), "claimed job invokes health port and completes", failures);
            var failurePoint = Guid.Parse("40000000-0000-0000-0000-000000000002");
            handler.Retry = true;
            jobs.ScheduleAsync(failurePoint, clock.UtcNow).Wait();
            var retryCycle = jobs.RunDueAsync("health-worker").Result;
            Check(retryCycle.Retried == 1, "retryable health failure reschedules", failures);
            clock.UtcNow = clock.UtcNow.AddSeconds(31);
            var reclaimed = jobs.ReconcileExpiredAsync().Result;
            Check(reclaimed == 0, "no live claim is falsely reclaimed", failures);
        });
        return failures;
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute, int second) =>
        new(year, month, day, hour, minute, second, DateTimeKind.Utc);

    private static void Case(string name, List<string> failures, Action body)
    {
        TestCount++;
        try { body(); }
        catch (Exception ex) { failures.Add($"{name}: {ex.Message}"); }
    }

    private static void Check(bool condition, string message, List<string> failures)
    {
        CheckCount++;
        if (!condition) failures.Add(message);
    }

    private sealed class Clock(DateTime value) : ITelemetryUtcClock
    {
        public DateTime UtcNow { get; set; } = value;
    }

    private sealed class Handler : ISourceHealthJobHandler
    {
        public bool Retry { get; set; }
        public List<Guid> Points { get; } = [];
        public Task<HealthJobExecutionResult> EvaluateAsync(Guid pointId, DateTime nowUtc, CancellationToken ct = default)
        {
            Points.Add(pointId);
            return Task.FromResult(Retry ? HealthJobExecutionResult.Retry("health-temporary") : HealthJobExecutionResult.Success());
        }
    }
}
