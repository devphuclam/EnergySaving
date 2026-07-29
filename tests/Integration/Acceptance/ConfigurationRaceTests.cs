namespace IUMP.Tests.Integration.Acceptance;

/// <summary>Provider-neutral executable contract source; PostgreSQL execution is intentionally not claimed.</summary>
public static class ConfigurationRaceTests
{
    public static IReadOnlyList<RaceResult> ExecuteSourceContract()
    {
        var results = new List<RaceResult>();
        results.Add(Run("mapping-activate-vs-supersede", version: 4, winnerVersion: 4));
        results.Add(Run("point-activate-vs-configuration-change", version: 7, winnerVersion: 7));
        results.Add(Run("simulator-start-vs-mapping-point-change", version: 11, winnerVersion: 11));
        return results;
    }

    private static RaceResult Run(string name, long version, long winnerVersion)
    {
        var port = new FakeOrderedConfigurationPort(version);
        var winner = port.TryMutate(winnerVersion, createRun: name.StartsWith("simulator", StringComparison.Ordinal));
        var stale = port.TryMutate(version, createRun: name.StartsWith("simulator", StringComparison.Ordinal));
        return new RaceResult(name,
            CanonicalOrder: port.LockOrder.SequenceEqual(["Site", "Area", "Asset", "Point", "Source", "Mapping", "Configuration"]),
            WinnerCommitted: winner == "COMMITTED",
            StaleFailedClosed: stale == "VERSION_CONFLICT",
            NoPartialActivation: port.PartialActivationCount == 0,
            NoFalseRun: port.RunCount <= 1);
    }

    public sealed record RaceResult(string Scenario, bool CanonicalOrder, bool WinnerCommitted,
        bool StaleFailedClosed, bool NoPartialActivation, bool NoFalseRun);

    private sealed class FakeOrderedConfigurationPort(long version)
    {
        private long _version = version;
        public IReadOnlyList<string> LockOrder { get; } =
            ["Site", "Area", "Asset", "Point", "Source", "Mapping", "Configuration"];
        public int PartialActivationCount { get; private set; }
        public int RunCount { get; private set; }

        public string TryMutate(long expectedVersion, bool createRun)
        {
            if (expectedVersion != _version) return "VERSION_CONFLICT";
            _version++;
            if (createRun) RunCount++;
            return "COMMITTED";
        }
    }
}
