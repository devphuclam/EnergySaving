using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeActivationOrganizationParticipant : IActivationOrganizationParticipant
{
    private readonly FakeOrganizationCommandRepository _repo;
    private readonly FakeAtomicBackend _backend;
    public List<Guid> TransactionIds { get; } = new();
    public ActivationOrganizationSnapshot? SnapshotOverride { get; set; }
    public int StageCount { get; private set; }

    public FakeActivationOrganizationParticipant(FakeOrganizationCommandRepository repo, FakeAtomicBackend backend)
    {
        _repo = repo;
        _backend = backend;
    }

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
        var ws = _backend.GetWorkspace(transaction);
        if (ws is null) throw new InvalidOperationException("UNKNOWN_TRANSACTION");

        var point = snapshot.Point;
        var oldStatus = point.Status;
        if (!(oldStatus == PointStatus.Draft ? point.TryActivate() : point.TryReactivate())) throw new InvalidOperationException("INVALID_STATE");

        ws.StagedPoint = point;
        ws.StagedLifecycle.Add(new PointLifecycleEntry(Guid.NewGuid().ToString(), point.Id.ToString(), point.Version,
            oldStatus, PointStatus.Active, actorUserId, actorUsername, oldStatus == PointStatus.Draft ? "Activated" : "Reactivated",
            DateTime.UtcNow, correlationId, causationId));
        StageCount++;
        return point;
    }

    public ValueTask AcquireLockAsync(IHostTransaction transaction, LockRequest request, CancellationToken ct = default)
    {
        TransactionIds.Add(transaction.TransactionId);
        return ValueTask.CompletedTask;
    }
}
