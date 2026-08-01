using IUMP.Api.Infrastructure;
using IUMP.Api;
using IUMP.Tests.Unit.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace IUMP.Tests.Unit.Api;

/// T057: red-first coverage for explicit hierarchy selection and refresh state.
public static class LatestSelectionTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }
    public static int FailureCount { get; private set; }

    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        TestCount = 0;
        AssertionCount = 0;
        var siteId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var pointA = Guid.NewGuid();
        var pointB = Guid.NewGuid();
        var selection = new TelemetryHierarchySelection(siteId, areaId, assetId, pointA);
        Check(TelemetrySelectionRules.Validate(selection, pointA) is null,
            "a complete selected hierarchy must validate", failures);
        Check(TelemetrySelectionRules.Validate(selection, pointB) == "POINT_HIERARCHY_MISMATCH",
            "a point outside the selected hierarchy must fail safely", failures);
        Check(TelemetrySelectionRules.Validate(selection with { AreaId = null }) == "AREA_SELECTION_REQUIRED" &&
              TelemetrySelectionRules.Validate(selection with { AssetId = null }) == "ASSET_SELECTION_REQUIRED",
            "the provider-neutral contract must require the complete hierarchy", failures);

        var options = new TelemetryWorkspaceOptions(
            new[] { new TelemetrySiteOption(siteId, "SITE-A", "Site A") },
            new[] { new TelemetryAreaOption(areaId, siteId, "AREA-A", "Area A") },
            new[] { new TelemetryAssetOption(assetId, siteId, areaId, "ASSET-A", "Asset A") },
            new[]
            {
                new TelemetryPointOption(pointA, siteId, areaId, assetId, "POINT-A", "Point A", "POWER", "kW"),
                new TelemetryPointOption(pointB, siteId, areaId, assetId, "POINT-B", "Point B", "POWER", "kW")
            });
        Check(options.SelectedPointId is null,
            "catalog ordering must never implicitly select a Point", failures);
        Check(options.Points.Count == 2 && options.Points[0].PointId != options.Points[1].PointId,
            "two visible Points require explicit selection", failures);

        var noData = TelemetryWorkspaceCurrent.NoData(selection, DateTime.UtcNow);
        Check(!noData.HasData && noData.Value is null,
            "No Data must be non-numeric and distinct from zero", failures);
        var zero = noData with { DataState = TelemetryDataState.Data, HasData = true, Value = 0d };
        Check(zero.HasData && zero.Value == 0d,
            "an accepted numeric zero must remain numeric zero", failures);
        Check(TelemetryRefreshPolicy.DefaultInterval == TimeSpan.FromSeconds(10) &&
              TelemetryRefreshPolicy.CanDisable && TelemetryRefreshPolicy.HasManualRefresh,
            "refresh defaults to ten seconds and supports disable/manual refresh", failures);
        Check(!TelemetryQueryEndpoints.Routes.Any(route => route.Contains("simulator", StringComparison.OrdinalIgnoreCase)),
            "Latest/Health reads must not start or control a Simulator Run", failures);
        var endpointPort = new FakeTelemetryWorkspacePort(noData);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString($"?siteId={siteId:D}&pointId={pointA:D}");
        var optionsResult = await TelemetryQueryEndpoints.OptionsAsync(
            context.Request, endpointPort, new FakeServerPrincipalAccessor(
                new ServerPrincipal(Guid.NewGuid(), "admin", new HashSet<string>(), new HashSet<string>(), true)), CancellationToken.None);
        Check(optionsResult is Ok<TelemetryWorkspaceOptions>,
            "authorized selector options must be exposed without an idempotency key", failures);
        context.Request.QueryString = new QueryString($"?siteId={siteId:D}&areaId={areaId:D}&assetId={assetId:D}&pointId={pointB:D}");
        endpointPort.ThrowConflict = true;
        var conflict = await TelemetryQueryEndpoints.WorkspaceCurrentAsync(
            context.Request, endpointPort, new FakeServerPrincipalAccessor(
                new ServerPrincipal(Guid.NewGuid(), "admin", new HashSet<string>(), new HashSet<string>(), true)), CancellationToken.None);
        Check(conflict.GetType().Name.StartsWith("NotFound", StringComparison.Ordinal),
            "hierarchy mismatch must fail safely as not visible/not found", failures);
        TestCount = 1;
        FailureCount = failures.Count;
        return failures;
    }

    private sealed class FakeTelemetryWorkspacePort(TelemetryWorkspaceCurrent current) : ITelemetryWorkspaceQueryPort
    {
        public bool ThrowConflict { get; set; }
        public Task<TelemetryWorkspaceOptions> GetOptionsAsync(ServerPrincipal principal, TelemetryOptionsQuery query, CancellationToken ct = default) =>
            Task.FromResult(new TelemetryWorkspaceOptions([], [], [], []));
        public Task<TelemetryWorkspaceCurrent> GetCurrentAsync(TelemetryHierarchySelection selection, ServerPrincipal principal, CancellationToken ct = default) =>
            ThrowConflict ? throw new TelemetryHierarchyConflictException("POINT_HIERARCHY_MISMATCH") : Task.FromResult(current);
    }

    private static void Check(bool condition, string message, ICollection<string> failures)
    {
        AssertionCount++;
        if (!condition) failures.Add(message);
    }
}
