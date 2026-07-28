using IUMP.Modules.Telemetry.Contracts;
using IUMP.Modules.Telemetry.Domain;

namespace IUMP.Modules.Telemetry.Application;

public sealed class IngestMeasurement
{
    private readonly ITelemetryIngestionRepository _repository;
    private readonly IImmutableSimulatorConfigurationQuery _configurations;
    private readonly ITelemetryProviderSnapshotQuery _providers;
    private readonly TelemetryPersistenceService _persistence;
    private readonly ITelemetryUtcClock _clock;
    private readonly int _futureSkewSeconds;

    public IngestMeasurement(
        ITelemetryIngestionRepository repository,
        IImmutableSimulatorConfigurationQuery configurations,
        ITelemetryProviderSnapshotQuery providers,
        TelemetryPersistenceService persistence,
        ITelemetryUtcClock clock,
        int futureSkewSeconds = 300)
    {
        if (futureSkewSeconds < 0) throw new ArgumentOutOfRangeException(nameof(futureSkewSeconds));
        _repository = repository;
        _configurations = configurations;
        _providers = providers;
        _persistence = persistence;
        _clock = clock;
        _futureSkewSeconds = futureSkewSeconds;
    }

    public async Task<TelemetryIngestionResult> ExecuteAsync(
        TelemetryMeasurementRequest request,
        TrustedProducerContext producer,
        CancellationToken ct = default)
    {
        if (!producer.IsTrusted ||
            !string.Equals(producer.ProducerIdentity, request.ProducerIdentity, StringComparison.Ordinal) ||
            !string.Equals(producer.ProducerType, "Simulator", StringComparison.Ordinal) ||
            producer.ProducerVersion != 1)
            return TelemetryIngestionResult.Failed(
                "UNTRUSTED_PRODUCER", request.CorrelationId);
        if (!MeasurementIdentityVerifier.TryVerify(request, out var measurementId))
            return TelemetryIngestionResult.Failed(
                "MEASUREMENT_ID_INVALID", request.CorrelationId);

        var fingerprint = TelemetryRequestFingerprintV1.Compute(request);
        var existing = await _repository.GetTerminalAsync(measurementId, ct);
        if (existing is not null)
            return TelemetryTerminalDecision.FromExisting(
                existing, fingerprint, request.CorrelationId);
        var slot = await _repository.GetTerminalBySlotAsync(
            request.SimulatorRunId, request.PointId, request.SourceSequence, ct);
        if (slot is not null && slot.MeasurementId != measurementId)
            return TelemetryIngestionResult.Failed(
                "MEASUREMENT_SLOT_CONFLICT", request.CorrelationId);

        if (!double.IsFinite(request.NumericValue))
            return await _persistence.PersistRejectedAsync(
                measurementId, fingerprint, request, "NUMERIC_VALUE_NONFINITE",
                _clock.UtcNow, null, ct);

        var staticError = ValidateStatic(request);
        if (staticError is not null)
            return await _persistence.PersistRejectedAsync(
                measurementId, fingerprint, request, staticError, _clock.UtcNow, null, ct);

        var configuration = await _configurations.GetVersionAsync(
            request.SimulatorConfigurationId, request.ConfigurationVersion, ct);
        if (configuration is null)
            return await _persistence.PersistRejectedAsync(
                measurementId, fingerprint, request, "CONFIGURATION_VERSION_MISSING",
                _clock.UtcNow, null, ct);

        var receivedAt = _clock.UtcNow;
        var provider = await _providers.GetAsync(request, receivedAt, ct);
        var providerError = ValidateProvider(request, provider);
        if (providerError is not null)
            return await _persistence.PersistRejectedAsync(
                measurementId, fingerprint, request, providerError, _clock.UtcNow, provider, ct);

        var (quality, reason) = Classify(
            request.NumericValue, configuration.MinimumValue, configuration.MaximumValue,
            request.SourceTimestampUtc, receivedAt, _futureSkewSeconds);
        var processingAt = _clock.UtcNow;
        return await _persistence.PersistAcceptedAsync(
            measurementId, fingerprint, request, provider!, quality, reason,
            receivedAt, processingAt, _clock.UtcNow, ct);
    }

