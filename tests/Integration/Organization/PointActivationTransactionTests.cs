using IUMP.BuildingBlocks.Persistence;

namespace IUMP.Tests.Integration.Organization;

// Provider-neutral integration contract. A real adapter supplies the same host transaction seam;
// this contract intentionally contains only the public host transaction seam.
public static class PointActivationTransactionTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        var tx = new HostTransactionCoordinator();
        var participant = new RecordingParticipant();
        tx.RegisterParticipant(LockTarget.IntegrationOutbox, participant);
        for (var order = 1; order <= 9; order++)
        {
            var target = (LockTarget)(order - 1);
            tx.LockAsync(target, $"fixture-{order}", order).GetAwaiter().GetResult();
        }
        if (tx.LockTrace.Count != 9) failures.Add("T103 must acquire all nine canonical lock targets.");
        if (tx.LockTrace[^1].Target != LockTarget.IntegrationOutbox) failures.Add("Integration outbox lock must be last.");
        if (tx.IsolationIntent != "REPEATABLE READ") failures.Add("T103 must request repeatable-read host isolation.");
        tx.RollbackAsync().GetAwaiter().GetResult();
        if (!participant.RolledBack) failures.Add("T103 rollback must reach the outbox participant.");
        return failures;
    }

    private sealed class RecordingParticipant : IHostTransactionParticipant
    {
        public bool RolledBack { get; private set; }
        public ValueTask AcquireLockAsync(IHostTransaction transaction, LockRequest request, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask CommitAsync(IHostTransaction transaction, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask RollbackAsync(IHostTransaction transaction, CancellationToken ct = default) { RolledBack = true; return ValueTask.CompletedTask; }
    }
}
