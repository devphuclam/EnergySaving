namespace IUMP.Api;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using IUMP.Api.Infrastructure;

public static class AuditEndpointPolicy
{
    public const string RequiredCapability = "AUDIT_READ";
    public const string Route = "/api/v1/audit-events";
}

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(AuditEndpointPolicy.Route, QueryAsync);
        return endpoints;
    }

    public static async Task<IResult> QueryAsync(HttpRequest request,
        IAuditQueryPort query, IServerPrincipalAccessor principalAccessor, CancellationToken ct)
    {
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        var filters = request.Query.ToDictionary(pair => pair.Key, pair => pair.Value.FirstOrDefault(), StringComparer.Ordinal);
        var pageSize = int.TryParse(request.Query["pageSize"], out var parsed) ? Math.Clamp(parsed, 1, 100) : 50;
        var page = await query.QueryAsync(filters, principal, request.Query["cursor"].FirstOrDefault(), pageSize, ct);
        return page.ErrorCode switch
        {
            "FORBIDDEN" => Results.Problem("Audit access is not authorized.", statusCode: StatusCodes.Status403Forbidden),
            _ => Results.Ok(page)
        };
    }
}
