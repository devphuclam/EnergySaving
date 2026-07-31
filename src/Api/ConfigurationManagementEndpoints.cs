namespace IUMP.Api;

using IUMP.Api.Infrastructure;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;

public static class ConfigurationManagementEndpointPolicy
{
    public const string RoutePrefix = "/api/v1/configuration-management";
}

public sealed record ActivateSimulatorConfigurationVersionRequest(
    long ExpectedHeadVersion,
    long DraftConfigurationVersion);

public static class ConfigurationManagementEndpoints
{

    public static IEndpointRouteBuilder MapConfigurationManagementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ConfigurationManagementEndpointPolicy.RoutePrefix);
        group.MapGet("/{resource}", ListAsync)
            .WithName("ConfigurationManagement.List");
        group.MapGet("/{resource}/{id:guid}", DetailAsync)
            .WithName("ConfigurationManagement.Detail");
        group.MapPost("/{resource}/{id:guid}/duplicate", DuplicateAsync)
            .WithMetadata(new RequireAntiforgeryCheckAttribute())
            .WithName("ConfigurationManagement.Duplicate");
        group.MapPost(
                "/simulator-configurations/{configurationId:guid}/activate",
                ActivateSimulatorConfigurationVersionAsync)
            .WithMetadata(new RequireAntiforgeryCheckAttribute())
            .WithName("ConfigurationManagement.ActivateSimulatorConfigurationVersion");
        return endpoints;
    }

    public static async Task<IResult> ListAsync(
        HttpRequest request,
        string resource,
        IConfigurationManagementQueryPort query,
        IServerPrincipalAccessor principalAccessor,
        CancellationToken ct)
    {
        if (!ConfigurationManagementResources.IsKnown(resource))
            return Results.Json(new { errorCode = "UNKNOWN_RESOURCE" },
                statusCode: StatusCodes.Status400BadRequest);
        if (principalAccessor.Current is not { } principal)
            return Results.Unauthorized();
        var page = Positive(request, "page", 1);
        var pageSize = ClampPositive(request, "pageSize", 20, 1, 200);
        if (page < 1 || pageSize < 1)
            return Results.Json(new { errorCode = "INVALID_PAGING" },
                statusCode: StatusCodes.Status400BadRequest);
        var filter = new ManagementQueryFilter(
            Search: Optional(request, "search"),
            Status: Optional(request, "status"),
            SiteId: Optional(request, "siteId"),
            AreaId: Optional(request, "areaId"),
            Page: page,
            PageSize: pageSize);
        try
        {
            var result = await query.QueryAsync(resource, filter, principal, ct);
            return Results.Ok(new
            {
                items = result.Items,
                totalCount = result.TotalCount,
                page = result.Page,
                pageSize = result.PageSize
            });
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or TimeoutException or Npgsql.NpgsqlException)
        {
            return Results.Json(new { errorCode = "DEPENDENCY_UNAVAILABLE" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static async Task<IResult> DetailAsync(
        Guid id,
        string resource,
        IConfigurationManagementQueryPort query,
        IServerPrincipalAccessor principalAccessor,
        CancellationToken ct)
    {
        if (!ConfigurationManagementResources.IsKnown(resource))
            return Results.Json(new { errorCode = "UNKNOWN_RESOURCE" },
                statusCode: StatusCodes.Status400BadRequest);
        if (principalAccessor.Current is not { } principal)
            return Results.Unauthorized();
        try
        {
            var detail = await query.GetDetailAsync(resource, id, principal, ct);
            return detail is null
                ? Results.Json(new { errorCode = "NOT_FOUND" },
                    statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(detail);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or TimeoutException or Npgsql.NpgsqlException)
        {
            return Results.Json(new { errorCode = "DEPENDENCY_UNAVAILABLE" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static async Task<IResult> DuplicateAsync(
        Guid id,
        string resource,
        HttpRequest request,
        IConfigurationManagementCommandPort commands,
        IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor,
        IHostTransactionFactory transactionFactory,
        CancellationToken ct)
    {
        if (!ConfigurationManagementResources.IsKnown(resource))
            return Results.Json(new { errorCode = "UNKNOWN_RESOURCE" },
                statusCode: StatusCodes.Status400BadRequest);
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) ||
            string.IsNullOrWhiteSpace(key))
            return Results.Problem("Idempotency-Key is required.",
                statusCode: StatusCodes.Status400BadRequest);
        if (principalAccessor.Current is not { } principal)
            return Results.Unauthorized();
        var identity = new CommandIdentity(
            principal.UserId, CommandOperationCodes.DuplicateConfiguration, key!);
        var fingerprint = CommandFingerprintV1.Compute(new CommandFingerprintInput(
            identity.OperationCode, principal.UserId, null, null,
            resource, id, null,
            [CommandFingerprintField.String("resource", resource)]));
        var response = await executor.ExecuteTransactionalAsync(
            identity, fingerprint, transactionFactory,
            (transaction, token) => commands.DuplicateAsync(
                resource, id, principal, transaction, token), ct);
        return new IdempotentHttpResult(response);
    }

    public static async Task<IResult> ActivateSimulatorConfigurationVersionAsync(
        Guid configurationId,
        ActivateSimulatorConfigurationVersionRequest? body,
        HttpRequest request,
        IConfigurationManagementCommandPort commands,
        IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor,
        IHostTransactionFactory transactionFactory,
        CancellationToken ct)
    {
        if (body is null ||
            body.ExpectedHeadVersion < 1 ||
            body.DraftConfigurationVersion < 1)
            return Results.Json(new { errorCode = "VERSION_FIELDS_REQUIRED" },
                statusCode: StatusCodes.Status400BadRequest);
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) ||
            string.IsNullOrWhiteSpace(key))
            return Results.Problem("Idempotency-Key is required.",
                statusCode: StatusCodes.Status400BadRequest);
        if (principalAccessor.Current is not { } principal)
            return Results.Unauthorized();
        var identity = new CommandIdentity(principal.UserId,
            CommandOperationCodes.ActivateSimulatorConfigurationVersion, key!);
        var fingerprint = CommandFingerprintV1.Compute(new CommandFingerprintInput(
            identity.OperationCode, principal.UserId,
            "SimulatorConfiguration", configurationId, null, null,
            body.ExpectedHeadVersion,
            [
                CommandFingerprintField.Int64(
                    "draftConfigurationVersion", body.DraftConfigurationVersion)
            ]));
        var response = await executor.ExecuteTransactionalAsync(
            identity, fingerprint, transactionFactory,
            (transaction, token) =>
                commands.ActivateSimulatorConfigurationVersionAsync(
                    configurationId, body.ExpectedHeadVersion,
                    body.DraftConfigurationVersion, principal, transaction, token),
            ct);
        return new IdempotentHttpResult(response);
    }

    private static string? Optional(HttpRequest request, string name) =>
        request.Query[name].FirstOrDefault() is { } value &&
        !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static int Positive(HttpRequest request, string name, int fallback) =>
        int.TryParse(request.Query[name].FirstOrDefault(), out var parsed) ? parsed : fallback;

    private static int ClampPositive(HttpRequest request, string name, int fallback, int min, int max) =>
        Math.Clamp(Positive(request, name, fallback), min, max);
}
