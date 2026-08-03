using IUMP.Api.Infrastructure;
using IUMP.Modules.Audit.Application;
using IUMP.Modules.Audit.Contracts;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Organization.Contracts;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace IUMP.Tests.Integration.OperationalWorkspace;

/// <summary>Phase 5 RED seam: real PostgreSQL scope-before-keyset behavior.</summary>
public static class AuditDashboardTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }

    public static async Task<IReadOnlyList<string>> RunAsync(IServiceProvider root)
    {
        TestCount = 0;
        AssertionCount = 0;
        var failures = new List<string>();
        using var scope = root.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuditQueryRepository>();
        var append = scope.ServiceProvider.GetRequiredService<IAuditAppendRepository>();
        var now = DateTime.UtcNow;
        var source = Guid.NewGuid();
        var site = Guid.NewGuid().ToString("D");
        var area = Guid.NewGuid().ToString("D");
        var outsideSite = Guid.NewGuid().ToString("D");
        var inEntityId = Guid.NewGuid().ToString("D");

        await append.AppendIfAbsentAsync(new AuditEventRecord(
            Guid.NewGuid(), source, "Phase5.Red.v1", "Source", inEntityId,
            "Create", "in-scope", now.AddSeconds(-2), now.AddSeconds(-2), "phase5-scope",
            null, null, new Dictionary<string, object?> { ["name"] = "in", ["credential"] = "private" },
            new Dictionary<string, object?> { ["name"] = "in" }, site, area, null)
        { PayloadHash = new string('a', 64) });
        await append.AppendIfAbsentAsync(new AuditEventRecord(
            Guid.NewGuid(), Guid.NewGuid(), "Phase5.Red.v1", "Source", Guid.NewGuid().ToString("D"),
            "Create", "in-scope-second", now.AddSeconds(-3), now.AddSeconds(-3), "phase5-scope-2",
            null, null, new Dictionary<string, object?>(), new Dictionary<string, object?>(), site, area, null)
        { PayloadHash = new string('c', 64) });
        await append.AppendIfAbsentAsync(new AuditEventRecord(
            Guid.NewGuid(), Guid.NewGuid(), "Phase5.Red.v1", "Source", Guid.NewGuid().ToString("D"),
            "Create", "out-of-scope", now.AddSeconds(-1), now.AddSeconds(-1), "phase5-outside",
            null, null, new Dictionary<string, object?>(), new Dictionary<string, object?>(),
            outsideSite, null, null)
        { PayloadHash = new string('b', 64) });

        var service = scope.ServiceProvider.GetRequiredService<AuditQueryService>();
        var filtered = await service.QueryAsync(
            new AuditQueryRequest("Source", "Create", null, null, now.AddMinutes(-1), 1, 25)
            {
                ToUtc = now, EntityId = inEntityId, SiteId = site, AreaId = area
            }, new AuditCaller(false, true, new HashSet<string> { site }, new HashSet<string> { area }));
        TestCount++;
        Check(filtered.Items.Count == 1 && filtered.Items[0].ObjectId == inEntityId,
            "T066: PostgreSQL Audit must apply time, entity, object, action, Site, and Area filters.", failures);

        var result = await service.QueryAsync(
            new AuditQueryRequest(null, null, null, null, now.AddMinutes(-1), 1, 1),
            new AuditCaller(false, true, new HashSet<string> { site }, new HashSet<string>()));
        TestCount++;
        Check(result.Items.Count == 1 && result.Items[0].SiteId == site,
            "T066: PostgreSQL Audit scope must be applied before keyset paging.", failures);
        TestCount++;
        Check(result.Items.All(item => item.SiteId != outsideSite),
            "T066: out-of-scope Audit rows must not consume or appear in a page.", failures);
        TestCount++;
        Check(result.Items.SingleOrDefault()?.CorrelationId is null,
            "T066: non-Administrator PostgreSQL Audit results must omit correlation IDs.", failures);
        TestCount++;
        Check(result.Items.SingleOrDefault()?.Before.Keys.All(key =>
                !key.Contains("credential", StringComparison.OrdinalIgnoreCase)) == true,
            "T066: PostgreSQL Audit before values must be redacted server-side.", failures);
        var next = await service.QueryAsync(
            new AuditQueryRequest(null, null, null, null, now.AddMinutes(-1), 1, 1)
            { KeysetCursor = result.NextCursor },
            new AuditCaller(false, true, new HashSet<string> { site }, new HashSet<string>()));
        TestCount++;
        Check(next.Items.Count == 1 && next.Items[0].Summary == "in-scope-second" &&
              next.Items.All(item => item.SiteId != outsideSite),
            "T066: strict OccurredAtUtc/AuditEventId keyset continuation must not repeat or leak rows.", failures);

        var dashboard = scope.ServiceProvider.GetRequiredService<IOperationalDashboardQueryPort>();
        var noScope = await dashboard.GetAsync(new ServerPrincipal(
            Guid.NewGuid(), "phase5-no-scope", new HashSet<string>(), new HashSet<string>()));
        TestCount++;
        Check(noScope.State == OperationalDashboardState.NoAuthorizedScope && noScope.Sites.Count == 0,
            "T066: dashboard without scope must not expose global summaries.", failures);
        var administrator = await dashboard.GetAsync(new ServerPrincipal(
            Guid.NewGuid(), "phase5-admin", new HashSet<string>(), new HashSet<string>(), true));
        TestCount++;
        Check(administrator.State == OperationalDashboardState.Ready &&
              administrator.RecentAudit.Items.All(item => item is not AuditEventRecord),
            "T066: authorized dashboard must return a ready public summary, never raw Audit records.", failures);

        var organization = scope.ServiceProvider.GetRequiredService<IOrganizationQueryRepository>();
        var globalSites = await organization.GetSitesAsync(
            OrganizationQueryScope.Global(), new ScopeFilter(1, 200));
        AreaSnapshot? scopedArea = null;
        foreach (var candidateSite in globalSites.Items)
        {
            var areas = await organization.GetAreasForSiteAsync(
                candidateSite.Id, OrganizationQueryScope.Global(), new ScopeFilter(1, 200));
            scopedArea = areas.Items.FirstOrDefault();
            if (scopedArea is not null) break;
        }
        TestCount++;
        Check(scopedArea is not null,
            "T066: PostgreSQL fixture must expose a real Area for scoped Dashboard evidence.", failures);
        if (scopedArea is not null)
        {
            var areaDashboard = await dashboard.GetAsync(new ServerPrincipal(
                Guid.NewGuid(), "phase5-area-engineer", new HashSet<string>(),
                new HashSet<string> { scopedArea.Id.ToString("D") }));
            var siteIds = areaDashboard.Sites.Items
                .Select(item => JsonSerializer.SerializeToElement(item).GetProperty("SiteId").GetGuid())
                .ToArray();
            TestCount++;
            Check(areaDashboard.State == OperationalDashboardState.Ready &&
                  siteIds.All(siteId => siteId == scopedArea.SiteId) &&
                  areaDashboard.Points.Count == areaDashboard.Points.Items.Count &&
                  areaDashboard.Sources.Count == areaDashboard.Sources.Items.Count,
                "T066: Area-scoped Dashboard summaries must remain limited to the authorized Area ancestry.", failures);

            var areaSiteId = scopedArea.SiteId.ToString("D");
            var areaId = scopedArea.Id.ToString("D");
            var areaSource = Guid.NewGuid();
            var areaEntityId = Guid.NewGuid().ToString("D");
            var areaEventOne = new AuditEventRecord(
                Guid.NewGuid(), areaSource, "Phase5.Corrective.v1", "Area", areaEntityId,
                "Update", "site-scope-area-one", now.AddSeconds(-4), now.AddSeconds(-4),
                "phase5-corrective-area-1", null, null, new Dictionary<string, object?>(),
                new Dictionary<string, object?>(), areaSiteId, areaId, null)
            { PayloadHash = new string('d', 64) };
            var areaEventTwo = areaEventOne with
            {
                AuditEventId = Guid.NewGuid(), SourceEventId = Guid.NewGuid(),
                ObjectId = areaEntityId, Summary = "site-scope-area-two",
                OccurredAtUtc = now.AddSeconds(-5), RecordedAtUtc = now.AddSeconds(-5),
                CorrelationId = "phase5-corrective-area-2", PayloadHash = new string('e', 64)
            };
            await append.AppendIfAbsentAsync(areaEventOne);
            await append.AppendIfAbsentAsync(areaEventTwo);

            var auditPort = new IUMP.Api.Infrastructure.PostgresAuditQueryPort(
                service, organization);
            var siteScopedPrincipal = new ServerPrincipal(
                Guid.NewGuid(), "phase5-site-scoped", new HashSet<string> { areaSiteId },
                new HashSet<string>(), false, null, new HashSet<string> { "AUDIT_READ" });
            var areaFilters = new Dictionary<string, string?>
                { ["areaId"] = areaId, ["entityId"] = areaEntityId };
            var areaPage = await auditPort.QueryAsync(areaFilters, siteScopedPrincipal, null, 25);
            TestCount++;
            Check(areaPage.ErrorCode is null && areaPage.Items.Count >= 2 &&
                  areaPage.Items.Any(item => JsonSerializer.Serialize(item).Contains("site-scope-area-one")) &&
                  areaPage.Items.Any(item => JsonSerializer.Serialize(item).Contains("site-scope-area-two")),
                "T066 corrective: Site scope must authorize a real child Area filter through server ancestry.", failures);

            TestCount++;
            var foreignArea = await FindForeignAreaAsync(organization, scopedArea.SiteId);
            Check(foreignArea is not null,
                "T066 corrective: PostgreSQL fixture must expose a foreign Area for cross-site scope evidence.", failures);
            if (foreignArea is not null)
            {
                var foreignPage = await auditPort.QueryAsync(
                    new Dictionary<string, string?> { ["areaId"] = foreignArea.Id.ToString("D") },
                    new ServerPrincipal(Guid.NewGuid(), "phase5-site-scoped", new HashSet<string> { areaSiteId },
                        new HashSet<string>(), false, null, new HashSet<string> { "AUDIT_READ" }),
                    null, 25);
                Check(foreignPage.ErrorCode is null && foreignPage.Items.Count == 0,
                    "T066 corrective: Site scope must fail closed for an Area outside the authorized Site.", failures);
            }

            var invalidSize = await auditPort.QueryAsync(areaFilters, siteScopedPrincipal, null, 0);
            var malformedCursor = await auditPort.QueryAsync(areaFilters, siteScopedPrincipal, "malformed", 25);
            TestCount++;
            Check(invalidSize.ErrorCode == "VALIDATION" && malformedCursor.ErrorCode == "VALIDATION",
                "T066 corrective: PostgreSQL Audit port must reject invalid pageSize and malformed cursor.", failures);

            var firstAreaPage = await auditPort.QueryAsync(areaFilters, siteScopedPrincipal, null, 1);
            var lastAreaPage = await auditPort.QueryAsync(areaFilters, siteScopedPrincipal, firstAreaPage.NextCursor, 1);
            TestCount++;
            Check(firstAreaPage.Items.Count == 1 && firstAreaPage.NextCursor is not null &&
                  lastAreaPage.Items.Count == 1 && lastAreaPage.NextCursor is null,
                "T066 corrective: pageSize+1 PostgreSQL keyset paging must expose no cursor on the last page.", failures);
        }
        return failures;
    }

    private static void Check(bool condition, string failure, ICollection<string> failures)
    {
        AssertionCount++;
        if (!condition) failures.Add(failure);
    }

    private static async Task<AreaSnapshot?> FindForeignAreaAsync(
        IOrganizationQueryRepository organization, Guid siteId)
    {
        var sites = await organization.GetSitesAsync(OrganizationQueryScope.Global(), new ScopeFilter(1, 200));
        foreach (var site in sites.Items.Where(site => site.Id != siteId))
        {
            var areas = await organization.GetAreasForSiteAsync(
                site.Id, OrganizationQueryScope.Global(), new ScopeFilter(1, 200));
            if (areas.Items.FirstOrDefault() is { } area) return area;
        }
        return null;
    }
}
