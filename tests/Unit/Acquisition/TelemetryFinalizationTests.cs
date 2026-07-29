using IUMP.Modules.Acquisition.Application;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Acquisition.Domain;
using IUMP.Tests.Unit.Fakes;
using IUMP.Tests.Unit.Telemetry;

namespace IUMP.Tests.Unit.Acquisition;

public static class TelemetryFinalizationTests
{
    public static int TestCount { get; private set; }
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        TestCount = 0;
        CheckCount = 0;
        var failures = new List<string>();
        Case("Accepted finalizes once and increments Accepted only", failures, () =>
        {
            var result = Dispatch(Accepted());
            Check(result.Result.FirstTransition, "first transition", failures);
            Check(result.Service.AcceptedCount == 1 && result.Service.RejectedCount == 0,
                "Accepted counter only", failures);
            Check(result.Result.Attempt.FinalClassification == ProductionFinalClassification.Accepted,
                "Accepted classification", failures);
            Check(result.Result.TelemetryResult.QualityCode == "Good" &&
                  result.Result.TelemetryResult.PersistedMeasurementId ==
                  Guid.Parse("aaaaaaaa-aaaa-5aaa-8aaa-aaaaaaaaaaaa") &&
                  result.Result.TelemetryResult.OriginalCorrelationId == "original-correlation" &&
                  result.Result.TelemetryResult.OriginalLineageId == "original-lineage",
                "canonical original result preserved", failures);
        });
        Case("Rejected finalizes once and increments Rejected only", failures, () =>
        {
            var result = Dispatch(Rejected());
            Check(result.Result.FirstTransition, "first transition", failures);
            Check(result.Service.AcceptedCount == 0 && result.Service.RejectedCount == 1,
                "Rejected counter only", failures);
            Check(result.Result.Attempt.FinalClassification == ProductionFinalClassification.Rejected,
                "Rejected classification", failures);
        });
        Case("Duplicate Accepted uses original classification", failures, () =>
        {
            var result = Dispatch(Accepted() with { Outcome = TelemetryAttemptOutcome.Duplicate });
            Check(result.Service.AcceptedCount == 1 && result.Service.RejectedCount == 0,
                "Duplicate Accepted counts once", failures);
            Check(result.Result.TelemetryResult.Outcome == TelemetryAttemptOutcome.Duplicate,
                "Duplicate disposition stored", failures);
        });
        Case("Duplicate Rejected uses original classification", failures, () =>
        {
            var result = Dispatch(Rejected() with { Outcome = TelemetryAttemptOutcome.Duplicate });
            Check(result.Service.AcceptedCount == 0 && result.Service.RejectedCount == 1,
                "Duplicate Rejected counts once", failures);
        });
        Case("crash after terminal commit leaves Pending", failures, () =>
        {
            var pending = Pending();
            var service = new FinalizationAttemptService(pending);
            var client = new DispatchClient(Accepted()) { ThrowAfterTerminal = true };
            var coordinator = new FinalizeTelemetryAttempt(service, client);
            try
            {
                coordinator.ExecuteAsync(pending).GetAwaiter().GetResult();
                failures.Add("crash injection did not fail");
            }
            catch (InvalidOperationException) { CheckCount++; }
            Check(service.Current.Status == SimulatorProductionAttemptStatus.Pending,
                "attempt remains Pending", failures);
            Check(service.AcceptedCount == 0 && service.RejectedCount == 0,
                "counters unchanged", failures);
        });
        Case("retry Duplicate finalizes once", failures, () =>
        {
            var pending = Pending();
            var stable = Accepted() with { Outcome = TelemetryAttemptOutcome.Duplicate };
            var service = new FinalizationAttemptService(pending);
            var coordinator = new FinalizeTelemetryAttempt(service, new DispatchClient(stable));
            var first = coordinator.ExecuteAsync(pending).GetAwaiter().GetResult();
            var replay = coordinator.ExecuteAsync(pending).GetAwaiter().GetResult();
            Check(first.FirstTransition && replay.Replay, "first then replay", failures);
            Check(service.AcceptedCount == 1, "counter exactly once", failures);
        });
        Case("same terminal replay is no-op", failures, () =>
        {
            var result = Dispatch(Accepted());
            var replay = new FinalizeTelemetryAttempt(
                result.Service, new DispatchClient(Accepted()))
                .ExecuteAsync(Pending()).GetAwaiter().GetResult();
            Check(replay.Replay && !replay.FirstTransition, "same replay", failures);
            Check(result.Service.AcceptedCount == 1, "no second increment", failures);
        });
        Case("different terminal replay is invariant conflict", failures, () =>
        {
            var result = Dispatch(Accepted());
            try
            {
                new FinalizeTelemetryAttempt(result.Service, new DispatchClient(Rejected()))
                    .ExecuteAsync(Pending()).GetAwaiter().GetResult();
                failures.Add("different replay did not fail");
            }
            catch (InvalidOperationException ex)
            {
                Check(ex.Message == "TERMINAL_RESULT_CONFLICT", "conflict code", failures);
            }
        });
        Case("finalization does not alter generation payload", failures, () =>
        {
            var pending = Pending();
            var originalPayload = pending.Payload;
            var result = Dispatch(Accepted(), pending);
            Check(result.Result.Attempt.Payload == originalPayload, "payload unchanged", failures);
            Check(result.Result.Attempt.SourceSequence == pending.SourceSequence,
                "sequence unchanged", failures);
        });
        Case("finalization rollback keeps Pending and counters", failures, () =>
        {
            var pending = Pending();
            var service = new FinalizationAttemptService(pending) { FailFinalize = true };
            try
            {
                new FinalizeTelemetryAttempt(service, new DispatchClient(Accepted()))
                    .ExecuteAsync(pending).GetAwaiter().GetResult();
                failures.Add("finalization failure did not throw");
            }
            catch (InvalidOperationException) { CheckCount++; }
            Check(service.Current.Status == SimulatorProductionAttemptStatus.Pending,
                "Pending preserved", failures);
            Check(service.AcceptedCount == 0 && service.RejectedCount == 0,
                "counters preserved", failures);
        });
        Case("payload-aware canonical validation rejects persisted ID mismatch", failures, () =>
        {
            var badId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
            var result = Accepted() with { PersistedMeasurementId = badId };
            try
            {
                new FinalizeTelemetryAttempt(
                    new FinalizationAttemptService(Pending()),
                    new DispatchClient(result)).ExecuteAsync(Pending()).GetAwaiter().GetResult();
                failures.Add("persisted ID mismatch accepted");
            }
            catch (InvalidOperationException ex) when (ex.Message == "CANONICAL_ORIGINAL_RESULT_INVALID") { }
        });
        Case("canonical validation rejects non-UTC completion", failures, () =>
        {
            var result = Accepted() with
            {
                CompletedAtUtc = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Local)
            };
            try
            {
                new FinalizeTelemetryAttempt(
                    new FinalizationAttemptService(Pending()),
                    new DispatchClient(result)).ExecuteAsync(Pending()).GetAwaiter().GetResult();
                failures.Add("non-UTC completion accepted");
            }
            catch (InvalidOperationException) { }
        });
        Case("Rejected preserves nullable LatestAdvanced", failures, () =>
        {
            var result = Rejected() with { LatestAdvanced = false };
            try
            {
                new FinalizeTelemetryAttempt(
                    new FinalizationAttemptService(Pending()),
                    new DispatchClient(result)).ExecuteAsync(Pending()).GetAwaiter().GetResult();
                failures.Add("Rejected false LatestAdvanced accepted");
            }
            catch (InvalidOperationException) { }
        });
        Case("Rejected canonical result maps LatestAdvanced to null", failures, () =>
        {
            var result = Dispatch(Rejected()).Result.TelemetryResult;
            Check(result.LatestAdvanced is null, "Rejected LatestAdvanced is null", failures);
        });
        Case("null completion is rejected without a clock fallback", failures, () =>
        {
            var result = Accepted() with { CompletedAtUtc = null };
            try
            {
                var client = new DispatchClient(result);
                _ = new FinalizeTelemetryAttempt(
                    new FinalizationAttemptService(Pending()), client)
                    .ExecuteAsync(Pending()).GetAwaiter().GetResult();
                failures.Add("null completion accepted");
            }
            catch (InvalidOperationException) { }
        });
        Case("blank provenance is rejected", failures, () =>
        {
            var result = Accepted() with { OriginalCorrelationId = "", OriginalLineageId = "" };
            try
            {
                _ = new FinalizeTelemetryAttempt(
                    new FinalizationAttemptService(Pending()), new DispatchClient(result))
                    .ExecuteAsync(Pending()).GetAwaiter().GetResult();
                failures.Add("blank provenance accepted");
            }
            catch (InvalidOperationException) { }
        });
        Case("client contract exposes only explicit canonical dispatch", failures, () =>
        {
            Check(typeof(ITelemetryIngestionClient).GetMethod("DispatchAsync") is null,
                "legacy DispatchAsync is not part of canonical client", failures);
            Check(typeof(ITelemetryIngestionClient).GetMethod("DispatchCanonicalAsync") is not null,
                "canonical dispatch is required", failures);
        });
        Case("fake client returns an explicit complete fixture", failures, () =>
        {
            var pending = Pending();
            var canonical = new FakeTelemetryIngestionClient()
                .DispatchCanonicalAsync(pending.Payload).GetAwaiter().GetResult();
            Check(canonical.OriginalResult.PersistedMeasurementId == pending.Payload.MeasurementId &&
                  canonical.OriginalResult.CompletedAtUtc?.Kind == DateTimeKind.Utc &&
                  !string.IsNullOrWhiteSpace(canonical.OriginalResult.OriginalCorrelationId) &&
                  !string.IsNullOrWhiteSpace(canonical.OriginalResult.OriginalLineageId),
                "fake canonical fixture is complete", failures);
        });
        Case("concrete repository round-trip preserves every terminal field", failures, () =>
        {
            var runId = Guid.Parse("abababab-abab-4aba-8aba-abababababab");
            var repositories = new FakeAcquisitionRunRepositories();
            var pending = Phase6Fixtures.Pending(runId);
            repositories.Seed(Phase6Fixtures.Run(runId), Phase6Fixtures.Point(runId));
            repositories.SeedAttempt(pending);
            var service = new ProductionAttemptService(
                repositories, repositories, ProductionAttemptTests.ConfigurationRepositoryAsync().GetAwaiter().GetResult(),
                repositories, new DeterministicGenerator(), new MeasurementIdentity(),
                new FakeUtcClock(Phase6Fixtures.Now));
            var result = Accepted() with
            {
                PersistedMeasurementId = pending.Payload.MeasurementId,
                CompletedAtUtc = TelemetryTestData.Now
            };
            var finalized = new FinalizeTelemetryAttempt(service, new DispatchClient(result))
                .ExecuteAsync(pending).GetAwaiter().GetResult();
            var roundTrip = repositories.GetAsync(runId, pending.PointId, pending.SourceSequence)
                .GetAwaiter().GetResult();
            Check(finalized.Attempt == roundTrip &&
                  roundTrip!.TelemetryOutcome == result.Outcome &&
                  roundTrip.FinalClassification == result.FinalClassification &&
                  roundTrip.MeasurementPersisted == result.MeasurementPersisted &&
                  roundTrip.PersistedMeasurementId == result.PersistedMeasurementId &&
                  roundTrip.QualityCode == result.QualityCode &&
                  roundTrip.ReasonCode == result.ReasonCode &&
                  roundTrip.LatestAdvanced == result.LatestAdvanced &&
                  roundTrip.ErrorCode == result.ErrorCode &&
                  roundTrip.RejectionCode == result.RejectionCode &&
                  roundTrip.CompletedAtUtc == result.CompletedAtUtc &&
                  roundTrip.OriginalCorrelationId == result.OriginalCorrelationId &&
                  roundTrip.OriginalLineageId == result.OriginalLineageId,
                "repository round-trip preserves exact terminal result", failures);
        });
        Case("concrete service rejects every terminal replay mutation", failures, () =>
        {
            var runId = Guid.Parse("cdcdcdcd-cdcd-4cdc-8dcd-cdcdcdcdcdcd");
            var repositories = new FakeAcquisitionRunRepositories();
            var pending = Phase6Fixtures.Pending(runId);
            repositories.Seed(Phase6Fixtures.Run(runId), Phase6Fixtures.Point(runId));
            repositories.SeedAttempt(pending);
            var service = new ProductionAttemptService(
                repositories, repositories,
                ProductionAttemptTests.ConfigurationRepositoryAsync().GetAwaiter().GetResult(),
                repositories, new DeterministicGenerator(), new MeasurementIdentity(),
                new FakeUtcClock(Phase6Fixtures.Now));
            var accepted = Accepted() with
            {
                PersistedMeasurementId = pending.Payload.MeasurementId,
                CompletedAtUtc = TelemetryTestData.Now
            };
            service.FinalizeAsync(runId, pending.PointId, pending.SourceSequence, accepted)
                .GetAwaiter().GetResult();
            var variants = new[]
            {
                accepted with { Outcome = TelemetryAttemptOutcome.Duplicate },
                accepted with { FinalClassification = ProductionFinalClassification.Rejected,
                    MeasurementPersisted = false, PersistedMeasurementId = null,
                    QualityCode = null, ReasonCode = null, LatestAdvanced = null,
                    RejectionCode = "POINT_INACTIVE" },
                accepted with { MeasurementPersisted = false },
                accepted with { PersistedMeasurementId = Guid.NewGuid() },
                accepted with { QualityCode = "Uncertain", ReasonCode = "SOURCE_TIMESTAMP_FUTURE" },
                accepted with { QualityCode = "Bad", ReasonCode = "VALUE_OUT_OF_RANGE", LatestAdvanced = false },
                accepted with { LatestAdvanced = false },
                accepted with { ErrorCode = "CHANGED" },
                accepted with { RejectionCode = "CHANGED" },
                accepted with { CompletedAtUtc = TelemetryTestData.Now.AddSeconds(1) },
                accepted with { OriginalCorrelationId = "changed" },
                accepted with { OriginalLineageId = "changed" }
            };
            foreach (var variant in variants)
            {
                try
                {
                    _ = service.FinalizeAsync(
                        runId, pending.PointId, pending.SourceSequence, variant)
                        .GetAwaiter().GetResult();
                    failures.Add("concrete replay mutation was accepted");
                }
                catch (InvalidOperationException ex)
                {
                    Check(ex.Message == "TERMINAL_RESULT_CONFLICT",
                        "concrete replay mutation is an exact conflict", failures);
                }
            }
        });
        Case("replay conflict checks each terminal field", failures, () =>
        {
            var result = Dispatch(Accepted());
            var variants = new[]
            {
                Accepted() with { Outcome = TelemetryAttemptOutcome.Duplicate },
                Accepted() with { FinalClassification = ProductionFinalClassification.Rejected, MeasurementPersisted = false, PersistedMeasurementId = null, QualityCode = null, ReasonCode = null, LatestAdvanced = null, RejectionCode = "POINT_INACTIVE" },
                Accepted() with { MeasurementPersisted = false },
                Accepted() with { PersistedMeasurementId = Guid.NewGuid() },
                Accepted() with { QualityCode = "Uncertain", ReasonCode = "SOURCE_TIMESTAMP_FUTURE" },
                Accepted() with { ReasonCode = "VALUE_OUT_OF_RANGE", QualityCode = "Bad", LatestAdvanced = false },
                Accepted() with { LatestAdvanced = false },
                Accepted() with { ErrorCode = "CHANGED" },
                Accepted() with { RejectionCode = "CHANGED" },
                Accepted() with { CompletedAtUtc = TelemetryTestData.Now.AddSeconds(1) },
                Accepted() with { OriginalCorrelationId = "changed" },
                Accepted() with { OriginalLineageId = "changed" }
            };
            foreach (var variant in variants)
            {
                try
                {
                    _ = new FinalizeTelemetryAttempt(result.Service, new DispatchClient(variant))
                        .ExecuteAsync(Pending()).GetAwaiter().GetResult();
                    failures.Add("replay conflict field was accepted");
                }
                catch (InvalidOperationException ex)
                {
                    Check(ex.Message is "TERMINAL_RESULT_CONFLICT" or "CANONICAL_ORIGINAL_RESULT_INVALID",
                        "replay field conflict code", failures);
                }
            }
        });
        Case("malformed canonical-result matrix fails closed before mutation", failures, () =>
        {
            var pending = Pending();
            var valid = CanonicalAccepted(pending.Payload);
            var rejected = new CanonicalTelemetryIngestionResult(
                CanonicalTelemetryDisposition.Rejected,
                new CanonicalTelemetryOriginalResult(
                    ProductionFinalClassification.Rejected, false, null, null, null,
                    "POINT_INACTIVE", null, TelemetryTestData.Now,
                    "original-correlation", "original-lineage"),
                null, pending.Payload.CorrelationId);
            var malformed = new[]
            {
                valid with { Disposition = CanonicalTelemetryDisposition.Rejected },
                valid with { OriginalResult = valid.OriginalResult with { MeasurementPersisted = false } },
                valid with { OriginalResult = valid.OriginalResult with { PersistedMeasurementId = Guid.NewGuid() } },
                valid with { OriginalResult = valid.OriginalResult with { QualityCode = null } },
                valid with { OriginalResult = valid.OriginalResult with { ReasonCode = "VALUE_OUT_OF_RANGE" } },
                valid with { OriginalResult = valid.OriginalResult with { RejectionCode = "POINT_INACTIVE" } },
                valid with { OriginalResult = valid.OriginalResult with { LatestAdvanced = null } },
                valid with { OriginalResult = valid.OriginalResult with { CompletedAtUtc = null } },
                valid with { OriginalResult = valid.OriginalResult with { CompletedAtUtc = new DateTime(2026, 7, 28, 6, 0, 0, DateTimeKind.Local) } },
                valid with { OriginalResult = valid.OriginalResult with { OriginalCorrelationId = " " } },
                valid with { OriginalResult = valid.OriginalResult with { OriginalLineageId = null } },
                valid with { OriginalResult = valid.OriginalResult with { FinalClassification = ProductionFinalClassification.Rejected } },
                valid with { OriginalResult = valid.OriginalResult with { QualityCode = "Uncertain", ReasonCode = null } },
                valid with { OriginalResult = valid.OriginalResult with { QualityCode = "Bad", ReasonCode = "VALUE_OUT_OF_RANGE", LatestAdvanced = true } },
                valid with { OriginalResult = valid.OriginalResult with { MeasurementPersisted = true, PersistedMeasurementId = null } },
                valid with { OriginalResult = valid.OriginalResult with { RejectionCode = "" } },
                valid with { OriginalResult = valid.OriginalResult with { QualityCode = "Unknown" } },
                rejected with { OriginalResult = rejected.OriginalResult with { LatestAdvanced = false } },
                rejected with { OriginalResult = rejected.OriginalResult with { QualityCode = "Good" } },
                rejected with { OriginalResult = rejected.OriginalResult with { RejectionCode = null } },
                rejected with { Disposition = CanonicalTelemetryDisposition.Accepted },
                valid with { Disposition = (CanonicalTelemetryDisposition)999 }
            };
            foreach (var candidate in malformed)
            {
                var service = new FinalizationAttemptService(pending);
                try
                {
                    _ = new FinalizeTelemetryAttempt(service, new CanonicalFixtureClient(candidate))
                        .ExecuteAsync(pending).GetAwaiter().GetResult();
                    failures.Add("malformed canonical result was accepted");
                }
                catch (InvalidOperationException ex)
                {
                    Check(ex.Message == CanonicalTelemetryOriginalResultValidator.InvalidCode,
                        "malformed canonical result code", failures);
                }
                Check(service.Current.Status == SimulatorProductionAttemptStatus.Pending &&
                      service.AcceptedCount == 0 && service.RejectedCount == 0,
                    "malformed result leaves attempt untouched", failures);
            }
        });
        return failures;
    }

    private static (FinalizeTelemetryAttemptResult Result, FinalizationAttemptService Service)
        Dispatch(TelemetryDispatchResult result, SimulatorProductionAttempt? pending = null)
    {
        var attempt = pending ?? Pending();
        var service = new FinalizationAttemptService(attempt);
        var output = new FinalizeTelemetryAttempt(service, new DispatchClient(result))
            .ExecuteAsync(attempt).GetAwaiter().GetResult();
        return (output, service);
    }

    private static SimulatorProductionAttempt Pending()
    {
        var payload = new SimulatorProductionPayload(
            Guid.Parse("aaaaaaaa-aaaa-5aaa-8aaa-aaaaaaaaaaaa"),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            1, 1, "IUMP-DETERMINISTIC-V1", 1, Guid.NewGuid(), 1,
            new DateTime(2026, 7, 28, 6, 0, 0, DateTimeKind.Utc), 12.5, "kW",
            "IUMP.Acquisition.Simulator.v1", "correlation", "lineage");
        return new SimulatorProductionAttempt(
            payload.RunId, payload.PointId, payload.SourceSequence, payload,
            SimulatorProductionAttemptStatus.Pending, null, null, null, null, null,
            null, null, null, null, payload.SourceTimestampUtc, null, null, null, 1);
    }

    private static TelemetryDispatchResult Accepted() => new(
        TelemetryAttemptOutcome.Accepted, ProductionFinalClassification.Accepted,
        true, true, null, null,
        Guid.Parse("aaaaaaaa-aaaa-5aaa-8aaa-aaaaaaaaaaaa"), "Good", null,
        TelemetryTestData.Now, "original-correlation", "original-lineage");

    private static TelemetryDispatchResult Rejected() => new(
        TelemetryAttemptOutcome.Rejected, ProductionFinalClassification.Rejected,
        false, null, "POINT_INACTIVE", "POINT_INACTIVE", null, null, null,
        TelemetryTestData.Now, "original-correlation", "original-lineage");

    private static CanonicalTelemetryIngestionResult CanonicalAccepted(
        SimulatorProductionPayload payload) => new(
            CanonicalTelemetryDisposition.Accepted,
            new CanonicalTelemetryOriginalResult(
                ProductionFinalClassification.Accepted, true, payload.MeasurementId,
                "Good", null, null, true, TelemetryTestData.Now,
                "original-correlation", "original-lineage"),
            null, payload.CorrelationId);

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

    private sealed class DispatchClient : ITelemetryIngestionClient
    {
        private readonly CanonicalTelemetryIngestionResult _result;
        public bool ThrowAfterTerminal { get; set; }
        public DispatchClient(TelemetryDispatchResult result)
        {
            _result = new CanonicalTelemetryIngestionResult(
                result.Outcome switch
                {
                    TelemetryAttemptOutcome.Accepted => CanonicalTelemetryDisposition.Accepted,
                    TelemetryAttemptOutcome.Rejected => CanonicalTelemetryDisposition.Rejected,
                    TelemetryAttemptOutcome.Duplicate => CanonicalTelemetryDisposition.Duplicate,
                    _ => throw new InvalidOperationException()
                },
                new CanonicalTelemetryOriginalResult(
                    result.FinalClassification,
                    result.MeasurementPersisted ??
                        throw new InvalidOperationException("CANONICAL_FIXTURE_REQUIRED"),
                    result.PersistedMeasurementId,
                    result.QualityCode,
                    result.ReasonCode,
                    result.RejectionCode,
                    result.LatestAdvanced,
                    result.CompletedAtUtc,
                    result.OriginalCorrelationId,
                    result.OriginalLineageId),
                 result.Outcome is TelemetryAttemptOutcome.Duplicate or
                    TelemetryAttemptOutcome.Rejected ? null : result.ErrorCode,
                 "dispatch-correlation");
        }
        public Task<CanonicalTelemetryIngestionResult> DispatchCanonicalAsync(
            SimulatorProductionPayload payload, CancellationToken ct = default)
        {
            if (ThrowAfterTerminal) throw new InvalidOperationException("CRASH_AFTER_TELEMETRY");
            return Task.FromResult(_result with { CorrelationId = payload.CorrelationId });
        }
    }

    private sealed class CanonicalFixtureClient : ITelemetryIngestionClient
    {
        private readonly CanonicalTelemetryIngestionResult _result;
        public CanonicalFixtureClient(CanonicalTelemetryIngestionResult result) => _result = result;
        public Task<CanonicalTelemetryIngestionResult> DispatchCanonicalAsync(
            SimulatorProductionPayload payload, CancellationToken ct = default) =>
            Task.FromResult(_result);
    }

    private sealed class FinalizationAttemptService : IProductionAttemptService
    {
        private TelemetryDispatchResult? _terminal;
        public SimulatorProductionAttempt Current { get; private set; }
        public long AcceptedCount { get; private set; }
        public long RejectedCount { get; private set; }
        public bool FailFinalize { get; set; }

        public FinalizationAttemptService(SimulatorProductionAttempt current) => Current = current;
        public Task<SimulatorProductionAttempt?> LoadPendingAsync(
            Guid runId, Guid pointId, CancellationToken ct = default) =>
            Task.FromResult<SimulatorProductionAttempt?>(
                Current.Status == SimulatorProductionAttemptStatus.Pending ? Current : null);
        public Task<AttemptReserveResult> ReserveAsync(
            Guid runId, Guid pointId, string correlationId, string lineageId,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AttemptFinalizeResult> FinalizeAsync(
            Guid runId, Guid pointId, long sourceSequence, TelemetryDispatchResult result,
            CancellationToken ct = default)
        {
            if (FailFinalize) throw new InvalidOperationException("FINALIZE_ROLLBACK");
            if (_terminal is not null)
            {
                if (!TerminalResultsEqual(_terminal, result))
                    throw new InvalidOperationException("TERMINAL_RESULT_CONFLICT");
                return Task.FromResult(new AttemptFinalizeResult(Current, false, true));
            }
            _terminal = result;
            if (result.FinalClassification == ProductionFinalClassification.Accepted) AcceptedCount++;
            else RejectedCount++;
            Current = Current with
            {
                Status = SimulatorProductionAttemptStatus.Completed,
                TelemetryOutcome = result.Outcome,
                FinalClassification = result.FinalClassification,
                MeasurementPersisted = result.MeasurementPersisted,
                PersistedMeasurementId = result.PersistedMeasurementId,
                QualityCode = result.QualityCode,
                ReasonCode = result.ReasonCode,
                LatestAdvanced = result.LatestAdvanced,
                ErrorCode = result.ErrorCode,
                RejectionCode = result.RejectionCode,
                CompletedAtUtc = result.CompletedAtUtc ?? throw new InvalidOperationException("TELEMETRY_COMPLETED_AT_REQUIRED"),
                OriginalCorrelationId = result.OriginalCorrelationId,
                OriginalLineageId = result.OriginalLineageId,
                Version = Current.Version + 1
            };
            return Task.FromResult(new AttemptFinalizeResult(Current, true, false));
        }

        private static bool TerminalResultsEqual(TelemetryDispatchResult left, TelemetryDispatchResult right) =>
            left.Outcome == right.Outcome &&
            left.FinalClassification == right.FinalClassification &&
            left.MeasurementPersisted == right.MeasurementPersisted &&
            left.LatestAdvanced == right.LatestAdvanced &&
            left.ErrorCode == right.ErrorCode &&
            left.RejectionCode == right.RejectionCode &&
            left.PersistedMeasurementId == right.PersistedMeasurementId &&
            left.QualityCode == right.QualityCode &&
            left.ReasonCode == right.ReasonCode &&
            left.CompletedAtUtc == right.CompletedAtUtc &&
            left.OriginalCorrelationId == right.OriginalCorrelationId &&
            left.OriginalLineageId == right.OriginalLineageId;
    }
}
