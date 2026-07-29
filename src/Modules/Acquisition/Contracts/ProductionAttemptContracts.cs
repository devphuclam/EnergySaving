namespace IUMP.Modules.Acquisition.Contracts;

public enum SimulatorProductionAttemptStatus
{
    Pending,
    Completed
}

public enum TelemetryAttemptOutcome
{
    Accepted,
    Rejected,
    Duplicate
}

public enum ProductionFinalClassification
{
    Accepted,
    Rejected
}

public sealed record SimulatorProductionPayload(
    Guid MeasurementId,
    Guid SourceId,
    Guid RunId,
    Guid PointId,
    Guid MappingId,
    long MappingVersion,
    long SourceSequence,
    string AlgorithmId,
    int AlgorithmVersion,
    Guid ConfigurationId,
    long ConfigurationVersion,
    DateTime SourceTimestampUtc,
    double NumericValue,
    string UnitCode,
    string ProducerIdentity,
    string CorrelationId,
    string LineageId);

public sealed record SimulatorProductionAttempt(
    Guid RunId,
    Guid PointId,
    long SourceSequence,
    SimulatorProductionPayload Payload,
    SimulatorProductionAttemptStatus Status,
    TelemetryAttemptOutcome? TelemetryOutcome,
    ProductionFinalClassification? FinalClassification,
    bool? MeasurementPersisted,
    Guid? PersistedMeasurementId,
    string? QualityCode,
    string? ReasonCode,
    bool? LatestAdvanced,
    string? ErrorCode,
    string? RejectionCode,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string? OriginalCorrelationId,
    string? OriginalLineageId,
    long Version);

public sealed record DeterministicGeneration(
    double Value,
    byte[] State,
    int DrawCount);

public interface ISimulatorValueGenerator
{
    byte[] Initialize(ulong seed, Guid pointId, Guid configurationId, long configurationVersion,
        int algorithmVersion);
    DeterministicGeneration Generate(byte[] state, SimulatorScenario scenario, double minimumValue,
        double maximumValue);
}

public interface IMeasurementIdentityFactory
{
    Guid Create(Guid sourceId, Guid runId, Guid pointId, Guid mappingId, long sourceSequence,
        int algorithmVersion);
}

public sealed record AttemptReserveResult(
    SimulatorProductionAttempt Attempt,
    bool ExistingPending,
    bool UniquenessWinnerReloaded);

public sealed record TelemetryDispatchResult(
    TelemetryAttemptOutcome Outcome,
    ProductionFinalClassification FinalClassification,
    bool? MeasurementPersisted,
    bool? LatestAdvanced,
    string? ErrorCode,
    string? RejectionCode,
    Guid? PersistedMeasurementId = null,
    string? QualityCode = null,
    string? ReasonCode = null,
    DateTime? CompletedAtUtc = null,
    string? OriginalCorrelationId = null,
    string? OriginalLineageId = null);

public enum CanonicalTelemetryDisposition
{
    Accepted,
    Rejected,
    Duplicate
}

public sealed record CanonicalTelemetryOriginalResult(
    ProductionFinalClassification FinalClassification,
    bool MeasurementPersisted,
    Guid? PersistedMeasurementId,
    string? QualityCode,
    string? ReasonCode,
    string? RejectionCode,
    bool? LatestAdvanced,
    DateTime? CompletedAtUtc,
    string? OriginalCorrelationId,
    string? OriginalLineageId);

public sealed record CanonicalTelemetryIngestionResult(
    CanonicalTelemetryDisposition Disposition,
    CanonicalTelemetryOriginalResult OriginalResult,
    string? ErrorCode,
    string CorrelationId);

public static class CanonicalTelemetryOriginalResultValidator
{
    public const string InvalidCode = "CANONICAL_ORIGINAL_RESULT_INVALID";

