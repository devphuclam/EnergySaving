namespace IUMP.Api;

using IUMP.Api.Infrastructure;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;

public static class OperationalWorkspaceEndpointPolicy
{
    public const string RoutePrefix = "/api/v1/operational-workspace";
    public static readonly string[] QueryRoutes =
        ["/status", "/engineers", "/chains/validate"];
    public const string EngineerAssignmentRoute =
        "/sites/{siteId:guid}/engineers/{engineerUserId:guid}";
    public const bool AssignmentRequiresIdempotency = true;
    public const bool AssignmentRequiresAntiforgery = true;
}

public static class OperationalWorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapOperationalWorkspaceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/operational-workspace");
        group.MapGet("/status", GetStatusAsync);
        group.MapGet("/engineers", ListEngineersAsync);
        group.MapGet("/chains/validate", ValidateAsync);
        group.MapPost("/sites/{siteId:guid}/engineers/{engineerUserId:guid}",
            AssignEngineerAsync)
            .WithMetadata(new RequireAntiforgeryCheckAttribute());
        return endpoints;
    }

    public static async Task<IResult> GetStatusAsync(
        IOperationalWorkspaceQueryPort query,
        IServerPrincipalAccessor principalAccessor,
        CancellationToken ct)
    {
        if (principalAccessor.Current is not { } principal)
            return Results.Unauthorized();
        try { return Results.Ok(await query.GetStatusAsync(principal, ct)); }
        catch (Exception exception) when (
            exception is InvalidOperationException or TimeoutException or Npgsql.NpgsqlException)
        {
            return Results.Json(new
            {
                landing = WorkspaceLanding.DependencyError.ToString(),
                dependency = new { status = "Unavailable", errorCode = "DEPENDENCY_UNAVAILABLE" }
            }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static async Task<IResult> ListEngineersAsync(
        IOperationalWorkspaceQueryPort query,
        IServerPrincipalAccessor principalAccessor,
        CancellationToken ct)
    {
        if (principalAccessor.Current is not { } principal)
            return Results.Unauthorized();
        if (!principal.IsAdministrator)
            return Results.Forbid();
        try
        {
            return Results.Ok(new
            {
                items = await query.ListEngineersAsync(principal, ct)
            });
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or TimeoutException or
                Npgsql.NpgsqlException)
        {
            return Results.Json(new { errorCode = "DEPENDENCY_UNAVAILABLE" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static async Task<IResult> ValidateAsync(
        HttpRequest request,
        IOperationalWorkspaceQueryPort query,
        IServerPrincipalAccessor principalAccessor,
        CancellationToken ct)
    {
        if (principalAccessor.Current is not { } principal)
            return Results.Unauthorized();
        if (!TryChain(request, out var chain))
            return Results.Problem(
                "All chain identifiers must be valid UUID values.",
                statusCode: StatusCodes.Status400BadRequest);
        try
        {
            var result = await query.ValidateChainAsync(chain!, principal, ct);
            return !result.Valid && result.Failures.Any(
                    failure => failure.ErrorCode == "NOT_FOUND")
                ? Results.Json(
                    result, statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(result);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or TimeoutException or
                Npgsql.NpgsqlException)
        {
            return Results.Json(new
            {
                errorCode = "DEPENDENCY_UNAVAILABLE",
                simulatorAutoStart = false
            }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool TryChain(
        HttpRequest request,
        out WorkspaceChainSelection? chain)
    {
        var names = new[]
        {
            "siteId", "areaId", "assetId", "pointId", "sourceId",
            "mappingId", "configurationId"
        };
        var ids = new Dictionary<string, Guid>();
        foreach (var name in names)
        {
            if (!Guid.TryParse(request.Query[name], out var value))
            {
                chain = null;
                return false;
            }
            ids[name] = value;
        }
        chain = new WorkspaceChainSelection(
            ids["siteId"], null, ids["areaId"], null, ids["assetId"], null,
            ids["pointId"], null, ids["sourceId"], null, ids["mappingId"], null,
            ids["configurationId"], null);
        return true;
    }

    public static async Task<IResult> AssignEngineerAsync(
        Guid siteId,
        Guid engineerUserId,
        HttpRequest request,
        IOperationalWorkspaceCommandPort commands,
        IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor,
        IHostTransactionFactory transactionFactory,
        CancellationToken ct)
    {
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) ||
            string.IsNullOrWhiteSpace(key))
            return Results.Problem("Idempotency-Key is required.",
                statusCode: StatusCodes.Status400BadRequest);
        if (principalAccessor.Current is not { } principal)
            return Results.Unauthorized();
        var identity = new CommandIdentity(
            principal.UserId, CommandOperationCodes.AssignEngineerSiteScope, key!);
        var fields = new[]
        {
            CommandFingerprintField.Uuid("siteId", siteId),
            CommandFingerprintField.Uuid("engineerUserId", engineerUserId)
        };
        var fingerprint = CommandFingerprintV1.Compute(new CommandFingerprintInput(
            identity.OperationCode, principal.UserId, "UserScope", engineerUserId,
            "Site", siteId, null, fields));
        var response = await executor.ExecuteTransactionalAsync(
            identity, fingerprint, transactionFactory,
            (transaction, token) => commands.AssignEngineerAsync(
                siteId, engineerUserId, principal, transaction, token), ct);
        return new IdempotentHttpResult(response);
    }
}
