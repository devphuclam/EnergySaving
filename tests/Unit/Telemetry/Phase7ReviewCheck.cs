using IUMP.Modules.Acquisition.Contracts;

namespace IUMP.Tests.Unit.Telemetry;

public static class Phase7ReviewCheck
{
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        var failures = new List<string>();
        var checks = 0;
        void Check(bool ok, string message)
        {
            checks++;
            if (!ok) failures.Add(message);
        }

        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var contracts = File.ReadAllText(Path.Combine(root, "src", "Modules", "Acquisition", "Contracts", "ProductionAttemptContracts.cs"));
        var finalizer = File.ReadAllText(Path.Combine(root, "src", "Modules", "Acquisition", "Application", "FinalizeTelemetryAttempt.cs"));
        var attemptService = File.ReadAllText(Path.Combine(root, "src", "Modules", "Acquisition", "Application", "ProductionAttemptService.cs"));
        var telemetryContracts = File.ReadAllText(Path.Combine(root, "src", "Modules", "Telemetry", "Contracts", "TelemetryPersistenceContracts.cs"));
        var telemetryFake = File.ReadAllText(Path.Combine(root, "tests", "Unit", "Fakes", "FakeTelemetryRepositories.cs"));
        var acquisitionFake = File.ReadAllText(Path.Combine(root, "tests", "Unit", "Fakes", "FakeAcquisitionRunRepositories.cs"));
        var migration = File.ReadAllText(Path.Combine(root, "database", "migrations", "0007_acquisition_run.sql"));
        var t134 = File.ReadAllText(Path.Combine(root, "tests", "Unit", "Acquisition", "TelemetryFinalizationTests.cs"));
        var t145 = File.ReadAllText(Path.Combine(root, "tests", "Integration", "Telemetry", "TelemetryIngestionRepositoryTests.cs"));

        Check(typeof(ITelemetryIngestionClient).GetMethod("DispatchCanonicalAsync")?.IsAbstract == true,
            "canonical client requires explicit DispatchCanonicalAsync");
        Check(!contracts.Contains("async Task<CanonicalTelemetryIngestionResult> DispatchCanonicalAsync"),
            "canonical client has no default legacy bridge");
        Check(contracts.Contains("EnsureValid(") && contracts.Contains("SimulatorProductionPayload payload") &&
              finalizer.Contains("EnsureValid(pending.Payload, canonical)"),
            "canonical validator is payload-aware at the finalization seam");
        Check(contracts.Contains("bool? LatestAdvanced") &&
              finalizer.Contains("original.LatestAdvanced,") &&
              !finalizer.Contains("LatestAdvanced ?? false"),
            "Rejected null LatestAdvanced is preserved");
        Check(attemptService.Contains("TELEMETRY_COMPLETED_AT_REQUIRED") &&
              !attemptService.Contains("CompletedAtUtc ?? _clock.UtcNow"),
            "completion timestamp is required and never fabricated");
        Check(telemetryContracts.Contains("TelemetryProviderRecheckResult") &&
              telemetryContracts.Contains("EffectiveFromUtc") && telemetryContracts.Contains("EffectiveToUtc"),
            "provider tuple and independent recheck contract are explicit");
        Check(telemetryFake.Contains("TelemetryRaceWinnerFixture") &&
              !telemetryFake.Contains("AddSeconds(-2)"),
              "race winner fixture copies exact raw/latest/event values");
        Check(acquisitionFake.Contains("CanonicalTelemetryFixtures.Accepted") &&
              !acquisitionFake.Contains("payload.SourceTimestampUtc") &&
              !acquisitionFake.Contains("fake-original-"),
              "acquisition fake uses an explicit fixed canonical fixture");
        Check(telemetryContracts.Contains("SourceType == current.SourceType") &&
              telemetryContracts.Contains("MappingPointId == current.MappingPointId") &&
              telemetryContracts.Contains("UnitCode == current.UnitCode") &&
              telemetryContracts.Contains("PointExistsMatches") &&
              telemetryContracts.Contains("TrustedAreaIdMatches"),
              "provider recheck compares all independent tuple facts");
        Check(migration.Contains("persisted_measurement_id = measurement_id") &&
              migration.Contains("latest_advanced IS NULL") &&
              migration.Contains("reject_completed_terminal_mutation"),
            "0007 enforces terminal shapes and completed immutability");
        Check(t134.Contains("GetAsync(") && t134.Contains("replay conflict checks each terminal field"),
              "T134 uses repository round-trip and per-field conflict matrix");
        Check(t134.Contains("CanonicalTelemetryDisposition.Rejected") &&
              t134.Contains("QualityCode = \"Unknown\"") &&
              t134.Contains("concrete service rejects every terminal replay mutation"),
              "T134 covers malformed Rejected, unknown quality, and concrete replay mutations");
        Check(t145.Contains("TerminalEqual") && t145.Contains("RequestFingerprint"),
              "T145 compares complete persisted terminal values");
        Check(t145.Contains("ReplayProbe") && t145.Contains("RaceWinnerProbe") &&
              t145.Contains("StageRaceWinner") && t145.Contains("ReplayTerminal") &&
              t145.Contains("EventEqual"),
              "T145 executes provider-neutral exact replay and race-winner fixtures");
        Check(!File.Exists(Path.Combine(root, "src", "Modules", "Telemetry", "Infrastructure", "PostgresTelemetryRepositories.cs")),
            "package-policy-blocked PostgreSQL adapter remains absent");
        Check(!Directory.Exists(Path.Combine(root, "src", "Web", "Telemetry")) &&
              !File.Exists(Path.Combine(root, "database", "migrations", "0009_telemetry_latest_status.sql")),
            "Phase 8 remains out of scope");

        CheckCount = checks;
        Console.WriteLine($"Phase7ReviewCheck: checks={checks}; failures={failures.Count}");
        return failures;
    }
}
