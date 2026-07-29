using IUMP.Modules.Audit.Contracts;
using IUMP.BuildingBlocks.Persistence;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeAuditAppendRepository : IAuditAppendRepository, IAuditQueryRepository, IAuditConflictRepository, ITransactionalAuditAppendRepository
{
    private readonly Dictionary<Guid, AuditEventRecord> _bySource = new();
    public IReadOnlyList<AuditEventRecord> Rows => _bySource.Values.ToArray();

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
        CancellationToken ct = default) => AppendIfAbsentAsync(record, ct);

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