    public static void EnsureValid(
        SimulatorProductionPayload payload,
        CanonicalTelemetryIngestionResult canonical)
    {
        if (canonical is null || canonical.OriginalResult is null ||
            canonical.CorrelationId != payload.CorrelationId ||
            string.IsNullOrWhiteSpace(canonical.CorrelationId) ||
            string.IsNullOrWhiteSpace(canonical.OriginalResult.OriginalCorrelationId) ||
            string.IsNullOrWhiteSpace(canonical.OriginalResult.OriginalLineageId) ||
            canonical.OriginalResult.CompletedAtUtc is not { Kind: DateTimeKind.Utc })
            throw new InvalidOperationException(InvalidCode);

        var original = canonical.OriginalResult;
        var valid = canonical.Disposition switch
        {
            CanonicalTelemetryDisposition.Accepted =>
                original.FinalClassification == ProductionFinalClassification.Accepted &&
                original.MeasurementPersisted &&
                original.PersistedMeasurementId == payload.MeasurementId &&
                QualityShape(original.QualityCode, original.ReasonCode, original.LatestAdvanced) &&
                original.RejectionCode is null &&
                string.IsNullOrWhiteSpace(canonical.ErrorCode),
            CanonicalTelemetryDisposition.Rejected =>
                original.FinalClassification == ProductionFinalClassification.Rejected &&
                !original.MeasurementPersisted &&
                original.PersistedMeasurementId is null &&
                original.QualityCode is null &&
                original.ReasonCode is null &&
                original.LatestAdvanced is null &&
                !string.IsNullOrWhiteSpace(original.RejectionCode),
            CanonicalTelemetryDisposition.Duplicate =>
                string.IsNullOrWhiteSpace(canonical.ErrorCode) &&
                original.FinalClassification switch
                {
                    ProductionFinalClassification.Accepted =>
                        original.MeasurementPersisted &&
                        original.PersistedMeasurementId == payload.MeasurementId &&
                        QualityShape(original.QualityCode, original.ReasonCode, original.LatestAdvanced) &&
                        original.RejectionCode is null,
                    ProductionFinalClassification.Rejected =>
                        !original.MeasurementPersisted &&
                        original.PersistedMeasurementId is null &&
                        original.QualityCode is null &&
                        original.ReasonCode is null &&
                        original.LatestAdvanced is null &&
                        !string.IsNullOrWhiteSpace(original.RejectionCode),
                    _ => false
                },
            _ => false
        };
        if (!valid)
            throw new InvalidOperationException(InvalidCode);
    }

    private static bool QualityShape(string? qualityCode, string? reasonCode, bool? latestAdvanced) =>
        qualityCode switch
        {
            "Good" => reasonCode is null && latestAdvanced is not null,
            "Uncertain" => reasonCode == "SOURCE_TIMESTAMP_FUTURE" && latestAdvanced is not null,
            "Bad" => reasonCode == "VALUE_OUT_OF_RANGE" && latestAdvanced == false,
            _ => false
        };
}

public static class TelemetryDispatchResultValidator
{
    public const string InvalidCode = "TERMINAL_RESULT_INVALID";

    public static void EnsureValid(SimulatorProductionPayload payload, TelemetryDispatchResult result)
    {
        EnsureValid(result);
        if (result.CompletedAtUtc is not { Kind: DateTimeKind.Utc } ||
            string.IsNullOrWhiteSpace(result.OriginalCorrelationId) ||
            string.IsNullOrWhiteSpace(result.OriginalLineageId) ||
            (result.FinalClassification == ProductionFinalClassification.Accepted &&
                (result.MeasurementPersisted != true ||
                 result.PersistedMeasurementId != payload.MeasurementId ||
                 !QualityShape(result.QualityCode, result.ReasonCode, result.LatestAdvanced) ||
                 result.RejectionCode is not null)) ||
            (result.FinalClassification == ProductionFinalClassification.Rejected &&
                (result.MeasurementPersisted != false || result.PersistedMeasurementId is not null ||
                 result.QualityCode is not null || result.ReasonCode is not null ||
                 result.LatestAdvanced is not null || string.IsNullOrWhiteSpace(result.RejectionCode))))
            throw new InvalidOperationException(InvalidCode);
    }