    public static (MeasurementQuality Quality, string? Reason) Classify(
        double value,
        double minimum,
        double maximum,
        DateTime sourceTimestampUtc,
        DateTime receivedAtUtc,
        int futureSkewSeconds = 300)
    {
        if (value < minimum || value > maximum)
            return (MeasurementQuality.Bad, "VALUE_OUT_OF_RANGE");
        if (sourceTimestampUtc > receivedAtUtc.AddSeconds(futureSkewSeconds))
            return (MeasurementQuality.Uncertain, "SOURCE_TIMESTAMP_FUTURE");
        return (MeasurementQuality.Good, null);
    }

    private static string? ValidateStatic(TelemetryMeasurementRequest request)
    {
        if (request.SourceId == Guid.Empty || request.SimulatorRunId == Guid.Empty ||
            request.PointId == Guid.Empty || request.MappingId == Guid.Empty ||
            request.SimulatorConfigurationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.AlgorithmId) ||
            string.IsNullOrWhiteSpace(request.UnitCode) ||
            string.IsNullOrWhiteSpace(request.ProducerIdentity) ||
            string.IsNullOrWhiteSpace(request.CorrelationId) ||
            string.IsNullOrWhiteSpace(request.LineageId))
            return "PROVENANCE_INVALID";
        if (request.SourceSequence < 0 || request.MappingVersion <= 0 ||
            request.ConfigurationVersion <= 0 || request.AlgorithmVersion <= 0)
            return "VERSION_INVALID";
        if (request.SourceTimestampUtc.Kind != DateTimeKind.Utc)
            return "SOURCE_TIMESTAMP_NOT_UTC";
        return null;
    }

    private static string? ValidateProvider(
        TelemetryMeasurementRequest request, TelemetryProviderSnapshot? provider)
    {
        if (provider is null || !provider.PointExists) return "POINT_MISSING";
        if (provider.PointId != request.PointId) return "POINT_MISMATCH";
        if (!provider.PointActive) return "POINT_INACTIVE";
        if (!provider.SiteActive) return "SITE_INACTIVE";
        if (!provider.AreaActive) return "AREA_INACTIVE";
        if (!provider.AssetActive) return "ASSET_INACTIVE";
        if (!provider.SourceExists) return "SOURCE_MISSING";
        if (provider.SourceId != request.SourceId) return "SOURCE_MISMATCH";
        if (!provider.SourceActive) return "SOURCE_INACTIVE";
        if (!string.Equals(provider.SourceType, "Simulator", StringComparison.Ordinal))
            return "SOURCE_TYPE_NOT_SIMULATOR";
        if (!provider.MappingExists) return "MAPPING_MISSING";
        if (!provider.MappingActive || !provider.MappingEffective) return "MAPPING_NOT_ACTIVE";
        if (provider.MappingPointId != request.PointId) return "MAPPING_POINT_MISMATCH";
        if (provider.MappingId != request.MappingId ||
            provider.MappingVersion != request.MappingVersion)
            return "MAPPING_VERSION_MISMATCH";
        if (!provider.MetricExists) return "METRIC_MISSING";
        if (!provider.MetricMatchesPoint) return "METRIC_MISMATCH";
        if (!provider.MetricActive) return "METRIC_INACTIVE";
        if (!provider.UnitExists) return "UNIT_MISSING";
        if (!provider.UnitActive) return "UNIT_INACTIVE";
        if (!provider.UnitCompatible) return "UNIT_INCOMPATIBLE";
        if (!string.Equals(provider.UnitCode, request.UnitCode, StringComparison.Ordinal))
            return "UNIT_MISMATCH";
        if (provider.SiteVersion <= 0 || provider.AreaVersion <= 0 ||
            provider.AssetVersion <= 0 || provider.PointVersion <= 0 ||
            provider.SourceVersion <= 0 || provider.MappingVersion <= 0 ||
            provider.MetricVersion <= 0 || provider.UnitVersion <= 0 ||
            provider.CompatibilityVersion <= 0)
            return "PROVIDER_VERSION_INVALID";
        if (string.IsNullOrWhiteSpace(provider.CompatibilityIdentity))
            return "COMPATIBILITY_IDENTITY_MISSING";
        if (!string.Equals(provider.CompatibilityStatus, "Active", StringComparison.Ordinal))
            return "COMPATIBILITY_STATUS_NOT_ACTIVE";
        return null;
    }
}
