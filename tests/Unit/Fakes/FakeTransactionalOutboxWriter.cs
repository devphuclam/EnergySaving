using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeTransactionalOutboxWriter : ITransactionalOutboxWriter, IHostTransactionParticipant, IOutboxTransactionParticipant
{
    private readonly List<OwnerEventEnvelope> _committed = new();
    private readonly Dictionary<Guid, List<OwnerEventEnvelope>> _staged = new();

    public bool FailOnEnqueue { get; set; }
    public IReadOnlyList<OwnerEventEnvelope> Enqueued => _committed.AsReadOnly();
    public int Count => _committed.Count;
    public bool WasEnqueued(Guid eventId) => _committed.Any(e => e.EventId == eventId);

    public ValueTask EnqueueAsync(OwnerEventEnvelope envelope, object hostTransaction, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (FailOnEnqueue) throw new InvalidOperationException("OUTBOX_WRITE_FAILED");
        if (hostTransaction is not IHostTransaction tx) throw new ArgumentException("A host transaction is required.", nameof(hostTransaction));
        var rows = _staged.TryGetValue(tx.TransactionId, out var existing)
            ? existing
            : (_staged[tx.TransactionId] = new List<OwnerEventEnvelope>());
        if (_committed.Any(e => e.EventId == envelope.EventId) || rows.Any(e => e.EventId == envelope.EventId))
            throw new InvalidOperationException("OUTBOX_DUPLICATE_EVENT");
        rows.Add(envelope);
        return ValueTask.CompletedTask;
    }

    public ValueTask AcquireLockAsync(IHostTransaction transaction, LockRequest request, CancellationToken ct = default) =>
        ValueTask.CompletedTask;

    public ValueTask CommitAsync(IHostTransaction transaction, CancellationToken ct = default)
    {
        if (_staged.Remove(transaction.TransactionId, out var rows)) _committed.AddRange(rows);
        return ValueTask.CompletedTask;
    }

    public ValueTask RollbackAsync(IHostTransaction transaction, CancellationToken ct = default)
    {
        _staged.Remove(transaction.TransactionId);
        return ValueTask.CompletedTask;
    }

    public ValueTask CommitAsync(object hostTransaction, CancellationToken ct = default) =>
        hostTransaction is IHostTransaction tx ? CommitAsync(tx, ct) : throw new ArgumentException("A host transaction is required.", nameof(hostTransaction));

    public ValueTask RollbackAsync(object hostTransaction, CancellationToken ct = default) =>
        hostTransaction is IHostTransaction tx ? RollbackAsync(tx, ct) : throw new ArgumentException("A host transaction is required.", nameof(hostTransaction));

    public void Clear()
    {
        _committed.Clear();
        _staged.Clear();
        FailOnEnqueue = false;
    }
}
