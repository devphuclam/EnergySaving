using IUMP.Modules.Organization.Domain;
using IUMP.Modules.Organization.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Integration.Organization;

public sealed class OrganizationRepositoryContractRunner
{
    private readonly IOrganizationCommandRepository _cmdRepo;
    private readonly List<string> _failures = new();
    private int _testCount;
    private int _assertionCount;

    public OrganizationRepositoryContractRunner(IOrganizationCommandRepository cmdRepo)
    {
        _cmdRepo = cmdRepo;
    }

    public IReadOnlyList<string> Failures => _failures;
    public int TestCount => _testCount;
    public int AssertionCount => _assertionCount;

    public async Task RunAllAsync()
    {
        await SiteCodeUniqueness();
        await AreaCodeUniquenessWithinSite();
        await AssetCodeUniquenessWithinArea();
        await PointCodeUniquenessWithinSite();
        await SiteLifecycleTransition();
        await PointDecommissionAndHistory();
        await OptimisticVersionBehavior();
        await TransactionCommit();
        await TransactionRollback();
    }

    private void Pass() { _testCount++; }
    private void Fail(string msg) { _failures.Add($"T071-CONTRACT: {msg}"); _testCount++; }
    private void Assert(bool condition, string msg) { _assertionCount++; if (!condition) Fail(msg); }

    private async Task SiteCodeUniqueness()
    {
        var repo = (FakeOrganizationCommandRepository)_cmdRepo;
        var id1 = SiteId.New();
        var id2 = SiteId.New();
        var s1 = new Site(id1, "UNIQUE-SITE", "First", null, "UTC", SiteStatus.Draft, 1);
        var s2 = new Site(id2, "unique-site", "Duplicate", null, "UTC", SiteStatus.Draft, 1);
        await repo.AddSiteAsync(s1);
        var before = (await repo.GetAllSitesAsync()).Count;
        Assert(before == 1, "SiteCodeUniqueness: One site must exist after first add.");
        try
        {
            await repo.AddSiteAsync(s2);
            Assert(false, "SiteCodeUniqueness: Duplicate site code must be rejected.");
        }
        catch (InvalidOperationException)
        {
            Assert(true, "SiteCodeUniqueness: Duplicate rejected.");
        }
        var after = (await repo.GetAllSitesAsync()).Count;
        Assert(after == before, "SiteCodeUniqueness: Count unchanged after reject.");
        Pass();
    }

    private async Task AreaCodeUniquenessWithinSite()
    {
        var repo = (FakeOrganizationCommandRepository)_cmdRepo;
        var siteId = SiteId.New();
        await repo.AddSiteAsync(new Site(siteId, "AREA-UNIQUE-SITE", "Test", null, "UTC", SiteStatus.Draft, 1));
        var a1 = new Area(AreaId.New(), siteId, "AREA-X", "First", null, AreaStatus.Draft, 1);
        await repo.AddAreaAsync(a1);
        var before = (await repo.GetAreasForSiteAsync(siteId)).Count;
        try
        {
            await repo.AddAreaAsync(new Area(AreaId.New(), siteId, "area-x", "Dupe", null, AreaStatus.Draft, 1));
            Assert(false, "AreaCodeUniquenessWithinSite: Duplicate must be rejected.");
        }
        catch (InvalidOperationException)
        {
            Assert(true, "AreaCodeUniquenessWithinSite: Duplicate rejected.");
        }
        var after = (await repo.GetAreasForSiteAsync(siteId)).Count;
        Assert(after == before, "AreaCodeUniquenessWithinSite: Count unchanged.");
        Pass();
    }

