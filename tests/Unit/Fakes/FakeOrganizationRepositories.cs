using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeOrganizationTransaction : IOrganizationTransaction
{
    private readonly FakeOrganizationCommandRepository _repo;
    private readonly FakeOrganizationSnapshot _snapshot;
    public bool IsCommitted { get; private set; }
    public bool IsRolledBack { get; private set; }

    public FakeOrganizationTransaction(FakeOrganizationCommandRepository repo)
    {
        _repo = repo;
        _snapshot = repo.CreateSnapshot();
    }

    public Task CommitAsync(CancellationToken ct = default) { IsCommitted = true; return Task.CompletedTask; }

    public Task RollbackAsync(CancellationToken ct = default)
    {
        if (!IsCommitted)
        {
            IsRolledBack = true;
            _repo.RestoreSnapshot(_snapshot);
        }
        return Task.CompletedTask;
    }
}

public sealed class FakeOrganizationSnapshot
{
    public Dictionary<Guid, Site> Sites { get; } = new();
    public Dictionary<Guid, Area> Areas { get; } = new();
    public Dictionary<Guid, Asset> Assets { get; } = new();
    public Dictionary<Guid, MeasurementPoint> Points { get; } = new();
    public List<PointLifecycleEntry> Lifecycle { get; } = new();
    public HashSet<string> ReservedPointCodes { get; } = new();
}

public sealed class FakeOrganizationCommandRepository : IOrganizationCommandRepository
{
    private readonly Dictionary<Guid, Site> _sites = new();
    private readonly Dictionary<Guid, Area> _areas = new();
    private readonly Dictionary<Guid, Asset> _assets = new();
    private readonly Dictionary<Guid, MeasurementPoint> _points = new();
    private readonly List<PointLifecycleEntry> _lifecycle = new();
    private readonly HashSet<string> _reservedPointCodes = new(StringComparer.OrdinalIgnoreCase);

    public FakeOrganizationSnapshot CreateSnapshot()
    {
        var snap = new FakeOrganizationSnapshot();
        foreach (var kv in _sites) snap.Sites[kv.Key] = Clone(kv.Value);
        foreach (var kv in _areas) snap.Areas[kv.Key] = Clone(kv.Value);
        foreach (var kv in _assets) snap.Assets[kv.Key] = Clone(kv.Value);
        foreach (var kv in _points) snap.Points[kv.Key] = Clone(kv.Value);
        snap.Lifecycle.AddRange(_lifecycle);
        foreach (var c in _reservedPointCodes) snap.ReservedPointCodes.Add(c);
        return snap;
    }

    public void RestoreSnapshot(FakeOrganizationSnapshot snap)
    {
        _sites.Clear();
        foreach (var kv in snap.Sites) _sites[kv.Key] = Clone(kv.Value);
        _areas.Clear();
        foreach (var kv in snap.Areas) _areas[kv.Key] = Clone(kv.Value);
        _assets.Clear();
        foreach (var kv in snap.Assets) _assets[kv.Key] = Clone(kv.Value);
        _points.Clear();
        foreach (var kv in snap.Points) _points[kv.Key] = Clone(kv.Value);
        _lifecycle.Clear();
        _lifecycle.AddRange(snap.Lifecycle);
        _reservedPointCodes.Clear();
        foreach (var c in snap.ReservedPointCodes) _reservedPointCodes.Add(c);
    }

    // Site
    public Task<Site?> GetSiteAsync(SiteId id, CancellationToken ct = default) =>
        Task.FromResult(_sites.TryGetValue(id.Value, out var s) ? Clone(s) : null);

    public Task<Site?> FindSiteByCodeAsync(string code, CancellationToken ct = default)
    {
        var norm = Site.NormalizeCode(code);
        return Task.FromResult(_sites.Values.FirstOrDefault(s => s.Code.Equals(norm, StringComparison.OrdinalIgnoreCase)) is { } site ? Clone(site) : null);
    }

