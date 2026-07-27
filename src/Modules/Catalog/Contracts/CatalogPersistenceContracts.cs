using IUMP.Modules.Catalog.Domain;

namespace IUMP.Modules.Catalog.Contracts;

public interface ICatalogTransaction
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

public interface ICatalogCommandRepository
{
    Task<Metric?> GetMetricAsync(MetricId id, CancellationToken ct = default);
    Task<Metric?> FindMetricByCodeAsync(string code, CancellationToken ct = default);
    Task AddMetricAsync(Metric metric, CancellationToken ct = default);
    Task UpdateMetricAsync(Metric metric, CancellationToken ct = default);
    Task<IReadOnlyList<Metric>> GetAllMetricsAsync(CancellationToken ct = default);

    Task<MetricUnit?> GetUnitAsync(UnitId id, CancellationToken ct = default);
    Task<MetricUnit?> FindUnitByCodeAsync(string code, CancellationToken ct = default);
    Task AddUnitAsync(MetricUnit unit, CancellationToken ct = default);
    Task UpdateUnitAsync(MetricUnit unit, CancellationToken ct = default);
    Task<IReadOnlyList<MetricUnit>> GetAllUnitsAsync(CancellationToken ct = default);

    Task AddCompatibilityAsync(MetricUnitCompatibility compat, CancellationToken ct = default);
    Task UpdateCompatibilityAsync(MetricUnitCompatibility compat, CancellationToken ct = default);
    Task<MetricUnitCompatibility?> GetCompatibilityAsync(MetricId metricId, UnitId unitId, CancellationToken ct = default);
    Task<IReadOnlyList<MetricUnitCompatibility>> GetCompatibilitiesForMetricAsync(MetricId metricId, CancellationToken ct = default);
    Task<MetricUnitCompatibility?> GetCanonicalUnitAsync(MetricId metricId, CancellationToken ct = default);

    Task<DataSource?> GetDataSourceAsync(DataSourceId id, CancellationToken ct = default);
    Task<DataSource?> FindDataSourceByCodeAsync(string code, CancellationToken ct = default);
    Task AddDataSourceAsync(DataSource source, CancellationToken ct = default);
    Task UpdateDataSourceAsync(DataSource source, CancellationToken ct = default);
    Task<IReadOnlyList<DataSource>> GetAllDataSourcesAsync(CancellationToken ct = default);
    Task<bool> HasDependentRunOrMeasurementAsync(DataSourceId id, CancellationToken ct = default);
    Task<CatalogDependencySnapshot> GetDataSourceDependencySnapshotAsync(DataSourceId id, CancellationToken ct = default);
    Task<CatalogDeletionDecision> DeleteDataSourceAsync(DataSourceId id, CancellationToken ct = default);

    Task<SourcePointMapping?> GetMappingAsync(MappingId id, CancellationToken ct = default);
    Task AddMappingAsync(SourcePointMapping mapping, CancellationToken ct = default);
    Task UpdateMappingAsync(SourcePointMapping mapping, CancellationToken ct = default);
    Task<IReadOnlyList<SourcePointMapping>> GetMappingsForPointAsync(string pointId, CancellationToken ct = default);
    Task<IReadOnlyList<SourcePointMapping>> GetMappingsForSourceAsync(DataSourceId dataSourceId, CancellationToken ct = default);
    Task<CatalogDependencySnapshot> GetMappingDependencySnapshotAsync(MappingId id, CancellationToken ct = default);
    Task<CatalogDeletionDecision> DeleteMappingAsync(MappingId id, CancellationToken ct = default);

    Task<ICatalogTransaction> BeginTransactionAsync(CancellationToken ct = default);
}
