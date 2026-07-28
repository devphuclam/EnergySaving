using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeTransactionalOutboxWriter : ITransactionalOutboxWriter
{
    private readonly List<OwnerEventEnvelope> _committed = new();
    private readonly Dictionary<Guid, List<OwnerEventEnvelope>> _staged = new();
    public bool FailOnEnqueue { get; set; }
    public bool FailOnPrepare { get; set; }
    public bool FailOnFinalize { get; set; }
    public IReadOnlyList<OwnerEventEnvelope> Enqueued => _committed.AsReadOnly();
    public int Count => _committed.Count;
    public bool WasEnqueued(Guid eventId) => _committed.Any(e => e.EventId == eventId);
    public List<Guid> TransactionIds { get; } = new();

    public ValueTask EnqueueAsync(OwnerEventEnvelope envelope, IHostTransaction hostTransaction, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        TransactionIds.Add(hostTransaction.TransactionId);
        if (FailOnEnqueue) throw new InvalidOperationException("OUTBOX_WRITE_FAILED");
        var rows = _staged.TryGetValue(hostTransaction.TransactionId, out var existing) ? existing : (_staged[hostTransaction.TransactionId] = new());
        if (_committed.Any(e => e.EventId == envelope.EventId) || rows.Any(e => e.EventId == envelope.EventId)) throw new InvalidOperationException("OUTBOX_DUPLICATE_EVENT");
        rows.Add(envelope);
        return ValueTask.CompletedTask;
    }

    public ValueTask AcquireLockAsync(IHostTransaction transaction, LockRequest request, CancellationToken ct = default) { TransactionIds.Add(transaction.TransactionId); return ValueTask.CompletedTask; }

    public ValueTask PrepareAsync(IHostTransaction transaction, CancellationToken ct = default)
    {
        if (FailOnPrepare) throw new InvalidOperationException("OUTBOX_PREPARE_FAILED");
        return ValueTask.CompletedTask;
    }

    public ValueTask FinalizeAsync(IHostTransaction transaction, CancellationToken ct = default)
    {
        if (FailOnFinalize) throw new InvalidOperationException("OUTBOX_FINALIZE_FAILED");
        if (_staged.Remove(transaction.TransactionId, out var rows)) _committed.AddRange(rows);
        return ValueTask.CompletedTask;
    }

    public ValueTask DiscardAsync(IHostTransaction transaction, CancellationToken ct = default)
    {
        _staged.Remove(transaction.TransactionId);
        return ValueTask.CompletedTask;
    }
}
