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

public sealed record TelemetryRaceWinnerFixture(
    TelemetryTerminalResult Terminal,
    RawMeasurement? Raw,
    LatestProjectionCandidate? Latest,
    TelemetryOwnerEvent? Event);

public enum TelemetryFlowLockTarget
{
    OrganizationSite = 1,
    OrganizationArea = 2,
    OrganizationAsset = 3,
    OrganizationPoint = 4,
    CatalogSource = 5,
    CatalogMapping = 6,
    CatalogMetric = 7,
    CatalogUnit = 8,
    TelemetryIdentityRawLatest = 9,
    IntegrationOutbox = 10
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
    long SiteVersion,
    long AreaVersion,
    long AssetVersion,
    long PointVersion,
    Guid SourceId,
    string SourceType,
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
    string CompatibilityIdentity,
    long CompatibilityVersion,
    string CompatibilityStatus,
    string TrustedSiteId,
    string? TrustedAreaId,
    string SiteId,
    string SiteStatus,
    string AreaId,
    string AreaStatus,
    string AssetId,
    string AssetStatus,
    string PointStatus,
    string SourceStatus,
    string MappingStatus,
    string MetricId,
    string MetricStatus,
    string UnitId,
    string UnitStatus,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc);

public sealed record TelemetryProviderRecheckResult(
    bool SiteIdMatches,
    bool SiteVersionMatches,
    bool SiteStatusMatches,
    bool AreaIdMatches,
    bool AreaVersionMatches,
    bool AreaStatusMatches,
    bool AssetIdMatches,
    bool AssetVersionMatches,
    bool AssetStatusMatches,
    bool PointIdMatches,
    bool PointVersionMatches,
    bool PointStatusMatches,
    bool SourceIdMatches,
    bool SourceVersionMatches,
    bool SourceStatusMatches,
    bool MappingIdMatches,
    bool MappingVersionMatches,
    bool MappingStatusMatches,
    bool MetricIdMatches,
    bool MetricVersionMatches,
    bool MetricStatusMatches,
    bool UnitIdMatches,
    bool UnitVersionMatches,
    bool UnitStatusMatches,
    bool EffectiveFromMatches,
    bool EffectiveToMatches,
    bool CompatibilityIdentityMatches,
    bool CompatibilityVersionMatches,
    bool CompatibilityStatusMatches,
    bool PointExistsMatches,
    bool PointActiveMatches,
    bool SiteActiveMatches,
    bool AreaActiveMatches,
    bool AssetActiveMatches,
    bool SourceTypeMatches,
    bool SourceExistsMatches,
    bool SourceActiveMatches,
    bool MappingPointIdMatches,
    bool MappingExistsMatches,
    bool MappingActiveMatches,
    bool MappingEffectiveMatches,
    bool MetricExistsMatches,
    bool MetricMatchesPointMatches,
    bool MetricActiveMatches,
    bool UnitExistsMatches,
    bool UnitActiveMatches,
    bool UnitCompatibleMatches,
    bool UnitCodeMatches,
    bool TrustedSiteIdMatches,
    bool TrustedAreaIdMatches)
{
    public bool IsExactMatch =>
        SiteIdMatches && SiteVersionMatches && SiteStatusMatches &&
        AreaIdMatches && AreaVersionMatches && AreaStatusMatches &&
        AssetIdMatches && AssetVersionMatches && AssetStatusMatches &&
        PointIdMatches && PointVersionMatches && PointStatusMatches &&
        SourceIdMatches && SourceVersionMatches && SourceStatusMatches &&
        MappingIdMatches && MappingVersionMatches && MappingStatusMatches &&
        MetricIdMatches && MetricVersionMatches && MetricStatusMatches &&
        UnitIdMatches && UnitVersionMatches && UnitStatusMatches &&
        EffectiveFromMatches && EffectiveToMatches &&
        CompatibilityIdentityMatches && CompatibilityVersionMatches && CompatibilityStatusMatches &&
        PointExistsMatches && PointActiveMatches && SiteActiveMatches && AreaActiveMatches &&
        AssetActiveMatches && SourceTypeMatches && SourceExistsMatches && SourceActiveMatches &&
        MappingPointIdMatches && MappingExistsMatches && MappingActiveMatches &&
        MappingEffectiveMatches && MetricExistsMatches && MetricMatchesPointMatches &&
        MetricActiveMatches && UnitExistsMatches && UnitActiveMatches && UnitCompatibleMatches &&
        UnitCodeMatches && TrustedSiteIdMatches && TrustedAreaIdMatches;

    public static TelemetryProviderRecheckResult Compare(
        TelemetryProviderSnapshot expected, TelemetryProviderSnapshot current) => new(
            expected.SiteId == current.SiteId,
            expected.SiteVersion == current.SiteVersion,
            expected.SiteStatus == current.SiteStatus,
            expected.AreaId == current.AreaId,
            expected.AreaVersion == current.AreaVersion,
            expected.AreaStatus == current.AreaStatus,
            expected.AssetId == current.AssetId,
            expected.AssetVersion == current.AssetVersion,
            expected.AssetStatus == current.AssetStatus,
            expected.PointId == current.PointId,
            expected.PointVersion == current.PointVersion,
            expected.PointStatus == current.PointStatus,
            expected.SourceId == current.SourceId,
            expected.SourceVersion == current.SourceVersion,
            expected.SourceStatus == current.SourceStatus,
            expected.MappingId == current.MappingId,
            expected.MappingVersion == current.MappingVersion,
            expected.MappingStatus == current.MappingStatus,
            expected.MetricId == current.MetricId,
            expected.MetricVersion == current.MetricVersion,
            expected.MetricStatus == current.MetricStatus,
            expected.UnitId == current.UnitId,
            expected.UnitVersion == current.UnitVersion,
            expected.UnitStatus == current.UnitStatus,
            expected.EffectiveFromUtc == current.EffectiveFromUtc,
            expected.EffectiveToUtc == current.EffectiveToUtc,
            expected.CompatibilityIdentity == current.CompatibilityIdentity,
            expected.CompatibilityVersion == current.CompatibilityVersion,
            expected.CompatibilityStatus == current.CompatibilityStatus,
            expected.PointExists == current.PointExists,
            expected.PointActive == current.PointActive,
            expected.SiteActive == current.SiteActive,
            expected.AreaActive == current.AreaActive,
            expected.AssetActive == current.AssetActive,
            expected.SourceType == current.SourceType,
            expected.SourceExists == current.SourceExists,
            expected.SourceActive == current.SourceActive,
            expected.MappingPointId == current.MappingPointId,
            expected.MappingExists == current.MappingExists,
            expected.MappingActive == current.MappingActive,
            expected.MappingEffective == current.MappingEffective,
            expected.MetricExists == current.MetricExists,
            expected.MetricMatchesPoint == current.MetricMatchesPoint,
            expected.MetricActive == current.MetricActive,
            expected.UnitExists == current.UnitExists,
            expected.UnitActive == current.UnitActive,
            expected.UnitCompatible == current.UnitCompatible,
            expected.UnitCode == current.UnitCode,
            expected.TrustedSiteId == current.TrustedSiteId,
            expected.TrustedAreaId == current.TrustedAreaId);
}

