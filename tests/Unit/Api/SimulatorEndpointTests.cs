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
            failures.Add("Start/Pause/Resume/Stop handlers must share the executor and server scope");
        var store = new FakeCommandIdempotencyStore();
        var executor = new IdempotentCommandExecutor(store);
        var principal = new ServerPrincipalAccessor(new ServerPrincipal(Guid.NewGuid(), "engineer", new HashSet<string> { "site-1" }, new HashSet<string>()));
        var transactionFactory = new FakePhase9TransactionFactory();
        var commands = new FakeSimulatorPorts();
        var context = new DefaultHttpContext();
        context.Request.Headers["Idempotency-Key"] = "simulator-1";
        var runId = Guid.NewGuid();
        var result = await SimulatorEndpoints.ExecuteAsync(runId, CommandOperationCodes.StartSimulator, context.Request,
            commands, executor, principal, transactionFactory, CancellationToken.None);
        assertions++; if (commands.MutationCalls != 1 || commands.LastRunId != runId || transactionFactory.BeginCount != 1 || result is not IdempotentHttpResult)
            failures.Add("Simulator Start must invoke the command port with server principal and host transaction");
        var query = await commands.GetRunAsync(runId, principal.Current!, CancellationToken.None);
        assertions++; if (!query.ToString()!.Contains("Running", StringComparison.Ordinal)) failures.Add("Simulator query must return provider-neutral run status/counters");
        TestCount = 4; AssertionCount = assertions;
        FailureCount = failures.Count;
        return failures;
    }

    private sealed class ServerPrincipalAccessor(ServerPrincipal current) : IServerPrincipalAccessor
    {
        public ServerPrincipal? Current { get; } = current;
    }
}
