using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Modules.Organization.Application;

namespace IUMP.Tests.Integration.Organization;

// T071 deliberately targets the public repository contracts.  The runner knows
// nothing about a concrete adapter; a provider supplies command/query ports and
// a small simulator observation seam for the deterministic test host.
public interface IOrganizationRepositoryTestProvider
{
    IOrganizationCommandRepository CommandRepository { get; }
    IOrganizationQueryRepository QueryRepository { get; }
    IRunningSimulatorQuery RunningSimulatorQuery { get; }
    void ConfigureRunningSimulator(string pointId, bool isRunning);
    bool IsRunningSimulator(string pointId);
    void Reset();
}

public interface IOrganizationRepositoryTestProviderFactory
{
    IOrganizationRepositoryTestProvider Create();
}

public sealed class OrganizationRepositoryContractRunner
{
    private readonly IOrganizationRepositoryTestProviderFactory _factory;
    private readonly List<string> _failures = new();
    private int _testCount;
    private int _assertionCount;

    public OrganizationRepositoryContractRunner(IOrganizationRepositoryTestProviderFactory factory) => _factory = factory;
    public IReadOnlyList<string> Failures => _failures;
    public int TestCount => _testCount;
    public int AssertionCount => _assertionCount;

    public async Task RunAllAsync()
    {
        await SiteCodeUniqueness();
        await AreaCodeUniquenessWithinSite();
        await AreaCodesMayRepeatAcrossSites();
        await AssetAndPointAncestry();
        await AssetCodeUniquenessAndScope();
        await AssetCodeDuplicateInSameAreaRejected();
        await AssetLifecycleTransitionPersistence();
        await PointCodeReservation();
        await PointCodeReservedAfterDecommission();
        await SiteLifecycleTransition();
        await AreaLifecycleTransition();
        await PointDecommissionAndHistory();
        await RunningSimulatorDependency();
        await PointActivationIsPhaseFiveOnly();
        await OptimisticVersionBehavior();
        await StaleApplicationCommandVersion();
        await TransactionCommitAndRollback();
        await DeepRollbackExistingAggregate();
        await QueryScopePagingAndStableOrder();
    }

    private IOrganizationRepositoryTestProvider NewProvider() => _factory.Create();
    private void Pass() => _testCount++;
    private void Fail(string msg) { _failures.Add($"T071-CONTRACT: {msg}"); _testCount++; }
    private void Assert(bool condition, string msg) { _assertionCount++; if (!condition) Fail(msg); }

    private static async Task<(Site site, Area area, Asset asset)> AddHierarchy(IOrganizationCommandRepository repo,
        string prefix, SiteStatus siteStatus = SiteStatus.Active, AreaStatus areaStatus = AreaStatus.Active,
        AssetStatus assetStatus = AssetStatus.Active)
    {
        var site = new Site(SiteId.New(), $"{prefix}-SITE", "Test", null, "UTC", siteStatus, 1);
        var area = new Area(AreaId.New(), site.Id, $"{prefix}-AREA", "Test", null, areaStatus, 1);
        var asset = new Asset(AssetId.New(), site.Id, area.Id, $"{prefix}-ASSET", "Test", null, assetStatus, 1);
        await repo.AddSiteAsync(site);
        await repo.AddAreaAsync(area);
        await repo.AddAssetAsync(asset);
        return (site, area, asset);
    }

    private async Task SiteCodeUniqueness()
    {
        var repo = NewProvider().CommandRepository;
        await repo.AddSiteAsync(new Site(SiteId.New(), "UNIQUE-SITE", "First", null, "UTC", SiteStatus.Draft, 1));
        try
        {
            await repo.AddSiteAsync(new Site(SiteId.New(), "unique-site", "Duplicate", null, "UTC", SiteStatus.Draft, 1));
            Assert(false, "Duplicate site code must be rejected.");
        }
        catch (InvalidOperationException) { Assert(true, "Duplicate site rejected."); }
        Assert((await repo.GetAllSitesAsync()).Count == 1, "Duplicate site must not mutate state.");
        Pass();
    }

