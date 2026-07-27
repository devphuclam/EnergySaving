using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Organization;

public static class DecommissionTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var (siteId, areaId, assetId, pointIds) = Setup(repo);

        // Asset decommission with no Active Point succeeds
        var assetNoActive = new Asset(AssetId.New(), siteId, areaId, "ASSET-NO-ACTIVE", "No Active Child", null, AssetStatus.Active, 1);
        repo.AddAssetAsync(assetNoActive).GetAwaiter().GetResult();
        var np = new MeasurementPoint(PointId.New(), siteId, areaId, assetNoActive.Id, "NP-01", null,
            "M", "U", "u", 60, 300, PointStatus.Inactive, 1);
        repo.AddPointAsync(np).GetAwaiter().GetResult();
        var decommissioned = assetNoActive.TryDecommission();
        if (!decommissioned) failures.Add("Asset decommission with no Active Point should succeed");
        repo.UpdateAssetAsync(assetNoActive).GetAwaiter().GetResult();

        // Asset decommission with Active Point fails atomically
        assetId = AssetId.New();
        var assetWithActive = new Asset(assetId, siteId, areaId, "ASSET-ACTIVE", "Active Child", null, AssetStatus.Active, 1);
        repo.AddAssetAsync(assetWithActive).GetAwaiter().GetResult();
        var activePt = new MeasurementPoint(PointId.New(), siteId, areaId, assetWithActive.Id, "AP-01", null,
            "M", "U", "u", 60, 300, PointStatus.Active, 1);
        repo.AddPointAsync(activePt).GetAwaiter().GetResult();
        var children = repo.GetPointsForAssetAsync(assetWithActive.Id).GetAwaiter().GetResult();
        var canDecom = DecommissionPolicy.CanDecommissionAsset(assetWithActive, children);
        if (canDecom) failures.Add("Asset with Active child must fail decommission policy check");

        // No child cascade
        var childStillActive = repo.GetPointAsync(activePt.Id).GetAwaiter().GetResult()!;
        if (childStillActive.IsActive != true)
            failures.Add("Failed Asset decommission must not cascade to child Point");

        // Point decommission with Running Simulator fails
        var pointToDecom = repo.GetPointAsync(pointIds[0]).GetAwaiter().GetResult()!;
        if (DecommissionPolicy.CanDecommissionPoint(pointToDecom, true))
            failures.Add("Point decommission must fail when simulator is Running");

        // Successful Point decommission after dependency clears
        if (!DecommissionPolicy.CanDecommissionPoint(pointToDecom, false))
            failures.Add("Point decommission must succeed when no Running simulator");
        var decomResult = pointToDecom.TryDecommission();
        if (!decomResult) failures.Add("Point decommission should succeed");
        repo.UpdatePointAsync(pointToDecom).GetAwaiter().GetResult();

        // Decommissioned terminal
        if (!pointToDecom.IsDecommissioned) failures.Add("Decommissioned Point status must be terminal");
        if (pointToDecom.TryDecommission()) failures.Add("Already decommissioned Point must not change");
        if (pointToDecom.TryActivate()) failures.Add("Decommissioned Point must not reactivate");

        // Lifecycle-history append on accepted Point transition
        var history = repo.GetLifecycleForPointAsync(pointToDecom.Id.ToString()).GetAwaiter().GetResult();
        var decomEntry = history.FirstOrDefault(e => e.NewStatus == PointStatus.Decommissioned);
        if (decomEntry is null) failures.Add("Accepted decommission must append lifecycle history");

        // No history/event after rejected decommission
        if (pointToDecom.TryDecommission()) failures.Add("Already decommissioned Point TryDecommission must return false");
        var historyAfterNoop = repo.GetLifecycleForPointAsync(pointToDecom.Id.ToString()).GetAwaiter().GetResult();
        var decomCount = historyAfterNoop.Count(e => e.NewStatus == PointStatus.Decommissioned);
        if (decomCount != 1) failures.Add("Rejected decommission must not create additional history");

        return failures;
    }

    private static (SiteId, AreaId, AssetId, List<PointId>) Setup(FakeOrganizationCommandRepository repo)
    {
        var siteId = SiteId.New();
        var site = new Site(siteId, "DECOM-SITE", "Decom Site", null, "UTC", SiteStatus.Active, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();
        var areaId = AreaId.New();
        var area = new Area(areaId, siteId, "DECOM-AREA", "Decom Area", null, AreaStatus.Active, 1);
        repo.AddAreaAsync(area).GetAwaiter().GetResult();
        var assetId = AssetId.New();
        var asset = new Asset(assetId, siteId, areaId, "DECOM-ASSET", "Decom Asset", null, AssetStatus.Active, 1);
        repo.AddAssetAsync(asset).GetAwaiter().GetResult();
        var pt1 = new MeasurementPoint(PointId.New(), siteId, areaId, assetId, "DECOM-PT1", null,
            "M", "U", "u", 60, 300, PointStatus.Active, 1);
        repo.AddPointAsync(pt1).GetAwaiter().GetResult();
        return (siteId, areaId, assetId, new List<PointId> { pt1.Id });
    }
}
