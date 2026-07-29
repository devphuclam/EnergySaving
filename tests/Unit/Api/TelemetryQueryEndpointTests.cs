using IUMP.Api;
using IUMP.Api.Infrastructure;
using IUMP.Tests.Unit.Fakes;
using Microsoft.AspNetCore.Http.HttpResults;

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
        // Latest handler with scope forwarding
        var pointId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var query = new FakeTelemetryPorts();
        var principal = new ServerPrincipal(Guid.NewGuid(), "engineer", new HashSet<string> { siteId.ToString("D") }, new HashSet<string>());
        var accessor = new FakeServerPrincipalAccessor(principal);
        var latest = await TelemetryQueryEndpoints.LatestAsync(pointId, query, accessor, CancellationToken.None);
        var health = await TelemetryQueryEndpoints.HealthAsync(pointId, query, accessor, CancellationToken.None);
        var current = await TelemetryQueryEndpoints.CurrentAsync(siteId, query, accessor, CancellationToken.None);
        var latestOk = latest as Ok<LatestQueryResult>;
        assertions++; if (latestOk is null || !latestOk.Value!.IsNoData || latestOk.Value.NumericValue is not null)
            failures.Add("Latest handler must execute scoped port and preserve textual No Data/null numeric value");
        assertions++; if (health is null || current is null || query.LastPrincipal is null || query.LastPointId != pointId || query.LastSiteId != siteId)
            failures.Add("Health and Site current handlers must forward server scope, point ID and site ID");
        assertions++; if (query.LastPrincipal!.UserId != principal.UserId) failures.Add("Latest handler must forward the exact server principal");
        // Unauthorized
        var nullAccessor = new FakeServerPrincipalAccessor(null);
        var unauthLatest = await TelemetryQueryEndpoints.LatestAsync(pointId, query, nullAccessor, CancellationToken.None);
        assertions++; if (unauthLatest is not UnauthorizedHttpResult) failures.Add("Null principal must return Unauthorized on Latest");
        var unauthHealth = await TelemetryQueryEndpoints.HealthAsync(pointId, query, nullAccessor, CancellationToken.None);
        assertions++; if (unauthHealth is not UnauthorizedHttpResult) failures.Add("Null principal must return Unauthorized on Health");
        var unauthCurrent = await TelemetryQueryEndpoints.CurrentAsync(siteId, query, nullAccessor, CancellationToken.None);
        assertions++; if (unauthCurrent is not UnauthorizedHttpResult) failures.Add("Null principal must return Unauthorized on Current");
        // No Data on Latest result
        assertions++; if (latestOk?.Value?.ReasonCode != "NO_DATA") failures.Add("No Data Latest must carry NO_DATA reason code");
        TestCount = assertions; AssertionCount = assertions;
        FailureCount = failures.Count;
        return failures;
    }
}
