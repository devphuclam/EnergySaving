namespace IUMP.Api;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using IUMP.Api.Infrastructure;
using IUMP.BuildingBlocks.Persistence;
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
        group.MapGet("/metrics", (IConfigurationQueryPort query, IServerPrincipalAccessor principal, CancellationToken ct) =>
            ListAsync("metrics", query, principal, ct));
        group.MapGet("/units", (IConfigurationQueryPort query, IServerPrincipalAccessor principal, CancellationToken ct) =>
            ListAsync("units", query, principal, ct));
        group.MapGet("/data-sources", (IConfigurationQueryPort query, IServerPrincipalAccessor principal, CancellationToken ct) =>
            ListAsync("data-sources", query, principal, ct));
        group.MapGet("/source-point-mappings", (IConfigurationQueryPort query, IServerPrincipalAccessor principal, CancellationToken ct) =>
            ListAsync("source-point-mappings", query, principal, ct));
        group.MapGet("/sites/{siteId:guid}/areas", (Guid siteId, IConfigurationQueryPort query, IServerPrincipalAccessor principal, CancellationToken ct) =>
            ListAsync($"areas:{siteId:D}", query, principal, ct));
        group.MapGet("/areas/{areaId:guid}/assets", (Guid areaId, IConfigurationQueryPort query, IServerPrincipalAccessor principal, CancellationToken ct) =>
            ListAsync($"assets:{areaId:D}", query, principal, ct));
        group.MapGet("/assets/{assetId:guid}/points", (Guid assetId, IConfigurationQueryPort query, IServerPrincipalAccessor principal, CancellationToken ct) =>
            ListAsync($"points:{assetId:D}", query, principal, ct));
        group.MapGet("/metrics/{metricId:guid}/compatible-units", (Guid metricId, IConfigurationQueryPort query, IServerPrincipalAccessor principal, CancellationToken ct) =>
            ListAsync($"compatible-units:{metricId:D}", query, principal, ct));
        group.MapGet("/data-sources/{sourceId:guid}", (Guid sourceId, IConfigurationQueryPort query, IServerPrincipalAccessor principal, CancellationToken ct) =>
            ListAsync($"data-source:{sourceId:D}", query, principal, ct));
        group.MapGet("/source-point-mappings/{mappingId:guid}", (Guid mappingId, IConfigurationQueryPort query, IServerPrincipalAccessor principal, CancellationToken ct) =>
            ListAsync($"source-point-mapping:{mappingId:D}", query, principal, ct));
        group.MapGet("/simulator-configurations/{configurationId:guid}", (Guid configurationId, IConfigurationQueryPort query, IServerPrincipalAccessor principal, CancellationToken ct) =>
            ListAsync($"simulator-configuration:{configurationId:D}", query, principal, ct));
        group.MapPost("/sites", CreateSiteAsync);
        group.MapPut("/sites/{siteId:guid}", UpdateSiteAsync);
        MapMutation(group, "/areas", "Organization.CreateArea.v1");
        MapMutation(group, "/sites/{siteId:guid}/areas", "Organization.CreateArea.v1");
        MapMutation(group, "/assets", "Organization.CreateAsset.v1");
        MapMutation(group, "/areas/{areaId:guid}/assets", "Organization.CreateAsset.v1");
        MapMutation(group, "/points", "Organization.CreatePoint.v1");
        MapMutation(group, "/assets/{assetId:guid}/points", "Organization.CreatePoint.v1");
        MapMutation(group, "/metrics", "Catalog.CreateMetric.v1");
        MapMutation(group, "/metrics/{metricId:guid}/compatible-units", "Catalog.SetMetricCompatibleUnits.v1");
        MapMutation(group, "/units", "Catalog.CreateUnit.v1");
        MapMutation(group, "/data-sources", "Acquisition.CreateSource.v1");
        MapMutation(group, "/source-point-mappings", "Acquisition.CreateMapping.v1");
        MapMutation(group, "/simulator-configurations", "Acquisition.CreateSimulatorConfiguration.v1");
        MapMutation(group, "/simulator-configurations/validate", "Acquisition.ValidateSimulatorConfiguration.v1");
        MapMutation(group, "/points/{pointId:guid}/activate", "Organization.ActivatePoint.v1");
        MapMutation(group, "/points/{pointId:guid}/deactivate", "Organization.DeactivatePoint.v1");
        MapMutation(group, "/sites/{siteId:guid}/activate", "Organization.ActivateSite.v1");
        MapMutation(group, "/sites/{siteId:guid}/deactivate", "Organization.DeactivateSite.v1");
        MapMutation(group, "/areas/{areaId:guid}/activate", "Organization.ActivateArea.v1");
        MapMutation(group, "/areas/{areaId:guid}/deactivate", "Organization.DeactivateArea.v1");
        MapMutation(group, "/assets/{assetId:guid}/activate", "Organization.ActivateAsset.v1");
        MapMutation(group, "/assets/{assetId:guid}/deactivate", "Organization.DeactivateAsset.v1");
        MapMutation(group, "/sites/{siteId:guid}/supersede", "Organization.SupersedeSite.v1");
        MapMutation(group, "/areas/{areaId:guid}/supersede", "Organization.SupersedeArea.v1");
        MapMutation(group, "/assets/{assetId:guid}/supersede", "Organization.SupersedeAsset.v1");
        MapMutation(group, "/points/{pointId:guid}/supersede", "Organization.SupersedePoint.v1");
        MapMutation(group, "/source-point-mappings/{mappingId:guid}/activate", "Acquisition.ActivateMapping.v1");
        MapMutation(group, "/source-point-mappings/{mappingId:guid}/inactivate", "Acquisition.InactivateMapping.v1");
        MapMutation(group, "/source-point-mappings/{mappingId:guid}/supersede", "Acquisition.SupersedeMapping.v1");
        MapMutation(group, "/data-sources/{sourceId:guid}/suspend", "Acquisition.SuspendSource.v1");
        MapMutation(group, "/data-sources/{sourceId:guid}/decommission", "Acquisition.DecommissionSource.v1");
        MapCommandMethods(group, "/areas/{areaId:guid}", "Organization.UpdateArea.v1", "PUT", "DELETE");
        MapCommandMethods(group, "/assets/{assetId:guid}", "Organization.UpdateAsset.v1", "PUT", "DELETE");
        MapCommandMethods(group, "/points/{pointId:guid}", "Organization.UpdatePoint.v1", "PUT", "DELETE");
        MapCommandMethods(group, "/metrics/{metricId:guid}", "Catalog.UpdateMetric.v1", "PUT");
        MapCommandMethods(group, "/units/{unitId:guid}", "Catalog.UpdateUnit.v1", "PUT");
        MapCommandMethods(group, "/data-sources/{sourceId:guid}", "Acquisition.UpdateSource.v1", "PUT", "DELETE");
        MapCommandMethods(group, "/source-point-mappings/{mappingId:guid}", "Acquisition.UpdateMapping.v1", "PUT", "DELETE");
        MapCommandMethods(group, "/simulator-configurations/{configurationId:guid}", "Acquisition.UpdateSimulatorConfiguration.v1", "PUT", "DELETE");
        return endpoints;
    }

    private static void MapCommandMethods(RouteGroupBuilder group, string route, string operationCode, params string[] methods)
    {
        group.MapMethods(route, methods, (HttpRequest request, IConfigurationCommandPort commands,
            IdempotentCommandExecutor executor, IServerPrincipalAccessor principalAccessor,
            IHostTransactionFactory transactionFactory, CancellationToken ct) =>
            ExecuteGenericAsync(operationCode, FindRouteTarget(request), request, commands, executor, principalAccessor, transactionFactory, ct));
    }

    private static Guid? FindRouteTarget(HttpRequest request)
    {
        foreach (var key in new[] { "siteId", "areaId", "assetId", "pointId", "metricId", "unitId", "sourceId", "mappingId", "configurationId" })
            if (request.RouteValues.TryGetValue(key, out var value) && Guid.TryParse(value?.ToString(), out var id)) return id;
        return null;
    }

    private static void MapMutation(RouteGroupBuilder group, string route, string operationCode)
    {
        if (route.Contains("{pointId", StringComparison.Ordinal))
        {
            group.MapPost(route, (Guid pointId, HttpRequest request, IConfigurationCommandPort commands,
                IdempotentCommandExecutor executor, IServerPrincipalAccessor principalAccessor,
                IHostTransactionFactory transactionFactory, CancellationToken ct) =>
                ExecuteGenericAsync(operationCode, pointId, request, commands, executor, principalAccessor, transactionFactory, ct));
            return;
        }
        if (route.Contains("{siteId", StringComparison.Ordinal))
        {
            group.MapPost(route, (Guid siteId, HttpRequest request, IConfigurationCommandPort commands,
                IdempotentCommandExecutor executor, IServerPrincipalAccessor principalAccessor,
                IHostTransactionFactory transactionFactory, CancellationToken ct) =>
                ExecuteGenericAsync(operationCode, siteId, request, commands, executor, principalAccessor, transactionFactory, ct));
            return;
        }
        if (route.Contains("{areaId", StringComparison.Ordinal))
        {
            group.MapPost(route, (Guid areaId, HttpRequest request, IConfigurationCommandPort commands,
                IdempotentCommandExecutor executor, IServerPrincipalAccessor principalAccessor,
                IHostTransactionFactory transactionFactory, CancellationToken ct) =>
                ExecuteGenericAsync(operationCode, areaId, request, commands, executor, principalAccessor, transactionFactory, ct));
            return;
        }
        if (route.Contains("{assetId", StringComparison.Ordinal))
        {
            group.MapPost(route, (Guid assetId, HttpRequest request, IConfigurationCommandPort commands,
                IdempotentCommandExecutor executor, IServerPrincipalAccessor principalAccessor,
                IHostTransactionFactory transactionFactory, CancellationToken ct) =>
                ExecuteGenericAsync(operationCode, assetId, request, commands, executor, principalAccessor, transactionFactory, ct));
            return;
        }
        if (route.Contains("{metricId", StringComparison.Ordinal))
        {
            group.MapPost(route, (Guid metricId, HttpRequest request, IConfigurationCommandPort commands,
                IdempotentCommandExecutor executor, IServerPrincipalAccessor principalAccessor,
                IHostTransactionFactory transactionFactory, CancellationToken ct) =>
                ExecuteGenericAsync(operationCode, metricId, request, commands, executor, principalAccessor, transactionFactory, ct));
            return;
        }
        group.MapPost(route, (HttpRequest request, IConfigurationCommandPort commands,
            IdempotentCommandExecutor executor, IServerPrincipalAccessor principalAccessor,
            IHostTransactionFactory transactionFactory, CancellationToken ct) =>
            ExecuteGenericAsync(operationCode, null, request, commands, executor, principalAccessor, transactionFactory, ct));
    }

    private static async Task<IResult> ListAsync(string resource, IConfigurationQueryPort query,
        IServerPrincipalAccessor principalAccessor, CancellationToken ct)
    {
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        return Results.Ok(await query.ListAsync(resource, principal, ct));
    }

    public static async Task<IResult> CreateSiteAsync(HttpRequest request,
        IConfigurationCommandPort commands, IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor, IHostTransactionFactory transactionFactory, CancellationToken ct)
    {
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
            return Results.Problem("Idempotency-Key is required.", statusCode: StatusCodes.Status400BadRequest);
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        var name = request.Query["name"].FirstOrDefault() ?? string.Empty;
        var fields = new[] { CommandFingerprintField.String("name", name) };
        var identity = new CommandIdentity(principal.UserId, CommandOperationCodes.CreateSite, key!);
        var fingerprint = CommandFingerprintV1.Compute(new CommandFingerprintInput(
            identity.OperationCode, principal.UserId, "Site", null, "Site", null, null, fields));
        var response = await executor.ExecuteTransactionalAsync(identity, fingerprint, transactionFactory,
            (transaction, token) => commands.CreateSiteAsync(new ConfigurationCommandRequest(null, name, null, fields), principal, transaction, token), ct);
        return ToResult(response);
    }

    public static async Task<IResult> UpdateSiteAsync(Guid siteId, HttpRequest request,
        IConfigurationCommandPort commands, IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor, IHostTransactionFactory transactionFactory, CancellationToken ct)
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
        var response = await executor.ExecuteTransactionalAsync(identity, fingerprint, transactionFactory,
            (transaction, token) => commands.UpdateSiteAsync(new ConfigurationCommandRequest(siteId, name, expectedVersion, fields), principal, transaction, token), ct);
        return ToResult(response);
    }

    private static IResult ToResult(IdempotentCommandResponse response)
        => new IdempotentHttpResult(response);

    public static async Task<IResult> ExecuteGenericAsync(string operationCode, Guid? targetId,
        HttpRequest request, IConfigurationCommandPort commands, IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor, IHostTransactionFactory transactionFactory, CancellationToken ct)
    {
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
            return Results.Problem("Idempotency-Key is required.", statusCode: StatusCodes.Status400BadRequest);
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        var name = request.Query["name"].FirstOrDefault() ?? string.Empty;
        var fields = new[] { CommandFingerprintField.String("name", name) };
        var identity = new CommandIdentity(principal.UserId, operationCode, key!);
        var fingerprint = CommandFingerprintV1.Compute(new CommandFingerprintInput(
            operationCode, principal.UserId, "Configuration", null, "Configuration", targetId, null, fields));
        var response = await executor.ExecuteTransactionalAsync(identity, fingerprint, transactionFactory,
            (transaction, token) => commands.ExecuteAsync(operationCode,
                new ConfigurationCommandRequest(targetId, name, null, fields), principal, transaction, token), ct);
        return ToResult(response);
    }
}
