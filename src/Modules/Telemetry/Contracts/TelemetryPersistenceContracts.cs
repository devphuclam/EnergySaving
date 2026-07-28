namespace IUMP.Modules.Telemetry.Contracts;

public enum TelemetryDisposition
{
    Accepted,
    Rejected,
    Duplicate,
    Failed
}

public enum TelemetryFinalClassification
{
    Accepted,
    Rejected
}

public enum MeasurementQuality
{
    Good,
    Uncertain,
    Bad
}

public sealed record TrustedProducerContext(
    bool IsTrusted,
    string ProducerIdentity,
    string ProducerType,
    int ProducerVersion);

public sealed record TelemetryMeasurementRequest(
    string MeasurementId,
    Guid SourceId,
    Guid SimulatorRunId,
    Guid PointId,
    Guid MappingId,
    long MappingVersion,
    long SourceSequence,
    string AlgorithmId,
    int AlgorithmVersion,
    Guid SimulatorConfigurationId,
    long ConfigurationVersion,
    DateTime SourceTimestampUtc,
    double NumericValue,
    string UnitCode,
    string ProducerIdentity,
    string CorrelationId,
    string LineageId);

public sealed record TelemetryTerminalResult(
    Guid MeasurementId,
    Guid SourceId,
    Guid SimulatorRunId,
    Guid PointId,
    Guid MappingId,
    long MappingVersion,
    long SourceSequence,
    string AlgorithmId,
    int AlgorithmVersion,
    Guid SimulatorConfigurationId,
    long ConfigurationVersion,
    TelemetryFinalClassification FinalClassification,
    bool MeasurementPersisted,
    Guid? PersistedMeasurementId,
    MeasurementQuality? QualityCode,
    string? ReasonCode,
    string? RejectionCode,
    bool? LatestAdvanced,
    DateTime CompletedAtUtc,
    string OriginalCorrelationId,
    string OriginalLineageId,
    byte[] RequestFingerprint)
{
    public TelemetryTerminalResult Copy() => this with
    {
        RequestFingerprint = RequestFingerprint.ToArray()
    };
}

public sealed record TelemetryIngestionResult(
    TelemetryDisposition Disposition,
    TelemetryTerminalResult? OriginalResult,
    string? ErrorCode,
    string CorrelationId)
{
    public static TelemetryIngestionResult Failed(string code, string correlationId) =>
        new(TelemetryDisposition.Failed, null, code, correlationId);
}

public sealed record RawMeasurement(
    Guid MeasurementId,
    Guid SourceId,
    Guid SimulatorRunId,
    Guid PointId,
    Guid MappingId,
    long MappingVersion,
    long SourceSequence,
    DateTime SourceTimestampUtc,
    DateTime ReceivedAtUtc,
    DateTime ProcessingAtUtc,
    double NumericValue,
    string UnitCode,
    MeasurementQuality QualityCode,
    string? ReasonCode,
    string CorrelationId,
    string LineageId);

public enum TelemetryFlowLockTarget
{
    OrganizationPoint = 1,
    CatalogSourceMappingMetricUnit = 2,
    TelemetryIdentityRawLatest = 3,
    IntegrationOutbox = 4
}

public sealed record TelemetryFlowLock(TelemetryFlowLockTarget Target, string Key);

public interface ITelemetryFlowTransaction : IAsyncDisposable
{
    Guid TransactionId { get; }
    string IsolationIntent { get; }
    bool IsCompleted { get; }
    IReadOnlyList<TelemetryFlowLock> LockTrace { get; }
    ValueTask AcquireLockAsync(TelemetryFlowLockTarget target, string key,
        CancellationToken ct = default);
    ValueTask CommitAsync(CancellationToken ct = default);
    ValueTask RollbackAsync(CancellationToken ct = default);
}

public interface ITelemetryFlowUnitOfWork
{
    ValueTask<ITelemetryFlowTransaction> BeginRepeatableReadAsync(
        CancellationToken ct = default);
}

public interface ITelemetryIngestionRepository
{
    Task<TelemetryTerminalResult?> GetTerminalAsync(Guid measurementId,
        CancellationToken ct = default);
    Task<TelemetryTerminalResult?> GetTerminalBySlotAsync(
        Guid runId, Guid pointId, long sourceSequence, CancellationToken ct = default);
    Task<TelemetryTerminalResult?> RecheckTerminalAsync(
        Guid measurementId, ITelemetryFlowTransaction transaction,
        CancellationToken ct = default);
    Task StageTerminalAsync(TelemetryTerminalResult result,
        ITelemetryFlowTransaction transaction, CancellationToken ct = default);
    Task StageRawAsync(RawMeasurement measurement,
        ITelemetryFlowTransaction transaction, CancellationToken ct = default);
    Task<IReadOnlyList<TelemetryTerminalResult>> ListCommittedTerminalsAsync(
        CancellationToken ct = default);
    Task<IReadOnlyList<RawMeasurement>> ListCommittedRawAsync(
        CancellationToken ct = default);
}

public sealed class TelemetryUniqueRaceException : Exception
{
    public TelemetryUniqueRaceException() : base("TELEMETRY_UNIQUE_RACE") { }
}

public sealed record ImmutableConfigurationSnapshot(
    Guid ConfigurationId,
    long ConfigurationVersion,
    double MinimumValue,
    double MaximumValue);

public interface IImmutableSimulatorConfigurationQuery
{
    Task<ImmutableConfigurationSnapshot?> GetVersionAsync(
        Guid configurationId, long configurationVersion, CancellationToken ct = default);
}

public sealed record TelemetryProviderSnapshot(
    Guid PointId,
    bool PointExists,
    bool PointActive,
    bool SiteActive,
    bool AreaActive,
    bool AssetActive,
    long OrganizationVersion,
    Guid SourceId,
    bool SourceExists,
    bool SourceActive,
    long SourceVersion,
    Guid MappingId,
    bool MappingExists,
    bool MappingActive,
    bool MappingEffective,
    Guid MappingPointId,
    long MappingVersion,
    bool MetricExists,
    bool MetricMatchesPoint,
    bool MetricActive,
    long MetricVersion,
    bool UnitExists,
    bool UnitActive,
    bool UnitCompatible,
    string UnitCode,
    long UnitVersion,
    string TrustedSiteId,
    string? TrustedAreaId);

public interface ITelemetryProviderSnapshotQuery
{
    Task<TelemetryProviderSnapshot?> GetAsync(
        TelemetryMeasurementRequest request, DateTime receivedAtUtc,
        CancellationToken ct = default);
    Task<bool> RecheckAsync(
        TelemetryProviderSnapshot snapshot, ITelemetryFlowTransaction transaction,
        CancellationToken ct = default);
}

public interface ITelemetryUtcClock
{
    DateTime UtcNow { get; }
}

public sealed record TelemetryOwnerEvent(
    Guid EventId,
    string EventType,
    int SchemaVersion,
    string Producer,
    string AggregateType,
    Guid AggregateId,
    long AggregateVersion,
    string ActorId,
    string ActorUsername,
    string Action,
    string Summary,
    DateTime OccurredAtUtc,
    string CorrelationId,
    string? CausationId,
    string SiteId,
    string? AreaId,
    IReadOnlyDictionary<string, object?> Before,
    IReadOnlyDictionary<string, object?> After);

public interface IMeasurementAcceptedEventWriter
{
    ValueTask StageAsync(TelemetryOwnerEvent ownerEvent,
        ITelemetryFlowTransaction transaction, CancellationToken ct = default);
    Task<IReadOnlyList<TelemetryOwnerEvent>> ListCommittedAsync(
        CancellationToken ct = default);
}
