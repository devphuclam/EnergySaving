using IUMP.Api;

namespace IUMP.Tests.Unit.Api;

public static class SimulatorEndpointTests
{
    public static List<string> Run() =>
        SimulatorEndpointPolicy.MutationOperations.Contains("Simulator.Start.v1")
            ? new List<string>() : new List<string> { "Simulator Start must be registered as an idempotent mutation" };
}
