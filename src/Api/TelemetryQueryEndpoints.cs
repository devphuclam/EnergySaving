namespace IUMP.Api;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using IUMP.Api.Infrastructure;

public static class TelemetryQueryEndpoints
{
    public static bool UsesCommandRegistry => false;
    public static string[] Routes => new[]
    {
        "/api/v1/points/{pointId}/latest",
        "/api/v1/points/{pointId}/source-health",
        "/api/v1/sites/{siteId}/points/current",
        "/api/v1/telemetry/workspace/options",
        "/api/v1/telemetry/workspace/current"
    };
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

    public static async Task<IResult> OptionsAsync(
        HttpRequest request,
        ITelemetryWorkspaceQueryPort query,
        IServerPrincipalAccessor principalAccessor,
        CancellationToken ct)
    {
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        if (!TryNullableGuid(request.Query["siteId"], out var siteId) ||
            !TryNullableGuid(request.Query["areaId"], out var areaId) ||
            !TryNullableGuid(request.Query["assetId"], out var assetId))
            return Results.UnprocessableEntity(new { errorCode = "INVALID_SELECTION" });
        var page = int.TryParse(request.Query["page"], out var parsedPage) ? parsedPage : 1;
        var pageSize = int.TryParse(request.Query["pageSize"], out var parsedSize) ? parsedSize : 500;
        try
        {
            return Results.Ok(await query.GetOptionsAsync(
                principal, new TelemetryOptionsQuery(page, pageSize, siteId, areaId, assetId), ct));
        }
        catch (TelemetryHierarchyConflictException exception)
        {
            return Results.NotFound(new { errorCode = exception.Message });
        }
        catch (Exception exception) when (IsRuntimeDependencyFailure(exception))
        {
            return Results.Json(new { errorCode = "RUNTIME_DEPENDENCY_UNAVAILABLE" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static async Task<IResult> WorkspaceCurrentAsync(
        HttpRequest request,
        ITelemetryWorkspaceQueryPort query,
        IServerPrincipalAccessor principalAccessor,
        CancellationToken ct)
    {
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        if (!TryGuid(request.Query["siteId"], out var siteId) ||
            !TryNullableGuid(request.Query["areaId"], out var areaId) ||
            !TryNullableGuid(request.Query["assetId"], out var assetId) ||
            !TryGuid(request.Query["pointId"], out var pointId))
            return Results.UnprocessableEntity(new { errorCode = "SELECTION_REQUIRED" });
        if (areaId is null || assetId is null)
            return Results.UnprocessableEntity(new { errorCode = "COMPLETE_HIERARCHY_REQUIRED" });
        try
        {
            return Results.Ok(await query.GetCurrentAsync(
                new TelemetryHierarchySelection(siteId, areaId, assetId, pointId), principal, ct));
        }
        catch (RuntimeScopeDeniedException) { return Results.NotFound(); }
        catch (TelemetryHierarchyConflictException exception)
        {
            return Results.NotFound(new { errorCode = exception.Message });
        }
        catch (Exception exception) when (IsRuntimeDependencyFailure(exception))
        {
            return Results.Json(new { errorCode = "RUNTIME_DEPENDENCY_UNAVAILABLE" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool IsRuntimeDependencyFailure(Exception exception) =>
        exception is Npgsql.NpgsqlException or TimeoutException or InvalidOperationException;

    private static bool TryGuid(string? value, out Guid result) =>
        Guid.TryParse(value, out result);

    private static bool TryNullableGuid(string? value, out Guid? result)
    {
        if (string.IsNullOrWhiteSpace(value)) { result = null; return true; }
        if (Guid.TryParse(value, out var parsed)) { result = parsed; return true; }
        result = null;
        return false;
    }
    public static IEndpointRouteBuilder MapTelemetryQueryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/points/{pointId:guid}/latest", LatestAsync);
        endpoints.MapGet("/api/v1/points/{pointId:guid}/source-health", HealthAsync);
        endpoints.MapGet("/api/v1/sites/{siteId:guid}/points/current", CurrentAsync);
        endpoints.MapGet("/api/v1/telemetry/workspace/options", OptionsAsync);
        endpoints.MapGet("/api/v1/telemetry/workspace/current", WorkspaceCurrentAsync);
        return endpoints;
    }
}