    private async Task AssetCodeUniquenessWithinArea()
    {
        var repo = (FakeOrganizationCommandRepository)_cmdRepo;
        var siteId = SiteId.New();
        var areaId = AreaId.New();
        await repo.AddSiteAsync(new Site(siteId, "ASSET-UNIQUE-SITE", "Test", null, "UTC", SiteStatus.Draft, 1));
        await repo.AddAreaAsync(new Area(areaId, siteId, "ASSET-AREA", "Test", null, AreaStatus.Draft, 1));
        var as1 = new Asset(AssetId.New(), siteId, areaId, "ASSET-1", "First", null, AssetStatus.Draft, 1);
        await repo.AddAssetAsync(as1);
        var before = (await repo.GetAssetsForAreaAsync(areaId)).Count;
        try
        {
            await repo.AddAssetAsync(new Asset(AssetId.New(), siteId, areaId, "asset-1", "Dupe", null, AssetStatus.Draft, 1));
            Assert(false, "AssetCodeUniquenessWithinArea: Duplicate must be rejected.");
        }
        catch (InvalidOperationException)
        {
            Assert(true, "AssetCodeUniquenessWithinArea: Duplicate rejected.");
        }
        var after = (await repo.GetAssetsForAreaAsync(areaId)).Count;
        Assert(after == before, "AssetCodeUniquenessWithinArea: Count unchanged.");
        Pass();
    }

    private async Task PointCodeUniquenessWithinSite()
    {
        var repo = (FakeOrganizationCommandRepository)_cmdRepo;
        var siteId = SiteId.New();
        var areaId = AreaId.New();
        var assetId = AssetId.New();
        await repo.AddSiteAsync(new Site(siteId, "PT-UNIQUE-SITE", "Test", null, "UTC", SiteStatus.Draft, 1));
        await repo.AddAreaAsync(new Area(areaId, siteId, "PT-AREA", "Test", null, AreaStatus.Draft, 1));
        await repo.AddAssetAsync(new Asset(assetId, siteId, areaId, "PT-ASSET", "Test", null, AssetStatus.Draft, 1));
        var p1 = new MeasurementPoint(PointId.New(), siteId, areaId, assetId, "PT-01", null, "M", "U", "u", 60, 300, PointStatus.Draft, 1);
        await repo.AddPointAsync(p1);
        var before = (await repo.GetPointsForSiteAsync(siteId)).Count;
        try
        {
            await repo.AddPointAsync(new MeasurementPoint(PointId.New(), siteId, areaId, assetId, "pt-01", null, "M", "U", "u", 60, 300, PointStatus.Draft, 1));
            Assert(false, "PointCodeUniquenessWithinSite: Duplicate must be rejected.");
        }
        catch (InvalidOperationException)
        {
            Assert(true, "PointCodeUniquenessWithinSite: Duplicate rejected.");
        }
        var after = (await repo.GetPointsForSiteAsync(siteId)).Count;
        Assert(after == before, "PointCodeUniquenessWithinSite: Count unchanged.");
        Pass();
    }

    private async Task SiteLifecycleTransition()
    {
        var repo = (FakeOrganizationCommandRepository)_cmdRepo;
        var siteId = SiteId.New();
        var site = new Site(siteId, "LIFECYCLE-SITE", "Test", null, "UTC", SiteStatus.Draft, 1);
        await repo.AddSiteAsync(site);
        var saved = await repo.GetSiteAsync(siteId);
        Assert(saved != null && saved.Status == SiteStatus.Draft, "SiteLifecycleTransition: Initial status is Draft.");
        saved!.TryActivate();
        await repo.UpdateSiteAsync(saved);
        var activated = await repo.GetSiteAsync(siteId);
        Assert(activated != null && activated.Status == SiteStatus.Active, "SiteLifecycleTransition: Status changed to Active.");
        Assert(activated!.Version == 2, "SiteLifecycleTransition: Version incremented on activate.");
        Pass();
    }

