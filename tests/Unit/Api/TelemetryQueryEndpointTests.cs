using IUMP.Api;
using IUMP.Api.Infrastructure;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Api;

public static class TelemetryQueryEndpointTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }
    public static int FailureCount { get; private set; }

    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        var noData = TelemetryQueryEndpoints.NoData(Guid.NewGuid());
        var assertions = 0;
        assertions++; if (!noData.IsNoData || noData.NumericValue is not null) failures.Add("No Data must not be represented as zero");
        assertions++; if (TelemetryQueryEndpoints.UsesCommandRegistry) failures.Add("queries must not use command idempotency");
        // Latest/Health handlers call the typed query port and preserve No Data as null, never zero.
        var pointId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var query = new FakeTelemetryPorts();
        var accessor = new FakeServerPrincipalAccessor(new ServerPrincipal(Guid.NewGuid(), "engineer", new HashSet<string> { siteId.ToString("D") }, new HashSet<string>()));
        var latest = await TelemetryQueryEndpoints.LatestAsync(pointId, query, accessor, CancellationToken.None);
        var health = await TelemetryQueryEndpoints.HealthAsync(pointId, query, accessor, CancellationToken.None);
        var current = await TelemetryQueryEndpoints.CurrentAsync(siteId, query, accessor, CancellationToken.None);
        assertions++; if (latest is not Microsoft.AspNetCore.Http.HttpResults.Ok<LatestQueryResult> latestOk || !latestOk.Value!.IsNoData || latestOk.Value.NumericValue is not null) failures.Add("Latest handler must execute scoped port and preserve textual No Data/null numeric value");
        assertions++; if (health is null || current is null || query.LastPrincipal is null || query.LastPointId != pointId || query.LastSiteId != siteId) failures.Add("Health and Site current handlers must forward server scope and real IDs");
        TestCount = 5; AssertionCount = assertions;
        FailureCount = failures.Count;
        return failures;
    }
}
