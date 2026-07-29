using IUMP.Api;

namespace IUMP.Tests.Unit.Api;

public static class SimulatorEndpointTests
{
    public const int TestCount = 5;
    public const int AssertionCount = 9;
    public static int FailureCount { get; private set; }

    public static List<string> Run()
    {
        var failures = SimulatorEndpointPolicy.MutationOperations.Contains("Simulator.Start.v1")
            ? new List<string>() : new List<string> { "Simulator Start must be registered as an idempotent mutation" };
        if (!SimulatorEndpointPolicy.MutationOperations.Contains("Simulator.Pause.v1") ||
            !SimulatorEndpointPolicy.MutationOperations.Contains("Simulator.Resume.v1") ||
            !SimulatorEndpointPolicy.MutationOperations.Contains("Simulator.Stop.v1"))
            failures.Add("Start/Pause/Resume/Stop handlers must share the executor and server scope");
        FailureCount = failures.Count;
        return failures;
    }
}
