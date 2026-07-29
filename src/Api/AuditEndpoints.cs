namespace IUMP.Api;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

public static class AuditEndpointPolicy
{
    public const string RequiredCapability = "AUDIT_READ";
    public const string Route = "/api/v1/audit-events";

    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Route, (HttpRequest request) =>
            request.Headers.ContainsKey("X-Audit-Read")
                ? Results.Ok(Array.Empty<object>())
                : Results.Problem("Audit access is not authorized.", statusCode: StatusCodes.Status403Forbidden));
        return endpoints;
    }
}