public static class TelemetryProviderSnapshotValidator
{
    public const string InvalidCode = "PROVIDER_SNAPSHOT_INVALID";

    public static void EnsureValid(TelemetryProviderSnapshot snapshot, DateTime atUtc)
    {
        if (snapshot.PointId == Guid.Empty || snapshot.SourceId == Guid.Empty ||
            snapshot.MappingId == Guid.Empty || snapshot.MappingPointId != snapshot.PointId ||
            snapshot.SiteVersion <= 0 || snapshot.AreaVersion <= 0 || snapshot.AssetVersion <= 0 ||
            snapshot.PointVersion <= 0 || snapshot.SourceVersion <= 0 || snapshot.MappingVersion <= 0 ||
            snapshot.MetricVersion <= 0 || snapshot.UnitVersion <= 0 ||
            string.IsNullOrWhiteSpace(snapshot.SiteId) || string.IsNullOrWhiteSpace(snapshot.AreaId) ||
            string.IsNullOrWhiteSpace(snapshot.AssetId) || string.IsNullOrWhiteSpace(snapshot.MetricId) ||
            string.IsNullOrWhiteSpace(snapshot.UnitId) ||
            !string.Equals(snapshot.SourceType, "Simulator", StringComparison.Ordinal) ||
            !string.Equals(snapshot.CompatibilityStatus, "Active", StringComparison.Ordinal) ||
            snapshot.EffectiveFromUtc.Kind != DateTimeKind.Utc ||
            snapshot.EffectiveToUtc is { Kind: not DateTimeKind.Utc } ||
            snapshot.EffectiveFromUtc > atUtc ||
            (snapshot.EffectiveToUtc is { } effectiveTo && effectiveTo <= atUtc))
            throw new InvalidOperationException(InvalidCode);

        var active = new[]
        {
            snapshot.SiteStatus, snapshot.AreaStatus, snapshot.AssetStatus,
            snapshot.PointStatus, snapshot.SourceStatus, snapshot.MappingStatus,
            snapshot.MetricStatus, snapshot.UnitStatus
        };
        if (active.Any(status => !string.Equals(status, "Active", StringComparison.Ordinal)) ||
            !snapshot.PointExists || !snapshot.PointActive || !snapshot.SiteActive ||
            !snapshot.AreaActive || !snapshot.AssetActive || !snapshot.SourceExists ||
            !snapshot.SourceActive || !snapshot.MappingExists || !snapshot.MappingActive ||
            !snapshot.MappingEffective || !snapshot.MetricExists || !snapshot.MetricMatchesPoint ||
            !snapshot.MetricActive || !snapshot.UnitExists || !snapshot.UnitActive ||
            !snapshot.UnitCompatible)
            throw new InvalidOperationException(InvalidCode);
    }
}

public interface ITelemetryProviderSnapshotQuery
{
    Task<TelemetryProviderSnapshot?> GetAsync(
        TelemetryMeasurementRequest request, DateTime receivedAtUtc,
        CancellationToken ct = default);
    Task<TelemetryProviderRecheckResult> RecheckAsync(
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
