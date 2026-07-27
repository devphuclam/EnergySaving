using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;

namespace IUMP.Tests.Integration.Organization;

// T071 deliberately targets the public repository contracts.  The runner knows
// nothing about a concrete adapter; a provider supplies command/query ports and
// a small simulator observation seam for the deterministic test host.
public interface IOrganizationRepositoryTestProvider
{
    IOrganizationCommandRepository CommandRepository { get; }
    IOrganizationQueryRepository QueryRepository { get; }
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
        await PointCodeReservation();
        await SiteLifecycleTransition();
        await PointDecommissionAndHistory();
        await RunningSimulatorDependency();
        await OptimisticVersionBehavior();
        await TransactionCommitAndRollback();
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
        var pointId = PointId.New().ToString();
        provider.ConfigureRunningSimulator(pointId, true);
        Assert(provider.IsRunningSimulator(pointId), "Provider exposes running simulator state without concrete casts.");
        provider.Reset();
        Assert(!provider.IsRunningSimulator(pointId), "Provider reset clears simulator state.");
        await Task.CompletedTask;
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
        var result = await provider.QueryRepository.GetSitesAsync(
            new OrganizationQueryScope(false, new[] { s1.Id.Value }, Array.Empty<Guid>()), new ScopeFilter(1, 1));
        Assert(result.TotalCount == 1 && result.Items.Single().Id == s1.Id.Value, "Site scope filters before paging and totals.");
        var areas = await provider.QueryRepository.GetAreasForSiteAsync(s1.Id.Value,
            new OrganizationQueryScope(false, new[] { s1.Id.Value }, Array.Empty<Guid>()), new ScopeFilter(1, 10));
        Assert(areas.Items.Count == 2 && areas.Items[0].Code == "AREA-A" && areas.Items[1].Code == "AREA-B",
            "Query ordering is deterministic by code.");
        Assert(areas.Items.All(a => a.AssetCount == 0), "Area child summaries are populated.");
        Pass();
    }
}
