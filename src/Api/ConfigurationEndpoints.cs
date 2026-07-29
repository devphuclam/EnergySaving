namespace IUMP.Api;

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

/// Public HTTP composition seam. Domain mutations are delegated to the configuration port;
/// this file owns only authentication, canonical request construction and HTTP replay metadata.
public static class ConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapConfigurationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1");
        group.MapGet("/sites", (IConfigurationQueryPort query, IServerPrincipalAccessor principal, CancellationToken ct) =>
            ListAsync("sites", query, principal, ct));
        group.MapGet("/areas", (IConfigurationQueryPort query, IServerPrincipalAccessor principal, CancellationToken ct) =>
            ListAsync("areas", query, principal, ct));
        group.MapGet("/assets", (IConfigurationQueryPort query, IServerPrincipalAccessor principal, CancellationToken ct) =>
            ListAsync("assets", query, principal, ct));
        group.MapGet("/points", (IConfigurationQueryPort query, IServerPrincipalAccessor principal, CancellationToken ct) =>
            ListAsync("points", query, principal, ct));
        group.MapPost("/sites", ExecuteCreateSiteAsync);
        group.MapPut("/sites/{siteId:guid}", ExecuteUpdateAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(string resource, IConfigurationQueryPort query,
        IServerPrincipalAccessor principalAccessor, CancellationToken ct)
    {
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        return Results.Ok(await query.ListAsync(resource, principal, ct));
    }

    private static async Task<IResult> ExecuteCreateSiteAsync(HttpRequest request,
        IConfigurationCommandPort commands, IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor, CancellationToken ct)
    {
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
            return Results.Problem("Idempotency-Key is required.", statusCode: StatusCodes.Status400BadRequest);
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        var name = request.Query["name"].FirstOrDefault() ?? string.Empty;
        var fields = new[] { CommandFingerprintField.String("name", name) };
        var identity = new CommandIdentity(principal.UserId, CommandOperationCodes.CreateSite, key!);
        var fingerprint = CommandFingerprintV1.Compute(new CommandFingerprintInput(
            identity.OperationCode, principal.UserId, "Site", null, "Site", null, null, fields));
        var response = await executor.ExecuteAsync(identity, fingerprint,
            token => commands.CreateSiteAsync(new ConfigurationCommandRequest(null, name, null, fields), principal, token), ct);
        return ToResult(response);
    }

    private static async Task<IResult> ExecuteUpdateAsync(Guid siteId, HttpRequest request,
        IConfigurationCommandPort commands, IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor, CancellationToken ct)
    {
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) || !request.Headers.ContainsKey("If-Match"))
            return Results.Problem("Idempotency-Key and If-Match are required.", statusCode: StatusCodes.Status400BadRequest);
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        var name = request.Query["name"].FirstOrDefault() ?? string.Empty;
        var expectedVersion = long.TryParse(request.Headers["If-Match"].FirstOrDefault()?.Trim('"'), out var version) ? version : 0;
        var fields = new[] { CommandFingerprintField.Uuid("siteId", siteId), CommandFingerprintField.String("name", name) };
        var identity = new CommandIdentity(principal.UserId, CommandOperationCodes.UpdateSite, key!);
        var fingerprint = CommandFingerprintV1.Compute(new CommandFingerprintInput(
            identity.OperationCode, principal.UserId, "Site", null, "Site", siteId, expectedVersion, fields));
        var response = await executor.ExecuteAsync(identity, fingerprint,
            token => commands.UpdateSiteAsync(new ConfigurationCommandRequest(siteId, name, expectedVersion, fields), principal, token), ct);
        return ToResult(response);
    }

    private static IResult ToResult(IdempotentCommandResponse response)
        => new IdempotentHttpResult(response);
}
