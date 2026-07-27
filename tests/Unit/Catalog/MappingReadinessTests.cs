using IUMP.Modules.Catalog.Application;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Catalog;

public static class MappingReadinessTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        AdapterReadiness(failures).GetAwaiter().GetResult();
        DraftMappingActivation(failures).GetAwaiter().GetResult();
        return failures;
    }

    private static async Task AdapterReadiness(List<string> failures)
    {
        var ids = (Site: Guid.NewGuid(), Area: Guid.NewGuid(), Asset: Guid.NewGuid(), Point: Guid.NewGuid());
        var query = new ReadinessQueryDouble();
        var adapter = new OrganizationPointReadinessAdapter(query);
        var missing = await adapter.GetPointReadinessAsync(Guid.NewGuid().ToString("D"));
        Assert(missing is { Exists: false }, failures, "Missing Point returns an explicit non-enumerating readiness snapshot.");

        query.Set(ids, SiteStatus.Draft, AreaStatus.Draft, AssetStatus.Draft, PointStatus.Draft, 60, 300);
        var draft = (await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!;
        Assert(draft.Exists && draft.IsConfigurationReady && !draft.IsProducingReady && draft.SiteId == ids.Site.ToString("D") && draft.AreaId == ids.Area.ToString("D"), failures, "Valid Draft Point is configuration-ready, trusted to its ancestors, and non-producing.");
        Assert(draft.ProviderVersion == 4, failures, "Readiness returns the provider snapshot version.");

        query.Set(ids, SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Active, 60, 300);
        var active = (await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!;
        Assert(active.IsConfigurationReady && active.IsProducingReady, failures, "Active Point with Active ancestors is producing-ready.");
        foreach (var status in new[] { SiteStatus.Inactive, SiteStatus.Active, SiteStatus.Active })
        {
            query.Set(ids, status, AreaStatus.Active, AssetStatus.Active, PointStatus.Active, 60, 300);
            var result = (await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!;
            if (status == SiteStatus.Inactive) Assert(!result.IsProducingReady, failures, "Inactive Site prevents producing readiness.");
        }
        query.Set(ids, SiteStatus.Active, AreaStatus.Inactive, AssetStatus.Active, PointStatus.Active, 60, 300);
        Assert(!(await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!.IsProducingReady, failures, "Inactive Area prevents producing readiness.");
        query.Set(ids, SiteStatus.Active, AreaStatus.Active, AssetStatus.Inactive, PointStatus.Active, 60, 300);
        Assert(!(await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!.IsProducingReady, failures, "Inactive Asset prevents producing readiness.");
        query.Set(ids, SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Inactive, 60, 300);
        Assert(!(await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!.IsProducingReady, failures, "Inactive Point prevents producing readiness.");
        query.Set(ids, SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Decommissioned, 60, 300);
        var decommissioned = (await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!;
        Assert(!decommissioned.IsConfigurationReady && !decommissioned.IsProducingReady, failures, "Decommissioned Point is not readiness eligible.");
        query.Set(ids, SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 0, 300);
        Assert(!(await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!.IsConfigurationReady, failures, "Invalid interval prevents configuration readiness.");
        query.Set(ids, SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 60);
        Assert(!(await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!.IsConfigurationReady, failures, "No-data threshold must exceed expected interval.");

        query.SetInconsistent(ids);
        Assert(!(await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!.Exists, failures, "Inconsistent trusted ancestry returns missing.");
    }

    private static async Task DraftMappingActivation(List<string> failures)
    {
        var repo = new FakeCatalogCommandRepository();
        var source = new DataSource(DataSourceId.New(), "READINESS-SOURCE", "Readiness", SourceType.Simulator, SourceStatus.Draft, 1);
        await repo.AddDataSourceAsync(source);
        var readiness = new FakePointReadinessQuery().Configure("draft-point", new PointReadinessSnapshot("draft-point", "trusted-site", "trusted-area", true, true, false, 4));
        var auth = new CatalogRoleScopeAuthorization(new CallerProvider());
        var handler = new CatalogCommandHandler(repo, auth, readiness);
        var created = await handler.HandleAsync(new CreateMappingCommand(source.Id, "draft-point", DateTime.UtcNow, "engineer", "forged-site"));
        Assert(created.IsSuccess, failures, "Mapping creation may target a configuration-ready Draft Point.");
        var mapping = (await repo.GetMappingsForPointAsync("draft-point")).Single();
        var activated = await handler.HandleAsync(new UpdateMappingStatusCommand(mapping.Id, "activate", "engineer", "forged-site"));
        var saved = await repo.GetMappingAsync(mapping.Id);
        Assert(activated.IsSuccess && saved?.Status == MappingStatus.Active && !readinessSnapshot(readiness).IsProducingReady, failures, "Draft mapping activation preserves producingReady=false.");
    }

    private static PointReadinessSnapshot readinessSnapshot(FakePointReadinessQuery query) =>
        query.GetPointReadinessAsync("draft-point").GetAwaiter().GetResult()!;

    private static void Assert(bool condition, List<string> failures, string message)
    {
        if (!condition) failures.Add($"T080: {message}");
    }

    private sealed class CallerProvider : ICatalogCallerSnapshotProvider
    {
        public Task<CatalogCallerSnapshot?> ResolveAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult<CatalogCallerSnapshot?>(new CatalogCallerSnapshot("engineer", "engineer", true, new[] { "Engineer" }, new[] { "trusted-site" }, Array.Empty<string>()));
    }

    private sealed class ReadinessQueryDouble : IOrganizationQueryRepository
    {
        private PointSnapshot? _point;
        private AssetSnapshot? _asset;
        private AreaSnapshot? _area;
        private SiteSnapshot? _site;

        public void Set((Guid Site, Guid Area, Guid Asset, Guid Point) ids, SiteStatus siteStatus, AreaStatus areaStatus,
            AssetStatus assetStatus, PointStatus pointStatus, int interval, int noData)
        {
            _site = new SiteSnapshot(ids.Site, "SITE", "Site", null, "UTC", siteStatus, 1);
            _area = new AreaSnapshot(ids.Area, ids.Site, "AREA", "Area", null, areaStatus, 2);
            _asset = new AssetSnapshot(ids.Asset, ids.Site, ids.Area, "ASSET", "Asset", null, assetStatus, 3);
            _point = new PointSnapshot(ids.Point, ids.Site, ids.Area, ids.Asset, "POINT", null, "metric", "unit", "owner", interval, noData, pointStatus, 4);
        }

        public void SetInconsistent((Guid Site, Guid Area, Guid Asset, Guid Point) ids) { Set(ids, SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Active, 60, 300); _asset = _asset! with { SiteId = Guid.NewGuid() }; }
        public Task<SiteSnapshot?> GetSiteSnapshotAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_site?.Id == id ? _site : null);
        public Task<AreaSnapshot?> GetAreaSnapshotAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_area?.Id == id ? _area : null);
        public Task<AssetSnapshot?> GetAssetSnapshotAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_asset?.Id == id ? _asset : null);
        public Task<PointSnapshot?> GetPointSnapshotAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_point?.Id == id ? _point : null);
        public Task<SiteSnapshot?> FindSiteByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult<SiteSnapshot?>(null);
        public Task<PagedResult<SiteSnapshot>> GetSitesAsync(OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) => Task.FromResult(Empty<SiteSnapshot>(filter));
        public Task<PagedResult<AreaSnapshot>> GetAreasForSiteAsync(Guid siteId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) => Task.FromResult(Empty<AreaSnapshot>(filter));
        public Task<PagedResult<AssetSnapshot>> GetAssetsForAreaAsync(Guid areaId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) => Task.FromResult(Empty<AssetSnapshot>(filter));
        public Task<PagedResult<PointSnapshot>> GetPointsForAssetAsync(Guid assetId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) => Task.FromResult(Empty<PointSnapshot>(filter));
        public Task<PagedResult<PointSnapshot>> GetPointsForSiteAsync(Guid siteId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) => Task.FromResult(Empty<PointSnapshot>(filter));
        public Task<bool> SiteExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_site?.Id == id);
        public Task<long> GetSiteVersionAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_site?.Id == id ? _site.Version : 0);
        public Task<AreaAncestrySnapshot?> GetAreaAncestryAsync(Guid areaId, CancellationToken ct = default) => Task.FromResult<AreaAncestrySnapshot?>(_area?.Id == areaId ? new AreaAncestrySnapshot(_area.Id, _area.SiteId) : null);
        private static PagedResult<T> Empty<T>(ScopeFilter filter) => new(Array.Empty<T>(), 0, filter.Page, filter.PageSize);
    }
}
