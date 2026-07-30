namespace IUMP.Api;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using IUMP.Api.Infrastructure;

public static class TelemetryQueryEndpoints
{
    public static bool UsesCommandRegistry => false;
    public static string[] Routes => new[] { "/api/v1/points/{pointId}/latest", "/api/v1/points/{pointId}/source-health", "/api/v1/sites/{siteId}/points/current" };
    public static LatestQueryResult NoData(Guid pointId) => new(pointId, null, null, "No Data", true, "NO_DATA");
    public static async Task<IResult> LatestAsync(Guid pointId, ITelemetryQueryPort query,
        IServerPrincipalAccessor principalAccessor, CancellationToken ct)
    {
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        try { return Results.Ok(await query.GetLatestAsync(pointId, principal, ct)); }
        catch (RuntimeScopeDeniedException) { return Results.NotFound(); }
    }
    public static async Task<IResult> HealthAsync(Guid pointId, ITelemetryQueryPort query,
        IServerPrincipalAccessor principalAccessor, CancellationToken ct)
    {
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        try { return Results.Ok(await query.GetSourceHealthAsync(pointId, principal, ct)); }
        catch (RuntimeScopeDeniedException) { return Results.NotFound(); }
    }
    public static async Task<IResult> CurrentAsync(Guid siteId, ITelemetryQueryPort query,
        IServerPrincipalAccessor principalAccessor, CancellationToken ct)
    {
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        try { return Results.Ok(await query.GetCurrentAsync(siteId, principal, ct)); }
        catch (RuntimeScopeDeniedException) { return Results.NotFound(); }
    }
    public static IEndpointRouteBuilder MapTelemetryQueryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/points/{pointId:guid}/latest", LatestAsync);
        endpoints.MapGet("/api/v1/points/{pointId:guid}/source-health", HealthAsync);
        endpoints.MapGet("/api/v1/sites/{siteId:guid}/points/current", CurrentAsync);
        return endpoints;
    }
}
