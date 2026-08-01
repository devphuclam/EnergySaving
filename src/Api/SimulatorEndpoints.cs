namespace IUMP.Api;

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using IUMP.Api.Infrastructure;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;

public static class SimulatorEndpointPolicy
{
    public static IReadOnlySet<string> MutationOperations { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Simulator.Start.v1", "Simulator.Pause.v1", "Simulator.Resume.v1", "Simulator.Stop.v1"
    };
}

public static class SimulatorEndpoints
{
    public static IEndpointRouteBuilder MapSimulatorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/simulators");
        group.MapGet("/{runId:guid}", async (Guid runId, ISimulatorQueryPort query,
            IServerPrincipalAccessor principalAccessor, CancellationToken ct) =>
        {
            if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
            return Results.Ok(await query.GetRunAsync(runId, principal, ct));
        });
        group.MapGet("/{sourceId:guid}/run", async (Guid sourceId, ISimulatorQueryPort query,
            IServerPrincipalAccessor principalAccessor, CancellationToken ct) =>
        {
            if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
            return Results.Ok(await query.GetRunAsync(sourceId, principal, ct));
        });
        group.MapGet("/workspace/selectors", GetWorkspaceSelectorsAsync);
        group.MapGet("/workspace", GetWorkspaceAsync);
        group.MapPost("/workspace/start", (HttpRequest request,
            ISimulatorWorkspaceCommandPort commands, IdempotentCommandExecutor executor,
            IServerPrincipalAccessor principalAccessor, IHostTransactionFactory transactionFactory,
            CancellationToken ct) => ExecuteWorkspaceAsync(null, CommandOperationCodes.StartSimulator,
                request, commands, executor, principalAccessor, transactionFactory, ct))
            .WithMetadata(new RequireAntiforgeryCheckAttribute());
        group.MapPost("/workspace/runs/{runId:guid}/pause", (Guid runId, HttpRequest request,
            ISimulatorWorkspaceCommandPort commands, IdempotentCommandExecutor executor,
            IServerPrincipalAccessor principalAccessor, IHostTransactionFactory transactionFactory,
            CancellationToken ct) => ExecuteWorkspaceAsync(runId, CommandOperationCodes.PauseSimulator,
                request, commands, executor, principalAccessor, transactionFactory, ct))
            .WithMetadata(new RequireAntiforgeryCheckAttribute());
        group.MapPost("/workspace/runs/{runId:guid}/resume", (Guid runId, HttpRequest request,
            ISimulatorWorkspaceCommandPort commands, IdempotentCommandExecutor executor,
            IServerPrincipalAccessor principalAccessor, IHostTransactionFactory transactionFactory,
            CancellationToken ct) => ExecuteWorkspaceAsync(runId, CommandOperationCodes.ResumeSimulator,
                request, commands, executor, principalAccessor, transactionFactory, ct))
            .WithMetadata(new RequireAntiforgeryCheckAttribute());
        group.MapPost("/workspace/runs/{runId:guid}/stop", (Guid runId, HttpRequest request,
            ISimulatorWorkspaceCommandPort commands, IdempotentCommandExecutor executor,
            IServerPrincipalAccessor principalAccessor, IHostTransactionFactory transactionFactory,
            CancellationToken ct) => ExecuteWorkspaceAsync(runId, CommandOperationCodes.StopSimulator,
                request, commands, executor, principalAccessor, transactionFactory, ct))
            .WithMetadata(new RequireAntiforgeryCheckAttribute());
        group.MapPost("/{sourceId:guid}/start", (Guid sourceId, HttpRequest request,
            ISimulatorCommandPort commands, IdempotentCommandExecutor executor,
            IServerPrincipalAccessor principalAccessor, IHostTransactionFactory transactionFactory, CancellationToken ct) =>
            ExecuteAsync(sourceId, CommandOperationCodes.StartSimulator, request, commands, executor, principalAccessor, transactionFactory, ct));
        group.MapPost("/{runId:guid}/pause", (Guid runId, HttpRequest request,
            ISimulatorCommandPort commands, IdempotentCommandExecutor executor,
            IServerPrincipalAccessor principalAccessor, IHostTransactionFactory transactionFactory, CancellationToken ct) =>
            ExecuteAsync(runId, CommandOperationCodes.PauseSimulator, request, commands, executor, principalAccessor, transactionFactory, ct));
        group.MapPost("/{runId:guid}/resume", (Guid runId, HttpRequest request,
            ISimulatorCommandPort commands, IdempotentCommandExecutor executor,
            IServerPrincipalAccessor principalAccessor, IHostTransactionFactory transactionFactory, CancellationToken ct) =>
            ExecuteAsync(runId, CommandOperationCodes.ResumeSimulator, request, commands, executor, principalAccessor, transactionFactory, ct));
        group.MapPost("/{runId:guid}/stop", (Guid runId, HttpRequest request,
            ISimulatorCommandPort commands, IdempotentCommandExecutor executor,
            IServerPrincipalAccessor principalAccessor, IHostTransactionFactory transactionFactory, CancellationToken ct) =>
            ExecuteAsync(runId, CommandOperationCodes.StopSimulator, request, commands, executor, principalAccessor, transactionFactory, ct));
        return endpoints;
    }

    public static async Task<IResult> GetWorkspaceSelectorsAsync(
        ISimulatorWorkspaceQueryPort query,
        IServerPrincipalAccessor principalAccessor,
        CancellationToken ct)
    {
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        try
        {
            return Results.Ok(await query.GetAsync(null, 1, 100, principal, ct));
        }
        catch (Exception exception) when (IsRuntimeFailure(exception))
        {
            return Results.Json(new { state = "dependency", errorCode = "RUNTIME_DEPENDENCY_UNAVAILABLE" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static async Task<IResult> GetWorkspaceAsync(
        Guid? siteId,
        Guid? areaId,
        Guid? assetId,
        Guid? sourceId,
        Guid? configurationId,
        long? configurationVersion,
        int? page,
        int? pageSize,
        ISimulatorWorkspaceQueryPort query,
        IServerPrincipalAccessor principalAccessor,
        CancellationToken ct)
    {
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        var any = siteId.HasValue || areaId.HasValue || assetId.HasValue || sourceId.HasValue ||
            configurationId.HasValue || configurationVersion.HasValue;
        var all = siteId.HasValue && sourceId.HasValue && configurationId.HasValue &&
            configurationVersion.HasValue;
        if (any && !all)
            return Results.Json(new { errorCode = "SIMULATOR_SELECTION_FIELDS_REQUIRED" }, statusCode: 400);
        var selection = all
            ? new SimulatorSelection(siteId!.Value, areaId, assetId, sourceId!.Value,
                configurationId!.Value, configurationVersion!.Value)
            : null;
        try
        {
            return Results.Ok(await query.GetAsync(selection, page ?? 1, pageSize ?? 20, principal, ct));
        }
        catch (Exception exception) when (IsRuntimeFailure(exception))
        {
            return Results.Json(new { state = "dependency", errorCode = "RUNTIME_DEPENDENCY_UNAVAILABLE" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static async Task<IResult> ExecuteWorkspaceAsync(
        Guid? runId,
        string operation,
        HttpRequest request,
        ISimulatorWorkspaceCommandPort commands,
        IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor,
        IHostTransactionFactory transactionFactory,
        CancellationToken ct)
    {
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) ||
            string.IsNullOrWhiteSpace(key))
            return Results.Problem("Idempotency-Key is required.", statusCode: 400);
        var selection = await ReadSelectionAsync(request, ct);
        if (selection is null)
            return Results.Json(new { errorCode = "SIMULATOR_SELECTION_REQUIRED" }, statusCode: 400);
        long? expectedVersion = null;
        if (operation != CommandOperationCodes.StartSimulator)
        {
            if (!request.Headers.TryGetValue("If-Match", out var ifMatch) ||
                ifMatch.Count != 1 || !long.TryParse(ifMatch[0]?.Trim().Trim('"'), out var value) || value <= 0)
                return Results.Problem("A valid If-Match is required.", statusCode: 400);
            expectedVersion = value;
        }
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        var identity = new CommandIdentity(principal.UserId, operation, key!);
        var fields = new List<CommandFingerprintField>
        {
            CommandFingerprintField.Uuid("siteId", selection.SiteId),
            CommandFingerprintField.Uuid("sourceId", selection.SourceId),
            CommandFingerprintField.Uuid("configurationId", selection.ConfigurationId),
            CommandFingerprintField.Int64("configurationVersion", selection.ConfigurationVersion)
        };
        if (selection.AreaId is { } area) fields.Add(CommandFingerprintField.Uuid("areaId", area));
        if (selection.AssetId is { } asset) fields.Add(CommandFingerprintField.Uuid("assetId", asset));
        var target = runId ?? selection.SourceId;
        var fingerprint = CommandFingerprintV1.Compute(new CommandFingerprintInput(
            operation, principal.UserId, "SimulatorRun", target, "SimulatorSelection", target,
            expectedVersion, fields));
        try
        {
            var response = await executor.ExecuteTransactionalAsync(identity, fingerprint, transactionFactory,
                (transaction, token) => commands.ExecuteAsync(operation, selection, runId,
                    expectedVersion, principal, transaction, token), ct);
            return new IdempotentHttpResult(response);
        }
        catch (Exception exception) when (IsRuntimeFailure(exception))
        {
            return Results.Json(new { errorCode = "RUNTIME_DEPENDENCY_UNAVAILABLE" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<SimulatorSelection?> ReadSelectionAsync(HttpRequest request,
        CancellationToken ct)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<SimulatorSelection>(request.Body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);
        }
        catch (JsonException) { return null; }
    }

    private static bool IsRuntimeFailure(Exception exception) => exception is Npgsql.NpgsqlException or
        TimeoutException or InvalidOperationException;

    public static async Task<IResult> ExecuteAsync(Guid target, string operation, HttpRequest request,
        ISimulatorCommandPort commands, IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor, IHostTransactionFactory transactionFactory, CancellationToken ct)
    {
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
            return Results.Problem("Idempotency-Key is required.", statusCode: StatusCodes.Status400BadRequest);
        var requiresExpectedVersion = operation != CommandOperationCodes.StartSimulator;
        long expectedVersion = 0;
        if (requiresExpectedVersion && (!request.Headers.TryGetValue("If-Match", out var ifMatch) ||
            ifMatch.Count != 1 || !long.TryParse(ifMatch[0]?.Trim().Trim('"'), out expectedVersion) ||
            expectedVersion <= 0))
            return Results.Problem("A valid If-Match is required.", statusCode: StatusCodes.Status400BadRequest);
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        var identity = new CommandIdentity(principal.UserId, operation, key!);
        var fields = new[] { CommandFingerprintField.Uuid("targetId", target) };
        var fingerprint = CommandFingerprintV1.Compute(new CommandFingerprintInput(
            operation, principal.UserId, "SimulatorRun", target, "SimulatorRun", target,
            requiresExpectedVersion ? expectedVersion : null, fields));
        var response = await executor.ExecuteTransactionalAsync(identity, fingerprint, transactionFactory,
            (transaction, token) => commands.ExecuteAsync(
                operation, target, requiresExpectedVersion ? expectedVersion : null,
                principal, transaction, token), ct);
        return new IdempotentHttpResult(response);
    }
}
