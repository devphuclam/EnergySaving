namespace IUMP.Api;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using IUMP.Api.Infrastructure;
using IUMP.Modules.Integration.Contracts;

public static class SimulatorEndpointPolicy
{
    public static IReadOnlySet<string> MutationOperations { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Simulator.Start.v1", "Simulator.Pause.v1", "Simulator.Resume.v1", "Simulator.Stop.v1"
    };

    public static string[] Routes => new[] { "/api/v1/simulators/{sourceId}/start", "/api/v1/simulators/{runId}/pause", "/api/v1/simulators/{runId}/resume", "/api/v1/simulators/{runId}/stop" };

    public static IEndpointRouteBuilder MapSimulatorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/simulators");
        group.MapGet("/{runId:guid}", (Guid runId) => Results.Ok(new { runId, status = "Stopped" }));
        group.MapPost("/{sourceId:guid}/start", (Guid sourceId, HttpRequest request, IdempotentCommandExecutor executor, CancellationToken ct) =>
            ExecuteAsync(sourceId, CommandOperationCodes.StartSimulator, request, executor, ct));
        group.MapPost("/{runId:guid}/pause", (Guid runId, HttpRequest request, IdempotentCommandExecutor executor, CancellationToken ct) =>
            ExecuteAsync(runId, CommandOperationCodes.PauseSimulator, request, executor, ct));
        group.MapPost("/{runId:guid}/resume", (Guid runId, HttpRequest request, IdempotentCommandExecutor executor, CancellationToken ct) =>
            ExecuteAsync(runId, CommandOperationCodes.ResumeSimulator, request, executor, ct));
        group.MapPost("/{runId:guid}/stop", (Guid runId, HttpRequest request, IdempotentCommandExecutor executor, CancellationToken ct) =>
            ExecuteAsync(runId, CommandOperationCodes.StopSimulator, request, executor, ct));
        return endpoints;
    }

    private static async Task<IResult> ExecuteAsync(Guid target, string operation, HttpRequest request,
        IdempotentCommandExecutor executor, CancellationToken ct)
    {
        if (!request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
            return Results.Problem("Idempotency-Key is required.", statusCode: StatusCodes.Status400BadRequest);
        if (!Guid.TryParse(request.Headers["X-Caller-Id"].FirstOrDefault(), out var caller)) return Results.Unauthorized();
        var response = await executor.ExecuteAsync(new CommandIdentity(caller, operation, key!),
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{target:D}|{operation}|{key}")),
            _ => Task.FromResult(CommandExecutionResult.Ok(202, "{\"status\":\"accepted\"}", target.ToString("D"))), ct);
        return Results.Content(response.Body, "application/json", statusCode: response.StatusCode);
    }
}
