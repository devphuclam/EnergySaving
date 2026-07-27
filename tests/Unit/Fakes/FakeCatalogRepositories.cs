using IUMP.Modules.Catalog.Domain;
using IUMP.Modules.Catalog.Contracts;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeCatalogTransaction : ICatalogTransaction
{
    private readonly FakeCatalogRepositorySnapshot _snapshot;
    private readonly FakeCatalogCommandRepository _repo;
    public bool IsCommitted { get; private set; }
    public bool IsRolledBack { get; private set; }

    public FakeCatalogTransaction(FakeCatalogCommandRepository repo)
    {
        _repo = repo;
        _snapshot = repo.CreateSnapshot();
    }

    public Task CommitAsync(CancellationToken ct = default) { IsCommitted = true; return Task.CompletedTask; }

    public Task RollbackAsync(CancellationToken ct = default)
    {
        IsRolledBack = true;
        _repo.RestoreSnapshot(_snapshot);
        return Task.CompletedTask;
    }
}

public sealed class FakeCatalogRepositorySnapshot
{
    public Dictionary<Guid, Metric> Metrics { get; } = new();
    public Dictionary<Guid, MetricUnit> Units { get; } = new();
    public List<MetricUnitCompatibility> Compatibilities { get; } = new();
    public Dictionary<Guid, DataSource> DataSources { get; } = new();
    public Dictionary<Guid, SourcePointMapping> Mappings { get; } = new();
}

public sealed class FakeCatalogCommandRepository : ICatalogCommandRepository
{
    private readonly Dictionary<Guid, Metric> _metrics = new();
    private readonly Dictionary<Guid, MetricUnit> _units = new();
    private readonly List<MetricUnitCompatibility> _compatibilities = new();
    private readonly Dictionary<Guid, DataSource> _dataSources = new();
    private readonly Dictionary<Guid, SourcePointMapping> _mappings = new();

    public FakeCatalogRepositorySnapshot CreateSnapshot()
    {
        var s = new FakeCatalogRepositorySnapshot();
        foreach (var kv in _metrics) s.Metrics[kv.Key] = kv.Value;
        foreach (var kv in _units) s.Units[kv.Key] = kv.Value;
        s.Compatibilities.AddRange(_compatibilities);
        foreach (var kv in _dataSources) s.DataSources[kv.Key] = kv.Value;
        foreach (var kv in _mappings) s.Mappings[kv.Key] = kv.Value;
        return s;
    }

    public void RestoreSnapshot(FakeCatalogRepositorySnapshot s)
    {
        _metrics.Clear(); foreach (var kv in s.Metrics) _metrics[kv.Key] = kv.Value;
        _units.Clear(); foreach (var kv in s.Units) _units[kv.Key] = kv.Value;
        _compatibilities.Clear(); _compatibilities.AddRange(s.Compatibilities);
        _dataSources.Clear(); foreach (var kv in s.DataSources) _dataSources[kv.Key] = kv.Value;
        _mappings.Clear(); foreach (var kv in s.Mappings) _mappings[kv.Key] = kv.Value;
    }

    public Task<Metric?> GetMetricAsync(MetricId id, CancellationToken ct = default)
        => Task.FromResult(_metrics.GetValueOrDefault(id.Value));

    public Task<Metric?> FindMetricByCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult(_metrics.Values.FirstOrDefault(m => m.Code.Equals(code, StringComparison.OrdinalIgnoreCase)));

