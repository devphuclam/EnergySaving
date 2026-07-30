using IUMP.Api;
using IUMP.Api.Infrastructure;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;
using IUMP.Tests.Unit.Fakes;
using Microsoft.AspNetCore.Http;

namespace IUMP.Tests.Unit.Api;

public static class OperationalWorkspaceEndpointTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }
    public static int FailureCount { get; private set; }

    public static async Task<IReadOnlyList<string>> Run()
    {
        var failures = new List<string>();
        var assertions = 0;
        void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) failures.Add(message);
        }

        Check(
            OperationalWorkspaceEndpointPolicy.RoutePrefix ==
                "/api/v1/operational-workspace" &&
            OperationalWorkspaceEndpointPolicy.QueryRoutes.Length == 3,
            "Operational workspace query routes must remain versioned and bounded.");
        Check(
            OperationalWorkspaceEndpointPolicy.AssignmentRequiresIdempotency &&
            OperationalWorkspaceEndpointPolicy.AssignmentRequiresAntiforgery,
            "Engineer assignment must require idempotency and antiforgery.");

        var anonymous = new PrincipalAccessor(null);
        var query = new FakeQueryPort(Status(0));
        var status401 = await OperationalWorkspaceEndpoints.GetStatusAsync(
            query, anonymous, CancellationToken.None);
        Check(status401 is Microsoft.AspNetCore.Http.HttpResults.UnauthorizedHttpResult,
            "Status without a server principal must return 401.");

        var engineer = new ServerPrincipal(
            Guid.NewGuid(), "engineer", new HashSet<string> { Guid.NewGuid().ToString("D") },
            new HashSet<string>());
        var list403 = await OperationalWorkspaceEndpoints.ListEngineersAsync(
            query, new PrincipalAccessor(engineer), CancellationToken.None);
        Check(list403 is Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult,
            "Engineer list must return 403 to a non-Administrator.");

        var dependency503 = await OperationalWorkspaceEndpoints.GetStatusAsync(
            new FakeQueryPort(Status(0), throwDependency: true),
            new PrincipalAccessor(engineer), CancellationToken.None);
        Check(dependency503.GetType().Name.Contains("JsonHttpResult", StringComparison.Ordinal),
            "Dependency failure must be mapped to a safe JSON result.");

        var validationRequest = new DefaultHttpContext().Request;
        validationRequest.QueryString = new QueryString(
            "?siteId=bad&areaId=bad&assetId=bad&pointId=bad&sourceId=bad" +
            "&mappingId=bad&configurationId=bad");
        var incompleteValidation = await OperationalWorkspaceEndpoints.ValidateAsync(
            validationRequest, query, new PrincipalAccessor(engineer),
            CancellationToken.None);
        Check(
            incompleteValidation is
                Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult invalid &&
            invalid.StatusCode == 400,
            "Malformed chain validation must return 400.");

        var noKey = new DefaultHttpContext();
        var assignment400 = await OperationalWorkspaceEndpoints.AssignEngineerAsync(
            Guid.NewGuid(), Guid.NewGuid(), noKey.Request, new FakeCommandPort(),
            new IdempotentCommandExecutor(new FakeCommandIdempotencyStore()),
            new PrincipalAccessor(engineer), new FakePhase9TransactionFactory(),
            CancellationToken.None);
        Check(
            assignment400 is Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult problem &&
            problem.StatusCode == StatusCodes.Status400BadRequest,
            "Engineer assignment without Idempotency-Key must return 400.");

        var keyedAnonymous = new DefaultHttpContext();
        keyedAnonymous.Request.Headers["Idempotency-Key"] = "workspace-assignment-anonymous";
        var assignment401 = await OperationalWorkspaceEndpoints.AssignEngineerAsync(
            Guid.NewGuid(), Guid.NewGuid(), keyedAnonymous.Request, new FakeCommandPort(),
            new IdempotentCommandExecutor(new FakeCommandIdempotencyStore()),
            anonymous, new FakePhase9TransactionFactory(), CancellationToken.None);
        Check(assignment401 is Microsoft.AspNetCore.Http.HttpResults.UnauthorizedHttpResult,
            "Engineer assignment without a server principal must return 401.");

        var keyed = new DefaultHttpContext();
        keyed.Request.Headers["Idempotency-Key"] = "workspace-assignment-safe";
        var commands = new FakeCommandPort();
        var assignment = await OperationalWorkspaceEndpoints.AssignEngineerAsync(
            Guid.NewGuid(), Guid.NewGuid(), keyed.Request, commands,
            new IdempotentCommandExecutor(new FakeCommandIdempotencyStore()),
            new PrincipalAccessor(new ServerPrincipal(
                Guid.NewGuid(), "admin", new HashSet<string>(), new HashSet<string>(), true)),
            new FakePhase9TransactionFactory(), CancellationToken.None);
        Check(assignment is IdempotentHttpResult && commands.CallCount == 1,
            "Valid assignment must flow through the idempotent transactional executor.");

        TestCount = assertions;
        AssertionCount = assertions;
        FailureCount = failures.Count;
        return failures;
    }

    private static OperationalWorkspaceStatus Status(int completed) =>
        OperationalWorkspaceStatusBuilder.Build(
            false, true, true, completed, 0, true,
            Array.Empty<WorkspaceSiteSummary>());

    private sealed class PrincipalAccessor(ServerPrincipal? current) :
        IServerPrincipalAccessor
    {
        public ServerPrincipal? Current { get; } = current;
    }

    private sealed class FakeQueryPort(
        OperationalWorkspaceStatus status,
        bool throwDependency = false) : IOperationalWorkspaceQueryPort
    {
        public Task<OperationalWorkspaceStatus> GetStatusAsync(
            ServerPrincipal principal, CancellationToken ct = default) =>
            throwDependency
                ? throw new InvalidOperationException("redacted dependency failure")
                : Task.FromResult(status);

        public Task<IReadOnlyList<WorkspaceEngineerCandidate>> ListEngineersAsync(
            ServerPrincipal principal, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkspaceEngineerCandidate>>([]);

        public Task<WorkspaceChainValidation> ValidateChainAsync(
            WorkspaceChainSelection requested,
            ServerPrincipal principal,
            CancellationToken ct = default) =>
            Task.FromResult(new WorkspaceChainValidation(
                false, [], new Dictionary<string, long>(), [], false));
    }

    private sealed class FakeCommandPort : IOperationalWorkspaceCommandPort
    {
        public int CallCount { get; private set; }

        public Task<CommandExecutionResult> AssignEngineerAsync(
            Guid siteId,
            Guid engineerUserId,
            ServerPrincipal principal,
            IHostTransaction transaction,
            CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(CommandExecutionResult.Ok(
                StatusCodes.Status201Created,
                """{"status":"Assigned"}""",
                engineerUserId.ToString("D")));
        }
    }
}
