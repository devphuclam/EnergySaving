namespace IUMP.Modules.Telemetry.Contracts;

public sealed record LatestProjectionCandidate(
    Guid MeasurementId,
    Guid PointId,
    DateTime SourceTimestampUtc,
    long SourceSequence,
    DateTime ProcessingAtUtc,
    MeasurementQuality QualityCode);

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
