namespace IUMP.Api;

using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using IUMP.Api.Infrastructure;
using IUMP.Modules.Integration.Contracts;

public static class ConfigurationEndpointPolicy
{
    public static bool RequiresIdempotency(string method) => method is "POST" or "PUT" or "PATCH" or "DELETE";
    public static bool RequiresIfMatch(string method) => method is "PUT" or "PATCH" or "DELETE";
    public static bool IsQuery(string method) => method is "GET";
}

public static class ConfigurationEndpoints
{
    public static string[] Routes => new[]
    {
        "/api/v1/sites", "/api/v1/areas", "/api/v1/assets", "/api/v1/points",
        "/api/v1/metrics", "/api/v1/units", "/api/v1/data-sources", "/api/v1/source-point-mappings"
    };

    public static IEndpointRouteBuilder MapConfigurationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1");
        group.MapGet("/sites", () => Results.Ok(Array.Empty<object>()));
        group.MapGet("/areas", () => Results.Ok(Array.Empty<object>()));
        group.MapGet("/assets", () => Results.Ok(Array.Empty<object>()));
        group.MapGet("/points", () => Results.Ok(Array.Empty<object>()));
        group.MapPost("/sites", ExecuteCreateSiteAsync);
        group.MapPut("/sites/{siteId:guid}", ExecuteUpdateAsync);
        return endpoints;
    }

    private static async Task<IResult> ExecuteCreateSiteAsync(HttpRequest request, IdempotentCommandExecutor executor,
        CancellationToken ct)
    {
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
            return Results.Problem("Idempotency-Key is required.", statusCode: StatusCodes.Status400BadRequest);
        var caller = Caller(request);
        if (caller == Guid.Empty) return Results.Unauthorized();
        var identity = new CommandIdentity(caller, CommandOperationCodes.CreateSite, key!);
        var response = await executor.ExecuteAsync(identity, SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key!)),
            _ => Task.FromResult(CommandExecutionResult.Ok(201, "{\"status\":\"Draft\"}", null)), ct);
        return Results.Content(response.Body, "application/json", statusCode: response.StatusCode);
    }

    private static async Task<IResult> ExecuteUpdateAsync(HttpRequest request, IdempotentCommandExecutor executor,
        CancellationToken ct)
    {
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) || !request.Headers.ContainsKey("If-Match"))
            return Results.Problem("Idempotency-Key and If-Match are required.", statusCode: StatusCodes.Status400BadRequest);
        var caller = Caller(request);
        if (caller == Guid.Empty) return Results.Unauthorized();
        var identity = new CommandIdentity(caller, CommandOperationCodes.UpdateSite, key!);
        var response = await executor.ExecuteAsync(identity, SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key! + request.Headers["If-Match"])),
            _ => Task.FromResult(CommandExecutionResult.Ok(200, "{\"status\":\"Draft\"}", null)), ct);
        return Results.Content(response.Body, "application/json", statusCode: response.StatusCode);
    }

    private static Guid Caller(HttpRequest request) =>
        Guid.TryParse(request.Headers["X-Caller-Id"].FirstOrDefault(), out var caller) ? caller : Guid.Empty;
}
