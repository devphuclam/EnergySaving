using IUMP.Modules.Catalog.Domain;

namespace IUMP.Modules.Catalog.Contracts;

public enum MetricUnitEligibilityOutcome
{
    MissingMetric,
    MissingUnit,
    InactiveMetric,
    InactiveUnit,
    Incompatible,
    Eligible
}

public sealed record MetricUnitEligibility(
    bool Exists,
    bool MetricActive,
    bool UnitActive,
    bool IsCompatible,
    bool IsCanonical,
    long Version,
    MetricUnitEligibilityOutcome Outcome = MetricUnitEligibilityOutcome.Incompatible)
{
    public bool IsEligible => Outcome == MetricUnitEligibilityOutcome.Eligible;
}

public enum MappingEligibilityOutcome { Missing, Multiple, Eligible }

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
    long Version,
    MappingEligibilityOutcome Outcome = MappingEligibilityOutcome.Missing);

public sealed record CatalogSourceMappingSnapshot(
    MappingId MappingId,
    DataSourceId DataSourceId,
    string PointId,
    SourceStatus SourceStatus,
    MappingStatus MappingStatus,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    long Version);

public interface ISourceMappingSnapshotQuery
{
    Task<CatalogSourceMappingSnapshot?> GetSourceMappingSnapshotAsync(MappingId mappingId, CancellationToken ct = default);
}

public sealed record ReadinessVersionTuple(
    long PointVersion,
    long AssetVersion,
    long AreaVersion,
    long SiteVersion)
{
    public static ReadinessVersionTuple Empty => new(0, 0, 0, 0);
}

public sealed record PointReadinessSnapshot(
    string PointId,
    string SiteId,
    string? AreaId,
    bool Exists,
    bool IsConfigurationReady,
    bool IsProducingReady,
    long ProviderVersion,
    ReadinessVersionTuple ReadinessVersions)
{
    public PointReadinessSnapshot(string pointId, string siteId, string? areaId, bool exists,
        bool isConfigurationReady, bool isProducingReady, long providerVersion)
        : this(pointId, siteId, areaId, exists, isConfigurationReady, isProducingReady,
              providerVersion, ReadinessVersionTuple.Empty)
    {
    }
}

public interface ICatalogPointReadinessQuery
{
    Task<PointReadinessSnapshot?> GetPointReadinessAsync(string pointId, CancellationToken ct = default);
}

public sealed record CatalogSourceMappedScopeSnapshot(
    MappingId MappingId,
    long MappingVersion,
    string PointId,
    string SiteId,
    string AreaId,
    ReadinessVersionTuple OrganizationReadinessVersions);

public sealed record CatalogSourceScopeSnapshot(
    Guid SourceId,
    bool Exists,
    string SourceType,
    string SourceStatus,
    long SourceVersion,
    IReadOnlyList<CatalogSourceMappedScopeSnapshot> MappedScopes);

public interface ICatalogSourceScopeQuery
{
    Task<CatalogSourceScopeSnapshot?> GetSourceScopeAsync(Guid sourceId, CancellationToken ct = default);
}

public interface ICatalogEligibilityQueryRepository
{
    Task<MetricUnitEligibility> GetMetricUnitEligibilityAsync(MetricId metricId, UnitId unitId, CancellationToken ct = default);
    Task<MetricUnitEligibility?> GetCanonicalUnitEligibilityAsync(MetricId metricId, CancellationToken ct = default);
    Task<SourceMappingEligibility> GetActiveMappingEligibilityAsync(string pointId, DateTime at, CancellationToken ct = default);
    Task<IReadOnlyList<SourceMappingEligibility>> GetMappingHistoryAsync(string pointId, CancellationToken ct = default);
}
