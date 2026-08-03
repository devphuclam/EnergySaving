namespace IUMP.Api;

using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using IUMP.Api.Infrastructure;
using IUMP.Modules.Audit.Contracts;

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
        var rawPageSize = request.Query["pageSize"].FirstOrDefault();
        var pageSize = 50;
        if (rawPageSize is not null &&
            (!int.TryParse(rawPageSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out pageSize) ||
             pageSize is < 1 or > 100))
            return Results.Problem("Audit page size is invalid.", statusCode: StatusCodes.Status422UnprocessableEntity);
        var cursor = request.Query["cursor"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(cursor) && !AuditKeysetCursor.TryDecode(cursor, out _))
            return Results.Problem("Audit cursor is invalid.", statusCode: StatusCodes.Status422UnprocessableEntity);
        var page = await query.QueryAsync(filters, principal, cursor, pageSize, ct);
        return page.ErrorCode switch
        {
            "FORBIDDEN" => Results.Problem("Audit access is not authorized.", statusCode: StatusCodes.Status403Forbidden),
            "VALIDATION" => Results.Problem("Audit filters are invalid.", statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Results.Ok(page)
        };
    }
}
