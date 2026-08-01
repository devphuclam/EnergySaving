using IUMP.Api.Infrastructure;
using IUMP.Api;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.IAM.Contracts;
using IUMP.Modules.IAM.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;

namespace IUMP.Tests.Integration.OperationalWorkspace;

/// T050: PostgreSQL red tests for selected Simulator controls, history, idempotency,
/// optimistic conflicts, and server-side scope.
public static class SimulatorOperationsTests
{
    private static int _testCount;
    private static int _assertionCount;

    public static async Task<IReadOnlyList<string>> RunAsync(IServiceProvider root)
    {
        _testCount = 0;
        _assertionCount = 0;
        var failures = new List<string>();
        try
        {
            using var scope = root.CreateScope();
            RouteMetadataTests(failures);
            var query = scope.ServiceProvider.GetRequiredService<ISimulatorWorkspaceQueryPort>();
            var iam = scope.ServiceProvider.GetRequiredService<IIamCommandRepository>();
            var admin = (await iam.GetAllUsersAsync()).FirstOrDefault(user =>
                user.Status == UserStatus.Active && user.HasRole(Role.Administrator));
            Check(admin is not null, "T050 requires the seeded Administrator account.", failures);
            if (admin is null) return failures;
            var engineer = (await iam.GetAllUsersAsync()).FirstOrDefault(user =>
                user.Status == UserStatus.Active && user.HasRole(Role.Engineer));
            var engineerScopes = engineer is null
                ? Array.Empty<Scope>()
                : (await iam.GetScopesForUserAsync(engineer.Id)).ToArray();
            Check(engineer is not null && engineerScopes.Length > 0,
                "T050 requires a scoped active Engineer for fail-closed scope evidence.", failures);
            if (engineer is null || engineerScopes.Length == 0) return failures;
            var principal = new ServerPrincipal(
                admin.Id.Value, admin.Username, new HashSet<string>(), new HashSet<string>(), true,
                new HashSet<string> { "Administrator" });
            _testCount++;
            var empty = await query.GetAsync(null, 1, 20, principal);
            Check(empty.Selection is null,
                "The PostgreSQL workspace query must not select the first Source.", failures);
            Check(empty.History.Page == 1 && empty.History.PageSize == 20,
                "Run history must expose a bounded page contract.", failures);

            Check(empty.Options.Count > 0,
                "T050 requires at least one authorized eligible Simulator context.", failures);
            var option = await FindUnusedOptionAsync(query, empty.Options, principal);
            if (option is not null)
            {
                var selection = new SimulatorSelection(option.SiteId, option.AreaId, option.AssetId,
                    option.SourceId, option.ConfigurationId, option.ConfigurationVersion);
                var selected = await query.GetAsync(selection, 1, 20, principal);
                Check(selected.Selection == selection && selected.State == "ready",
                    "An explicit Source/configuration selection must return its own workspace.", failures);

                var workspaceCommands = scope.ServiceProvider.GetRequiredService<ISimulatorWorkspaceCommandPort>();
                var invalidSelection = selection with { ConfigurationVersion = selection.ConfigurationVersion + 1000 };
                var invalid = await workspaceCommands.ExecuteAsync(
                    CommandOperationCodes.StartSimulator, invalidSelection, null, null,
                    principal, new TestTransaction(), CancellationToken.None);
                Check(invalid.StatusCode == 422 && invalid.Body.Contains("SIMULATOR_SELECTION_NOT_FOUND", StringComparison.Ordinal),
                    "An ineligible configuration must be rejected before Start creates a Run.", failures);

                var outOfScope = await query.GetAsync(
                    selection with { SiteId = Guid.NewGuid() }, 1, 20, principal);
                Check(outOfScope.ErrorCode == "SIMULATOR_SELECTION_NOT_FOUND" &&
                      outOfScope.State == "validation",
                    "An unknown or out-of-scope Site must fail closed as not-found.", failures);
                var engineerPrincipal = new ServerPrincipal(
                    engineer.Id.Value, engineer.Username,
                    engineerScopes.Where(scope => scope.SiteId.HasValue)
                        .Select(scope => scope.SiteId!.Value.ToString("D"))
                        .ToHashSet(StringComparer.Ordinal),
                    engineerScopes.Where(scope => scope.AreaId.HasValue)
                        .Select(scope => scope.AreaId!.Value.ToString("D"))
                        .ToHashSet(StringComparer.Ordinal),
                    false, new HashSet<string> { "Engineer" });
                var engineerOutOfScope = await query.GetAsync(
                    selection with { SiteId = Guid.NewGuid() }, 1, 20, engineerPrincipal);
                Check(engineerOutOfScope.ErrorCode == "SIMULATOR_SELECTION_NOT_FOUND" &&
                      engineerOutOfScope.State == "validation",
                    "A scoped Engineer must receive not-found for a Site outside assigned scope.", failures);

                var missingRun = await workspaceCommands.ExecuteAsync(
                    CommandOperationCodes.PauseSimulator, selection, Guid.NewGuid(), 1,
                    principal, new TestTransaction(), CancellationToken.None);
                Check(missingRun.StatusCode == 404 && missingRun.Body.Contains("SIMULATOR_RUN_NOT_FOUND", StringComparison.Ordinal),
                    "Pause must scope-check the selected Run and return not-found for an unrelated Run.", failures);

                var store = scope.ServiceProvider.GetRequiredService<ICommandIdempotencyStore>();
                var executor = new IdempotentCommandExecutor(store);
                var accessor = new TestPrincipalAccessor(principal);
                var missingKeyContext = Context(selection);
                var missingKey = await SimulatorEndpoints.ExecuteWorkspaceAsync(
                    null, CommandOperationCodes.StartSimulator, missingKeyContext.Request,
                    workspaceCommands, executor, accessor,
                    scope.ServiceProvider.GetRequiredService<IHostTransactionFactory>(), CancellationToken.None);
                Check(missingKey is ProblemHttpResult { StatusCode: 400 },
                    "Selected Start must require an idempotency key.", failures);

                var noVersionContext = Context(selection);
                noVersionContext.Request.Headers["Idempotency-Key"] = "t050-no-version";
                var noVersion = await SimulatorEndpoints.ExecuteWorkspaceAsync(
                    Guid.NewGuid(), CommandOperationCodes.PauseSimulator, noVersionContext.Request,
                    workspaceCommands, executor, accessor,
                    scope.ServiceProvider.GetRequiredService<IHostTransactionFactory>(), CancellationToken.None);
                Check(noVersion is ProblemHttpResult { StatusCode: 400 },
                    "Selected Pause must require an optimistic If-Match version.", failures);

                var factory = scope.ServiceProvider.GetRequiredService<IHostTransactionFactory>();
                var run = selected.CurrentRun;
                var createdHere = false;
                var startKey = $"t050-start-{Guid.NewGuid():N}";
                if (run is null)
                {
                    var started = await MutateAsync(null, CommandOperationCodes.StartSimulator,
                        selection, startKey, null, workspaceCommands, executor, accessor, factory);
                    Check(started is not null && started.Response.StatusCode == 202,
                        "Explicit Start must create one PostgreSQL Run.", failures);
                    if (started is null || started.Response.StatusCode != 202) return failures;
                    run = ReadRun(started.Response.Body, selection);
                    createdHere = true;

                    var replay = await MutateAsync(null, CommandOperationCodes.StartSimulator,
                        selection, startKey, null, workspaceCommands, executor, accessor, factory);
                    Check(replay is not null && replay.Response.IsReplay,
                        "Repeating the same Start key and request must replay the original result.", failures);

                    var conflictSelection = selection with
                    {
                        ConfigurationVersion = selection.ConfigurationVersion + 1
                    };
                    var conflict = await MutateAsync(null, CommandOperationCodes.StartSimulator,
                        conflictSelection, startKey, null, workspaceCommands, executor, accessor, factory);
                    Check(conflict is not null && conflict.Response.StatusCode == 409 &&
                        conflict.Response.Body.Contains("IDEMPOTENCY_CONFLICT", StringComparison.Ordinal),
                        "Reusing a Start key with a different canonical selection must conflict.", failures);
                }

                if (run is not null)
                {
                    if (run.Status == "Running")
                    {
                        var staleVersion = run.Version;
                        run = await ChangeStatusAsync(run, selection, "pause", workspaceCommands,
                            executor, accessor, factory, failures);
                        var stale = await MutateAsync(run.RunId, CommandOperationCodes.PauseSimulator,
                            selection, $"t050-stale-{Guid.NewGuid():N}", staleVersion,
                            workspaceCommands, executor, accessor, factory);
                        Check(stale is not null && stale.Response.StatusCode == 409 &&
                            stale.Response.Body.Contains("VERSION_CONFLICT", StringComparison.Ordinal),
                            "A stale If-Match must return a Run version conflict.", failures);
                    }
                    if (run.Status == "Paused")
                    {
                        run = await ChangeStatusAsync(run, selection, "resume", workspaceCommands,
                            executor, accessor, factory, failures);
                    }
                    if (createdHere || run.Status is "Running" or "Paused")
                    {
                        run = await ChangeStatusAsync(run, selection, "stop", workspaceCommands,
                            executor, accessor, factory, failures);
                    }
                    var after = await query.GetAsync(selection, 1, 20, principal);
                    Check(after.History.TotalCount >= selected.History.TotalCount + (createdHere ? 1 : 0),
                        "Run history must persist the selected Run and remain page-bounded.", failures);
                    var persisted = after.History.Items.FirstOrDefault(item => item.RunId == run.RunId);
                    Check(persisted is not null && persisted.Status == "Stopped" &&
                        persisted.GeneratedCount >= 0 && persisted.AcceptedCount >= 0 && persisted.RejectedCount >= 0,
                        "Selected Run history must expose pinned status and non-negative counters.", failures);
                    if (persisted is not null)
                    {
                        Check(persisted.AcceptedCount + persisted.RejectedCount <= persisted.GeneratedCount,
                            "Accepted and rejected production counters must not exceed generated samples.", failures);
                        Check(persisted.LastProductionAtUtc is null ||
                              persisted.LastProductionAtUtc >= persisted.CreatedAtUtc,
                            "Last production time must be absent or no earlier than Run creation.", failures);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            failures.Add($"T050 unexpected exception: {exception.Message}");
        }

        Console.WriteLine($"T050: cases={_testCount}; assertions={_assertionCount}; failures={failures.Count}");
        return failures;
    }

    private static void Check(bool condition, string message, List<string> failures)
    {
        _assertionCount++;
        if (!condition) failures.Add(message);
    }

    private static void RouteMetadataTests(List<string> failures)
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddSingleton<ISimulatorQueryPort>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<ISimulatorCommandPort>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<ISimulatorWorkspaceQueryPort>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<ISimulatorWorkspaceCommandPort>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<IServerPrincipalAccessor>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<IdempotentCommandExecutor>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<IHostTransactionFactory>(_ => throw new NotSupportedException());
        using var app = builder.Build();
        app.MapSimulatorEndpoints();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Contains(
                "/api/v1/simulators/workspace", StringComparison.Ordinal) == true)
            .ToArray();
        var mutationEndpoints = endpoints.Where(endpoint =>
                     endpoint.RoutePattern.RawText?.Contains("/start", StringComparison.Ordinal) == true ||
                     endpoint.RoutePattern.RawText?.Contains("/pause", StringComparison.Ordinal) == true ||
                     endpoint.RoutePattern.RawText?.Contains("/resume", StringComparison.Ordinal) == true ||
                     endpoint.RoutePattern.RawText?.Contains("/stop", StringComparison.Ordinal) == true)
            .ToArray();
        foreach (var endpoint in mutationEndpoints)
        {
            Check(endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation == true,
                $"Mutation endpoint {endpoint.RoutePattern.RawText} must require antiforgery validation.", failures);
        }
        Check(mutationEndpoints.Length == 4,
            "The selected Simulator workspace must expose exactly four protected mutation routes.", failures);
    }

    private static DefaultHttpContext Context(SimulatorSelection selection)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(selection)));
        return context;
    }

    private static async Task<SimulatorSelectionOption?> FindUnusedOptionAsync(
        ISimulatorWorkspaceQueryPort query,
        IReadOnlyList<SimulatorSelectionOption> options,
        ServerPrincipal principal)
    {
        foreach (var option in options)
        {
            var selection = new SimulatorSelection(option.SiteId, option.AreaId, option.AssetId,
                option.SourceId, option.ConfigurationId, option.ConfigurationVersion);
            var snapshot = await query.GetAsync(selection, 1, 20, principal);
            if (snapshot.CurrentRun is null) return option;
        }
        return options.FirstOrDefault();
    }

    private static async Task<IdempotentHttpResult?> MutateAsync(
        Guid? runId,
        string operation,
        SimulatorSelection selection,
        string key,
        long? expectedVersion,
        ISimulatorWorkspaceCommandPort commands,
        IdempotentCommandExecutor executor,
        IServerPrincipalAccessor accessor,
        IHostTransactionFactory factory)
    {
        var context = Context(selection);
        context.Request.Headers["Idempotency-Key"] = key;
        if (expectedVersion is not null) context.Request.Headers["If-Match"] = $"\"{expectedVersion}\"";
        var result = await SimulatorEndpoints.ExecuteWorkspaceAsync(runId, operation, context.Request,
            commands, executor, accessor, factory, CancellationToken.None);
        return result as IdempotentHttpResult;
    }

    private static SimulatorRunHistoryItem ReadRun(string body, SimulatorSelection selection)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var statusValue = root.GetProperty("status");
        var status = statusValue.ValueKind == JsonValueKind.String
            ? statusValue.GetString() ?? "Running"
            : statusValue.GetInt32() switch { 0 => "Running", 1 => "Paused", _ => "Stopped" };
        return new SimulatorRunHistoryItem(
            root.GetProperty("runId").GetGuid(), selection.SourceId, selection.ConfigurationId,
            selection.ConfigurationVersion, status,
            root.GetProperty("version").GetInt64(), 0, 0, 0, null, 1, DateTime.UtcNow);
    }

    private static async Task<SimulatorRunHistoryItem> ChangeStatusAsync(
        SimulatorRunHistoryItem run,
        SimulatorSelection selection,
        string operation,
        ISimulatorWorkspaceCommandPort commands,
        IdempotentCommandExecutor executor,
        IServerPrincipalAccessor accessor,
        IHostTransactionFactory factory,
        List<string> failures)
    {
        var result = await MutateAsync(run.RunId,
            operation switch
            {
                "pause" => CommandOperationCodes.PauseSimulator,
                "resume" => CommandOperationCodes.ResumeSimulator,
                _ => CommandOperationCodes.StopSimulator
            }, selection, $"t050-{operation}-{Guid.NewGuid():N}", run.Version,
            commands, executor, accessor, factory);
        Check(result is not null && result.Response.StatusCode == 200,
            $"Explicit {operation} must change the selected Run status.", failures);
        if (result is null || result.Response.StatusCode != 200) return run;
        return ReadRun(result.Response.Body, selection) with { RunId = run.RunId };
    }

    private sealed class TestPrincipalAccessor(ServerPrincipal principal) : IServerPrincipalAccessor
    {
        public ServerPrincipal? Current { get; } = principal;
    }

    private sealed class TestTransaction : IHostTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public string IsolationIntent => "test";
        public bool IsCompleted => true;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
