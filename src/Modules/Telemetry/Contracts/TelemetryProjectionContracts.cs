namespace IUMP.Modules.Telemetry.Contracts;

public sealed record LatestProjectionCandidate(
    Guid MeasurementId,
    Guid PointId,
    DateTime SourceTimestampUtc,
    long SourceSequence,
    DateTime ProcessingAtUtc,
    MeasurementQuality QualityCode,
    double NumericValue = 0,
    string UnitCode = "",
    DateTime? ReceivedAtUtc = null,
    string? ReasonCode = null);

public interface ILatestProjectionRepository
{
    Task<bool> EvaluateAdvanceAsync(
        LatestProjectionCandidate candidate,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default);
    Task StageAdvanceAsync(
        LatestProjectionCandidate candidate,
        bool latestAdvanced,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default);
}

public interface ISourceHealthRepository
{
    Task<SourceHealthSnapshot?> GetSourceHealthAsync(
        Guid pointId, CancellationToken ct = default);
}

public sealed record SourceHealthSnapshot(
    Guid PointId,
    string Status,
    DateTime? LastReceivedAtUtc,
    DateTime EvaluatedAtUtc,
    long ProviderVersion);

public interface ITelemetryQueryRepository
{
    Task<RawMeasurement?> GetMeasurementAsync(
        Guid measurementId, CancellationToken ct = default);
}

// Phase 8 provider-neutral projection contracts.  The existing ingestion
// repository remains the transaction owner; these seams only describe the
// Latest and Source Health projections and never expose database types.
public sealed record LatestOrderingTuple(
    DateTime SourceTimestampUtc,
    long SourceSequence,
    DateTime ProcessingAtUtc,
    Guid MeasurementId);

public sealed record PointLatestProjection(
    Guid PointId,
    Guid MeasurementId,
    Guid SourceId,
    Guid SimulatorRunId,
    Guid MappingId,
    long MappingVersion,
    double NumericValue,
    string UnitCode,
    MeasurementQuality QualityCode,
    string? ReasonCode,
    DateTime SourceTimestampUtc,
    long SourceSequence,
    DateTime ReceivedAtUtc,
    DateTime ProcessingAtUtc,
    long Version)
{
    public LatestOrderingTuple Ordering => new(
        SourceTimestampUtc, SourceSequence, ProcessingAtUtc, MeasurementId);

    public static PointLatestProjection FromCandidate(LatestProjectionCandidate candidate) => new(
        candidate.PointId,
        candidate.MeasurementId,
        Guid.Empty,
        Guid.Empty,
        Guid.Empty,
        0,
        candidate.NumericValue,
        candidate.UnitCode,
        candidate.QualityCode,
        candidate.ReasonCode,
        candidate.SourceTimestampUtc,
        candidate.SourceSequence,
        candidate.ReceivedAtUtc ?? candidate.ProcessingAtUtc,
        candidate.ProcessingAtUtc,
        1);
}

public sealed record PointLatestAdvanceResult(
    bool Advanced,
    PointLatestProjection? Previous,
    PointLatestProjection? Current);

public sealed record PointLatestAdvancedEvent(
    Guid EventId,
    Guid PointId,
    Guid OldMeasurementId,
    Guid NewMeasurementId,
    LatestOrderingTuple? OldOrdering,
    LatestOrderingTuple NewOrdering,
    DateTime OccurredAtUtc,
    string CorrelationId,
    string SiteId,
    string? AreaId)
{
    public string EventType => "PointLatestAdvanced.v1";
    public int SchemaVersion => 1;
}

public interface IPointLatestProjectionRepository : ILatestProjectionRepository
{
    Task<PointLatestProjection?> GetCurrentAsync(
        Guid pointId, CancellationToken ct = default);

    Task<PointLatestAdvanceResult> CompareAndSetAsync(
        LatestProjectionCandidate candidate,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default);

    ValueTask StageAdvancedEventAsync(
        PointLatestAdvancedEvent latestEvent,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default);
}

public static class LatestOrdering
{
    public static int Compare(LatestOrderingTuple left, LatestOrderingTuple right)
    {
        var result = left.SourceTimestampUtc.CompareTo(right.SourceTimestampUtc);
        if (result != 0) return result;
        result = left.SourceSequence.CompareTo(right.SourceSequence);
        if (result != 0) return result;
        result = left.ProcessingAtUtc.CompareTo(right.ProcessingAtUtc);
        if (result != 0) return result;
        return left.MeasurementId.CompareTo(right.MeasurementId);
    }
}

public enum SourceHealthStatus
{
    Online,
    Stale,
    NoData,
    Suspended,
    Decommissioned
}

public sealed record SourceHealthEvaluationInput(
    Guid PointId,
    Guid SourceId,
    string SiteId,
    string? AreaId,
    string PointStatus,
    string SourceStatus,
    string? RunStatus,
    long GeneratedCount,
    long AcceptedCount,
    long RejectedCount,
    DateTime? LastAcceptedReceivedAtUtc,
    int ExpectedIntervalSeconds,
    int NoDataAfterSeconds,
    long PointVersion,
    long SourceVersion,
    long ProviderVersion);

public sealed record PointSourceHealthProjection(
    Guid PointId,
    Guid SourceId,
    SourceHealthStatus Status,
    DateTime? LastAcceptedReceivedAtUtc,
    int ExpectedIntervalSeconds,
    int NoDataAfterSeconds,
    string? RunStatus,
    long GeneratedCount,
    long AcceptedCount,
    long RejectedCount,
    long PointVersion,
    long SourceVersion,
    long ProviderVersion,
    long Version,
    DateTime EvaluatedAtUtc,
    string SiteId,
    string? AreaId);

public sealed record SourceHealthEvaluationResult(
    bool Changed,
    PointSourceHealthProjection Current,
    PointSourceHealthProjection? Previous);

public sealed record PointSourceHealthChangedEvent(
    Guid EventId,
    Guid PointId,
    Guid SourceId,
    SourceHealthStatus OldStatus,
    SourceHealthStatus NewStatus,
    DateTime OccurredAtUtc,
    DateTime? LastAcceptedReceivedAtUtc,
    string SiteId,
    string? AreaId)
{
    public string EventType => "PointSourceHealthChanged.v1";
    public int SchemaVersion => 1;
}

public interface ISourceHealthProjectionRepository
{
    Task<PointSourceHealthProjection?> GetCurrentAsync(
        Guid pointId, CancellationToken ct = default);

    Task<SourceHealthEvaluationResult> CompareAndSetAsync(
        SourceHealthEvaluationInput input,
        SourceHealthStatus status,
        DateTime evaluatedAtUtc,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default);

    ValueTask StageChangedEventAsync(
        PointSourceHealthChangedEvent healthEvent,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default);
}

public interface ISourceHealthProvider
{
    Task<SourceHealthEvaluationInput?> GetAsync(
        Guid pointId, CancellationToken ct = default);
}
