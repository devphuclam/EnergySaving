using IUMP.Modules.Telemetry.Contracts;
using IUMP.Modules.Telemetry.Domain;

namespace IUMP.Modules.Telemetry.Application;

public sealed class TelemetryPersistenceService
{
    private readonly ITelemetryFlowUnitOfWork _unitOfWork;
    private readonly ITelemetryIngestionRepository _repository;
    private readonly ILatestProjectionRepository _latest;
    private readonly IMeasurementAcceptedEventWriter _events;
    private readonly ITelemetryProviderSnapshotQuery _providers;

    public TelemetryPersistenceService(
        ITelemetryFlowUnitOfWork unitOfWork,
        ITelemetryIngestionRepository repository,
        ILatestProjectionRepository latest,
        IMeasurementAcceptedEventWriter events,
        ITelemetryProviderSnapshotQuery providers)
    {
        _unitOfWork = unitOfWork;
        _repository = repository;
        _latest = latest;
        _events = events;
        _providers = providers;
    }

    public async Task<TelemetryIngestionResult> PersistAcceptedAsync(
        Guid measurementId,
        byte[] fingerprint,
        TelemetryMeasurementRequest request,
        TelemetryProviderSnapshot provider,
        MeasurementQuality quality,
        string? reasonCode,
        DateTime receivedAtUtc,
        DateTime processingAtUtc,
        DateTime completedAtUtc,
        CancellationToken ct = default)
    {
        await using var transaction = await _unitOfWork.BeginRepeatableReadAsync(ct);
        try
        {
            await AcquireOwnerLocksAsync(transaction, request, ct);
            if (!await _providers.RecheckAsync(provider, transaction, ct))
                throw new InvalidOperationException("PROVIDER_VERSION_DRIFT");
            var existing = await _repository.RecheckTerminalAsync(measurementId, transaction, ct);
            if (existing is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return TelemetryTerminalDecision.FromExisting(
                    existing, fingerprint, request.CorrelationId);
            }

            var raw = new RawMeasurement(
                measurementId, request.SourceId, request.SimulatorRunId, request.PointId,
                request.MappingId, request.MappingVersion, request.SourceSequence,
                request.SourceTimestampUtc, receivedAtUtc, processingAtUtc,
                request.NumericValue, request.UnitCode, quality, reasonCode,
                request.CorrelationId, request.LineageId);
            var latestCandidate = new LatestProjectionCandidate(
                measurementId, request.PointId, request.SourceTimestampUtc,
                request.SourceSequence, processingAtUtc, quality);
            var latestAdvanced = quality != MeasurementQuality.Bad &&
                await _latest.EvaluateAdvanceAsync(latestCandidate, transaction, ct);
            var terminal = new TelemetryTerminalResult(
                measurementId, request.SourceId, request.SimulatorRunId, request.PointId,
                request.MappingId, request.MappingVersion, request.SourceSequence,
                request.AlgorithmId, request.AlgorithmVersion,
                request.SimulatorConfigurationId, request.ConfigurationVersion,
                TelemetryFinalClassification.Accepted, true, measurementId,
                quality, reasonCode, null, latestAdvanced, completedAtUtc,
                SafeProvenance(request.CorrelationId, "correlation", measurementId),
                SafeProvenance(request.LineageId, "lineage", measurementId),
                fingerprint.ToArray());
            await _repository.StageTerminalAsync(terminal, transaction, ct);
            await _repository.StageRawAsync(raw, transaction, ct);
            if (quality != MeasurementQuality.Bad)
                await _latest.StageAdvanceAsync(
                    latestCandidate, latestAdvanced, transaction, ct);
            await transaction.AcquireLockAsync(
                TelemetryFlowLockTarget.IntegrationOutbox, measurementId.ToString("D"), ct);
            await _events.StageAsync(MeasurementAcceptedEventFactory.Create(
                raw, latestAdvanced, provider), transaction, ct);
            await transaction.CommitAsync(ct);
            return new TelemetryIngestionResult(
                TelemetryDisposition.Accepted, terminal.Copy(), null, request.CorrelationId);
        }
        catch (TelemetryUniqueRaceException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return await ResolveUniqueRaceAsync(
                measurementId, fingerprint, request, ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<TelemetryIngestionResult> PersistRejectedAsync(
        Guid measurementId,
        byte[] fingerprint,
        TelemetryMeasurementRequest request,
        string rejectionCode,
        DateTime completedAtUtc,
        TelemetryProviderSnapshot? provider,
        CancellationToken ct = default)
    {
        await using var transaction = await _unitOfWork.BeginRepeatableReadAsync(ct);
        try
        {
            await AcquireOwnerLocksAsync(transaction, request, ct);
            if (provider is not null &&
                !await _providers.RecheckAsync(provider, transaction, ct))
                throw new InvalidOperationException("PROVIDER_VERSION_DRIFT");
            var existing = await _repository.RecheckTerminalAsync(measurementId, transaction, ct);
            if (existing is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return TelemetryTerminalDecision.FromExisting(
                    existing, fingerprint, request.CorrelationId);
            }
            var terminal = new TelemetryTerminalResult(
                measurementId, request.SourceId, request.SimulatorRunId, request.PointId,
                request.MappingId, request.MappingVersion, request.SourceSequence,
                request.AlgorithmId, request.AlgorithmVersion,
                request.SimulatorConfigurationId, request.ConfigurationVersion,
                TelemetryFinalClassification.Rejected, false, null,
                null, null, rejectionCode, null, completedAtUtc,
                SafeProvenance(request.CorrelationId, "correlation", measurementId),
                SafeProvenance(request.LineageId, "lineage", measurementId),
                fingerprint.ToArray());
            await _repository.StageTerminalAsync(terminal, transaction, ct);
            await transaction.AcquireLockAsync(
                TelemetryFlowLockTarget.IntegrationOutbox, measurementId.ToString("D"), ct);
            await transaction.CommitAsync(ct);
            return new TelemetryIngestionResult(
                TelemetryDisposition.Rejected, terminal.Copy(), null,
                terminal.OriginalCorrelationId);
        }
        catch (TelemetryUniqueRaceException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return await ResolveUniqueRaceAsync(
                measurementId, fingerprint, request, ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async ValueTask AcquireOwnerLocksAsync(
        ITelemetryFlowTransaction transaction,
        TelemetryMeasurementRequest request,
        CancellationToken ct)
    {
        await transaction.AcquireLockAsync(
            TelemetryFlowLockTarget.OrganizationPoint, request.PointId.ToString("D"), ct);
        await transaction.AcquireLockAsync(
            TelemetryFlowLockTarget.CatalogSourceMappingMetricUnit,
            $"{request.SourceId:D}|{request.MappingId:D}", ct);
        await transaction.AcquireLockAsync(
            TelemetryFlowLockTarget.TelemetryIdentityRawLatest, request.MeasurementId, ct);
    }

    private async Task<TelemetryIngestionResult> ResolveUniqueRaceAsync(
        Guid measurementId,
        byte[] fingerprint,
        TelemetryMeasurementRequest request,
        CancellationToken ct)
    {
        var identityWinner = await _repository.GetTerminalAsync(measurementId, ct);
        if (identityWinner is not null)
            return TelemetryTerminalDecision.FromExisting(
                identityWinner, fingerprint, request.CorrelationId);
        var slotWinner = await _repository.GetTerminalBySlotAsync(
            request.SimulatorRunId, request.PointId, request.SourceSequence, ct);
        if (slotWinner is not null)
            return TelemetryIngestionResult.Failed(
                "MEASUREMENT_SLOT_CONFLICT", request.CorrelationId);
        throw new InvalidOperationException("TELEMETRY_UNIQUE_RACE_WINNER_MISSING");
    }

    private static string SafeProvenance(string value, string kind, Guid measurementId) =>
        string.IsNullOrWhiteSpace(value)
            ? $"telemetry-{kind}-{measurementId:D}"
            : value;
}

public static class MeasurementAcceptedEventFactory
{
    private static readonly HashSet<string> AllowedAfter = new(StringComparer.Ordinal)
    {
        "measurementId", "sourceId", "simulatorRunId", "pointId", "mappingId",
        "mappingVersion", "sourceSequence", "sourceTimestampUtc", "receivedAtUtc",
        "processingAtUtc", "numericValue", "unitCode", "qualityCode", "reasonCode",
        "latestAdvanced", "correlationId", "lineageId"
    };

    public static TelemetryOwnerEvent Create(
        RawMeasurement measurement,
        bool latestAdvanced,
        TelemetryProviderSnapshot provider)
    {
        var after = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["measurementId"] = measurement.MeasurementId.ToString("D"),
            ["sourceId"] = measurement.SourceId.ToString("D"),
            ["simulatorRunId"] = measurement.SimulatorRunId.ToString("D"),
            ["pointId"] = measurement.PointId.ToString("D"),
            ["mappingId"] = measurement.MappingId.ToString("D"),
            ["mappingVersion"] = measurement.MappingVersion,
            ["sourceSequence"] = measurement.SourceSequence,
            ["sourceTimestampUtc"] = measurement.SourceTimestampUtc,
            ["receivedAtUtc"] = measurement.ReceivedAtUtc,
            ["processingAtUtc"] = measurement.ProcessingAtUtc,
            ["numericValue"] = measurement.NumericValue,
            ["unitCode"] = measurement.UnitCode,
            ["qualityCode"] = measurement.QualityCode.ToString(),
            ["reasonCode"] = measurement.ReasonCode,
            ["latestAdvanced"] = latestAdvanced,
            ["correlationId"] = measurement.CorrelationId,
            ["lineageId"] = measurement.LineageId
        };
        if (after.Keys.Any(key => !AllowedAfter.Contains(key)))
            throw new InvalidOperationException("EVENT_ALLOWLIST_VIOLATION");
        return new TelemetryOwnerEvent(
            Guid.NewGuid(), "MeasurementAccepted.v1", 1, "IUMP.Telemetry",
            "Measurement", measurement.MeasurementId, 1, "IUMP.Telemetry",
            "trusted-simulator", "Accepted", "Measurement accepted.",
            measurement.ProcessingAtUtc, measurement.CorrelationId, null,
            provider.TrustedSiteId, provider.TrustedAreaId,
            new Dictionary<string, object?>(StringComparer.Ordinal), after);
    }
}
