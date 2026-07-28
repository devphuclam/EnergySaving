using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Organization.Contracts;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeActivationIdentityQuery : IActivationIdentityQuery, IActivationIdentityParticipant
{
    public ActivationDataOwnerSnapshot Snapshot { get; set; } = new("owner-user", true, true, true, true, false, 1, 1);
    public bool ChangeOnSecondRead { get; set; }
    public int TransientFailures { get; set; }
    public List<Guid> TransactionIds { get; } = new();
    private int _reads;

    public Task<ActivationDataOwnerSnapshot> GetDataOwnerAsync(string dataOwnerUserId, string siteId, string areaId, CancellationToken ct = default) => Task.FromResult(Read(dataOwnerUserId, siteId, areaId));
    public Task<ActivationDataOwnerSnapshot> ReadDataOwnerAsync(IHostTransaction transaction, string dataOwnerUserId, string siteId, string areaId, CancellationToken ct = default) { TransactionIds.Add(transaction.TransactionId); return Task.FromResult(Read(dataOwnerUserId, siteId, areaId)); }
    public Task<ActivationDataOwnerSnapshot> RecheckDataOwnerAsync(IHostTransaction transaction, string dataOwnerUserId, string siteId, string areaId, CancellationToken ct = default) { TransactionIds.Add(transaction.TransactionId); return Task.FromResult(Read(dataOwnerUserId, siteId, areaId)); }
    private ActivationDataOwnerSnapshot Read(string dataOwnerUserId, string siteId, string areaId)
    {
        _reads++;
        var result = Snapshot;
        return ChangeOnSecondRead && _reads > 1 ? result with { UserVersion = result.UserVersion + 1 } : result;
    }
    public ValueTask AcquireLockAsync(IHostTransaction transaction, LockRequest request, CancellationToken ct = default)
    {
        TransactionIds.Add(transaction.TransactionId);
        if (TransientFailures-- > 0) throw new TransientDatabaseConflictException("TRANSIENT_DATABASE_CONFLICT");
        return ValueTask.CompletedTask;
    }
    public ValueTask PrepareAsync(IHostTransaction transaction, CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask FinalizeAsync(IHostTransaction transaction, CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask DiscardAsync(IHostTransaction transaction, CancellationToken ct = default) => ValueTask.CompletedTask;
}

public sealed class FakeActivationCatalogQuery : IActivationCatalogQuery, IActivationCatalogParticipant
{
    public ActivationCatalogSnapshot? Snapshot { get; set; }
    public bool ChangeOnSecondRead { get; set; }
    public List<Guid> TransactionIds { get; } = new();
    private int _reads;

    public Task<ActivationCatalogSnapshot?> GetActivationSnapshotAsync(string pointId, string metricId, string unitId, DateTime atUtc, CancellationToken ct = default) => Task.FromResult(Snapshot);
    public Task<ActivationCatalogSnapshot?> ReadActivationSnapshotAsync(IHostTransaction transaction, string pointId, string metricId, string unitId, DateTime atUtc, CancellationToken ct = default) { TransactionIds.Add(transaction.TransactionId); return Task.FromResult(Read()); }
    public Task<ActivationCatalogSnapshot?> RecheckActivationSnapshotAsync(IHostTransaction transaction, string pointId, string metricId, string unitId, DateTime atUtc, CancellationToken ct = default) { TransactionIds.Add(transaction.TransactionId); return Task.FromResult(Read()); }
    private ActivationCatalogSnapshot? Read()
    {
        _reads++;
        if (Snapshot is null) return null;
        return ChangeOnSecondRead && _reads > 1 ? Snapshot with { MappingVersion = Snapshot.MappingVersion + 1 } : Snapshot;
    }
    public ValueTask AcquireLockAsync(IHostTransaction transaction, LockRequest request, CancellationToken ct = default) { TransactionIds.Add(transaction.TransactionId); return ValueTask.CompletedTask; }
    public ValueTask PrepareAsync(IHostTransaction transaction, CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask FinalizeAsync(IHostTransaction transaction, CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask DiscardAsync(IHostTransaction transaction, CancellationToken ct = default) => ValueTask.CompletedTask;
}