    private async Task AreaCodeUniquenessWithinSite()
    {
        var repo = NewProvider().CommandRepository;
        var (site, _, _) = await AddHierarchy(repo, "AREA", SiteStatus.Draft, AreaStatus.Draft, AssetStatus.Draft);
        try
        {
            await repo.AddAreaAsync(new Area(AreaId.New(), site.Id, "area-area", "Dupe", null, AreaStatus.Draft, 1));
            Assert(false, "Area code must be unique within a Site.");
        }
        catch (InvalidOperationException) { Assert(true, "Duplicate Area rejected."); }
        Assert((await repo.GetAreasForSiteAsync(site.Id)).Count == 1, "Duplicate Area must not mutate state.");
        Pass();
    }

    private async Task AreaCodesMayRepeatAcrossSites()
    {
        var repo = NewProvider().CommandRepository;
        var s1 = new Site(SiteId.New(), "AREA-S1", "One", null, "UTC", SiteStatus.Draft, 1);
        var s2 = new Site(SiteId.New(), "AREA-S2", "Two", null, "UTC", SiteStatus.Draft, 1);
        await repo.AddSiteAsync(s1); await repo.AddSiteAsync(s2);
        await repo.AddAreaAsync(new Area(AreaId.New(), s1.Id, "COMMON", "One", null, AreaStatus.Draft, 1));
        await repo.AddAreaAsync(new Area(AreaId.New(), s2.Id, "COMMON", "Two", null, AreaStatus.Draft, 1));
        Assert((await repo.GetAreasForSiteAsync(s1.Id)).Count == 1 && (await repo.GetAreasForSiteAsync(s2.Id)).Count == 1,
            "Area code uniqueness must be scoped to its parent Site.");
        Pass();
    }

    private async Task AssetAndPointAncestry()
    {
        var repo = NewProvider().CommandRepository;
        var (site, area, asset) = await AddHierarchy(repo, "ANCESTRY", SiteStatus.Active, AreaStatus.Active, AssetStatus.Active);
        try
        {
            await repo.AddAssetAsync(new Asset(AssetId.New(), SiteId.New(), area.Id, "BAD-ASSET", "Bad", null, AssetStatus.Draft, 1));
            Assert(false, "Cross-site Asset ancestry must be rejected.");
        }
        catch (InvalidOperationException) { Assert(true, "Cross-site Asset rejected."); }
        try
        {
            await repo.AddPointAsync(new MeasurementPoint(PointId.New(), site.Id, AreaId.New(), asset.Id, "BAD-POINT", null,
                "M", "U", "owner", 60, 300, PointStatus.Draft, 1));
            Assert(false, "Cross-area Point ancestry must be rejected.");
        }
        catch (InvalidOperationException) { Assert(true, "Cross-area Point rejected."); }
        Pass();
    }