    public Task AddMetricAsync(Metric metric, CancellationToken ct = default)
    {
        if (_metrics.Values.Any(m => m.Code.Equals(metric.Code, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Metric with code '{metric.Code}' already exists.");
        _metrics[metric.Id.Value] = metric;
        return Task.CompletedTask;
    }

    public Task UpdateMetricAsync(Metric metric, CancellationToken ct = default)
    {
        _metrics[metric.Id.Value] = metric;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Metric>> GetAllMetricsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Metric>>(_metrics.Values.ToList());

    public Task<MetricUnit?> GetUnitAsync(UnitId id, CancellationToken ct = default)
        => Task.FromResult(_units.GetValueOrDefault(id.Value));

    public Task<MetricUnit?> FindUnitByCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult(_units.Values.FirstOrDefault(u => u.Code.Equals(code, StringComparison.OrdinalIgnoreCase)));

    public Task AddUnitAsync(MetricUnit unit, CancellationToken ct = default)
    {
        if (_units.Values.Any(u => u.Code.Equals(unit.Code, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Unit with code '{unit.Code}' already exists.");
        _units[unit.Id.Value] = unit;
        return Task.CompletedTask;
    }

    public Task UpdateUnitAsync(MetricUnit unit, CancellationToken ct = default)
    {
        _units[unit.Id.Value] = unit;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MetricUnit>> GetAllUnitsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MetricUnit>>(_units.Values.ToList());

    public Task AddCompatibilityAsync(MetricUnitCompatibility compat, CancellationToken ct = default)
    {
        var existing = _compatibilities.FirstOrDefault(c => c.MetricId == compat.MetricId && c.UnitId == compat.UnitId);
        if (existing != null) _compatibilities.Remove(existing);
        _compatibilities.Add(compat);
        return Task.CompletedTask;
    }

    public Task UpdateCompatibilityAsync(MetricUnitCompatibility compat, CancellationToken ct = default)
    {
        var existing = _compatibilities.FirstOrDefault(c => c.MetricId == compat.MetricId && c.UnitId == compat.UnitId);
        if (existing != null) _compatibilities.Remove(existing);
        _compatibilities.Add(compat);
        return Task.CompletedTask;
    }

    public Task<MetricUnitCompatibility?> GetCompatibilityAsync(MetricId metricId, UnitId unitId, CancellationToken ct = default)
        => Task.FromResult(_compatibilities.FirstOrDefault(c => c.MetricId == metricId && c.UnitId == unitId));

    public Task<IReadOnlyList<MetricUnitCompatibility>> GetCompatibilitiesForMetricAsync(MetricId metricId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MetricUnitCompatibility>>(_compatibilities.Where(c => c.MetricId == metricId).ToList());

    public Task<MetricUnitCompatibility?> GetCanonicalUnitAsync(MetricId metricId, CancellationToken ct = default)
        => Task.FromResult(_compatibilities.FirstOrDefault(c => c.MetricId == metricId && c.IsCanonical));

    public Task<DataSource?> GetDataSourceAsync(DataSourceId id, CancellationToken ct = default)
        => Task.FromResult(_dataSources.GetValueOrDefault(id.Value));

    public Task<DataSource?> FindDataSourceByCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult(_dataSources.Values.FirstOrDefault(ds => ds.Code.Equals(code, StringComparison.OrdinalIgnoreCase)));

    public Task AddDataSourceAsync(DataSource source, CancellationToken ct = default)
    {
        if (_dataSources.Values.Any(ds => ds.Code.Equals(source.Code, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"DataSource with code '{source.Code}' already exists.");
        _dataSources[source.Id.Value] = source;
        return Task.CompletedTask;
    }

    public Task UpdateDataSourceAsync(DataSource source, CancellationToken ct = default)
    {
        _dataSources[source.Id.Value] = source;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DataSource>> GetAllDataSourcesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DataSource>>(_dataSources.Values.ToList());

    public Task<bool> HasDependentRunOrMeasurementAsync(DataSourceId id, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task<SourcePointMapping?> GetMappingAsync(MappingId id, CancellationToken ct = default)
        => Task.FromResult(_mappings.GetValueOrDefault(id.Value));

    public Task AddMappingAsync(SourcePointMapping mapping, CancellationToken ct = default)
    {
        _mappings[mapping.Id.Value] = mapping;
        return Task.CompletedTask;
    }

    public Task UpdateMappingAsync(SourcePointMapping mapping, CancellationToken ct = default)
    {
        _mappings[mapping.Id.Value] = mapping;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SourcePointMapping>> GetMappingsForPointAsync(string pointId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SourcePointMapping>>(_mappings.Values.Where(m => m.PointId == pointId).ToList());

    public Task<IReadOnlyList<SourcePointMapping>> GetMappingsForSourceAsync(DataSourceId dataSourceId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SourcePointMapping>>(_mappings.Values.Where(m => m.DataSourceId == dataSourceId).ToList());

    public Task DeleteMappingAsync(MappingId id, CancellationToken ct = default)
    {
        _mappings.Remove(id.Value);
        return Task.CompletedTask;
    }

    public Task<ICatalogTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => Task.FromResult<ICatalogTransaction>(new FakeCatalogTransaction(this));
}

public sealed class FakeCatalogEligibilityQueryRepository : ICatalogEligibilityQueryRepository
{
    private readonly ICatalogCommandRepository _repo;

    public FakeCatalogEligibilityQueryRepository(ICatalogCommandRepository repo) { _repo = repo; }

    public async Task<MetricUnitEligibility> GetMetricUnitEligibilityAsync(MetricId metricId, UnitId unitId, CancellationToken ct = default)
    {
        var metric = await _repo.GetMetricAsync(metricId, ct);
        var unit = await _repo.GetUnitAsync(unitId, ct);
        var compat = metric != null ? await _repo.GetCompatibilityAsync(metricId, unitId, ct) : null;
        return new MetricUnitEligibility(
            metric != null && unit != null,
            metric?.IsActive() ?? false,
            unit?.IsActive() ?? false,
            compat != null,
            compat?.IsCanonical ?? false,
            compat?.Version ?? 0);
    }

    public async Task<MetricUnitEligibility?> GetCanonicalUnitEligibilityAsync(MetricId metricId, CancellationToken ct = default)
    {
        var canonical = await _repo.GetCanonicalUnitAsync(metricId, ct);
        if (canonical == null) return null;
        return await GetMetricUnitEligibilityAsync(metricId, canonical.UnitId, ct);
    }

    public async Task<SourceMappingEligibility> GetActiveMappingEligibilityAsync(string pointId, DateTime at, CancellationToken ct = default)
    {
        var mappings = await _repo.GetMappingsForPointAsync(pointId, ct);
        var active = mappings.FirstOrDefault(m =>
            m.Status == MappingStatus.Active &&
            m.EffectiveFrom <= at &&
            (m.EffectiveTo == null || m.EffectiveTo > at));
        if (active == null)
            return new SourceMappingEligibility(false, "No active mapping for point at the given time.", null, null, null, null, null, null, pointId, 0);
        var ds = await _repo.GetDataSourceAsync(active.DataSourceId, ct);
        return new SourceMappingEligibility(true, null, active.Id, active.DataSourceId, ds?.Status, active.Status, active.EffectiveFrom, active.EffectiveTo, pointId, active.Version);
    }

    public async Task<IReadOnlyList<SourceMappingEligibility>> GetMappingHistoryAsync(string pointId, CancellationToken ct = default)
    {
        var mappings = await _repo.GetMappingsForPointAsync(pointId, ct);
        var results = new List<SourceMappingEligibility>();
        foreach (var m in mappings)
        {
            var ds = await _repo.GetDataSourceAsync(m.DataSourceId, ct);
            results.Add(new SourceMappingEligibility(true, null, m.Id, m.DataSourceId, ds?.Status, m.Status, m.EffectiveFrom, m.EffectiveTo, pointId, m.Version));
        }
        return results;
    }
}
