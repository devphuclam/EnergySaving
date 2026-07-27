using IUMP.Modules.Catalog.Domain;

namespace IUMP.Modules.Catalog.Contracts;

public sealed record MetricUnitEligibility(
    bool Exists,
    bool MetricActive,
    bool UnitActive,
    bool IsCompatible,
    bool IsCanonical,
    long Version);

public sealed record SourceMappingEligibility(
    bool Exists,
    string? FailureReason,
    MappingId? MappingId,
    DataSourceId? DataSourceId,
    SourceStatus? SourceStatus,
    MappingStatus? MappingStatus,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo,
    string? PointId,
    long Version);

public interface ICatalogEligibilityQueryRepository
{
    Task<MetricUnitEligibility> GetMetricUnitEligibilityAsync(MetricId metricId, UnitId unitId, CancellationToken ct = default);
    Task<MetricUnitEligibility?> GetCanonicalUnitEligibilityAsync(MetricId metricId, CancellationToken ct = default);
    Task<SourceMappingEligibility> GetActiveMappingEligibilityAsync(string pointId, DateTime at, CancellationToken ct = default);
    Task<IReadOnlyList<SourceMappingEligibility>> GetMappingHistoryAsync(string pointId, CancellationToken ct = default);
}