    private async Task PointCodeReservation()
    {
        var repo = NewProvider().CommandRepository;
        var (site, area, asset) = await AddHierarchy(repo, "RESERVE");
        await repo.AddPointAsync(new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "PT-01", null,
            "M", "U", "owner", 60, 300, PointStatus.Draft, 1));
        Assert(await repo.IsPointCodeReservedAsync(site.Id, "pt-01"), "Point code reservation must be observable.");
        try
        {
            await repo.AddPointAsync(new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "PT-01", null,
                "M", "U", "owner", 60, 300, PointStatus.Draft, 1));
            Assert(false, "Reserved Point code must be rejected.");
        }
        catch (InvalidOperationException) { Assert(true, "Reserved Point code rejected."); }
        Pass();
    }

    private async Task AssetCodeUniquenessAndScope()
    {
        var repo = NewProvider().CommandRepository;
        var site = new Site(SiteId.New(), "ASSET-SCOPE-SITE", "Test", null, "UTC", SiteStatus.Active, 1);
        var area1 = new Area(AreaId.New(), site.Id, "AREA-ONE", "One", null, AreaStatus.Active, 1);
        var area2 = new Area(AreaId.New(), site.Id, "AREA-TWO", "Two", null, AreaStatus.Active, 1);
        await repo.AddSiteAsync(site); await repo.AddAreaAsync(area1); await repo.AddAreaAsync(area2);
        await repo.AddAssetAsync(new Asset(AssetId.New(), site.Id, area1.Id, "COMMON-ASSET", "One", null, AssetStatus.Draft, 1));
        await repo.AddAssetAsync(new Asset(AssetId.New(), site.Id, area2.Id, "COMMON-ASSET", "Two", null, AssetStatus.Draft, 1));
        Assert((await repo.GetAssetsForAreaAsync(area1.Id)).Count == 1 && (await repo.GetAssetsForAreaAsync(area2.Id)).Count == 1,
            "Same Asset code is allowed in another Area but not duplicated within one Area.");
        Pass();
    }

    private async Task AssetCodeDuplicateInSameAreaRejected()
    {
        var repo = NewProvider().CommandRepository;
        var site = new Site(SiteId.New(), "DUP-ASSET-SITE", "Test", null, "UTC", SiteStatus.Active, 1);
        var area = new Area(AreaId.New(), site.Id, "DUP-ASSET-AREA", "Test", null, AreaStatus.Active, 1);
        await repo.AddSiteAsync(site); await repo.AddAreaAsync(area);
        await repo.AddAssetAsync(new Asset(AssetId.New(), site.Id, area.Id, "DUP-ASSET", "One", null, AssetStatus.Draft, 1));
        try
        {
            await repo.AddAssetAsync(new Asset(AssetId.New(), site.Id, area.Id, "dup-asset", "Duplicate", null, AssetStatus.Draft, 1));
            Assert(false, "Duplicate Asset code in same Area must be rejected.");
        }
        catch (InvalidOperationException) { Assert(true, "Duplicate Asset in same Area rejected."); }
        Assert((await repo.GetAssetsForAreaAsync(area.Id)).Count == 1, "Rejected Asset must not mutate state.");
        Pass();
    }

    private async Task AssetLifecycleTransitionPersistence()
    {
        var repo = NewProvider().CommandRepository;
        var site = new Site(SiteId.New(), "ASSET-LIFE-SITE", "Test", null, "UTC", SiteStatus.Active, 1);
        var area = new Area(AreaId.New(), site.Id, "ASSET-LIFE-AREA", "Test", null, AreaStatus.Active, 1);
        await repo.AddSiteAsync(site); await repo.AddAreaAsync(area);
        var asset = new Asset(AssetId.New(), site.Id, area.Id, "ASSET-LIFE", "Test", null, AssetStatus.Draft, 1);
        await repo.AddAssetAsync(asset);
        Assert(asset.TryActivate(), "Draft Asset activates.");
        await repo.UpdateAssetAsync(asset);
        var saved = await repo.GetAssetAsync(asset.Id);
        Assert(saved?.Status == AssetStatus.Active && saved.Version == 2, "Asset lifecycle transition persists status and version.");
        Pass();
    }

    private async Task PointCodeReservedAfterDecommission()
    {
        var repo = NewProvider().CommandRepository;
        var (site, area, asset) = await AddHierarchy(repo, "DECOM-RESERVE");
        var point = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "DECOM-RESERVE-PT", null,
            "M", "U", "owner", 60, 300, PointStatus.Active, 1);
        await repo.AddPointAsync(point);
        Assert(await repo.IsPointCodeReservedAsync(site.Id, "DECOM-RESERVE-PT"), "Point code is reserved before decommission.");
        Assert(point.TryDecommission(), "Active Point decommission succeeds.");
        await repo.UpdatePointAsync(point);
        Assert(await repo.IsPointCodeReservedAsync(site.Id, "DECOM-RESERVE-PT"), "Point code remains reserved after decommission.");
        Pass();
    }

    private async Task StaleApplicationCommandVersion()
    {
        var provider = NewProvider();
        var repo = provider.CommandRepository;
        var (site, area, asset) = await AddHierarchy(repo, "STALE-VER");
        var handler = new OrganizationCommandHandler(repo, new ContractAdminAuthorization(), provider.RunningSimulatorQuery);
        var ctx = new OrganizationCommandContext("contract-admin", "stale-corr", "stale-caus");
        var staleResult = await handler.HandleAsync(new UpdateSiteCommand(site.Id, "Stale", null, "UTC", 42, "contract-admin"), ctx);
        Assert(staleResult.Code == "VERSION_CONFLICT" && (await repo.GetSiteAsync(site.Id))!.Name == "Test" &&
               (await repo.GetSiteAsync(site.Id))!.Version == 1,
            "Stale ExpectedVersion must fail with VERSION_CONFLICT and no mutation.");
        Assert(!handler.HasEvents, "Stale ExpectedVersion failure must not emit an event.");
        var currentResult = await handler.HandleAsync(new UpdateSiteCommand(site.Id, "Updated", null, "UTC", 1, "contract-admin"), ctx);
        Assert(currentResult.IsSuccess && (await repo.GetSiteAsync(site.Id))!.Name == "Updated" && (await repo.GetSiteAsync(site.Id))!.Version == 2,
            "Current ExpectedVersion must succeed and increment version.");
        Pass();
    }

    private async Task SiteLifecycleTransition()
    {
        var repo = NewProvider().CommandRepository;
        var site = new Site(SiteId.New(), "LIFECYCLE-SITE", "Test", null, "UTC", SiteStatus.Draft, 1);
        await repo.AddSiteAsync(site);
        Assert(site.TryActivate(), "Draft Site activates.");
        await repo.UpdateSiteAsync(site);
        var saved = await repo.GetSiteAsync(site.Id);
        Assert(saved?.Status == SiteStatus.Active && saved.Version == 2, "Lifecycle update persists status and version.");
        Pass();
    }

    private async Task AreaLifecycleTransition()
    {
        var repo = NewProvider().CommandRepository;
        var site = new Site(SiteId.New(), "AREA-LIFECYCLE-SITE", "Test", null, "UTC", SiteStatus.Active, 1);
        var area = new Area(AreaId.New(), site.Id, "AREA-LIFECYCLE", "Test", null, AreaStatus.Draft, 1);
        await repo.AddSiteAsync(site); await repo.AddAreaAsync(area);
        Assert(area.TryActivate(), "Draft Area activates.");
        await repo.UpdateAreaAsync(area);
        var saved = await repo.GetAreaAsync(area.Id);
        Assert(saved?.Status == AreaStatus.Active && saved.Version == 2, "Area lifecycle update persists status and version.");
        Pass();
    }

    private async Task PointDecommissionAndHistory()
    {
        var repo = NewProvider().CommandRepository;
        var (site, area, asset) = await AddHierarchy(repo, "DECOM");
        var point = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "DECOM-PT", null,
            "M", "U", "owner", 60, 300, PointStatus.Active, 1);
        await repo.AddPointAsync(point);
        var old = point.Status;
        Assert(point.TryDecommission(), "Active Point decommission succeeds.");
        await repo.UpdatePointAsync(point);
        await repo.AddLifecycleEntryAsync(new PointLifecycleEntry(Guid.NewGuid().ToString(), point.Id.ToString(), point.Version,
            old, point.Status, "actor", "actor@example", "test", DateTime.UtcNow, null, null));
        var history = await repo.GetLifecycleForPointAsync(point.Id.ToString());
        Assert(history.Count == 1 && history[0].OldStatus == old && history[0].NewStatus == PointStatus.Decommissioned,
            "Lifecycle history is explicit and records actual old/new status.");
        Pass();
    }

    private async Task RunningSimulatorDependency()
    {
        var provider = NewProvider();
        var repo = provider.CommandRepository;
        var (site, area, asset) = await AddHierarchy(repo, "RUNNING");
        var point = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "RUNNING-POINT", null, "M", "U", "owner", 60, 300, PointStatus.Active, 1);
        await repo.AddPointAsync(point);
        provider.ConfigureRunningSimulator(point.Id.ToString(), true);
        var handler = new OrganizationCommandHandler(repo, new ContractAdminAuthorization(), provider.RunningSimulatorQuery);
        var blocked = await handler.HandleAsync(new DecommissionPointCommand(point.Id, 1, "contract-admin"), new OrganizationCommandContext("contract-admin", "c", "x"));
        Assert(blocked.Code == "RUNNING_SIMULATOR" && (await repo.GetPointAsync(point.Id))!.Status == PointStatus.Active,
            "Provider-neutral decommission must be blocked by a running Simulator.");
        provider.ConfigureRunningSimulator(point.Id.ToString(), false);
        var accepted = await handler.HandleAsync(new DecommissionPointCommand(point.Id, 1, "contract-admin"), new OrganizationCommandContext("contract-admin", "c2", "x2"));
        Assert(accepted.IsSuccess && (await repo.GetLifecycleForPointAsync(point.Id.ToString())).Count == 1,
            "The same provider-neutral dependency permits eligible decommission after the run stops.");
        Pass();
    }

    private async Task PointActivationIsPhaseFiveOnly()
    {
        var provider = NewProvider();
        var repo = provider.CommandRepository;
        var (site, area, asset) = await AddHierarchy(repo, "PHASE5");
        var point = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "PHASE5-POINT", null, "M", "U", "owner", 60, 300, PointStatus.Draft, 1);
        await repo.AddPointAsync(point);
        var handler = new OrganizationCommandHandler(repo, new ContractAdminAuthorization(), provider.RunningSimulatorQuery);
        var result = await handler.HandleAsync(new UpdatePointStatusCommand(point.Id, "activate", 1, "contract-admin"), new OrganizationCommandContext("contract-admin", null, null));
        Assert(result.Code == "PHASE5_REQUIRED" && !(await repo.GetPointAsync(point.Id))!.IsActive, "Normal Point activation is unavailable before Phase 5.");
        Pass();
    }

    private async Task OptimisticVersionBehavior()
    {
        var repo = NewProvider().CommandRepository;
        var site = new Site(SiteId.New(), "OPT-VER-SITE", "Test", null, "UTC", SiteStatus.Draft, 1);
        await repo.AddSiteAsync(site);
        try
        {
            await repo.UpdateSiteAsync(new Site(site.Id, site.Code, site.Name, null, "UTC", SiteStatus.Active, 1));
            Assert(false, "Stale version must be rejected.");
        }
        catch (InvalidOperationException) { Assert(true, "Stale version rejected."); }
        Pass();
    }

    private async Task TransactionCommitAndRollback()
    {
        var provider = NewProvider();
        var repo = provider.CommandRepository;
        var committed = new Site(SiteId.New(), "TX-COMMIT", "Commit", null, "UTC", SiteStatus.Draft, 1);
        var tx1 = await repo.BeginTransactionAsync();
        await repo.AddSiteAsync(committed); await tx1.CommitAsync();
        Assert(await repo.GetSiteAsync(committed.Id) is not null, "Committed data remains visible.");
        var rolledBack = new Site(SiteId.New(), "TX-ROLLBACK", "Rollback", null, "UTC", SiteStatus.Draft, 1);
        var tx2 = await repo.BeginTransactionAsync();
        await repo.AddSiteAsync(rolledBack); await tx2.RollbackAsync();
        Assert(await repo.GetSiteAsync(rolledBack.Id) is null, "Rolled back data is removed.");
        Pass();
    }

    private async Task DeepRollbackExistingAggregate()
    {
        var repo = NewProvider().CommandRepository;
        var site = new Site(SiteId.New(), "TX-DEEP", "Before", null, "UTC", SiteStatus.Draft, 1);
        await repo.AddSiteAsync(site);
        var tx = await repo.BeginTransactionAsync();
        var changed = new Site(site.Id, site.Code, "After", null, "UTC", SiteStatus.Active, 2);
        await repo.UpdateSiteAsync(changed);
        await tx.RollbackAsync();
        var restored = await repo.GetSiteAsync(site.Id);
        Assert(restored?.Name == "Before" && restored.Status == SiteStatus.Draft && restored.Version == 1,
            "Rollback must restore a mutation to an existing aggregate.");
        Pass();
    }

    private async Task QueryScopePagingAndStableOrder()
    {
        var provider = NewProvider();
        var repo = provider.CommandRepository;
        var s1 = new Site(SiteId.New(), "QUERY-S1", "One", null, "UTC", SiteStatus.Active, 1);
        var s2 = new Site(SiteId.New(), "QUERY-S2", "Two", null, "UTC", SiteStatus.Active, 1);
        await repo.AddSiteAsync(s1); await repo.AddSiteAsync(s2);
        var a1 = new Area(AreaId.New(), s1.Id, "AREA-B", "B", null, AreaStatus.Active, 1);
        var a2 = new Area(AreaId.New(), s1.Id, "AREA-A", "A", null, AreaStatus.Active, 1);
        await repo.AddAreaAsync(a1); await repo.AddAreaAsync(a2);
        var asset1 = new Asset(AssetId.New(), s1.Id, a1.Id, "ASSET-A", "A", null, AssetStatus.Active, 1);
        var asset2 = new Asset(AssetId.New(), s1.Id, a2.Id, "ASSET-B", "B", null, AssetStatus.Active, 1);
        await repo.AddAssetAsync(asset1); await repo.AddAssetAsync(asset2);
        await repo.AddPointAsync(new MeasurementPoint(PointId.New(), s1.Id, a1.Id, asset1.Id, "POINT-A", null,
            "M", "U", "owner", 60, 300, PointStatus.Draft, 1));
        var result = await provider.QueryRepository.GetSitesAsync(
            new OrganizationQueryScope(false, new[] { s1.Id.Value }, Array.Empty<Guid>()), new ScopeFilter(1, 1));
        Assert(result.TotalCount == 1 && result.Items.Single().Id == s1.Id.Value, "Site scope filters before paging and totals.");
        var areas = await provider.QueryRepository.GetAreasForSiteAsync(s1.Id.Value,
            new OrganizationQueryScope(false, new[] { s1.Id.Value }, Array.Empty<Guid>()), new ScopeFilter(1, 10));
        Assert(areas.Items.Count == 2 && areas.Items[0].Code == "AREA-A" && areas.Items[1].Code == "AREA-B",
            "Query ordering is deterministic by code.");
        Assert(areas.Items.Single(a => a.Id == a1.Id.Value).AssetCount == 1 &&
               areas.Items.Single(a => a.Id == a2.Id.Value).AssetCount == 1,
            "Area child summaries are populated.");

        var areaScope = new OrganizationQueryScope(false, Array.Empty<Guid>(), new[] { a1.Id.Value });
        var scopedAreas = await provider.QueryRepository.GetAreasForSiteAsync(s1.Id.Value, areaScope, new ScopeFilter(1, 10));
        Assert(scopedAreas.TotalCount == 1 && scopedAreas.Items.Single().Id == a1.Id.Value,
            "Area scope filters before paging and totals without leaking a sibling Area.");
        var scopedAssets = await provider.QueryRepository.GetAssetsForAreaAsync(a1.Id.Value, areaScope, new ScopeFilter(1, 10));
        Assert(scopedAssets.TotalCount == 1 && scopedAssets.Items.Single().Id == asset1.Id.Value && scopedAssets.Items.Single().PointCount == 1,
            "Area scope returns only descendant Assets with Point summaries.");
        var scopedPoints = await provider.QueryRepository.GetPointsForSiteAsync(s1.Id.Value, areaScope, new ScopeFilter(1, 10));
        Assert(scopedPoints.TotalCount == 1 && scopedPoints.Items.Single().AreaId == a1.Id.Value,
            "Area scope returns descendant Points and no sibling leakage.");
        Pass();
    }
}

internal sealed class ContractAdminAuthorization : IOrganizationAuthorization
{
    private static readonly OrganizationCallerSnapshot Caller = new("contract-admin", "contract-admin", true,
        new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>());
    public Task<OrganizationAuthorizationDecision> AuthorizeAsync(string requestedByUserId, OrganizationResource resource,
        string? targetSiteId = null, CancellationToken ct = default) => Task.FromResult(OrganizationAuthorizationDecision.Allowed());
    public Task<OrganizationCallerSnapshot?> ResolveCallerAsync(string requestedByUserId, CancellationToken ct = default) => Task.FromResult<OrganizationCallerSnapshot?>(Caller);
}
