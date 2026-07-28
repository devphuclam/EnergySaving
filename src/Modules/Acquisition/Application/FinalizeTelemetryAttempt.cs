using IUMP.Modules.Acquisition.Contracts;

namespace IUMP.Modules.Acquisition.Application;

public sealed record FinalizeTelemetryAttemptResult(
    SimulatorProductionAttempt Attempt,
    TelemetryDispatchResult TelemetryResult,
    bool FirstTransition,
    bool Replay);

public sealed class FinalizeTelemetryAttempt
{
    private readonly IProductionAttemptService _attempts;
    private readonly ITelemetryIngestionClient _telemetry;

    public FinalizeTelemetryAttempt(
        IProductionAttemptService attempts,
        ITelemetryIngestionClient telemetry)
    {
        _attempts = attempts;
        _telemetry = telemetry;
    }

    public async Task<FinalizeTelemetryAttemptResult> ExecuteAsync(
        SimulatorProductionAttempt pending,
        CancellationToken ct = default)
    {
        if (pending.Status != SimulatorProductionAttemptStatus.Pending)
            throw new InvalidOperationException("ATTEMPT_NOT_PENDING");
        var canonical = await _telemetry.DispatchCanonicalAsync(pending.Payload, ct);
        var original = canonical.OriginalResult;
        CanonicalTelemetryOriginalResultValidator.EnsureValid(original);
        var outcome = canonical.Disposition switch
        {
            CanonicalTelemetryDisposition.Accepted => TelemetryAttemptOutcome.Accepted,
            CanonicalTelemetryDisposition.Rejected => TelemetryAttemptOutcome.Rejected,
            CanonicalTelemetryDisposition.Duplicate => TelemetryAttemptOutcome.Duplicate,
            _ => throw new InvalidOperationException("TERMINAL_RESULT_INVALID")
        };
        var result = new TelemetryDispatchResult(
            outcome, original.FinalClassification, original.MeasurementPersisted,
            original.LatestAdvanced ?? false, canonical.ErrorCode, original.RejectionCode,
            original.PersistedMeasurementId, original.QualityCode, original.ReasonCode,
            original.CompletedAtUtc, original.OriginalCorrelationId, original.OriginalLineageId);
        TelemetryDispatchResultValidator.EnsureValid(result);
        var finalized = await _attempts.FinalizeAsync(
            pending.RunId, pending.PointId, pending.SourceSequence, result, ct);
        return new FinalizeTelemetryAttemptResult(
            finalized.Attempt, result, finalized.FirstTransition, finalized.Replay);
    }
}
