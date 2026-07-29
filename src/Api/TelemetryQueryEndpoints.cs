namespace IUMP.Api;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

public sealed record LatestQueryResult(Guid PointId, double? NumericValue, string? UnitCode, string Status, bool IsNoData,
    string? ReasonCode = null);

public static class TelemetryQueryEndpoints
{
    public static bool UsesCommandRegistry => false;
    public static string[] Routes => new[] { "/api/v1/points/{pointId}/latest", "/api/v1/points/{pointId}/source-health", "/api/v1/sites/{siteId}/points/current" };
    public static LatestQueryResult NoData(Guid pointId) => new(pointId, null, null, "No Data", true, "NO_DATA");
    public static IEndpointRouteBuilder MapTelemetryQueryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/points/{pointId:guid}/latest", (Guid pointId) => Results.Ok(NoData(pointId)));
        endpoints.MapGet("/api/v1/points/{pointId:guid}/source-health", (Guid pointId) => Results.Ok(new { pointId, status = "NoData" }));
        endpoints.MapGet("/api/v1/sites/{siteId:guid}/points/current", (Guid siteId) => Results.Ok(Array.Empty<LatestQueryResult>()));
        return endpoints;
    }
}