    private async Task PointDecommissionAndHistory()
    {
        var repo = (FakeOrganizationCommandRepository)_cmdRepo;
        var siteId = SiteId.New();
        var areaId = AreaId.New();
        var assetId = AssetId.New();
        await repo.AddSiteAsync(new Site(siteId, "DECOM-SITE", "Test", null, "UTC", SiteStatus.Active, 1));
        await repo.AddAreaAsync(new Area(areaId, siteId, "DECOM-AREA", "Test", null, AreaStatus.Active, 1));
        await repo.AddAssetAsync(new Asset(assetId, siteId, areaId, "DECOM-ASSET", "Test", null, AssetStatus.Active, 1));
        var ptId = PointId.New();
        var pt = new MeasurementPoint(ptId, siteId, areaId, assetId, "DECOM-PT", null, "M", "U", "u", 60, 300, PointStatus.Active, 1);
        await repo.AddPointAsync(pt);
        var savedPt = await repo.GetPointAsync(ptId);
        Assert(savedPt != null, "PointDecommissionAndHistory: Point exists.");
        Assert(savedPt!.TryDecommission(), "PointDecommissionAndHistory: Decommission succeeds.");
        await repo.UpdatePointAsync(savedPt);
        var decomState = await repo.GetPointAsync(ptId);
        Assert(decomState!.Status == PointStatus.Decommissioned, "PointDecommissionAndHistory: Status is Decommissioned.");
        var history = await repo.GetLifecycleForPointAsync(ptId.ToString());
        Assert(history.Count == 1, "PointDecommissionAndHistory: One lifecycle entry recorded.");
        Assert(history[0].OldStatus == PointStatus.Active, "PointDecommissionAndHistory: Old status is Active.");
        Assert(history[0].NewStatus == PointStatus.Decommissioned, "PointDecommissionAndHistory: New status is Decommissioned.");
        Pass();
    }

    private async Task OptimisticVersionBehavior()
    {
        var repo = (FakeOrganizationCommandRepository)_cmdRepo;
        var siteId = SiteId.New();
        var site = new Site(siteId, "OPT-VER-SITE", "Test", null, "UTC", SiteStatus.Draft, 1);
        await repo.AddSiteAsync(site);
        var savedSite = await repo.GetSiteAsync(siteId);
        Assert(savedSite != null && savedSite.Version == 1, "OptimisticVersion: Initial version is 1.");
        var stale = new Site(siteId, "OPT-VER-SITE", "Stale", null, "UTC", SiteStatus.Active, 1);
        try
        {
            await repo.UpdateSiteAsync(stale);
            Assert(false, "OptimisticVersion: Stale version must be rejected.");
        }
        catch (InvalidOperationException)
        {
            Assert(true, "OptimisticVersion: Stale version rejected.");
        }
        Pass();
    }

    private async Task TransactionCommit()
    {
        var repo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var tx = await repo.BeginTransactionAsync();
        var site = new Site(siteId, "TX-COMMIT-SITE", "Test", null, "UTC", SiteStatus.Draft, 1);
        await repo.AddSiteAsync(site);
        await tx.CommitAsync();
        var found = await repo.GetSiteAsync(siteId);
        Assert(found != null, "TransactionCommit: Site found after commit.");
        var fakeTx = (FakeOrganizationTransaction)tx;
        Assert(fakeTx.IsCommitted, "TransactionCommit: Transaction committed.");
        Pass();
    }

    private async Task TransactionRollback()
    {
        var repo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var areaId = AreaId.New();
        var existingSite = new Site(siteId, "TX-ROLLBACK-EXISTING", "Existing", null, "UTC", SiteStatus.Draft, 1);
        await repo.AddSiteAsync(existingSite);
        var tx = await repo.BeginTransactionAsync();
        var newSite = new Site(SiteId.New(), "TX-ROLLBACK-NEW", "New", null, "UTC", SiteStatus.Draft, 1);
        await repo.AddSiteAsync(newSite);
        await tx.RollbackAsync();
        var existingAfter = await repo.GetSiteAsync(siteId);
        Assert(existingAfter != null, "TransactionRollback: Existing site persists.");
        var newAfter = await repo.GetSiteAsync(newSite.Id);
        Assert(newAfter == null, "TransactionRollback: New site removed.");
        var fakeTx = (FakeOrganizationTransaction)tx;
        Assert(fakeTx.IsRolledBack, "TransactionRollback: Transaction rolled back.");
        Pass();
    }
}
