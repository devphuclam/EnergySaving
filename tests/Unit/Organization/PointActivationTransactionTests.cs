using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Organization;

public static class PointActivationTransactionTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        Check(failures, "exact lock order", ExactLockOrder);
        Check(failures, "lock order violation", LockOrderViolationIsRejected);
        Check(failures, "reverse rollback", RollbackIsReverseOrder);
        Check(failures, "outbox commit", OutboxCommitIsAtomic);
        Check(failures, "cancellation rollback", CancellationRollsBack);
        Check(failures, "transient retry", TransientLockRetriesThreeTimes);
        return failures;
    }

    private static void Check(List<string> failures, string name, Func<string?> test)
    {
        try { if (test() is { } failure) failures.Add($"{name}: {failure}"); }
        catch (Exception ex) { failures.Add($"{name}: unexpected {ex.GetType().Name}: {ex.Message}"); }
    }

    private static string? ExactLockOrder()
    {
        var tx = new HostTransactionCoordinator();
        tx.BeginAsync().GetAwaiter().GetResult();
        var targets = Enum.GetValues<LockTarget>();
        for (var i = 0; i < targets.Length; i++) tx.LockAsync(targets[i], $"id-{i}", i + 1).GetAwaiter().GetResult();
        var trace = tx.LockTrace;
        if (tx.IsolationIntent != "REPEATABLE READ") return "host transaction isolation intent must be REPEATABLE READ.";
        if (trace.Count != 9 || trace.Select(x => x.Target).SequenceEqual(targets) == false) return "lock trace must be IAM→Organization→Catalog→Integration in exact order.";
        if (trace.Select(x => x.Order).SequenceEqual(Enumerable.Range(1, 9)) == false) return "lock order numbers must be 1..9.";
        tx.RollbackAsync().GetAwaiter().GetResult();
        return null;
    }

    private static string? LockOrderViolationIsRejected()
    {
        var tx = new HostTransactionCoordinator();
        tx.LockAsync(LockTarget.OrganizationPoint, "point", 5).GetAwaiter().GetResult();
        try
        {
            tx.LockAsync(LockTarget.IamUser, "owner", 1).GetAwaiter().GetResult();
            return "descending lock acquisition must fail.";
        }
        catch (InvalidOperationException ex) when (ex.Message == "LOCK_ORDER_VIOLATION") { tx.RollbackAsync().GetAwaiter().GetResult(); return null; }
    }

    private static string? RollbackIsReverseOrder()
    {
        var tx = new HostTransactionCoordinator();
        var first = new RecordingParticipant();
        var second = new RecordingParticipant();
        tx.RegisterParticipant(LockTarget.IamUser, first);
        tx.RegisterParticipant(LockTarget.OrganizationSite, second);
        tx.LockAsync(LockTarget.IamUser, "owner", 1).GetAwaiter().GetResult();
        tx.LockAsync(LockTarget.OrganizationSite, "site", 2).GetAwaiter().GetResult();
        tx.RollbackAsync().GetAwaiter().GetResult();
        return second.RollbackIndex == 0 && first.RollbackIndex == 1 ? null : "participants must rollback in reverse acquisition order.";
    }

    private static string? OutboxCommitIsAtomic()
    {
        var outbox = new FakeTransactionalOutboxWriter();
        var tx = new HostTransactionCoordinator();
        tx.RegisterParticipant(LockTarget.IntegrationOutbox, outbox);
        tx.LockAsync(LockTarget.IntegrationOutbox, "point", 9).GetAwaiter().GetResult();
        var envelope = Envelope();
        outbox.EnqueueAsync(envelope, tx).GetAwaiter().GetResult();
        if (outbox.Count != 0) return "outbox row must remain staged before host commit.";
        tx.CommitAsync().GetAwaiter().GetResult();
        if (outbox.Count != 1 || !outbox.WasEnqueued(envelope.EventId)) return "host commit must publish exactly one staged outbox row.";

        var rollbackTx = new HostTransactionCoordinator();
        rollbackTx.RegisterParticipant(LockTarget.IntegrationOutbox, outbox);
        rollbackTx.LockAsync(LockTarget.IntegrationOutbox, "point-2", 9).GetAwaiter().GetResult();
        outbox.EnqueueAsync(Envelope(), rollbackTx).GetAwaiter().GetResult();
        rollbackTx.RollbackAsync().GetAwaiter().GetResult();
        return outbox.Count == 1 ? null : "rollback must discard staged outbox rows.";
    }

    private static string? CancellationRollsBack()
    {
        var tx = new HostTransactionCoordinator();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        try
        {
            tx.LockAsync(LockTarget.IamUser, "owner", 1, cts.Token).GetAwaiter().GetResult();
            return "cancelled lock must throw.";
        }
        catch (OperationCanceledException) { tx.RollbackAsync().GetAwaiter().GetResult(); return tx.IsCompleted ? null : "cancelled transaction must complete rollback."; }
    }

    private static string? TransientLockRetriesThreeTimes()
    {
        var tx = new HostTransactionCoordinator();
        var participant = new TransientParticipant(2);
        tx.RegisterParticipant(LockTarget.IamUser, participant);
        tx.LockWithRetryAsync(LockTarget.IamUser, "owner", 1).GetAwaiter().GetResult();
        tx.RollbackAsync().GetAwaiter().GetResult();
        return participant.Attempts == 3 && tx.LockTrace.Count == 1 ? null : "transient lock conflicts must retry with the 50/150/450ms policy.";
    }

    private static OwnerEventEnvelope Envelope() => new(Guid.NewGuid(), "PointStatusChanged.v1", 1, "IUMP.Organization", "MeasurementPoint",
        "point", 2, "actor", "actor@test", new Dictionary<string, object?>(), new Dictionary<string, object?>(),
        "Activated", "safe", DateTime.UtcNow, Guid.NewGuid().ToString("D"), "causation", "site", "area");

    private sealed class RecordingParticipant : IHostTransactionParticipant
    {
        private static int _sequence;
        public int RollbackIndex { get; private set; } = -1;
        public ValueTask AcquireLockAsync(IHostTransaction transaction, LockRequest request, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask CommitAsync(IHostTransaction transaction, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask RollbackAsync(IHostTransaction transaction, CancellationToken ct = default) { RollbackIndex = _sequence++; return ValueTask.CompletedTask; }
    }

    private sealed class TransientParticipant : IHostTransactionParticipant
    {
        private int _remainingFailures;
        public int Attempts { get; private set; }
        public TransientParticipant(int failures) => _remainingFailures = failures;
        public ValueTask AcquireLockAsync(IHostTransaction transaction, LockRequest request, CancellationToken ct = default)
        {
            Attempts++;
            if (_remainingFailures-- > 0) throw new TransientDatabaseConflictException("serialization failure");
            return ValueTask.CompletedTask;
        }
        public ValueTask CommitAsync(IHostTransaction transaction, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask RollbackAsync(IHostTransaction transaction, CancellationToken ct = default) => ValueTask.CompletedTask;
    }
}
