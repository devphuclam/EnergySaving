using IUMP.Modules.Acquisition.Application;
using IUMP.Modules.Acquisition.Contracts;
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
        false, false, "POINT_INACTIVE", "POINT_INACTIVE", null, null, null,
        TelemetryTestData.Now, "original-correlation", "original-lineage");

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
                    result.MeasurementPersisted ?? result.FinalClassification == ProductionFinalClassification.Accepted,
                    result.PersistedMeasurementId,
                    result.QualityCode,
                    result.ReasonCode,
                    result.RejectionCode,
                    result.LatestAdvanced,
                    result.CompletedAtUtc ?? TelemetryTestData.Now,
                    result.OriginalCorrelationId ?? "original-correlation",
                    result.OriginalLineageId ?? "original-lineage"),
                result.ErrorCode,
                "dispatch-correlation");
        }
        public Task<TelemetryDispatchResult> DispatchAsync(
            SimulatorProductionPayload payload, CancellationToken ct = default) =>
            throw new InvalidOperationException("LEGACY_DISPATCH_MUST_NOT_BE_USED");

        public Task<CanonicalTelemetryIngestionResult> DispatchCanonicalAsync(
            SimulatorProductionPayload payload, CancellationToken ct = default)
        {
            if (ThrowAfterTerminal) throw new InvalidOperationException("CRASH_AFTER_TELEMETRY");
            return Task.FromResult(_result);
        }
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
                CompletedAtUtc = result.CompletedAtUtc ?? TelemetryTestData.Now,
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
