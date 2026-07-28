using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeTransactionalOutboxWriter : ITransactionalOutboxWriter
{
    private readonly FakeAtomicBackend _backend;
    public bool FailOnEnqueue { get; set; }
    public List<Guid> TransactionIds { get; } = new();
    public IReadOnlyList<OwnerEventEnvelope> Enqueued => _backend.CommittedEnvelopes.AsReadOnly();
    public int Count => _backend.CommittedEnvelopes.Count;

    public FakeTransactionalOutboxWriter(FakeAtomicBackend backend)
    {
        _backend = backend;
    }

    public ValueTask EnqueueAsync(OwnerEventEnvelope envelope, IHostTransaction hostTransaction, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        TransactionIds.Add(hostTransaction.TransactionId);
        if (FailOnEnqueue) throw new InvalidOperationException("OUTBOX_WRITE_FAILED");
        var ws = _backend.GetWorkspace(hostTransaction);
        if (ws is null) throw new InvalidOperationException("UNKNOWN_TRANSACTION");
        if (ws.StagedEnvelopes.Any(e => e.EventId == envelope.EventId))
            throw new InvalidOperationException("OUTBOX_DUPLICATE_EVENT");
        ws.StagedEnvelopes.Add(envelope);
        return ValueTask.CompletedTask;
    }

    public ValueTask AcquireLockAsync(IHostTransaction transaction, LockRequest request, CancellationToken ct = default)
    {
        TransactionIds.Add(transaction.TransactionId);
        return ValueTask.CompletedTask;
    }
}
