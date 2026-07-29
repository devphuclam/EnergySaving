using IUMP.Modules.Telemetry.Contracts;

namespace IUMP.Modules.Telemetry.Application;

/// <summary>
/// Applies the provider-neutral Latest policy inside the transaction supplied
/// by the Phase 7 ingestion flow.  It never owns a second transaction and it
/// never changes the terminal registry or raw-measurement ownership.
/// </summary>
public sealed class PointLatestService
{
    private readonly ILatestProjectionRepository _repository;

    public PointLatestService(ILatestProjectionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public static bool IsEligible(MeasurementQuality quality) =>
        quality is MeasurementQuality.Good or MeasurementQuality.Uncertain;

    public static bool ShouldAdvance(
        LatestProjectionCandidate candidate,
        PointLatestProjection? current)
    {
        if (!IsEligible(candidate.QualityCode)) return false;
        if (current is null) return true;
        return LatestOrdering.Compare(
            new LatestOrderingTuple(candidate.SourceTimestampUtc, candidate.SourceSequence,
                candidate.ProcessingAtUtc, candidate.MeasurementId),
            current.Ordering) > 0;
    }

    public async Task<bool> ApplyAsync(
        LatestProjectionCandidate candidate,
        ITelemetryFlowTransaction transaction,
        string siteId = "phase8-site",
        string? areaId = "phase8-area",
        string? correlationId = null,
        CancellationToken ct = default)
    {
        if (candidate.SourceTimestampUtc.Kind != DateTimeKind.Utc ||
            candidate.ProcessingAtUtc.Kind != DateTimeKind.Utc ||
            (candidate.ReceivedAtUtc is { } received && received.Kind != DateTimeKind.Utc))
            throw new InvalidOperationException("LATEST_TIMESTAMP_INVALID");
        if (candidate.MeasurementId == Guid.Empty || candidate.PointId == Guid.Empty)
            throw new InvalidOperationException("LATEST_ID_INVALID");
        if (!IsEligible(candidate.QualityCode)) return false;

        if (_repository is IPointLatestProjectionRepository projection)
        {
            var result = await projection.CompareAndSetAsync(candidate, transaction, ct);
            if (!result.Advanced) return false;

            var previous = result.Previous;
            var current = result.Current ?? PointLatestProjection.FromCandidate(candidate);
            var latestEvent = new PointLatestAdvancedEvent(
                Guid.NewGuid(),
                candidate.PointId,
                previous?.MeasurementId ?? Guid.Empty,
                current.MeasurementId,
                previous?.Ordering,
                current.Ordering,
                candidate.ProcessingAtUtc,
                correlationId ?? $"telemetry-latest-{candidate.MeasurementId:D}",
                siteId,
                areaId);
            await projection.StageAdvancedEventAsync(latestEvent, transaction, ct);
            return true;
        }

        // Legacy Phase 7 repositories still participate in the same transaction.
        // Their atomic compare-and-set remains authoritative; no independent
        // transaction or synthetic event is created here.
        var advanced = await _repository.EvaluateAdvanceAsync(candidate, transaction, ct);
        await _repository.StageAdvanceAsync(candidate, advanced, transaction, ct);
        return advanced;
    }

    public Task<bool> EvaluateAsync(
        LatestProjectionCandidate candidate,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default) =>
        ApplyAsync(candidate, transaction, ct: ct);

    public Task<bool> EvaluateAndStageAsync(
        LatestProjectionCandidate candidate,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default) =>
        ApplyAsync(candidate, transaction, ct: ct);
}
