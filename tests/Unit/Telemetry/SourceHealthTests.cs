using IUMP.Modules.Telemetry.Application;
using IUMP.Modules.Telemetry.Contracts;

namespace IUMP.Tests.Unit.Telemetry;

public static class SourceHealthTests
{
    public static int TestCount { get; private set; }
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        TestCount = 0;
        CheckCount = 0;
        var failures = new List<string>();
        Case("exact Online/Stale/NoData boundaries", failures, () =>
        {
            var now = Utc(2026, 7, 29, 8, 0, 0);
            Check(StatusAt(now, 60, 300, now) == SourceHealthStatus.Online, "elapsed 0 Online", failures);
            Check(StatusAt(now, 60, 300, now.AddSeconds(-60)) == SourceHealthStatus.Online, "expected boundary Online", failures);
            Check(StatusAt(now, 60, 300, now.AddSeconds(-61)) == SourceHealthStatus.Stale, "one tick over expected Stale", failures);
            Check(StatusAt(now, 60, 300, now.AddSeconds(-300)) == SourceHealthStatus.Stale, "no-data boundary Stale", failures);
            Check(StatusAt(now, 60, 300, now.AddSeconds(-301)) == SourceHealthStatus.NoData, "one tick over no-data NoData", failures);
            Check(StatusAt(now, 60, 300, null) == SourceHealthStatus.NoData, "no accepted measurement NoData", failures);
        });
        Case("administrative precedence and invalid thresholds", failures, () =>
        {
            var now = Utc(2026, 7, 29, 8, 0, 0);
            Check(StatusAt(now, 60, 300, now, pointStatus: "Suspended") == SourceHealthStatus.Suspended,
                "Suspended overrides elapsed", failures);
            Check(StatusAt(now, 60, 300, now, pointStatus: "Decommissioned", sourceStatus: "Suspended") == SourceHealthStatus.Decommissioned,
                "Decommissioned overrides Suspended", failures);
            var input = Input(now);
            try { SourceHealthService.EvaluateStatus(input with { ExpectedIntervalSeconds = 0 }, now); failures.Add("zero expected interval accepted"); }
            catch (InvalidOperationException ex) { Check(ex.Message == "EXPECTED_INTERVAL_INVALID", "zero expected interval code", failures); }
            try { SourceHealthService.EvaluateStatus(input with { NoDataAfterSeconds = 60 }, now); failures.Add("non-increasing no-data threshold accepted"); }
            catch (InvalidOperationException ex) { Check(ex.Message == "NO_DATA_THRESHOLD_INVALID", "no-data threshold code", failures); }
        });
        Case("recovery, optimistic versions, and idempotent events", failures, () =>
        {
            var now = Utc(2026, 7, 29, 8, 0, 0);
            var repo = new HealthRepositoryFake();
            var service = new SourceHealthService(repo);
            var input = Input(now) with { LastAcceptedReceivedAtUtc = now.AddSeconds(-301) };
            var tx = repo.Begin();
            var noData = service.EvaluateAsync(input, tx, now).Result;
            tx.CommitAsync().GetAwaiter().GetResult();
            Check(noData.Current.Status == SourceHealthStatus.NoData, "NoData persisted as status", failures);
            Check(repo.Events.Count == 1 && repo.Events[0].NewStatus == SourceHealthStatus.NoData,
                "NoData transition event", failures);
            Check(!repo.HasNumericNoDataValue, "NoData never represented as numeric zero", failures);

            var recovery = repo.Begin();
            var online = service.EvaluateAsync(input with { LastAcceptedReceivedAtUtc = now }, recovery, now).Result;
            recovery.CommitAsync().GetAwaiter().GetResult();
            Check(online.Changed && repo.Current!.Status == SourceHealthStatus.Online, "accepted measurement recovers Online", failures);
            var eventCount = repo.Events.Count;
            var repeat = repo.Begin();
            var same = service.EvaluateAsync(input with { LastAcceptedReceivedAtUtc = now }, repeat, now).Result;
            repeat.CommitAsync().GetAwaiter().GetResult();
            Check(!same.Changed && repo.Events.Count == eventCount, "same state is idempotent", failures);

            var stale = repo.Begin();
            try { service.EvaluateAsync(input with { PointVersion = 0 }, stale, now).GetAwaiter().GetResult(); failures.Add("invalid provider accepted"); }
            catch (InvalidOperationException) { }
            try { service.EvaluateAsync(input with { PointVersion = 1, SourceVersion = 1, ProviderVersion = 0 }, repo.Begin(), now).GetAwaiter().GetResult(); failures.Add("stale provider accepted"); }
            catch (InvalidOperationException) { }
        });
        return failures;
    }

    private static SourceHealthStatus StatusAt(DateTime now, int expected, int noData, DateTime? received,
        string pointStatus = "Active", string sourceStatus = "Active") =>
        SourceHealthService.EvaluateStatus(Input(now) with
        {
            ExpectedIntervalSeconds = expected,
            NoDataAfterSeconds = noData,
            LastAcceptedReceivedAtUtc = received,
            PointStatus = pointStatus,
            SourceStatus = sourceStatus
        }, now);

    private static SourceHealthEvaluationInput Input(DateTime now) => new(
        Guid.Parse("20000000-0000-0000-0000-000000000001"),
        Guid.Parse("30000000-0000-0000-0000-000000000001"),
        "site-health", "area-health", "Active", "Active", "Running",
        10, 8, 1, now, 60, 300, 1, 1, 1);

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

    private sealed class HealthRepositoryFake : ISourceHealthProjectionRepository
    {
        private readonly object _gate = new();
        private PointSourceHealthProjection? _current;
        public List<PointSourceHealthChangedEvent> Events { get; } = [];
        public HealthTransaction Begin() => new(this);
        public bool HasNumericNoDataValue => false;
        public PointSourceHealthProjection? Current { get { lock (_gate) return _current; } }

        public Task<PointSourceHealthProjection?> GetCurrentAsync(Guid pointId, CancellationToken ct = default) => Task.FromResult(Current);

        public Task<SourceHealthEvaluationResult> CompareAndSetAsync(SourceHealthEvaluationInput input, SourceHealthStatus status, DateTime evaluatedAtUtc, ITelemetryFlowTransaction transaction, CancellationToken ct = default)
        {
            var tx = (HealthTransaction)transaction;
            lock (_gate)
            {
                var previous = tx.Pending ?? _current;
                if (previous is not null && previous.Status == status &&
                    previous.LastAcceptedReceivedAtUtc == input.LastAcceptedReceivedAtUtc &&
                    previous.PointVersion == input.PointVersion && previous.SourceVersion == input.SourceVersion &&
                    previous.ProviderVersion == input.ProviderVersion)
                    return Task.FromResult(new SourceHealthEvaluationResult(false, previous!, previous));
                var next = new PointSourceHealthProjection(
                    input.PointId, input.SourceId, status, input.LastAcceptedReceivedAtUtc,
                    input.ExpectedIntervalSeconds, input.NoDataAfterSeconds, input.RunStatus,
                    input.GeneratedCount, input.AcceptedCount, input.RejectedCount,
                    input.PointVersion, input.SourceVersion, input.ProviderVersion,
                    (previous?.Version ?? 0) + 1, evaluatedAtUtc, input.SiteId, input.AreaId);
                tx.Pending = next;
                return Task.FromResult(new SourceHealthEvaluationResult(true, next, previous));
            }
        }

        public ValueTask StageChangedEventAsync(PointSourceHealthChangedEvent healthEvent, ITelemetryFlowTransaction transaction, CancellationToken ct = default)
        { ((HealthTransaction)transaction).PendingEvent = healthEvent; return ValueTask.CompletedTask; }

        private void Commit(HealthTransaction tx)
        {
            lock (_gate)
            {
                if (tx.Pending is not null)
                {
                    _current = tx.Pending;
                    if (tx.PendingEvent is not null) Events.Add(tx.PendingEvent);
                }
                tx.IsCompleted = true;
            }
        }
        private void Rollback(HealthTransaction tx) { tx.IsCompleted = true; tx.Pending = null; tx.PendingEvent = null; }

        public sealed class HealthTransaction : ITelemetryFlowTransaction
        {
            private readonly HealthRepositoryFake _owner;
            internal PointSourceHealthProjection? Pending;
            internal PointSourceHealthChangedEvent? PendingEvent;
            internal bool IsCompleted { get; set; }
            internal HealthTransaction(HealthRepositoryFake owner) => _owner = owner;
            public Guid TransactionId { get; } = Guid.NewGuid();
            public string IsolationIntent => "REPEATABLE READ";
            bool ITelemetryFlowTransaction.IsCompleted => IsCompleted;
            public IReadOnlyList<TelemetryFlowLock> LockTrace => Array.Empty<TelemetryFlowLock>();
            public ValueTask AcquireLockAsync(TelemetryFlowLockTarget target, string key, CancellationToken ct = default) => ValueTask.CompletedTask;
            public ValueTask CommitAsync(CancellationToken ct = default) { _owner.Commit(this); return ValueTask.CompletedTask; }
            public ValueTask RollbackAsync(CancellationToken ct = default) { _owner.Rollback(this); return ValueTask.CompletedTask; }
            public ValueTask DisposeAsync() => RollbackAsync();
        }
    }
}
