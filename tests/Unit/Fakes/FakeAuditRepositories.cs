using IUMP.Modules.Audit.Contracts;
using IUMP.BuildingBlocks.Persistence;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeAuditAppendRepository : IAuditAppendRepository, IAuditQueryRepository, IAuditConflictRepository, ITransactionalAuditAppendRepository
{
    private readonly Dictionary<Guid, AuditEventRecord> _bySource = new();
    private readonly Dictionary<Guid, Dictionary<Guid, AuditEventRecord>> _staged = new();
    public IReadOnlyList<AuditEventRecord> Rows => _bySource.Values.ToArray();
    public int CommitCount { get; private set; }
    public int RollbackCount { get; private set; }

    public Task<AuditEventRecord?> AppendIfAbsentAsync(AuditEventRecord record, CancellationToken ct = default)
    {
        if (_bySource.TryGetValue(record.SourceEventId, out var existing)) return Task.FromResult<AuditEventRecord?>(existing);
        _bySource[record.SourceEventId] = record; return Task.FromResult<AuditEventRecord?>(record);
    }

    public Task<bool> IsSourceHashConflictAsync(Guid sourceEventId, string payloadHash, CancellationToken ct = default)
    {
        return Task.FromResult(_bySource.TryGetValue(sourceEventId, out var existing) &&
            !string.Equals(existing.PayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase));
    }

    public Task<AuditEventRecord?> AppendIfAbsentAsync(AuditEventRecord record, IHostTransaction transaction,
        CancellationToken ct = default)
    {
        if (_bySource.TryGetValue(record.SourceEventId, out var existing)) return Task.FromResult<AuditEventRecord?>(existing);
        if (!_staged.TryGetValue(transaction.TransactionId, out var rows))
            _staged[transaction.TransactionId] = rows = new();
        if (rows.TryGetValue(record.SourceEventId, out existing)) return Task.FromResult<AuditEventRecord?>(existing);
        rows[record.SourceEventId] = record;
        if (transaction is not FakeHostTransaction fakeTx)
            throw new InvalidOperationException("FAKE_TRANSACTION_ENLISTMENT_REQUIRED");
        if (rows.Count == 1)
            fakeTx.Enlist(() => CommitTransactionAsync(transaction.TransactionId),
                () => RollbackTransactionAsync(transaction.TransactionId));
        return Task.FromResult<AuditEventRecord?>(record);
    }

    public Task CommitTransactionAsync(Guid transactionId)
    {
        if (_staged.Remove(transactionId, out var rows))
            foreach (var kv in rows) _bySource[kv.Key] = kv.Value;
        CommitCount++;
        return Task.CompletedTask;
    }

    public Task RollbackTransactionAsync(Guid transactionId)
    {
        _staged.Remove(transactionId);
        RollbackCount++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEventRecord>> QueryAsync(AuditQueryRequest request, CancellationToken ct = default)
    {
        IEnumerable<AuditEventRecord> rows = _bySource.Values;
        if (request.ObjectType is not null) rows = rows.Where(row => row.ObjectType == request.ObjectType);
        if (request.Action is not null) rows = rows.Where(row => row.Action == request.Action);
        if (request.ActorId is not null) rows = rows.Where(row => row.ActorId == request.ActorId);
        if (request.CorrelationId is not null) rows = rows.Where(row => row.CorrelationId == request.CorrelationId);
        if (request.FromUtc is not null) rows = rows.Where(row => row.OccurredAtUtc >= request.FromUtc);
        return Task.FromResult<IReadOnlyList<AuditEventRecord>>(rows.ToArray());
    }
}
