using IUMP.Api;
using IUMP.Api.Infrastructure;
using IUMP.Modules.Integration.Contracts;
using IUMP.Tests.Unit.Fakes;
using Microsoft.AspNetCore.Http;

namespace IUMP.Tests.Unit.Api;

public static class SimulatorEndpointTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }
    public static int FailureCount { get; private set; }

    public static async Task<List<string>> Run()
    {
        var failures = SimulatorEndpointPolicy.MutationOperations.Contains("Simulator.Start.v1")
            ? new List<string>() : new List<string> { "Simulator Start must be registered as an idempotent mutation" };
        var assertions = 1;
        assertions++; if (!SimulatorEndpointPolicy.MutationOperations.Contains("Simulator.Pause.v1") ||
            !SimulatorEndpointPolicy.MutationOperations.Contains("Simulator.Resume.v1") ||
            !SimulatorEndpointPolicy.MutationOperations.Contains("Simulator.Stop.v1"))
            failures.Add("Start/Pause/Resume/Stop handlers must share the executor");
        // Start
        var store = new FakeCommandIdempotencyStore();
        var executor = new IdempotentCommandExecutor(store);
        var principal = new ServerPrincipalAccessor(new ServerPrincipal(Guid.NewGuid(), "engineer", new HashSet<string> { "site-1" }, new HashSet<string>()));
        var transactionFactory = new FakePhase9TransactionFactory();
        var commands = new FakeSimulatorPorts();
        var startContext = new DefaultHttpContext();
        startContext.Request.Headers["Idempotency-Key"] = "simulator-start-1";
        var runId = Guid.NewGuid();
        var startResult = await SimulatorEndpoints.ExecuteAsync(runId, CommandOperationCodes.StartSimulator, startContext.Request,
            commands, executor, principal, transactionFactory, CancellationToken.None);
        assertions++; if (commands.MutationCalls != 1 || commands.LastRunId != runId ||
            commands.LastPrincipal?.UserId != principal.Current!.UserId ||
            transactionFactory.BeginCount != 1 || startResult is not IdempotentHttpResult)
            failures.Add("Simulator Start must invoke the command port with server principal and host transaction");
        // Pause
        var pauseContext = new DefaultHttpContext();
        pauseContext.Request.Headers["Idempotency-Key"] = "simulator-pause-1";
        pauseContext.Request.Headers["If-Match"] = "\"2\"";
        var pauseResult = await SimulatorEndpoints.ExecuteAsync(runId, CommandOperationCodes.PauseSimulator, pauseContext.Request,
            commands, executor, principal, transactionFactory, CancellationToken.None);
        assertions++; if (commands.MutationCalls != 2 || commands.LastOperationCode != "Simulator.Pause.v1")
            failures.Add("Simulator Pause must invoke correct operation code");
        // Resume
        var resumeContext = new DefaultHttpContext();
        resumeContext.Request.Headers["Idempotency-Key"] = "simulator-resume-1";
        resumeContext.Request.Headers["If-Match"] = "\"3\"";
        var resumeResult = await SimulatorEndpoints.ExecuteAsync(runId, CommandOperationCodes.ResumeSimulator, resumeContext.Request,
            commands, executor, principal, transactionFactory, CancellationToken.None);
        assertions++; if (commands.MutationCalls != 3 || commands.LastOperationCode != "Simulator.Resume.v1")
            failures.Add("Simulator Resume must invoke correct operation code");
        // Stop
        var stopContext = new DefaultHttpContext();
        stopContext.Request.Headers["Idempotency-Key"] = "simulator-stop-1";
        stopContext.Request.Headers["If-Match"] = "\"4\"";
        var stopResult = await SimulatorEndpoints.ExecuteAsync(runId, CommandOperationCodes.StopSimulator, stopContext.Request,
            commands, executor, principal, transactionFactory, CancellationToken.None);
        assertions++; if (commands.MutationCalls != 4 || commands.LastOperationCode != "Simulator.Stop.v1")
            failures.Add("Simulator Stop must invoke correct operation code");
        // Run query
        var query = await commands.GetRunAsync(runId, principal.Current!, CancellationToken.None);
        assertions++; if (query is not null && query.ToString()!.Contains("Running", StringComparison.Ordinal)) { } else failures.Add("Simulator query must return status");
        // Missing Idempotency-Key
        var noKeyContext = new DefaultHttpContext();
        var noKeyResult = await SimulatorEndpoints.ExecuteAsync(runId, CommandOperationCodes.StartSimulator, noKeyContext.Request,
            commands, executor, principal, transactionFactory, CancellationToken.None);
        assertions++; if (noKeyResult is Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult keyProblem && keyProblem.StatusCode == 400) { } else failures.Add("Missing Idempotency-Key must return 400");
        // Missing/malformed expected version on lifecycle mutations
        var noVersionContext = new DefaultHttpContext();
        noVersionContext.Request.Headers["Idempotency-Key"] = "simulator-pause-no-version";
        var noVersionResult = await SimulatorEndpoints.ExecuteAsync(runId, CommandOperationCodes.PauseSimulator,
            noVersionContext.Request, commands, executor, principal, transactionFactory, CancellationToken.None);
        assertions++; if (noVersionResult is not Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult noVersionProblem ||
            noVersionProblem.StatusCode != 400) failures.Add("Pause without If-Match must return 400");
        var malformedVersionContext = new DefaultHttpContext();
        malformedVersionContext.Request.Headers["Idempotency-Key"] = "simulator-stop-bad-version";
        malformedVersionContext.Request.Headers["If-Match"] = "not-a-version";
        var malformedVersionResult = await SimulatorEndpoints.ExecuteAsync(runId, CommandOperationCodes.StopSimulator,
            malformedVersionContext.Request, commands, executor, principal, transactionFactory, CancellationToken.None);
        assertions++; if (malformedVersionResult is not Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult malformedProblem ||
            malformedProblem.StatusCode != 400) failures.Add("Stop with malformed If-Match must return 400");
        // Unauthorized principal
        var nullPrincipal = new ServerPrincipalAccessor(null!);
        var unauthContext = new DefaultHttpContext();
        unauthContext.Request.Headers["Idempotency-Key"] = "unauth-1";
        var unauthResult = await SimulatorEndpoints.ExecuteAsync(runId, CommandOperationCodes.StartSimulator, unauthContext.Request,
            commands, executor, nullPrincipal, transactionFactory, CancellationToken.None);
        assertions++; if (unauthResult is Microsoft.AspNetCore.Http.HttpResults.UnauthorizedHttpResult) { } else failures.Add("Null principal must return 401");
        TestCount = assertions; AssertionCount = assertions;
        FailureCount = failures.Count;
        return failures;
    }

    private sealed class ServerPrincipalAccessor(ServerPrincipal current) : IServerPrincipalAccessor
    {
        public ServerPrincipal? Current { get; } = current;
    }
}
