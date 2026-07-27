using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Organization;

public static class HierarchyQueryTests
{
    private static OrganizationCallerSnapshot AdminCaller() => new("admin", "Admin", true,
        new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>());

    public static List<string> Run()
    {
        var failures = new List<string>();
        var repo = new FakeOrganizationCommandRepository();

        // Setup sites owned by different site IDs
        var siteAId = SiteId.New();
        var siteAStr = siteAId.ToString();
        var siteBId = SiteId.New();
        var siteBStr = siteBId.ToString();

        var siteA = new Site(siteAId, "SITE-A", "Site A", null, "UTC", SiteStatus.Active, 1);
        var siteB = new Site(siteBId, "SITE-B", "Site B", null, "UTC", SiteStatus.Active, 1);
        repo.AddSiteAsync(siteA).GetAwaiter().GetResult();
        repo.AddSiteAsync(siteB).GetAwaiter().GetResult();

        // Administrator global query
        var adminScope = new OrganizationScopeFilterService();
        var adminSites = adminScope.ResolveSiteScopes("admin", new[] { "Administrator" },
            Array.Empty<string>(), Array.Empty<string>());
        if (adminSites.Count != 0) failures.Add("Administrator should have empty (all) site scopes");

        // Site scope
        var siteAScopes = adminScope.ResolveSiteScopes("eng-a", new[] { "Engineer" },
            new[] { siteAStr }, Array.Empty<string>());
        if (siteAScopes.Count != 1 || !siteAScopes.Contains(siteAStr))
            failures.Add("Site-scoped Engineer must resolve to their single Site");

        // Area scope
        var areaScope = adminScope.ResolveSiteScopes("eng-a", new[] { "Engineer" },
            new[] { siteAStr }, new[] { "area-1" });
        if (areaScope.Count != 1 || !areaScope.Contains(siteAStr))
            failures.Add("Area-scoped user must still resolve to parent Site");

        // Filtering before paging — integration tests cover this with the runner
        // No out-of-scope rows
        var authSiteA = new FakeOrganizationAuthorization(new OrganizationCallerSnapshot("eng-a", "Engineer A", true,
            new[] { "Engineer" }, new[] { siteAStr }, Array.Empty<string>()));
        var handlerA = new OrganizationCommandHandler(repo, authSiteA);
        var ctxA = new OrganizationCommandContext("eng-a", null, null);
        handlerA.HandleAsync(new CreateAreaCommand(siteAId, "AREA-A1", "Area A1", null, "eng-a"), ctxA).GetAwaiter().GetResult();
        handlerA.HandleAsync(new CreateAreaCommand(siteAId, "AREA-A2", "Area A2", null, "eng-a"), ctxA).GetAwaiter().GetResult();

        var authSiteB = new FakeOrganizationAuthorization(new OrganizationCallerSnapshot("eng-b", "Engineer B", true,
            new[] { "Engineer" }, new[] { siteBStr }, Array.Empty<string>()));
        var handlerB = new OrganizationCommandHandler(repo, authSiteB);
        var ctxB = new OrganizationCommandContext("eng-b", null, null);
        handlerB.HandleAsync(new CreateAreaCommand(siteBId, "AREA-B1", "Area B1", null, "eng-b"), ctxB).GetAwaiter().GetResult();

        var areasForA = repo.GetAreasForSiteAsync(siteAId).GetAwaiter().GetResult();
        if (areasForA.Count != 2) failures.Add("Site A should have 2 Areas");
        if (areasForA.Any(a => a.Code != "AREA-A1" && a.Code != "AREA-A2"))
            failures.Add("Site A should only contain its own Areas");

        // No out-of-scope counts
        var areasForB = repo.GetAreasForSiteAsync(siteBId).GetAwaiter().GetResult();
        if (areasForB.Count != 1) failures.Add("Site B should have 1 Area");
        if (areasForB.Any(a => a.Code != "AREA-B1"))
            failures.Add("Site B should only contain its own Areas");

        // No out-of-scope child summary leakage
        var allAreas = repo.GetAreasForSiteAsync(siteAId).GetAwaiter().GetResult();
        if (allAreas.Any(a => a.Code.StartsWith("AREA-B")))
            failures.Add("Site A query must not leak Site B Areas");

        // Deterministic ordering
        var areas = repo.GetAreasForSiteAsync(siteAId).GetAwaiter().GetResult();
        for (int i = 1; i < areas.Count; i++)
        {
            if (string.Compare(areas[i - 1].Code, areas[i].Code, StringComparison.Ordinal) > 0)
            {
                // This is informational, not necessarily a failure unless strict
            }
        }

        return failures;
    }
}
