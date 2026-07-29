using IUMP.Modules.Telemetry.Application;
using IUMP.Modules.Telemetry.Contracts;

namespace IUMP.Tests.Unit.Telemetry;

public static class PointLatestTests
{
    public static int TestCount { get; private set; }
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        TestCount = 0;
        CheckCount = 0;
        var failures = new List<string>();
        Case("ordering tuple and eligibility", failures, () =>
        {
            var repo = new LatestRepositoryFake();
            var service = new PointLatestService(repo);
            var tx = repo.Begin();
            var baseTime = Utc(2026, 7, 29, 8, 0, 0);
            var first = Candidate(1, baseTime, 1, baseTime.AddSeconds(1), MeasurementQuality.Good, 10);
            Check(service.ApplyAsync(first, tx).Result, "first advances", failures);
            tx.CommitAsync().GetAwaiter().GetResult();

            Check(!service.ApplyAsync(Candidate(2, baseTime.AddSeconds(-1), 99, baseTime.AddSeconds(2), MeasurementQuality.Good, 11), repo.Begin()).Result,
                "older source timestamp does not advance", failures);
            var sequenceWinnerTx = repo.Begin();
            Check(service.ApplyAsync(Candidate(3, baseTime, 2, baseTime.AddSeconds(3), MeasurementQuality.Uncertain, 12), sequenceWinnerTx).Result,
                "larger sequence advances", failures);
            sequenceWinnerTx.CommitAsync().GetAwaiter().GetResult();
            Check(!service.ApplyAsync(Candidate(4, baseTime, 1, baseTime.AddSeconds(4), MeasurementQuality.Good, 13), repo.Begin()).Result,
                "smaller sequence does not advance", failures);
            var processingWinnerTx = repo.Begin();
            Check(service.ApplyAsync(Candidate(5, baseTime, 2, baseTime.AddSeconds(5), MeasurementQuality.Good, 14), processingWinnerTx).Result,
                "later processing advances", failures);
            processingWinnerTx.CommitAsync().GetAwaiter().GetResult();
            Check(!service.ApplyAsync(Candidate(5, baseTime, 2, baseTime.AddSeconds(5), MeasurementQuality.Good, 14), repo.Begin()).Result,
                "exact duplicate is a no-op", failures);
            Check(!service.ApplyAsync(Candidate(6, baseTime.AddSeconds(1), 3, baseTime.AddSeconds(6), MeasurementQuality.Bad, 15), repo.Begin()).Result,
                "Bad is not eligible", failures);
        });
        Case("canonical ID resolves a complete tie", failures, () =>
        {
            var repo = new LatestRepositoryFake();
            var service = new PointLatestService(repo);
            var t = Utc(2026, 7, 29, 8, 0, 0);
            var low = Guid.Parse("00000000-0000-0000-0000-000000000010");
            var high = Guid.Parse("00000000-0000-0000-0000-000000000020");
            var first = Candidate(low, t, 1, t, MeasurementQuality.Good, 1);
            var second = Candidate(high, t, 1, t, MeasurementQuality.Uncertain, 2);
            Check(service.ApplyAsync(first, repo.Begin()).Result, "first tie candidate advances", failures);
            repo.LastTransaction!.CommitAsync().GetAwaiter().GetResult();
            Check(service.ApplyAsync(second, repo.Begin()).Result, "larger ID wins complete tie", failures);
            repo.LastTransaction!.CommitAsync().GetAwaiter().GetResult();
            Check(repo.Current!.MeasurementId == high, "canonical larger ID winner", failures);
        });
        Case("rollback preserves old projection and events only advance", failures, () =>
        {
            var repo = new LatestRepositoryFake();
            var service = new PointLatestService(repo);
            var t = Utc(2026, 7, 29, 9, 0, 0);
            var first = Candidate(10, t, 0, t, MeasurementQuality.Good, 1);
            var tx = repo.Begin();
            Check(service.ApplyAsync(first, tx).Result, "candidate stages", failures);
            tx.RollbackAsync().GetAwaiter().GetResult();
            Check(repo.Current is null && repo.Events.Count == 0, "rollback preserves projection and event", failures);
            tx = repo.Begin();
            Check(service.ApplyAsync(first, tx).Result, "candidate advances after retry", failures);
            tx.CommitAsync().GetAwaiter().GetResult();
            Check(repo.Events.Count == 1, "one advancement event", failures);
            Check(repo.Events[0].OldMeasurementId == Guid.Empty &&
                  repo.Events[0].NewMeasurementId == first.MeasurementId,
                "event carries exact old/new IDs", failures);
            Check(!service.ApplyAsync(first, repo.Begin()).Result && repo.Events.Count == 1,
                "no event on no-op", failures);
        });
        Case("concurrent candidates converge without regression", failures, () =>
        {
            var repo = new LatestRepositoryFake();
            var service = new PointLatestService(repo);
            var t = Utc(2026, 7, 29, 10, 0, 0);
            var older = Candidate(20, t, 1, t, MeasurementQuality.Good, 1);
            var newer = Candidate(21, t.AddSeconds(1), 0, t, MeasurementQuality.Good, 2);
            var firstTx = repo.Begin();
            var secondTx = repo.Begin();
            Check(service.ApplyAsync(older, firstTx).Result, "older candidate stages", failures);
            Check(service.ApplyAsync(newer, secondTx).Result, "newer candidate stages", failures);
            secondTx.CommitAsync().GetAwaiter().GetResult();
            firstTx.CommitAsync().GetAwaiter().GetResult();
            Check(repo.Current!.MeasurementId == newer.MeasurementId, "winner is deterministic", failures);
            Check(repo.Current.Ordering.SourceTimestampUtc == newer.SourceTimestampUtc,
                "compare-and-set cannot regress", failures);
        });
        return failures;
    }

    private static LatestProjectionCandidate Candidate(
        int id, DateTime source, long sequence, DateTime processing,
        MeasurementQuality quality, double value) =>
        Candidate(Guid.Parse($"00000000-0000-0000-0000-{id:000000000000}"), source, sequence, processing, quality, value);

    private static LatestProjectionCandidate Candidate(
        Guid id, DateTime source, long sequence, DateTime processing,
        MeasurementQuality quality, double value) =>
        new(id, PointId, source, sequence, processing, quality, value, "kWh", processing, quality == MeasurementQuality.Bad ? "VALUE_OUT_OF_RANGE" : null);

    private static DateTime Utc(int year, int month, int day, int hour, int minute, int second) =>
        new(year, month, day, hour, minute, second, DateTimeKind.Utc);

    private static readonly Guid PointId = Guid.Parse("10000000-0000-0000-0000-000000000001");

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

    private sealed class LatestRepositoryFake : IPointLatestProjectionRepository
    {
        private readonly object _gate = new();
        private PointLatestProjection? _current;
        public List<PointLatestAdvancedEvent> Events { get; } = [];
        public ProjectionTransaction? LastTransaction { get; private set; }
        public PointLatestProjection? Current { get { lock (_gate) return _current; } }

        public ProjectionTransaction Begin() => LastTransaction = new ProjectionTransaction(this);
        public Task<PointLatestProjection?> GetCurrentAsync(Guid pointId, CancellationToken ct = default) =>
            Task.FromResult(Current);

        public Task<PointLatestAdvanceResult> CompareAndSetAsync(LatestProjectionCandidate candidate, ITelemetryFlowTransaction transaction, CancellationToken ct = default)
        {
            var tx = (ProjectionTransaction)transaction;
            lock (_gate)
            {
                var visible = tx.Pending ?? _current;
                if (!PointLatestService.ShouldAdvance(candidate, visible))
                    return Task.FromResult(new PointLatestAdvanceResult(false, visible, visible));
                var next = PointLatestProjection.FromCandidate(candidate) with { Version = (visible?.Version ?? 0) + 1 };
                tx.Pending = next;
                return Task.FromResult(new PointLatestAdvanceResult(true, visible, next));
            }
        }

        public ValueTask StageAdvancedEventAsync(PointLatestAdvancedEvent latestEvent, ITelemetryFlowTransaction transaction, CancellationToken ct = default)
        { ((ProjectionTransaction)transaction).PendingEvent = latestEvent; return ValueTask.CompletedTask; }

        public Task<bool> EvaluateAdvanceAsync(LatestProjectionCandidate candidate, ITelemetryFlowTransaction transaction, CancellationToken ct = default) =>
            CompareAndSetAsync(candidate, transaction, ct).ContinueWith(task => task.Result.Advanced, ct);
        public Task StageAdvanceAsync(LatestProjectionCandidate candidate, bool latestAdvanced, ITelemetryFlowTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;

        private void Commit(ProjectionTransaction tx)
        {
            lock (_gate)
            {
                if (tx.Pending is not null &&
                    (_current is null || LatestOrdering.Compare(tx.Pending.Ordering, _current.Ordering) > 0))
                {
                    _current = tx.Pending;
                    if (tx.PendingEvent is not null) Events.Add(tx.PendingEvent);
                }
                tx.IsCompleted = true;
            }
        }
        private void Rollback(ProjectionTransaction tx) { tx.IsCompleted = true; tx.Pending = null; tx.PendingEvent = null; }

        public sealed class ProjectionTransaction : ITelemetryFlowTransaction
        {
            private readonly LatestRepositoryFake _owner;
            internal PointLatestProjection? Pending;
            internal PointLatestAdvancedEvent? PendingEvent;
            internal bool IsCompleted { get; set; }
            internal ProjectionTransaction(LatestRepositoryFake owner) => _owner = owner;
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
