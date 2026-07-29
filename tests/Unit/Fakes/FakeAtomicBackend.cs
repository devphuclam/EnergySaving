using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.Organization.Domain;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeHostTransaction : IHostTransaction, IHostTransactionController
{
    public Guid TransactionId { get; }
    public string IsolationIntent => "REPEATABLE READ";
    public bool IsCompleted { get; internal set; }
    public bool IsDisposed { get; private set; }
    public int CommitCount { get; private set; }
    public int RollbackCount { get; private set; }

    public FakeHostTransaction(Guid transactionId)
    {
        TransactionId = transactionId;
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask CommitAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        CommitCount++;
        IsCompleted = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask RollbackAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        RollbackCount++;
        IsCompleted = true;
        return ValueTask.CompletedTask;
    }
}

public sealed class TransactionWorkspace
{
    public MeasurementPoint? StagedPoint { get; set; }
    public List<PointLifecycleEntry> StagedLifecycle { get; } = new();

    public List<OwnerEventEnvelope> StagedEnvelopes { get; } = new();

    public bool HasStagedOrgState => StagedPoint is not null || StagedLifecycle.Count > 0;
    public bool HasStagedIntegrationState => StagedEnvelopes.Count > 0;
    public bool IsEmpty => !HasStagedOrgState && !HasStagedIntegrationState;
}

public sealed class FakeAtomicBackend : IHostTransactionBackend
{
    private readonly Dictionary<Guid, TransactionWorkspace> _workspaces = new();

    public FakeOrganizationCommandRepository OrganizationRepo { get; }
    public List<OwnerEventEnvelope> CommittedEnvelopes { get; } = new();

    public int CommitCount { get; private set; }
    public int RollbackCount { get; private set; }
    public bool FailOnCommit { get; set; }
    public bool FailOnRollback { get; set; }
    public bool FailOnBegin { get; set; }

    public FakeAtomicBackend(FakeOrganizationCommandRepository orgRepo)
    {
        OrganizationRepo = orgRepo ?? throw new ArgumentNullException(nameof(orgRepo));
    }

    public TransactionWorkspace? GetWorkspace(IHostTransaction transaction)
    {
        _workspaces.TryGetValue(transaction.TransactionId, out var ws);
        return ws;
    }

    public ValueTask<IHostTransaction> BeginAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (FailOnBegin) throw new InvalidOperationException("BEGIN_FAILED");
        var tx = new FakeHostTransaction(Guid.NewGuid());
        _workspaces[tx.TransactionId] = new TransactionWorkspace();
        return ValueTask.FromResult<IHostTransaction>(tx);
    }

    public ValueTask CommitAsync(IHostTransaction transaction, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_workspaces.TryGetValue(transaction.TransactionId, out var ws))
            throw new InvalidOperationException("UNKNOWN_TRANSACTION");
        if (FailOnCommit)
            throw new InvalidOperationException("ATOMIC_COMMIT_FAILED");

        if (ws.StagedPoint is not null)
        {
            OrganizationRepo.ReplacePointDirect(ws.StagedPoint);
            foreach (var entry in ws.StagedLifecycle)
                OrganizationRepo.AddLifecycleEntryDirect(entry);
        }
        CommittedEnvelopes.AddRange(ws.StagedEnvelopes);

        _workspaces.Remove(transaction.TransactionId);
        CommitCount++;
        return ValueTask.CompletedTask;
    }

    public ValueTask RollbackAsync(IHostTransaction transaction, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        RollbackCount++;
        if (FailOnRollback) throw new InvalidOperationException("ROLLBACK_FAILED");
        _workspaces.Remove(transaction.TransactionId);
        return ValueTask.CompletedTask;
    }
}
