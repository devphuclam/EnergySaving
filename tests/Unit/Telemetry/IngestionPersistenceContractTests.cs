using IUMP.Modules.Telemetry.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Telemetry;

public static class IngestionPersistenceContractTests
{
    public static int TestCount { get; private set; }
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        TestCount = 0;
        CheckCount = 0;
        var failures = new List<string>();
        Case("Accepted commits registry raw Latest event atomically", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            var result = IngestionOrchestrationTests.Execute(system, TelemetryTestData.Request());
            Check(result.Disposition == TelemetryDisposition.Accepted, "Accepted disposition", failures);
            Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 1, "one registry", failures);
            Check(system.Store.ListCommittedRawAsync().Result.Count == 1, "one raw", failures);
            Check(system.Store.LatestCount == 1, "one Latest", failures);
            Check(system.Store.ListCommittedAsync().Result.Count == 1, "one event", failures);
            Check(system.Store.LastLockTrace.Select(item => item.Target).SequenceEqual(
                [
                    TelemetryFlowLockTarget.OrganizationPoint,
                    TelemetryFlowLockTarget.CatalogSourceMappingMetricUnit,
                    TelemetryFlowLockTarget.TelemetryIdentityRawLatest,
                    TelemetryFlowLockTarget.IntegrationOutbox
                ]), "exact lock order", failures);
        });
        Case("Rejected commits registry only", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            system.Providers.Snapshot = TelemetryTestData.Provider() with { PointActive = false };
            var result = IngestionOrchestrationTests.Execute(system, TelemetryTestData.Request());
            Check(result.Disposition == TelemetryDisposition.Rejected, "Rejected disposition", failures);
            Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 1, "Rejected registry", failures);
            Check(system.Store.ListCommittedRawAsync().Result.Count == 0, "Rejected no raw", failures);
            Check(system.Store.LatestCount == 0, "Rejected no Latest", failures);
            Check(system.Store.ListCommittedAsync().Result.Count == 0, "Rejected no event", failures);
        });
        Case("pre-commit changes are invisible", failures, () =>
        {
            var store = new FakeTelemetryRepositories();
            var request = TelemetryTestData.Request();
            var terminal = TelemetryTestData.Terminal(request, TelemetryFinalClassification.Accepted);
            var raw = new RawMeasurement(
                terminal.MeasurementId, request.SourceId, request.SimulatorRunId,
                request.PointId, request.MappingId, request.MappingVersion,
                request.SourceSequence, request.SourceTimestampUtc,
                TelemetryTestData.Now, TelemetryTestData.Now, request.NumericValue,
                request.UnitCode, MeasurementQuality.Good, null,
                request.CorrelationId, request.LineageId);
            var tx = store.BeginRepeatableReadAsync().Result;
            store.StageTerminalAsync(terminal, tx).GetAwaiter().GetResult();
            store.StageRawAsync(raw, tx).GetAwaiter().GetResult();
            Check(store.ListCommittedTerminalsAsync().Result.Count == 0, "precommit invisible", failures);
            Check(store.ListCommittedRawAsync().Result.Count == 0, "precommit raw invisible", failures);
            tx.CommitAsync().GetAwaiter().GetResult();
            Check(store.ListCommittedTerminalsAsync().Result.Count == 1, "postcommit visible", failures);
            Check(store.ListCommittedRawAsync().Result.Count == 1, "postcommit raw visible", failures);
            tx.DisposeAsync().GetAwaiter().GetResult();
        });
        Case("every stage failure rolls back all local state", failures, () =>
        {
            foreach (var point in new[]
                     {
                         TelemetryFakeFailure.OrganizationLock,
                         TelemetryFakeFailure.CatalogLock,
                         TelemetryFakeFailure.TerminalInsert,
                         TelemetryFakeFailure.RawInsert,
                         TelemetryFakeFailure.Latest,
                         TelemetryFakeFailure.Outbox,
                         TelemetryFakeFailure.Commit
                     })
            {
                var system = IngestionOrchestrationTests.Create();
                system.Store.Failure = point;
                try
                {
                    IngestionOrchestrationTests.Execute(system, TelemetryTestData.Request());
                    failures.Add($"{point} did not fail");
                }
                catch (InvalidOperationException) { CheckCount++; }
                Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 0, $"{point} registry rollback", failures);
                Check(system.Store.ListCommittedRawAsync().Result.Count == 0, $"{point} raw rollback", failures);
                Check(system.Store.LatestCount == 0, $"{point} latest rollback", failures);
                Check(system.Store.ListCommittedAsync().Result.Count == 0, $"{point} event rollback", failures);
            }
        });
        Case("matching unique-race winner returns Duplicate", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            var request = TelemetryTestData.Request();
            system.Store.RaceWinnerOnStage =
                TelemetryTestData.Terminal(request, TelemetryFinalClassification.Accepted);
            var result = IngestionOrchestrationTests.Execute(system, request);
            Check(result.Disposition == TelemetryDisposition.Duplicate, "race duplicate", failures);
            Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 1, "winner only", failures);
            Check(system.Store.ListCommittedRawAsync().Result.Count == 1, "winner raw only", failures);
            Check(system.Store.LatestCount == 1, "winner Latest only", failures);
            Check(system.Store.ListCommittedAsync().Result.Count == 1, "winner event only", failures);
        });
        Case("conflicting unique-race winner returns conflict", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            var request = TelemetryTestData.Request();
            var winner = TelemetryTestData.Terminal(
                request with { NumericValue = 44 }, TelemetryFinalClassification.Accepted);
            system.Store.RaceWinnerOnStage = winner;
            var result = IngestionOrchestrationTests.Execute(system, request);
            Check(result.ErrorCode == "IDEMPOTENCY_CONFLICT", "race conflict", failures);
            Check(system.Store.ListCommittedRawAsync().Result.Count == 1, "conflict winner raw only", failures);
            Check(system.Store.ListCommittedAsync().Result.Count == 1, "conflict winner event only", failures);
        });
        Case("different-ID slot-race winner returns slot conflict", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            var winnerRequest = TelemetryTestData.Request();
            system.Store.RaceWinnerOnStage =
                TelemetryTestData.Terminal(
                    winnerRequest, TelemetryFinalClassification.Accepted);
            var mapping = Guid.Parse("66666666-6666-4666-8666-666666666666");
            var loserRequest = IngestionOrchestrationTests.WithIdentity(
                winnerRequest with { MappingId = mapping });
            system.Providers.Snapshot =
                TelemetryTestData.Provider() with { MappingId = mapping };
            var result = IngestionOrchestrationTests.Execute(system, loserRequest);
            Check(result.ErrorCode == "MEASUREMENT_SLOT_CONFLICT", "slot race conflict", failures);
            Check(system.Store.ListCommittedTerminalsAsync().Result.Count == 1, "slot winner only", failures);
        });
        Case("Bad bypasses Latest seam and still persists raw", failures, () =>
        {
            var system = IngestionOrchestrationTests.Create();
            system.Store.Failure = TelemetryFakeFailure.Latest;
            var result = IngestionOrchestrationTests.Execute(
                system, TelemetryTestData.Request() with { NumericValue = 101 });
            Check(result.Disposition == TelemetryDisposition.Accepted, "Bad accepted", failures);
            Check(result.OriginalResult?.LatestAdvanced == false, "Bad false", failures);
            Check(system.Store.ListCommittedRawAsync().Result.Count == 1, "Bad raw", failures);
        });
        return failures;
    }

    private static void Case(string name, List<string> failures, Action action)
    {
        TestCount++;
        try { action(); }
        catch (Exception ex) { failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}"); }
    }

    private static void Check(bool condition, string message, List<string> failures)
    {
        CheckCount++;
        if (!condition) failures.Add(message);
    }
}
