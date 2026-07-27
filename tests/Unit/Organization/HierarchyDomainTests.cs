using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Organization;

public static class HierarchyDomainTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var site = new Site(siteId, "MY-SITE", "My Site", null, "Asia/Ho_Chi_Minh", SiteStatus.Draft, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();

        // Site global code uniqueness
        ExpectFail(() => repo.AddSiteAsync(new Site(SiteId.New(), "my-site", "Duplicate", null, "UTC", SiteStatus.Draft, 1)).GetAwaiter().GetResult(),
            failures, "Site duplicate normalized code must be rejected");

        // Area code uniqueness within Site
        var areaId = AreaId.New();
        var area = new Area(areaId, siteId, "AREA-A", "Area A", null, AreaStatus.Draft, 1);
        repo.AddAreaAsync(area).GetAwaiter().GetResult();
        ExpectFail(() => repo.AddAreaAsync(new Area(AreaId.New(), siteId, "area-a", "Dupe Area", null, AreaStatus.Draft, 1)).GetAwaiter().GetResult(),
            failures, "Area duplicate code within Site must be rejected");

        // Same Area code permitted in a different Site
        var site2Id = SiteId.New();
        var site2 = new Site(site2Id, "SITE-2", "Site 2", null, "UTC", SiteStatus.Draft, 1);
        repo.AddSiteAsync(site2).GetAwaiter().GetResult();
        var areaInOtherSite = new Area(AreaId.New(), site2Id, "AREA-A", "Area A in other site", null, AreaStatus.Draft, 1);
        repo.AddAreaAsync(areaInOtherSite).GetAwaiter().GetResult();

        // Asset code uniqueness within Area
        var assetId = AssetId.New();
        var asset = new Asset(assetId, siteId, areaId, "ASSET-1", "Asset 1", null, AssetStatus.Draft, 1);
        repo.AddAssetAsync(asset).GetAwaiter().GetResult();
        ExpectFail(() => repo.AddAssetAsync(new Asset(AssetId.New(), siteId, areaId, "asset-1", "Dupe", null, AssetStatus.Draft, 1)).GetAwaiter().GetResult(),
            failures, "Asset duplicate code within Area must be rejected");

        // Point code uniqueness within Site
        var pointId = PointId.New();
        var point = new MeasurementPoint(pointId, siteId, areaId, assetId, "PT-01", null,
            "METRIC-1", "UNIT-1", "user-1", 60, 300, PointStatus.Draft, 1);
        repo.AddPointAsync(point).GetAwaiter().GetResult();
        ExpectFail(() => repo.AddPointAsync(new MeasurementPoint(PointId.New(), siteId, areaId, assetId, "pt-01", null,
            "METRIC-1", "UNIT-1", "user-1", 60, 300, PointStatus.Draft, 1)).GetAwaiter().GetResult(),
            failures, "Point duplicate code within Site must be rejected");

        // Point code remains reserved after decommission
        point = repo.GetPointAsync(pointId).GetAwaiter().GetResult()!;
        // Activate then decommission
        if (!point.TryActivate()) failures.Add("Point should activate first");
        repo.UpdatePointAsync(point).GetAwaiter().GetResult();
        point = repo.GetPointAsync(pointId).GetAwaiter().GetResult()!;
        var decommissioned = point.TryDecommission();
        if (!decommissioned)
            failures.Add("Point should be able to decommission via state change (no running sim)");
        repo.UpdatePointAsync(point).GetAwaiter().GetResult();
        var reserved = repo.IsPointCodeReservedAsync(siteId, "PT-01").GetAwaiter().GetResult();
        if (!reserved)
            failures.Add("Point code must remain reserved after decommission");

        // Normalized uppercase codes
        var siteMixed = new Site(SiteId.New(), "Mixed-Code", "Mixed", null, "UTC", SiteStatus.Draft, 1);
        if (siteMixed.Code != "MIXED-CODE")
            failures.Add("Site code must be normalized to uppercase");

        // Required fields
        ExpectFail(() => new Site(SiteId.New(), "", "No code", null, "UTC", SiteStatus.Draft, 1), failures, "Site requires non-empty code");
        ExpectFail(() => new Site(SiteId.New(), "CODE", "", null, "UTC", SiteStatus.Draft, 1), failures, "Site requires non-empty name");
        ExpectFail(() => new Site(SiteId.New(), "CODE", "Name", null, "", SiteStatus.Draft, 1), failures, "Site requires non-empty timezone");

        // Positive versions
        ExpectFail(() => new Site(SiteId.New(), "CODE", "Name", null, "UTC", SiteStatus.Draft, 0), failures, "Site version must be positive");
        ExpectFail(() => new Site(SiteId.New(), "CODE", "Name", null, "UTC", SiteStatus.Draft, -1), failures, "Site version must be positive");

        // Interval invariants
        ExpectFail(() => new MeasurementPoint(PointId.New(), siteId, areaId, assetId, "PT-BAD", null,
            "M", "U", "u", 0, 300, PointStatus.Draft, 1), failures, "expected_interval must be positive");
        ExpectFail(() => new MeasurementPoint(PointId.New(), siteId, areaId, assetId, "PT-BAD2", null,
            "M", "U", "u", 60, 30, PointStatus.Draft, 1), failures, "no_data_after must be greater than expected_interval");

        // Draft child under Draft parent
        var draftSiteId = SiteId.New();
        var draftSite = new Site(draftSiteId, "DRAFT-SITE", "Draft", null, "UTC", SiteStatus.Draft, 1);
        repo.AddSiteAsync(draftSite).GetAwaiter().GetResult();
        var draftArea = new Area(AreaId.New(), draftSiteId, "DRAFT-AREA", "Draft Area", null, AreaStatus.Draft, 1);
        repo.AddAreaAsync(draftArea).GetAwaiter().GetResult();
        var draftAsset = new Asset(AssetId.New(), draftSiteId, draftArea.Id, "DRAFT-ASSET", "Draft Asset", null, AssetStatus.Draft, 1);
        repo.AddAssetAsync(draftAsset).GetAwaiter().GetResult();
        var draftPoint = new MeasurementPoint(PointId.New(), draftSiteId, draftArea.Id, draftAsset.Id, "DRAFT-PT", null,
            "M", "U", "u", 60, 300, PointStatus.Draft, 1);
        repo.AddPointAsync(draftPoint).GetAwaiter().GetResult();

        // Create a fresh point for top-down activation tests
        var freshPtId = PointId.New();
        var freshPt = new MeasurementPoint(freshPtId, siteId, areaId, assetId, "PT-FRESH", null,
            "METRIC-1", "UNIT-1", "user-1", 60, 300, PointStatus.Draft, 1);
        repo.AddPointAsync(freshPt).GetAwaiter().GetResult();

        // Top-down activation
        // Site activation
        var siteActive = repo.GetSiteAsync(siteId).GetAwaiter().GetResult()!;
        var activated = siteActive.TryActivate();
        if (!activated) failures.Add("Draft Site should activate");
        // Redundant activate is no-op
        if (siteActive.TryActivate()) failures.Add("Already active Site must not change");
        repo.UpdateSiteAsync(siteActive).GetAwaiter().GetResult();

        // Area activation requires Active parent
        var areaToActivate = repo.GetAreaAsync(areaId).GetAwaiter().GetResult()!;
        if (!areaToActivate.TryActivate()) failures.Add("Draft Area under Active Site should activate");
        repo.UpdateAreaAsync(areaToActivate).GetAwaiter().GetResult();

        // Asset activation requires Active parent
        var assetToActivate = repo.GetAssetAsync(assetId).GetAwaiter().GetResult()!;
        if (!assetToActivate.TryActivate()) failures.Add("Draft Asset under Active Area should activate");
        repo.UpdateAssetAsync(assetToActivate).GetAwaiter().GetResult();

        // Point activation at domain level (status transition only)
        var freshPtFromRepo = repo.GetPointAsync(freshPtId).GetAwaiter().GetResult()!;
        if (!freshPtFromRepo.TryActivate()) failures.Add("Draft Point should activate at domain level");
        // Domain-level TryActivate succeeds even when parent is inactive
        var ptUnderDraft = repo.GetPointAsync(draftPoint.Id).GetAwaiter().GetResult()!;
        if (!ptUnderDraft.TryActivate()) failures.Add("Draft Point under Draft parent should activate at domain level");

        // Rejected/no-op transitions preserve state/version
        var versionBefore = siteActive.Version;
        if (siteActive.TryActivate()) failures.Add("Active Site trying to activate again must be no-op");
        if (siteActive.Version != versionBefore) failures.Add("No-op Site activate must not increment version");

        return failures;
    }

    private static void ExpectFail(Action action, List<string> failures, string invariant)
    {
        try { action(); failures.Add(invariant); }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
    }
}
