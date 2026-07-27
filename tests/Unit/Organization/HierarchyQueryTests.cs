using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Organization;

public static class HierarchyQueryTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        var commands = new FakeOrganizationCommandRepository();
        var queries = new FakeOrganizationQueryRepository(commands);
        var siteA = new Site(SiteId.New(), "SITE-A", "Site A", null, "UTC", SiteStatus.Active, 1);
        var siteB = new Site(SiteId.New(), "SITE-B", "Site B", null, "UTC", SiteStatus.Active, 1);
        commands.AddSiteAsync(siteA).GetAwaiter().GetResult();
        commands.AddSiteAsync(siteB).GetAwaiter().GetResult();
        var a1 = new Area(AreaId.New(), siteA.Id, "AREA-B", "Area B", null, AreaStatus.Active, 1);
        var a2 = new Area(AreaId.New(), siteA.Id, "AREA-A", "Area A", null, AreaStatus.Active, 1);
        var b1 = new Area(AreaId.New(), siteB.Id, "AREA-B1", "Area B1", null, AreaStatus.Active, 1);
        commands.AddAreaAsync(a1).GetAwaiter().GetResult();
        commands.AddAreaAsync(a2).GetAwaiter().GetResult();
        commands.AddAreaAsync(b1).GetAwaiter().GetResult();
        var asset = new Asset(AssetId.New(), siteA.Id, a2.Id, "ASSET-A", "Asset A", null, AssetStatus.Active, 1);
        commands.AddAssetAsync(asset).GetAwaiter().GetResult();
        var point = new MeasurementPoint(PointId.New(), siteA.Id, a2.Id, asset.Id, "POINT-A", null, "M", "U", "owner", 60, 300, PointStatus.Draft, 1);
        commands.AddPointAsync(point).GetAwaiter().GetResult();

        var callers = new Dictionary<string, OrganizationCallerSnapshot>(StringComparer.Ordinal)
        {
            ["admin"] = new("admin", "Administrator", true, new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>()),
            ["site-a"] = new("site-a", "Site A Engineer", true, new[] { "Engineer" }, new[] { siteA.Id.ToString() }, Array.Empty<string>()),
            ["area-a"] = new("area-a", "Area A Engineer", true, new[] { "Engineer" }, Array.Empty<string>(), new[] { a2.Id.ToString() }),
            ["site-b"] = new("site-b", "Site B Engineer", true, new[] { "Engineer" }, new[] { siteB.Id.ToString() }, Array.Empty<string>())
        };
        var service = new OrganizationQueryService(queries, new CallerProvider(callers));

        var global = service.GetSitesAsync("admin", new ScopeFilter(1, 10)).GetAwaiter().GetResult();
        if (global.TotalCount != 2 || global.Items.Any(s => s.AreaCount == 0))
            failures.Add("Administrator query must see all Sites and child Area summaries.");

        var scoped = service.GetAreasAsync("site-a", siteA.Id.Value, new ScopeFilter(1, 1)).GetAwaiter().GetResult();
        if (scoped.TotalCount != 2 || scoped.Items.Count != 1 || scoped.Items[0].Code != "AREA-A")
            failures.Add("Site scope must filter before paging and preserve deterministic code order.");

        var areaScoped = service.GetAssetsAsync("area-a", a2.Id.Value, new ScopeFilter(1, 10)).GetAwaiter().GetResult();
        if (areaScoped.TotalCount != 1 || areaScoped.Items[0].PointCount != 1)
            failures.Add("Area scope must include descendants and child Point summaries.");

        var outOfScope = service.GetAreaAsync("site-b", a2.Id.Value).GetAwaiter().GetResult();
        if (outOfScope is not null)
            failures.Add("Out-of-scope detail must be indistinguishable from NotFound.");

        var siteBRows = service.GetSitesAsync("site-b", new ScopeFilter(1, 10)).GetAwaiter().GetResult();
        if (siteBRows.TotalCount != 1 || siteBRows.Items[0].Id != siteB.Id.Value)
            failures.Add("Scoped query must not leak another Site.");

        return failures;
    }

    private sealed class CallerProvider : IOrganizationCallerSnapshotProvider
    {
        private readonly IReadOnlyDictionary<string, OrganizationCallerSnapshot> _callers;
        public CallerProvider(IReadOnlyDictionary<string, OrganizationCallerSnapshot> callers) => _callers = callers;
        public Task<OrganizationCallerSnapshot?> ResolveAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(_callers.TryGetValue(userId, out var caller) ? caller : null);
    }
}
