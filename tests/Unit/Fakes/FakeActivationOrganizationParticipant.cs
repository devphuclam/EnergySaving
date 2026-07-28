using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeActivationOrganizationParticipant : IActivationOrganizationParticipant
{
    private readonly FakeOrganizationCommandRepository _repo;
    private readonly Dictionary<Guid, FakeOrganizationSnapshot> _snapshots = new();
    public List<Guid> TransactionIds { get; } = new();
    public bool FailOnPrepare { get; set; }
    public ActivationOrganizationSnapshot? SnapshotOverride { get; set; }
    public int StageCount { get; private set; }

    public FakeActivationOrganizationParticipant(FakeOrganizationCommandRepository repo) => _repo = repo;

    public async Task<ActivationOrganizationSnapshot?> ReadLockedSnapshotAsync(IHostTransaction transaction, PointId pointId, CancellationToken ct = default)
    {
        TransactionIds.Add(transaction.TransactionId);
        var point = await _repo.GetPointAsync(pointId, ct);
        if (point is null) return null;
        var site = await _repo.GetSiteAsync(point.SiteId, ct);
        var area = await _repo.GetAreaAsync(point.AreaId, ct);
        var asset = await _repo.GetAssetAsync(point.AssetId, ct);
        return SnapshotOverride ?? (site is null || area is null || asset is null ? null : new ActivationOrganizationSnapshot(point, site, area, asset));
    }

    public async Task<MeasurementPoint> StageActivationAsync(IHostTransaction transaction, ActivationOrganizationSnapshot snapshot, string actorUserId, string? actorUsername, string? correlationId, string? causationId, CancellationToken ct = default)
    {
        TransactionIds.Add(transaction.TransactionId);
        _snapshots.TryAdd(transaction.TransactionId, _repo.CreateSnapshot());
        var point = snapshot.Point;
        var oldStatus = point.Status;
        if (!(oldStatus == PointStatus.Draft ? point.TryActivate() : point.TryReactivate())) throw new InvalidOperationException("INVALID_STATE");
        await _repo.UpdatePointAsync(point, ct);
        await _repo.AddLifecycleEntryAsync(new PointLifecycleEntry(Guid.NewGuid().ToString(), point.Id.ToString(), point.Version,
            oldStatus, PointStatus.Active, actorUserId, actorUsername, oldStatus == PointStatus.Draft ? "Activated" : "Reactivated",
            DateTime.UtcNow, correlationId, causationId), ct);
        StageCount++;
        return point;
    }

    public ValueTask AcquireLockAsync(IHostTransaction transaction, LockRequest request, CancellationToken ct = default) { TransactionIds.Add(transaction.TransactionId); return ValueTask.CompletedTask; }
    public ValueTask PrepareAsync(IHostTransaction transaction, CancellationToken ct = default)
    {
        if (FailOnPrepare) throw new InvalidOperationException("ORGANIZATION_PREPARE_FAILED");
        return ValueTask.CompletedTask;
    }
    public ValueTask FinalizeAsync(IHostTransaction transaction, CancellationToken ct = default)
    {
        // Keep the rollback snapshot until the host transaction is fully complete.
        return ValueTask.CompletedTask;
    }
    public ValueTask DiscardAsync(IHostTransaction transaction, CancellationToken ct = default)
    {
        if (_snapshots.Remove(transaction.TransactionId, out var snapshot)) { _repo.RestoreSnapshot(snapshot); if (StageCount > 0) StageCount--; }
        return ValueTask.CompletedTask;
    }
}
