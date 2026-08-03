using IUMP.Api.Infrastructure;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Audit.Application;
using IUMP.Modules.Audit.Contracts;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Telemetry.Contracts;
using Npgsql;

namespace IUMP.Composition.Postgres;

/// <summary>
/// Read-only composition root for the operational landing view. It calls public owner contracts;
/// it does not write, start a Run, or expose module repositories to the API/Web layers.
/// </summary>
public sealed class PostgresOperationalDashboardPorts(
    IOperationalWorkspaceQueryPort workspace,
    IOrganizationQueryRepository organization,
    CatalogRuntimeGateway catalog,
    IAcquisitionRunRepository runs,
    IPointLatestProjectionRepository latest,
    ISourceHealthRepository health,
    AuditQueryService audit) : IOperationalDashboardQueryPort
{
    public async Task<OperationalDashboardSnapshot> GetAsync(
        ServerPrincipal principal, CancellationToken ct = default)
    {
        if (!principal.IsAdministrator && principal.SiteIds.Count == 0 && principal.AreaIds.Count == 0)
            return Empty(OperationalDashboardState.NoAuthorizedScope, WorkspaceRoleMode.ReadOnly);

        try
        {
            var status = await workspace.GetStatusAsync(principal, ct: ct);
            var scope = principal.IsAdministrator
                ? OrganizationQueryScope.Global()
                : new OrganizationQueryScope(false, Parse(principal.SiteIds), Parse(principal.AreaIds));
            var points = new List<PointSnapshot>();
            foreach (var site in status.AuthorizedSites)
                points.AddRange(await All(filter => organization.GetPointsForSiteAsync(
                    site.SiteId, scope, filter, ct)));

            var siteIds = status.AuthorizedSites.Select(site => site.SiteId).ToHashSet();
            var scopedPointIds = points.Select(point => point.Id).ToHashSet();
            var directSiteIds = principal.SiteIds
                .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
                .Where(value => value != Guid.Empty).ToHashSet();
            var mappings = principal.IsAdministrator
                ? []
                : await catalog.GetMappingSnapshotsAsync(ct);
            var sources = principal.IsAdministrator
                ? (await catalog.GetDataSourceSnapshotsAsync(ct)).ToArray()
                : (await catalog.GetDataSourceSnapshotsForSitesAsync(siteIds, ct))
                    .Where(source => source.SiteId is { } sourceSiteId &&
                        (directSiteIds.Contains(sourceSiteId) || mappings.Any(mapping =>
                            mapping.DataSourceId == source.Id && scopedPointIds.Contains(mapping.PointId))))
                    .ToArray();
            var sourceIds = sources.Select(source => source.Id).ToHashSet();
            var running = principal.IsAdministrator
                ? (await runs.ListRunningAsync(ct)).ToArray()
                : (await runs.ListRunningForSourcesAsync(sourceIds, ct)).ToArray();

            var latestItems = new List<object>();
            var healthItems = new List<object>();
            foreach (var point in points)
            {
                var current = await latest.GetCurrentAsync(point.Id, ct);
                if (current is not null)
                    latestItems.Add(new DashboardLatestItem(point.Id, current.NumericValue,
                        current.UnitCode, current.QualityCode.ToString(), current.ReceivedAtUtc));
                var sourceHealth = await health.GetSourceHealthAsync(point.Id, ct);
                if (sourceHealth is not null)
                    healthItems.Add(new DashboardHealthItem(point.Id, sourceHealth.Status,
                        sourceHealth.LastReceivedAtUtc));
            }

            var caller = new AuditCaller(
                principal.IsAdministrator, principal.HasCapability("AUDIT_READ"),
                principal.SiteIds, principal.AreaIds, true, principal.IsAdministrator);
            var auditResult = await audit.QueryAsync(
                new AuditQueryRequest(null, null, null, null, DateTime.UtcNow.AddDays(-7), 1, 5)
                {
                    ToUtc = DateTime.UtcNow
                }, caller, ct);

            return new OperationalDashboardSnapshot(
                OperationalDashboardState.Ready,
                status.RoleMode,
                new(status.AuthorizedSites.Count,
                    status.AuthorizedSites.Select(site => (object)new DashboardSiteItem(
                        site.SiteId, site.Code, site.Name, site.Status)).ToArray()),
                new(sources.Length, sources.Select(source => (object)new DashboardSourceItem(
                    source.Id, source.Code, source.Name, source.Status)).ToArray()),
                new(points.Count, points.Select(point => (object)new DashboardPointItem(
                    point.Id, point.Code, point.Description)).ToArray()),
                new(running.Length, running.Select(run => (object)new DashboardRunItem(
                    run.RunId, run.SourceId, run.Status.ToString(), run.GeneratedCount,
                    run.AcceptedCount, run.RejectedCount)).ToArray()),
                new(latestItems.Count, latestItems),
                new(healthItems.Count, healthItems),
                new(status.IncompleteChainCount, status.NextStep?.ToString()),
                new(auditResult.Items.Select(item => (object)new DashboardAuditItem(
                    item.ActorUsername ?? item.ActorId ?? "",
                    item.OccurredAtUtc,
                    item.ObjectType,
                    item.ObjectId,
                    item.Action,
                    item.Summary,
                    item.SiteId,
                    item.AreaId,
                    item.CorrelationId)).ToArray(), auditResult.NextCursor),
                new("Available", running.Length > 0),
                new("Available", null, null));
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            return Empty(OperationalDashboardState.DependencyError, principal.IsAdministrator
                ? WorkspaceRoleMode.Administrator : WorkspaceRoleMode.Engineer,
                "DEPENDENCY_UNAVAILABLE");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Empty(OperationalDashboardState.RuntimeError, principal.IsAdministrator
                ? WorkspaceRoleMode.Administrator : WorkspaceRoleMode.Engineer,
                "RUNTIME_FAILURE");
        }
    }

    private static OperationalDashboardSnapshot Empty(
        OperationalDashboardState state, WorkspaceRoleMode mode, string? errorCode = null) =>
        new(state, mode, new(0, []), new(0, []), new(0, []), new(0, []), new(0, []),
            new(0, []), new(0, null), new([], null), new("Unavailable", false),
            new(state == OperationalDashboardState.DependencyError ? "Unavailable" : "Available",
                errorCode, null));

    private static IReadOnlyCollection<Guid> Parse(IEnumerable<string> values) => values
        .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
        .Where(value => value != Guid.Empty).ToArray();

    private static async Task<IReadOnlyList<T>> All<T>(
        Func<ScopeFilter, Task<PagedResult<T>>> query)
    {
        var values = new List<T>();
        var page = 1;
        while (true)
        {
            var result = await query(new ScopeFilter(page, 200));
            values.AddRange(result.Items);
            if (values.Count >= result.TotalCount || result.Items.Count == 0)
                return values;
            page++;
        }
    }

    private sealed record DashboardSiteItem(Guid SiteId, string Code, string Name, string Status);
    private sealed record DashboardSourceItem(Guid SourceId, string Code, string Name, string Status);
    private sealed record DashboardPointItem(Guid PointId, string Code, string? Description);
    private sealed record DashboardRunItem(Guid RunId, Guid SourceId, string Status,
        long Generated, long Accepted, long Rejected);
    private sealed record DashboardLatestItem(Guid PointId, double Value, string Unit,
        string Quality, DateTime ReceivedAtUtc);
    private sealed record DashboardHealthItem(Guid PointId, string Status, DateTime? LastReceivedAtUtc);
    private sealed record DashboardAuditItem(string Actor, DateTime OccurredAtUtc,
        string ObjectType, string EntityId, string Action, string Summary,
        string? SiteId, string? AreaId, string? CorrelationId);
}
