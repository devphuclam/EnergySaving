namespace IUMP.Api;

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

    public static async Task<IResult> ExecuteAsync(Guid target, string operation, HttpRequest request,
        ISimulatorCommandPort commands, IdempotentCommandExecutor executor,
        IServerPrincipalAccessor principalAccessor, IHostTransactionFactory transactionFactory, CancellationToken ct)
    {
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
            return Results.Problem("Idempotency-Key is required.", statusCode: StatusCodes.Status400BadRequest);
        if (principalAccessor.Current is not { } principal) return Results.Unauthorized();
        var identity = new CommandIdentity(principal.UserId, operation, key!);
        var fields = new[] { CommandFingerprintField.Uuid("targetId", target) };
        var fingerprint = CommandFingerprintV1.Compute(new CommandFingerprintInput(
            operation, principal.UserId, "SimulatorRun", target, "SimulatorRun", target, null, fields));
        var response = await executor.ExecuteTransactionalAsync(identity, fingerprint, transactionFactory,
            (transaction, token) => commands.ExecuteAsync(operation, target, principal, transaction, token), ct);
        return new IdempotentHttpResult(response);
    }
}
