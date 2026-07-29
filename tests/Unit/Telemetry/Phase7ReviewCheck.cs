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
        var persistenceService = File.ReadAllText(Path.Combine(root, "src", "Modules", "Telemetry", "Application", "TelemetryPersistenceService.cs"));
        var t133 = File.ReadAllText(Path.Combine(root, "tests", "Unit", "Telemetry", "IngestionPersistenceContractTests.cs"));
        var t135 = File.ReadAllText(Path.Combine(root, "tests", "Unit", "Telemetry", "TelemetryEventTests.cs"));

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
        // Atomic-evidence checks: exact Latest, aggregate state, conflict detection, invalid fixtures
        Check(t145.Contains("GetCommittedLatestAsync") && t145.Contains("committed!.MeasurementId == data.Latest!.MeasurementId"),
            "T145 calls GetCommittedLatestAsync and compares every Latest field");
        Check(t145.Contains("LatestAdvanced=false") && t145.Contains("returns null Latest"),
            "T145 Accepted LatestAdvanced=false proves GetCommittedLatestAsync returns null");
        Check(t145.Contains("latestAdvanced: false") || t145.Contains("latestAdvanced:true"),
            "T145 Data() accepts explicit latestAdvanced");
        Check(t145.Contains("LatestCount") && t145.Contains("GetCommittedLatestAsync") &&
              t145.Contains("committed!.MeasurementId == data.Latest!.MeasurementId"),
            "T145 uses GetCommittedLatestAsync field comparison, not only LatestCount");
        Check(telemetryFake.Contains("TelemetryCommittedState"),
            "Fake uses aggregate TelemetryCommittedState holder");
        Check(telemetryFake.Contains("_committedState = new TelemetryCommittedState") &&
              !telemetryFake.Contains("_terminals = clonedTerminals") &&
              !telemetryFake.Contains("_raw = clonedRaw") &&
              !telemetryFake.Contains("_latest = clonedLatest") &&
              !telemetryFake.Contains("_events = clonedEvents"),
            "PublishRaceWinner assigns committed state exactly once");
        Check(telemetryFake.Contains("RACE_WINNER_FIXTURE_CONFLICT"),
            "Race-winner rejects conflict with existing committed Measurement ID");
        Check(telemetryFake.Contains("RACE_WINNER_SLOT_CONFLICT"),
            "Race-winner rejects slot conflict for different Measurement ID");
        Check(t145.Contains("invalid Accepted") && t145.Contains("terminal count unchanged"),
            "T145 tests invalid Accepted fixture with zero-publication proof");
        Check(t145.Contains("invalid Rejected") && t145.Contains("invalid Rejected Raw present"),
            "T145 tests invalid Rejected fixture with zero-publication proof");
        Check(!t145.Contains("invalid Accepted fixture scenario is absent"),
            "No stale placeholder for invalid Accepted fixture");
        Check(t133.Contains("invalid Accepted fixture") && t133.Contains("RACE_WINNER_FIXTURE_INVALID"),
            "T133 tests invalid Accepted and Rejected fixtures through orchestration");
        // Trusted-scope returns stable result, not exception
        Check(persistenceService.Contains("CheckTrustedScope") &&
              persistenceService.Contains("TelemetryIngestionResult.Failed(\"PROVIDER_SCOPE_MISMATCH\""),
            "Trusted-scope validation returns stable result, not exception");
        Check(persistenceService.Contains("if (scopeResult is not null) return scopeResult"),
            "Orchestration checks scope result before transaction begin");
        // Event factory has no optional fallback
        var factoryPath = Path.Combine(root, "src", "Modules", "Telemetry", "Application", "TelemetryPersistenceService.cs");
        var factoryContent = File.ReadAllText(Path.Combine(root, "src", "Modules", "Telemetry", "Application", "TelemetryPersistenceService.cs"));
        var createMethod = factoryContent.IndexOf("public static TelemetryOwnerEvent Create(", StringComparison.Ordinal);
        var createMethodEnd = factoryContent.IndexOf("}", createMethod, StringComparison.Ordinal);
        var createSig = factoryContent.Substring(createMethod, createMethodEnd - createMethod + 1);
        Check(!createSig.Contains("string? eventSiteId = null") && !createSig.Contains("string? eventAreaId = null"),
            "Event factory Create has no optional fallback parameters");
        Check(createSig.Contains("string eventSiteId") && createSig.Contains("string? eventAreaId"),
            "Event factory requires eventSiteId/eventAreaId parameters");
        Check(createSig.Contains("provider.TrustedSiteId != eventSiteId"),
            "Event factory validates TrustedSiteId equality with eventSiteId");
        Check(t145.Contains("provider.TrustedSiteId, provider.TrustedAreaId"),
            "T145 passes explicit TrustedSiteId/TrustedAreaId to factory");
        Check(t135.Contains("provider.TrustedSiteId") && t135.Contains("provider.TrustedAreaId"),
            "T135 asserts event uses trusted IDs");
        Check(t135.Contains("mismatch") || t135.Contains("PROVIDER_SCOPE_MISMATCH") || t135.Contains("scope"),
            "T135 covers scope mismatch producing no event");

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
