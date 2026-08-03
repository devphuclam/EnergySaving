using IUMP.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace IUMP.Api;

public static class OperationalDashboardEndpoints
{
    public const string Route = "/api/v1/operational-dashboard";

    public static IEndpointRouteBuilder MapOperationalDashboardEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Route, GetAsync);
        return endpoints;
    }

    public static async Task<IResult> GetAsync(
        IOperationalDashboardQueryPort dashboard,
        IServerPrincipalAccessor principalAccessor,
        CancellationToken ct)
    {
        if (principalAccessor.Current is not { } principal)
            return Results.Unauthorized();
        var snapshot = await dashboard.GetAsync(principal, ct);
        return snapshot.State switch
        {
            OperationalDashboardState.DependencyError => Results.Json(snapshot,
                statusCode: StatusCodes.Status503ServiceUnavailable),
            OperationalDashboardState.RuntimeError => Results.Json(snapshot,
                statusCode: StatusCodes.Status500InternalServerError),
            OperationalDashboardState.Forbidden => Results.Forbid(),
            _ => Results.Ok(snapshot)
        };
    }
}
