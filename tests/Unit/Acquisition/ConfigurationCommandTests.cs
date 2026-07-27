using IUMP.Modules.Acquisition.Application;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Catalog.Application;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Acquisition;

public static class ConfigurationCommandTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        AdminCreateAndEdit(failures).GetAwaiter().GetResult();
        EngineerCreateWithSingleScope(failures).GetAwaiter().GetResult();
        EngineerEditWithSingleScope(failures).GetAwaiter().GetResult();
        ManagerDenied(failures).GetAwaiter().GetResult();
        ViewerDenied(failures).GetAwaiter().GetResult();
        return failures;
    }

    private static async Task AdminCreateAndEdit(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var acqRepo = new FakeAcquisitionConfigurationRepository();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId,
            "trusted-site", "trusted-area", SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300);
        var service = new SimulatorConfigurationService(acqRepo, callers, adapter);

        var create = await service.CreateAsync(Command("admin", sourceId, 42, "corr-create", "caus-create"));
        Assert(create.IsSuccess, failures, "Administrator can create globally and source identity is resolved server-side.");
        var head = await acqRepo.GetBySourceIdAsync(sourceId);
        Assert(head is not null, failures, "Create persists a configuration head.");
        var firstEvent = service.Events.Single();
        Assert(firstEvent.EventType == SimulatorConfigurationConstants.EventType, failures, "EventType is correct.");
        Assert(firstEvent.SchemaVersion == "1", failures, "SchemaVersion is 1.");
        Assert(firstEvent.Producer == SimulatorConfigurationConstants.Producer, failures, "Producer is IUMP.Acquisition.");
        Assert(firstEvent.AggregateType == "SimulatorConfiguration", failures, "AggregateType is correct.");
        Assert(firstEvent.AggregateId == head!.ConfigurationId.ToString("D"), failures, "AggregateId matches head.");
        Assert(firstEvent.AggregateVersion == head.Version, failures, "AggregateVersion matches head version.");
        Assert(firstEvent.ActorId == "admin", failures, "ActorId is snapshot.");
        Assert(firstEvent.ActorUsername == "admin.user", failures, "ActorUsername is snapshot.");
        Assert(firstEvent.Action == "Created", failures, "Action is Created.");
        Assert(firstEvent.Summary == "Simulator configuration created.", failures, "Summary is correct.");
        Assert(firstEvent.OccurredAtUtc.Kind == DateTimeKind.Utc, failures, "OccurredAtUtc is UTC.");
        Assert(firstEvent.CorrelationId == "corr-create", failures, "CorrelationId is exact supplied value.");
        Assert(firstEvent.CausationId == "caus-create", failures, "CausationId is exact supplied value.");
        Assert(firstEvent.SiteIds.Count == 1 && firstEvent.SiteIds[0] == GuidHash("trusted-site").ToString("D"), failures, "Event has trusted SiteIds collection.");

        var allowedFields = new[] { "sourceId", "configurationId", "configurationVersion", "intervalSeconds", "minimumValue", "maximumValue", "deterministicSeed", "deterministicSeedHex", "scenarioType", "algorithmId", "algorithmVersion" };
        Assert(firstEvent.After.Keys.OrderBy(x => x).SequenceEqual(allowedFields.OrderBy(x => x)), failures, "Event after fields use the explicit safe allowlist.");
        Assert(!firstEvent.After.Keys.Any(k => k.Contains("password", StringComparison.OrdinalIgnoreCase) || k.Contains("secret", StringComparison.OrdinalIgnoreCase) || k.Contains("connection", StringComparison.OrdinalIgnoreCase)), failures, "Event contains no credentials, secrets or connection information.");
        Assert(firstEvent.After["deterministicSeed"] is string ds && ds == "42", failures, "deterministicSeed is invariant decimal string.");
        Assert(firstEvent.After["deterministicSeedHex"] is string dh && dh == "000000000000002a", failures, "deterministicSeedHex is exact lowercase 16-hex.");
        Assert(firstEvent.Before.Count == 0, failures, "Create event has empty Before dictionary.");

        var edit = await service.EditAsync(Edit("admin", head!.ConfigurationId, head.Version, 7, 30, 2, 4, SimulatorScenario.Normal, "corr-edit", "caus-edit"));
        Assert(edit.IsSuccess && service.Events.Count == 2, failures, "Edit creates exactly one next immutable version and one owner event.");
        var versions = await acqRepo.ListVersionsAsync(head.ConfigurationId);
        Assert(versions.Count == 2 && versions[0].MinimumValue == 1 && versions[1].MinimumValue == 2, failures, "Previous version remains unchanged after edit.");

        var editEvent = service.Events.Last();
        Assert(editEvent.CorrelationId == "corr-edit", failures, "Edit event CorrelationId is exact supplied value.");
        Assert(editEvent.CausationId == "caus-edit", failures, "Edit event CausationId is exact supplied value.");
        Assert(editEvent.AggregateVersion == 2, failures, "Edit event AggregateVersion is incremented.");
        Assert(editEvent.Action == "Edited", failures, "Edit event Action is Edited.");
        Assert(editEvent.Summary == "Simulator configuration changed.", failures, "Edit event Summary is correct.");
        Assert(editEvent.Before["deterministicSeed"] is string bds && bds == "42", failures, "Before has exact previous seed.");
        Assert(editEvent.After["deterministicSeed"] is string ads && ads == "7", failures, "After has exact new seed.");
        Assert(editEvent.SiteIds.Count == 1 && editEvent.SiteIds[0] == GuidHash("trusted-site").ToString("D"), failures, "Edit event has trusted SiteIds collection.");

        var stale = await service.EditAsync(Edit("admin", head.ConfigurationId, 1, 99, 40, 5, 6, SimulatorScenario.Normal, "corr-stale", "caus-stale"));
        Assert(stale.Code == "VERSION_CONFLICT" && service.Events.Count == 2 && (await acqRepo.ListVersionsAsync(head.ConfigurationId)).Count == 2, failures, "Stale ExpectedVersion emits no version and no event.");
        var noop = await service.EditAsync(Edit("admin", head.ConfigurationId, 2, 7, 30, 2, 4, SimulatorScenario.Normal, "corr-noop", "caus-noop"));
        Assert(noop.Code == "NO_OP" && service.Events.Count == 2 && (await acqRepo.ListVersionsAsync(head.ConfigurationId)).Count == 2, failures, "No-op edit emits no version and no event.");
    }

    private static async Task EngineerCreateWithSingleScope(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId,
            "eng-site", "eng-area", SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300,
            new ConfigurationCallerSnapshot("engineer", "engineer.user", true, new[] { "Engineer" }, new[] { "eng-site" }));
        var service = new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, adapter);

        var result = await service.CreateAsync(Command("engineer", sourceId, 42, "corr-eng", "caus-eng"));
        Assert(result.IsSuccess, failures, "Engineer with matching Site scope can create configuration.");
        var createdEvent = service.Events.Single();
        Assert(createdEvent.ActorId == "engineer", failures, "Engineer event ActorId is snapshot.");
        Assert(createdEvent.ActorUsername == "engineer.user", failures, "Engineer event ActorUsername is snapshot.");
        Assert(createdEvent.SiteIds.Count == 1 && createdEvent.SiteIds[0] == GuidHash("eng-site").ToString("D"), failures, "Engineer event SiteIds matches scope.");
    }

    private static async Task EngineerEditWithSingleScope(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var acqRepo = new FakeAcquisitionConfigurationRepository();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId,
            "eng-site", "eng-area", SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300,
            new ConfigurationCallerSnapshot("engineer", "engineer.user", true, new[] { "Engineer" }, new[] { "eng-site" }));
        var service = new SimulatorConfigurationService(acqRepo, callers, adapter);

        var create = await service.CreateAsync(Command("engineer", sourceId, 42, "corr-eng", "caus-eng"));
        Assert(create.IsSuccess, failures, "Engineer creates configuration for edit test.");
        var head = await acqRepo.GetBySourceIdAsync(sourceId);

        var edit = await service.EditAsync(Edit("engineer", head!.ConfigurationId, head.Version, 7, 30, 2, 4, SimulatorScenario.Normal, "corr-eng-edit", "caus-eng-edit"));
        Assert(edit.IsSuccess, failures, "Engineer can edit own-scope configuration.");
        Assert(service.Events.Count == 2, failures, "Engineer edit produces exactly two events.");
        var editEvent = service.Events.Last();
        Assert(editEvent.ActorId == "engineer", failures, "Engineer edit event ActorId is snapshot.");
        Assert(editEvent.Action == "Edited", failures, "Engineer edit event Action is Edited.");
    }

    private static async Task ManagerDenied(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId,
            "site-a", "area-a", SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300,
            new ConfigurationCallerSnapshot("manager", "manager.user", true, new[] { "Manager" }, new[] { "site-a" }));
        var acqRepo = new FakeAcquisitionConfigurationRepository();
        var service = new SimulatorConfigurationService(acqRepo, callers, adapter);

        var result = await service.CreateAsync(Command("manager", sourceId, 1, "corr-mgr", "caus-mgr"));
        Assert(result.Code == "FORBIDDEN", failures, "Manager role is denied configuration mutation.");
        Assert(service.Events.Count == 0, failures, "Manager denial emits no event.");
    }

    private static async Task ViewerDenied(List<string> failures)
    {
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var (adapter, _, callers) = CreateAdapterChain(sourceId, pointId,
            "site-a", "area-a", SiteStatus.Active, AreaStatus.Active, AssetStatus.Active, PointStatus.Draft, 60, 300,
            new ConfigurationCallerSnapshot("viewer", "viewer.user", true, new[] { "Viewer" }, new[] { "site-a" }));
        var acqRepo = new FakeAcquisitionConfigurationRepository();
        var service = new SimulatorConfigurationService(acqRepo, callers, adapter);

        var result = await service.CreateAsync(Command("viewer", sourceId, 1, "corr-view", "caus-view"));
        Assert(result.Code == "FORBIDDEN", failures, "Viewer role is denied configuration mutation.");
        Assert(service.Events.Count == 0, failures, "Viewer denial emits no event.");
    }

    private static (CatalogSourceScopeQueryAdapter, FakeCatalogCommandRepository, FakeCallerProvider) CreateAdapterChain(
        Guid sourceId, Guid pointId, string siteId, string areaId,
        SiteStatus siteStatus, AreaStatus areaStatus, AssetStatus assetStatus, PointStatus pointStatus,
        int interval, int noData,
        ConfigurationCallerSnapshot? engineerCaller = null)
    {
        var catalog = new FakeCatalogCommandRepository();
        var dataSource = new DataSource(new DataSourceId(sourceId), "SRC-" + sourceId.ToString("N")[..6], "Test Source", SourceType.Simulator, SourceStatus.Active, 1);
        var dsId = dataSource.Id;
        catalog.AddDataSourceAsync(dataSource).GetAwaiter().GetResult();
        var mapping = new SourcePointMapping(MappingId.New(), dsId, pointId.ToString("D"), MappingStatus.Active,
            DateTime.UtcNow.AddDays(-1), null, 1);
        catalog.AddMappingAsync(mapping).GetAwaiter().GetResult();

        // Derive deterministic GUIDs from the friendly siteId/areaId for consistency
        var siteGuid = GuidHash(siteId);
        var areaGuid = GuidHash(areaId);
        var assetGuid = Guid.NewGuid();
        var orgIds = (Site: siteGuid, Area: areaGuid, Asset: assetGuid, Point: pointId);
        var readiness = new ReadinessQueryDouble();
        readiness.Set(orgIds, siteStatus, areaStatus, assetStatus, pointStatus, interval, noData);
        var readinessAdapter = new OrganizationPointReadinessAdapter(readiness);
        var scopeAdapter = new CatalogSourceScopeQueryAdapter(catalog, readinessAdapter);

        var callers = new FakeCallerProvider();
        callers.Set(new ConfigurationCallerSnapshot("admin", "admin.user", true, new[] { "Administrator" }, Array.Empty<string>()));
        if (engineerCaller is not null)
        {
            var mappedSiteScopes = engineerCaller.SiteScopes
                .Select(s => GuidHash(s).ToString("D"))
                .ToList();
            callers.Set(new ConfigurationCallerSnapshot(
                engineerCaller.UserId, engineerCaller.Username, engineerCaller.IsActive,
                engineerCaller.Roles, mappedSiteScopes));
        }

        return (scopeAdapter, catalog, callers);
    }

    private static Guid GuidHash(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(bytes[..16]);
    }

    private static SimulatorConfigurationCreateCommand Command(string actor, Guid sourceId, ulong seed, string correlation, string causation) =>
        new(sourceId, seed, 60, 1, 1, SimulatorScenario.Constant, SimulatorConfigurationConstants.AlgorithmId,
            SimulatorConfigurationConstants.AlgorithmVersion, actor, correlation, causation);

    private static SimulatorConfigurationEditCommand Edit(string actor, Guid configurationId, long expected, ulong seed,
        int interval, double min, double max, SimulatorScenario scenario, string correlation, string causation) =>
        new(configurationId, expected, seed, interval, min, max, scenario,
            SimulatorConfigurationConstants.AlgorithmId, SimulatorConfigurationConstants.AlgorithmVersion, actor, correlation, causation);

    private static void Assert(bool condition, List<string> failures, string message)
    {
        if (!condition) failures.Add($"T079: {message}");
    }

    private sealed class FakeCallerProvider : IConfigurationCallerSnapshotProvider
    {
        private readonly Dictionary<string, ConfigurationCallerSnapshot> _callers = new(StringComparer.Ordinal);
        public void Set(ConfigurationCallerSnapshot caller) => _callers[caller.UserId] = caller;
        public Task<ConfigurationCallerSnapshot?> ResolveAsync(string userId, CancellationToken ct = default) => Task.FromResult(_callers.GetValueOrDefault(userId));
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

        public Task<SiteSnapshot?> GetSiteSnapshotAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_site?.Id == id ? _site : null);
        public Task<AreaSnapshot?> GetAreaSnapshotAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_area?.Id == id ? _area : null);
        public Task<AssetSnapshot?> GetAssetSnapshotAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_asset?.Id == id ? _asset : null);
        public Task<PointSnapshot?> GetPointSnapshotAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_point?.Id == id ? _point : null);
        public Task<SiteSnapshot?> FindSiteByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult<SiteSnapshot?>(null);
        public Task<PagedResult<SiteSnapshot>> GetSitesAsync(OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) => Task.FromResult(new PagedResult<SiteSnapshot>(Array.Empty<SiteSnapshot>(), 0, filter.Page, filter.PageSize));
        public Task<PagedResult<AreaSnapshot>> GetAreasForSiteAsync(Guid siteId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) => Task.FromResult(new PagedResult<AreaSnapshot>(Array.Empty<AreaSnapshot>(), 0, filter.Page, filter.PageSize));
        public Task<PagedResult<AssetSnapshot>> GetAssetsForAreaAsync(Guid areaId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) => Task.FromResult(new PagedResult<AssetSnapshot>(Array.Empty<AssetSnapshot>(), 0, filter.Page, filter.PageSize));
        public Task<PagedResult<PointSnapshot>> GetPointsForAssetAsync(Guid assetId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) => Task.FromResult(new PagedResult<PointSnapshot>(Array.Empty<PointSnapshot>(), 0, filter.Page, filter.PageSize));
        public Task<PagedResult<PointSnapshot>> GetPointsForSiteAsync(Guid siteId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default) => Task.FromResult(new PagedResult<PointSnapshot>(Array.Empty<PointSnapshot>(), 0, filter.Page, filter.PageSize));
        public Task<bool> SiteExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_site?.Id == id);
        public Task<long> GetSiteVersionAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_site?.Id == id ? _site.Version : 0);
        public Task<AreaAncestrySnapshot?> GetAreaAncestryAsync(Guid areaId, CancellationToken ct = default) => Task.FromResult<AreaAncestrySnapshot?>(_area?.Id == areaId ? new AreaAncestrySnapshot(_area.Id, _area.SiteId) : null);
    }
}