    public static void EnsureValid(TelemetryDispatchResult result)
    {
        if (!Enum.IsDefined(result.Outcome) || !Enum.IsDefined(result.FinalClassification))
            throw new InvalidOperationException(InvalidCode);

        var valid = result.Outcome switch
        {
            TelemetryAttemptOutcome.Accepted =>
                result.FinalClassification == ProductionFinalClassification.Accepted &&
                result.RejectionCode is null,
            TelemetryAttemptOutcome.Rejected =>
                result.FinalClassification == ProductionFinalClassification.Rejected &&
                (result.LatestAdvanced is null || result.LatestAdvanced == false) &&
                !string.IsNullOrWhiteSpace(result.RejectionCode),
            TelemetryAttemptOutcome.Duplicate =>
                result.FinalClassification switch
                {
                    ProductionFinalClassification.Accepted => result.RejectionCode is null,
                    ProductionFinalClassification.Rejected =>
                        (result.LatestAdvanced is null || result.LatestAdvanced == false) &&
                        !string.IsNullOrWhiteSpace(result.RejectionCode),
                    _ => false
                },
            _ => false
        };
        if (!valid)
            throw new InvalidOperationException(InvalidCode);
    }

    private static bool QualityShape(string? qualityCode, string? reasonCode, bool? latestAdvanced) =>
        qualityCode switch
        {
            "Good" => reasonCode is null && latestAdvanced is not null,
            "Uncertain" => reasonCode == "SOURCE_TIMESTAMP_FUTURE" && latestAdvanced is not null,
            "Bad" => reasonCode == "VALUE_OUT_OF_RANGE" && latestAdvanced == false,
            _ => false
        };
}

public sealed record AttemptFinalizeResult(
    SimulatorProductionAttempt Attempt,
    bool FirstTransition,
    bool Replay);

public interface ISimulatorProductionAttemptRepository
{
    Task<SimulatorProductionAttempt?> GetPendingAsync(Guid runId, Guid pointId,
        CancellationToken ct = default);
    Task<SimulatorProductionAttempt?> GetAsync(Guid runId, Guid pointId, long sourceSequence,
        CancellationToken ct = default);
    Task<IReadOnlyList<SimulatorProductionAttempt>> ListPendingAsync(CancellationToken ct = default);
    Task<bool> TryReserveAsync(
        SimulatorProductionAttempt attempt,
        SimulatorRunPointReservationTransition transition,
        ISimulatorRunTransaction transaction,
        CancellationToken ct = default);
    Task<AttemptFinalizeResult> FinalizeAsync(Guid runId, Guid pointId, long sourceSequence,
        TelemetryDispatchResult result, DateTime completedAtUtc, ISimulatorRunTransaction transaction,
        CancellationToken ct = default);
}

public interface ITelemetryIngestionClient
{
    Task<CanonicalTelemetryIngestionResult> DispatchCanonicalAsync(
        SimulatorProductionPayload payload,
        CancellationToken ct = default);
}

public interface ILegacyTelemetryDispatchClient
{
    Task<TelemetryDispatchResult> DispatchAsync(SimulatorProductionPayload payload,
        CancellationToken ct = default);
}

public interface ISimulatorProductionEligibility
{
    Task<(bool IsActive, string? ErrorCode)> IsPinnedInputActiveAsync(
        SimulatorRun run, SimulatorRunPointState pointState, CancellationToken ct = default);
}

public interface IProductionAttemptService
{
    Task<SimulatorProductionAttempt?> LoadPendingAsync(
        Guid runId,
        Guid pointId,
        CancellationToken ct = default);

    Task<AttemptReserveResult> ReserveAsync(
        Guid runId,
        Guid pointId,
        string correlationId,
        string lineageId,
        CancellationToken ct = default);

    Task<AttemptFinalizeResult> FinalizeAsync(
        Guid runId,
        Guid pointId,
        long sourceSequence,
        TelemetryDispatchResult result,
        CancellationToken ct = default);
}

public sealed record SimulatorProductionFailure(
    Guid RunId,
    Guid PointId,
    string CorrelationId,
    string Code);

public sealed record SimulatorProductionCycleResult(
    int RunningRuns,
    int ClaimedPoints,
    int DispatchedAttempts,
    int FinalizedAttempts,
    int FailedPoints,
    IReadOnlyList<SimulatorProductionFailure> Failures);

public interface ISimulatorProductionCoordinator
{
    Task<SimulatorProductionCycleResult> RunOnceAsync(
        string workerId,
        CancellationToken ct = default);
}
