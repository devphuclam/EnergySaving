using IUMP.Api.Infrastructure;
using IUMP.Infrastructure.Postgres;
using IUMP.Composition.Postgres;
using Microsoft.Extensions.DependencyInjection;

namespace IUMP.Tests.Integration.OperationalWorkspace;

/// T058: PostgreSQL-backed selector/latest/health contract. The test is read-only;
/// it uses whatever approved fixtures are already present in the development database.
public static class LatestHealthTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }

    public static async Task<IReadOnlyList<string>> RunAsync(IServiceProvider root)
    {
        var failures = new List<string>();
        TestCount = 0;
        AssertionCount = 0;
        var configuration = PostgresRuntimeConfiguration.CreateRuntime();
        Check(configuration.Host == PostgresRuntimeConfiguration.ApprovedLocalHost &&
              configuration.Port == PostgresRuntimeConfiguration.ApprovedLocalPort &&
              configuration.Database == PostgresRuntimeConfiguration.ApprovedLocalDatabase,
            "T058 must target only 127.0.0.1:5433/iump_dev", failures);
        using var scope = root.CreateScope();
        var port = scope.ServiceProvider.GetRequiredService<ITelemetryWorkspaceQueryPort>();
        var principal = new ServerPrincipal(
            Guid.NewGuid(), "phase4-read-only", new HashSet<string>(), new HashSet<string>(), true);
        var options = await port.GetOptionsAsync(principal, new TelemetryOptionsQuery(1, 500), CancellationToken.None);
        Check(options.Points.All(point => options.Sites.Any(site => site.SiteId == point.SiteId)),
            "selector scope must be applied before hierarchy composition", failures);
        Check(options.Points.All(point => options.Areas.Any(area => area.AreaId == point.AreaId && area.SiteId == point.SiteId)),
            "Point hierarchy must retain Site/Area identity", failures);
        Check(options.ScopedCount >= options.Points.Count,
            "scoped count must be computed after authorization and before paging", failures);
        foreach (var point in options.Points)
        {
            var current = await port.GetCurrentAsync(
                new TelemetryHierarchySelection(point.SiteId, point.AreaId, point.AssetId, point.PointId),
                principal, CancellationToken.None);
            Check(current.Selection.PointId == point.PointId,
                "selected Latest must belong to the explicitly selected Point", failures);
            Check(current.HasData ? current.Value is not null : current.Value is null,
                "Latest data state must agree with numeric value", failures);
            Check(current.Point.PointId == point.PointId,
                "Health and counters must remain in the selected Point context", failures);
        }
        TestCount = 1;
        return failures;
    }

    private static void Check(bool condition, string message, ICollection<string> failures)
    {
        AssertionCount++;
        if (!condition) failures.Add(message);
    }
}
