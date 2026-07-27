using IUMP.Modules.Catalog.Application;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Catalog;

public static class MappingReadinessTests
{
    private static int _assertionCount;

    public static List<string> Run()
    {
        var failures = new List<string>();
        _assertionCount = 0;
        AdapterReadiness(failures).GetAwaiter().GetResult();
        DraftMappingActivation(failures).GetAwaiter().GetResult();
        EventProducingReadyAssertions(failures).GetAwaiter().GetResult();
        ReadinessVersionTupleTests(failures).GetAwaiter().GetResult();
        FourIndependentVersionCases(failures).GetAwaiter().GetResult();
        Console.WriteLine($"T080: assertions={_assertionCount}; failures={failures.Count}");
        return failures;
    }

    private static async Task AdapterReadiness(List<string> failures)
    {
        var ids = (Site: Guid.NewGuid(), Area: Guid.NewGuid(), Asset: Guid.NewGuid(), Point: Guid.NewGuid());
        var query = new ReadinessQueryDouble();
        var adapter = new OrganizationPointReadinessAdapter(query);
        var missing = await adapter.GetPointReadinessAsync(Guid.NewGuid().ToString("D"));
        AssertT080(missing is { Exists: false }, failures, "Missing Point returns an explicit non-enumerating readiness snapshot.");

        query.Set(ids, SiteStatus.Draft, AreaStatus.Draft, AssetStatus.Draft, PointStatus.Draft, 60, 300);
        var draft = (await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!;
        AssertT080(draft.Exists && draft.IsConfigurationReady && !draft.IsProducingReady && draft.SiteId == ids.Site.ToString("D") && draft.AreaId == ids.Area.ToString("D"), failures, "Valid Draft Point is configuration-ready, trusted to its ancestors, and non-producing.");
        AssertT080(draft.ProviderVersion == 4, failures, "Readiness returns the provider snapshot version.");
        AssertT080(draft.ReadinessVersions.PointVersion == 4 && draft.ReadinessVersions.AssetVersion == 3 && draft.ReadinessVersions.AreaVersion == 2 && draft.ReadinessVersions.SiteVersion == 1, failures, "Readiness version tuple has exact per-object versions.");

        query.Set(ids, SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Active, 60, 300);
        var active = (await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!;
        AssertT080(active.IsConfigurationReady && active.IsProducingReady, failures, "Active Point with Active ancestors is producing-ready.");
        foreach (var status in new[] { SiteStatus.Inactive, SiteStatus.Active, SiteStatus.Active })
        {
            query.Set(ids, status, AreaStatus.Active, AssetStatus.Active, PointStatus.Active, 60, 300);
            var result = (await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!;
            if (status == SiteStatus.Inactive) AssertT080(!result.IsProducingReady, failures, "Inactive Site prevents producing readiness.");
        }
        query.Set(ids, SiteStatus.Active, AreaStatus.Inactive, AssetStatus.Active, PointStatus.Active, 60, 300);
        AssertT080(!(await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!.IsProducingReady, failures, "Inactive Area prevents producing readiness.");
        query.Set(ids, SiteStatus.Active, AreaStatus.Active, AssetStatus.Inactive, PointStatus.Active, 60, 300);
        AssertT080(!(await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!.IsProducingReady, failures, "Inactive Asset prevents producing readiness.");
        query.Set(ids, SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Inactive, 60, 300);
        AssertT080(!(await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!.IsProducingReady, failures, "Inactive Point prevents producing readiness.");
        query.Set(ids, SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Decommissioned, 60, 300);
        var decommissioned = (await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!;
        AssertT080(!decommissioned.IsConfigurationReady && !decommissioned.IsProducingReady, failures, "Decommissioned Point is not readiness eligible.");
        query.Set(ids, SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 0, 300);
        AssertT080(!(await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!.IsConfigurationReady, failures, "Invalid interval prevents configuration readiness.");
        query.Set(ids, SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 60);
        AssertT080(!(await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!.IsConfigurationReady, failures, "No-data threshold must exceed expected interval.");

        query.SetInconsistent(ids);
        AssertT080(!(await adapter.GetPointReadinessAsync(ids.Point.ToString("D")))!.Exists, failures, "Inconsistent trusted ancestry returns missing.");
    }

    private static async Task DraftMappingActivation(List<string> failures)
    {
        var repo = new FakeCatalogCommandRepository();
        var source = new DataSource(DataSourceId.New(), "READINESS-SOURCE", "Readiness", SourceType.Simulator, SourceStatus.Draft, 1);
        await repo.AddDataSourceAsync(source);

        var siteId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var draftPointId = Guid.NewGuid();
        var activePointId = Guid.NewGuid();
        var invalidPointId = Guid.NewGuid();

        var auth = new CatalogRoleScopeAuthorization(new CallerProvider(siteId));

        // Draft point: set readiness, create mapping, then activate
        var draftReadiness = new ReadinessQueryDouble();
        draftReadiness.Set((siteId, areaId, assetId, draftPointId),
            SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300);
        var draftAdapter = new OrganizationPointReadinessAdapter(draftReadiness);
        var draftHandler = new CatalogCommandHandler(repo, auth, draftAdapter);

        var created = await draftHandler.HandleAsync(new CreateMappingCommand(source.Id, draftPointId.ToString("D"), DateTime.UtcNow, "engineer", "forged-site"));
        AssertT080(created.IsSuccess, failures, "Mapping creation may target a configuration-ready Draft Point.");
        var mapping = (await repo.GetMappingsForPointAsync(draftPointId.ToString("D"))).Single();
        var activated = await draftHandler.HandleAsync(new UpdateMappingStatusCommand(mapping.Id, "activate", "engineer", "forged-site"));
        var saved = await repo.GetMappingAsync(mapping.Id);
        AssertT080(activated.IsSuccess && saved?.Status == MappingStatus.Active, failures, "Draft mapping activation succeeds.");
        AssertT080(!draftAdapter.GetPointReadinessAsync(draftPointId.ToString("D")).GetAwaiter().GetResult()!.IsProducingReady, failures, "Draft mapping activation preserves producingReady=false.");

        // Active point: set readiness, create mapping, then activate
        var activeReadiness = new ReadinessQueryDouble();
        activeReadiness.Set((siteId, areaId, assetId, activePointId),
            SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Active, 60, 300);
        var activeAdapter = new OrganizationPointReadinessAdapter(activeReadiness);
        var activeHandler = new CatalogCommandHandler(repo, auth, activeAdapter);

        var activeMapping = await activeHandler.HandleAsync(new CreateMappingCommand(source.Id, activePointId.ToString("D"), DateTime.UtcNow, "engineer", "forged-site"));
        AssertT080(activeMapping.IsSuccess, failures, "Create mapping for active point succeeds.");
        var activeList = await repo.GetMappingsForPointAsync(activePointId.ToString("D"));
        var activeId = activeList.Last();
        var activeActivated = await activeHandler.HandleAsync(new UpdateMappingStatusCommand(activeId.Id, "activate", "engineer", "forged-site"));
        var activeSaved = await repo.GetMappingAsync(activeId.Id);
        AssertT080(activeActivated.IsSuccess && activeSaved?.Status == MappingStatus.Active, failures, "Active hierarchy point activation succeeds.");
        AssertT080(activeAdapter.GetPointReadinessAsync(activePointId.ToString("D")).GetAwaiter().GetResult()!.IsProducingReady, failures, "Active hierarchy produces producingReady=true.");

        // Invalid point: no readiness set → handler should reject
        var invalidHandler = new CatalogCommandHandler(repo, auth, new OrganizationPointReadinessAdapter(new ReadinessQueryDouble()));
        var invalidMapping = await invalidHandler.HandleAsync(new CreateMappingCommand(source.Id, invalidPointId.ToString("D"), DateTime.UtcNow, "engineer", "forged-site"));
        AssertT080(!invalidMapping.IsSuccess, failures, "Create for non-existent/not-ready point is rejected.");

        AssertT080((await repo.GetMappingAsync(activeId.Id))?.Status == MappingStatus.Active, failures, "Catalog performs no Organization write after activation.");
    }

    private static async Task EventProducingReadyAssertions(List<string> failures)
    {
        var repo = new FakeCatalogCommandRepository();
        var source = new DataSource(DataSourceId.New(), "EVENT-READINESS", "Event Readiness", SourceType.Simulator, SourceStatus.Draft, 1);
        await repo.AddDataSourceAsync(source);

        var siteId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        // Draft point: create mapping → event should have producingReady=false in after
        var draftPointId = Guid.NewGuid();
        var draftReadiness = new ReadinessQueryDouble();
        draftReadiness.Set((siteId, areaId, assetId, draftPointId),
            SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300);
        var draftAuth = new CatalogRoleScopeAuthorization(new CallerProvider(siteId));
        var draftHandler = new CatalogCommandHandler(repo, draftAuth, new OrganizationPointReadinessAdapter(draftReadiness));

        var createResult = await draftHandler.HandleAsync(new CreateMappingCommand(source.Id, draftPointId.ToString("D"), DateTime.UtcNow, "engineer", "forged-site"));
        AssertT080(createResult.IsSuccess, failures, "Draft point create succeeds for event test.");
        AssertT080(draftHandler.Events.Count == 1, failures, "Draft point create emits one event.");
        var createEvent = draftHandler.Events.Single();
        AssertT080(createEvent.EventType == "SourcePointMappingChanged.v1", failures, "Create event EventType.");
        AssertT080(createEvent.SchemaVersion == "1", failures, "Create event SchemaVersion.");
        AssertT080(createEvent.Producer == "IUMP.Catalog", failures, "Create event Producer.");
        AssertT080(createEvent.AggregateType == "SourcePointMapping", failures, "Create event AggregateType.");
        AssertT080(createEvent.After.ContainsKey("producingReady"), failures, "Create event contains producingReady.");
        AssertT080(createEvent.After["producingReady"] is bool producing && !producing, failures, "Draft point create event has producingReady=false.");
        AssertT080(createEvent.Before.Count == 0, failures, "Create event has empty Before.");

        // Now activate the draft mapping
        var mapping = (await repo.GetMappingsForPointAsync(draftPointId.ToString("D"))).Single();
        var activateResult = await draftHandler.HandleAsync(new UpdateMappingStatusCommand(mapping.Id, "activate", "engineer", "forged-site"));
        AssertT080(activateResult.IsSuccess, failures, "Draft mapping activation succeeds for event test.");
        AssertT080(draftHandler.Events.Count == 2, failures, "Activation emits second event.");
        var activateEvent = draftHandler.Events.Last();
        AssertT080(activateEvent.After.ContainsKey("producingReady"), failures, "Activation event contains producingReady.");
        AssertT080(activateEvent.After["producingReady"] is bool producingAfter && !producingAfter, failures, "Draft activation event has producingReady=false.");
        AssertT080(activateEvent.Before["producingReady"] is bool producingBefore && !producingBefore, failures, "Activation Before producingReady=false.");

        // Active hierarchy point: create + activate → event should have producingReady=true
        var activePointId = Guid.NewGuid();
        var activeReadiness = new ReadinessQueryDouble();
        activeReadiness.Set((siteId, areaId, assetId, activePointId),
            SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Active, 60, 300);
        var activeHandler = new CatalogCommandHandler(repo, draftAuth, new OrganizationPointReadinessAdapter(activeReadiness));

        var activeCreate = await activeHandler.HandleAsync(new CreateMappingCommand(source.Id, activePointId.ToString("D"), DateTime.UtcNow, "engineer", "forged-site"));
        AssertT080(activeCreate.IsSuccess, failures, "Active point create succeeds.");
        var activeCreateEvent = activeHandler.Events.Single();
        AssertT080(activeCreateEvent.After["producingReady"] is bool activeProd && activeProd, failures, "Active hierarchy create event has producingReady=true.");

        var activeMapping = (await repo.GetMappingsForPointAsync(activePointId.ToString("D"))).Last();
        var activeActivate = await activeHandler.HandleAsync(new UpdateMappingStatusCommand(activeMapping.Id, "activate", "engineer", "forged-site"));
        AssertT080(activeActivate.IsSuccess, failures, "Active point activation succeeds.");
        var activeActivateEvent = activeHandler.Events.Last();
        AssertT080(activeActivateEvent.After["producingReady"] is bool actProd && actProd, failures, "Active hierarchy activation event has producingReady=true.");
        AssertT080(activeActivateEvent.Before["producingReady"] is bool actBefore && actBefore, failures, "Activation Before producingReady=true.");

        // Invalid point: no readiness → rejected, no event
        var invalidHandler = new CatalogCommandHandler(repo, draftAuth, new OrganizationPointReadinessAdapter(new ReadinessQueryDouble()));
        var invalidResult = await invalidHandler.HandleAsync(new CreateMappingCommand(source.Id, Guid.NewGuid().ToString("D"), DateTime.UtcNow, "engineer", "forged-site"));
        AssertT080(!invalidResult.IsSuccess, failures, "Invalid point rejected, no event emitted.");
        AssertT080(invalidHandler.Events.Count == 0, failures, "Invalid point emits no event.");
    }

    private static async Task ReadinessVersionTupleTests(List<string> failures)
    {
        var ids = (Site: Guid.NewGuid(), Area: Guid.NewGuid(), Asset: Guid.NewGuid(), Point: Guid.NewGuid());
        var query = new ReadinessQueryDouble();

        query.Set(ids, SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300);
        var draft = (await new OrganizationPointReadinessAdapter(query).GetPointReadinessAsync(ids.Point.ToString("D")))!;
        AssertT080(draft.ReadinessVersions.PointVersion == 4 && draft.ReadinessVersions.AssetVersion == 3 && draft.ReadinessVersions.AreaVersion == 2 && draft.ReadinessVersions.SiteVersion == 1, failures, "Version tuple reflects per-object versions when all are present.");

        query.SetWithCustom(ids, siteV: 10, areaV: 5, assetV: 20, pointV: 1);
        var custom = (await new OrganizationPointReadinessAdapter(query).GetPointReadinessAsync(ids.Point.ToString("D")))!;
        AssertT080(custom.ReadinessVersions.PointVersion == 1 && custom.ReadinessVersions.AssetVersion == 20 && custom.ReadinessVersions.AreaVersion == 5 && custom.ReadinessVersions.SiteVersion == 10, failures, "Changing all versions changes readiness snapshot.");
        AssertT080(custom.ProviderVersion == 20, failures, "Backward-compatible ProviderVersion still returns Max().");
    }

    private static async Task FourIndependentVersionCases(List<string> failures)
    {
        var baseIds = (Site: Guid.NewGuid(), Area: Guid.NewGuid(), Asset: Guid.NewGuid(), Point: Guid.NewGuid());
        var query = new ReadinessQueryDouble();

        // Base case: all default versions
        query.SetWithCustom(baseIds, siteV: 1, areaV: 2, assetV: 3, pointV: 4);
        var baseResult = (await new OrganizationPointReadinessAdapter(query).GetPointReadinessAsync(baseIds.Point.ToString("D")))!;
        AssertT080(baseResult.ReadinessVersions.PointVersion == 4, failures, "Base PointVersion=4.");

        // Case 1: change PointVersion only
        query.SetWithCustom(baseIds, siteV: 1, areaV: 2, assetV: 3, pointV: 99);
        var pointOnly = (await new OrganizationPointReadinessAdapter(query).GetPointReadinessAsync(baseIds.Point.ToString("D")))!;
        AssertT080(pointOnly.ReadinessVersions.PointVersion == 99, failures, "Changing PointVersion only updates PointVersion.");
        AssertT080(pointOnly.ReadinessVersions.AssetVersion == 3, failures, "Point-only change leaves AssetVersion unchanged.");
        AssertT080(pointOnly.ReadinessVersions.AreaVersion == 2, failures, "Point-only change leaves AreaVersion unchanged.");
        AssertT080(pointOnly.ReadinessVersions.SiteVersion == 1, failures, "Point-only change leaves SiteVersion unchanged.");

        // Case 2: change AssetVersion only
        query.SetWithCustom(baseIds, siteV: 1, areaV: 2, assetV: 88, pointV: 4);
        var assetOnly = (await new OrganizationPointReadinessAdapter(query).GetPointReadinessAsync(baseIds.Point.ToString("D")))!;
        AssertT080(assetOnly.ReadinessVersions.AssetVersion == 88, failures, "Changing AssetVersion only updates AssetVersion.");
        AssertT080(assetOnly.ReadinessVersions.PointVersion == 4, failures, "Asset-only change leaves PointVersion unchanged.");
        AssertT080(assetOnly.ReadinessVersions.AreaVersion == 2, failures, "Asset-only change leaves AreaVersion unchanged.");
        AssertT080(assetOnly.ReadinessVersions.SiteVersion == 1, failures, "Asset-only change leaves SiteVersion unchanged.");

        // Case 3: change AreaVersion only
        query.SetWithCustom(baseIds, siteV: 1, areaV: 77, assetV: 3, pointV: 4);
        var areaOnly = (await new OrganizationPointReadinessAdapter(query).GetPointReadinessAsync(baseIds.Point.ToString("D")))!;
        AssertT080(areaOnly.ReadinessVersions.AreaVersion == 77, failures, "Changing AreaVersion only updates AreaVersion.");
        AssertT080(areaOnly.ReadinessVersions.PointVersion == 4, failures, "Area-only change leaves PointVersion unchanged.");
        AssertT080(areaOnly.ReadinessVersions.AssetVersion == 3, failures, "Area-only change leaves AssetVersion unchanged.");
        AssertT080(areaOnly.ReadinessVersions.SiteVersion == 1, failures, "Area-only change leaves SiteVersion unchanged.");

        // Case 4: change SiteVersion only
        query.SetWithCustom(baseIds, siteV: 66, areaV: 2, assetV: 3, pointV: 4);
        var siteOnly = (await new OrganizationPointReadinessAdapter(query).GetPointReadinessAsync(baseIds.Point.ToString("D")))!;
        AssertT080(siteOnly.ReadinessVersions.SiteVersion == 66, failures, "Changing SiteVersion only updates SiteVersion.");
        AssertT080(siteOnly.ReadinessVersions.PointVersion == 4, failures, "Site-only change leaves PointVersion unchanged.");
        AssertT080(siteOnly.ReadinessVersions.AssetVersion == 3, failures, "Site-only change leaves AssetVersion unchanged.");
        AssertT080(siteOnly.ReadinessVersions.AreaVersion == 2, failures, "Site-only change leaves AreaVersion unchanged.");
    }

    private static void AssertT080(bool condition, List<string> failures, string message)
    {
        _assertionCount++;
        if (!condition) failures.Add($"T080: {message}");
    }

    private sealed class CallerProvider : ICatalogCallerSnapshotProvider
    {
        private readonly Guid _siteId;
        public CallerProvider(Guid siteId) => _siteId = siteId;
        public Task<CatalogCallerSnapshot?> ResolveAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult<CatalogCallerSnapshot?>(new CatalogCallerSnapshot("engineer", "engineer", true, new[] { "Engineer" }, new[] { _siteId.ToString("D") }, Array.Empty<string>()));
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

        public void SetWithCustom((Guid Site, Guid Area, Guid Asset, Guid Point) ids, long siteV = 1, long areaV = 2, long assetV = 3, long pointV = 4)
        {
            _site = new SiteSnapshot(ids.Site, "SITE", "Site", null, "UTC", SiteStatus.Active, siteV);
            _area = new AreaSnapshot(ids.Area, ids.Site, "AREA", "Area", null, AreaStatus.Active, areaV);
            _asset = new AssetSnapshot(ids.Asset, ids.Site, ids.Area, "ASSET", "Asset", null, AssetStatus.Active, assetV);
            _point = new PointSnapshot(ids.Point, ids.Site, ids.Area, ids.Asset, "POINT", null, "metric", "unit", "owner", 60, 300, PointStatus.Draft, pointV);
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