    public Task AddSiteAsync(Site site, CancellationToken ct = default)
    {
        if (_sites.ContainsKey(site.Id.Value)) throw new InvalidOperationException("Site already exists.");
        if (_sites.Values.Any(s => s.Code.Equals(site.Code, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Site code already exists.");
        _sites[site.Id.Value] = Clone(site);
        return Task.CompletedTask;
    }

    public Task UpdateSiteAsync(Site site, CancellationToken ct = default)
    {
        if (!_sites.TryGetValue(site.Id.Value, out var current)) throw new InvalidOperationException("Site not found.");
        if (site.Version <= current.Version) throw new InvalidOperationException("VERSION_CONFLICT");
        _sites[site.Id.Value] = Clone(site);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Site>> GetAllSitesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Site>>(_sites.Values.Select(Clone).ToList());

    // Area
    public Task<Area?> GetAreaAsync(AreaId id, CancellationToken ct = default) =>
        Task.FromResult(_areas.TryGetValue(id.Value, out var a) ? Clone(a) : null);

    public Task<Area?> FindAreaByCodeAsync(SiteId siteId, string code, CancellationToken ct = default)
    {
        var norm = Site.NormalizeCode(code);
        return Task.FromResult(_areas.Values.FirstOrDefault(a => a.SiteId == siteId && a.Code.Equals(norm, StringComparison.OrdinalIgnoreCase)) is { } area ? Clone(area) : null);
    }

    public Task AddAreaAsync(Area area, CancellationToken ct = default)
    {
        if (_areas.ContainsKey(area.Id.Value)) throw new InvalidOperationException("Area already exists.");
        if (_areas.Values.Any(a => a.SiteId == area.SiteId && a.Code.Equals(area.Code, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Area code already exists in this Site.");
        _areas[area.Id.Value] = Clone(area);
        return Task.CompletedTask;
    }

    public Task UpdateAreaAsync(Area area, CancellationToken ct = default)
    {
        if (!_areas.TryGetValue(area.Id.Value, out var current)) throw new InvalidOperationException("Area not found.");
        if (area.Version <= current.Version) throw new InvalidOperationException("VERSION_CONFLICT");
        _areas[area.Id.Value] = Clone(area);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Area>> GetAreasForSiteAsync(SiteId siteId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Area>>(_areas.Values.Where(a => a.SiteId == siteId).Select(Clone).ToList());

    // Asset
    public Task<Asset?> GetAssetAsync(AssetId id, CancellationToken ct = default) =>
        Task.FromResult(_assets.TryGetValue(id.Value, out var a) ? Clone(a) : null);

    public Task<Asset?> FindAssetByCodeAsync(AreaId areaId, string code, CancellationToken ct = default)
    {
        var norm = Site.NormalizeCode(code);
        return Task.FromResult(_assets.Values.FirstOrDefault(a => a.AreaId == areaId && a.Code.Equals(norm, StringComparison.OrdinalIgnoreCase)) is { } asset ? Clone(asset) : null);
    }

    public Task AddAssetAsync(Asset asset, CancellationToken ct = default)
    {
        if (_assets.ContainsKey(asset.Id.Value)) throw new InvalidOperationException("Asset already exists.");
        if (_assets.Values.Any(a => a.AreaId == asset.AreaId && a.Code.Equals(asset.Code, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Asset code already exists in this Area.");
        _assets[asset.Id.Value] = Clone(asset);
        return Task.CompletedTask;
    }

    public Task UpdateAssetAsync(Asset asset, CancellationToken ct = default)
    {
        if (!_assets.TryGetValue(asset.Id.Value, out var current)) throw new InvalidOperationException("Asset not found.");
        if (asset.Version <= current.Version) throw new InvalidOperationException("VERSION_CONFLICT");
        _assets[asset.Id.Value] = Clone(asset);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Asset>> GetAssetsForAreaAsync(AreaId areaId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Asset>>(_assets.Values.Where(a => a.AreaId == areaId).Select(Clone).ToList());

    public Task<IReadOnlyList<Asset>> GetAssetsForSiteAsync(SiteId siteId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Asset>>(_assets.Values.Where(a => a.SiteId == siteId).Select(Clone).ToList());

    // Point
    public Task<MeasurementPoint?> GetPointAsync(PointId id, CancellationToken ct = default) =>
        Task.FromResult(_points.TryGetValue(id.Value, out var p) ? Clone(p) : null);

    public Task<MeasurementPoint?> FindPointByCodeAsync(SiteId siteId, string code, CancellationToken ct = default)
    {
        var norm = Site.NormalizeCode(code);
        return Task.FromResult(_points.Values.FirstOrDefault(p => p.SiteId == siteId && p.Code.Equals(norm, StringComparison.OrdinalIgnoreCase)) is { } pt ? Clone(pt) : null);
    }

    public Task AddPointAsync(MeasurementPoint point, CancellationToken ct = default)
    {
        if (_points.ContainsKey(point.Id.Value)) throw new InvalidOperationException("Point already exists.");
        if (_points.Values.Any(p => p.SiteId == point.SiteId && p.Code.Equals(point.Code, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Point code already exists in this Site.");
        _points[point.Id.Value] = Clone(point);
        _reservedPointCodes.Add($"{point.SiteId}:{point.Code}");
        return Task.CompletedTask;
    }

    public Task UpdatePointAsync(MeasurementPoint point, CancellationToken ct = default)
    {
        if (!_points.TryGetValue(point.Id.Value, out var current)) throw new InvalidOperationException("Point not found.");
        if (point.Version <= current.Version) throw new InvalidOperationException("VERSION_CONFLICT");
        if (point.Status != current.Status)
            _lifecycle.Add(new PointLifecycleEntry(
                Guid.NewGuid().ToString(), point.Id.ToString(), point.Version,
                current.Status, point.Status, "system", null, null,
                DateTime.UtcNow, null, null));
        _points[point.Id.Value] = Clone(point);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MeasurementPoint>> GetPointsForAssetAsync(AssetId assetId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MeasurementPoint>>(_points.Values.Where(p => p.AssetId == assetId).Select(Clone).ToList());

    public Task<IReadOnlyList<MeasurementPoint>> GetPointsForSiteAsync(SiteId siteId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MeasurementPoint>>(_points.Values.Where(p => p.SiteId == siteId).Select(Clone).ToList());

    // Lifecycle
    public Task AddLifecycleEntryAsync(PointLifecycleEntry entry, CancellationToken ct = default)
    {
        _lifecycle.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PointLifecycleEntry>> GetLifecycleForPointAsync(string pointId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PointLifecycleEntry>>(_lifecycle.Where(e => e.PointId == pointId).ToList());

    // Code reservation
    public Task<bool> IsPointCodeReservedAsync(SiteId siteId, string code, CancellationToken ct = default)
    {
        var norm = Site.NormalizeCode(code);
        return Task.FromResult(_reservedPointCodes.Contains($"{siteId}:{norm}") || _points.Values.Any(p => p.SiteId == siteId && p.Code.Equals(norm, StringComparison.OrdinalIgnoreCase)));
    }

    // Transaction
    public Task<IOrganizationTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        Task.FromResult<IOrganizationTransaction>(new FakeOrganizationTransaction(this));

    // Cloning
    private static Site Clone(Site s) => new(s.Id, s.Code, s.Name, s.Description, s.Timezone, s.Status, s.Version);
    private static Area Clone(Area a) => new(a.Id, a.SiteId, a.Code, a.Name, a.Description, a.Status, a.Version);
    private static Asset Clone(Asset a) => new(a.Id, a.SiteId, a.AreaId, a.Code, a.Name, a.Description, a.Status, a.Version);
    private static MeasurementPoint Clone(MeasurementPoint p) => new(p.Id, p.SiteId, p.AreaId, p.AssetId,
        p.Code, p.Description, p.MetricId, p.UnitId, p.DataOwnerUserId,
        p.ExpectedIntervalSeconds, p.NoDataAfterSeconds, p.Status, p.Version);
}
