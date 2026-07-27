using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeCatalogTransaction : ICatalogTransaction
{
    private readonly FakeCatalogCommandRepository _repo;
    private readonly FakeCatalogRepositorySnapshot _snapshot;
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
        if (!IsCommitted)
        {
            IsRolledBack = true;
            _repo.RestoreSnapshot(_snapshot);
        }
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
    public Dictionary<Guid, CatalogDependencySnapshot> SourceDependencies { get; } = new();
    public Dictionary<Guid, CatalogDependencySnapshot> MappingDependencies { get; } = new();
}

public sealed class FakeCatalogCommandRepository : ICatalogCommandRepository
{
    private readonly Dictionary<Guid, Metric> _metrics = new();
    private readonly Dictionary<Guid, MetricUnit> _units = new();
    private readonly Dictionary<(Guid MetricId, Guid UnitId), MetricUnitCompatibility> _compatibilities = new();
    private readonly Dictionary<Guid, DataSource> _dataSources = new();
    private readonly Dictionary<Guid, SourcePointMapping> _mappings = new();
    private readonly Dictionary<Guid, CatalogDependencySnapshot> _sourceDependencies = new();
    private readonly Dictionary<Guid, CatalogDependencySnapshot> _mappingDependencies = new();

    public void SetDataSourceDependencies(DataSourceId id, CatalogDependencySnapshot snapshot) => _sourceDependencies[id.Value] = snapshot;
    public void SetMappingDependencies(MappingId id, CatalogDependencySnapshot snapshot) => _mappingDependencies[id.Value] = snapshot;

    public FakeCatalogRepositorySnapshot CreateSnapshot()
    {
        var snapshot = new FakeCatalogRepositorySnapshot();
        foreach (var item in _metrics) snapshot.Metrics[item.Key] = Clone(item.Value);
        foreach (var item in _units) snapshot.Units[item.Key] = Clone(item.Value);
        snapshot.Compatibilities.AddRange(_compatibilities.Values.Select(Clone));
        foreach (var item in _dataSources) snapshot.DataSources[item.Key] = Clone(item.Value);
        foreach (var item in _mappings) snapshot.Mappings[item.Key] = Clone(item.Value);
        foreach (var item in _sourceDependencies) snapshot.SourceDependencies[item.Key] = item.Value;
        foreach (var item in _mappingDependencies) snapshot.MappingDependencies[item.Key] = item.Value;
        return snapshot;
    }

    public void RestoreSnapshot(FakeCatalogRepositorySnapshot snapshot)
    {
        _metrics.Clear(); foreach (var item in snapshot.Metrics) _metrics[item.Key] = Clone(item.Value);
        _units.Clear(); foreach (var item in snapshot.Units) _units[item.Key] = Clone(item.Value);
        _compatibilities.Clear(); foreach (var item in snapshot.Compatibilities) _compatibilities[(item.MetricId.Value, item.UnitId.Value)] = Clone(item);
        _dataSources.Clear(); foreach (var item in snapshot.DataSources) _dataSources[item.Key] = Clone(item.Value);
        _mappings.Clear(); foreach (var item in snapshot.Mappings) _mappings[item.Key] = Clone(item.Value);
        _sourceDependencies.Clear(); foreach (var item in snapshot.SourceDependencies) _sourceDependencies[item.Key] = item.Value;
        _mappingDependencies.Clear(); foreach (var item in snapshot.MappingDependencies) _mappingDependencies[item.Key] = item.Value;
    }

    public Task<Metric?> GetMetricAsync(MetricId id, CancellationToken ct = default) => Task.FromResult(_metrics.TryGetValue(id.Value, out var value) ? Clone(value) : null);
    public Task<Metric?> FindMetricByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(_metrics.Values.FirstOrDefault(m => m.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase)) is { } m ? Clone(m) : null);

    public Task AddMetricAsync(Metric metric, CancellationToken ct = default)
    {
        if (_metrics.ContainsKey(metric.Id.Value) || _metrics.Values.Any(m => m.Code.Equals(metric.Code, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Metric code or ID already exists.");
        _metrics[metric.Id.Value] = Clone(metric);
        return Task.CompletedTask;
    }

    public Task UpdateMetricAsync(Metric metric, CancellationToken ct = default)
    {
        if (!_metrics.TryGetValue(metric.Id.Value, out var current)) throw new InvalidOperationException("Metric not found.");
        if (metric.Version <= current.Version) throw new InvalidOperationException("VERSION_CONFLICT");
        if (_metrics.Values.Any(m => m.Id != metric.Id && m.Code.Equals(metric.Code, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Metric code already exists.");
        _metrics[metric.Id.Value] = Clone(metric);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Metric>> GetAllMetricsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Metric>>(_metrics.Values.Select(Clone).ToList());

    public Task<MetricUnit?> GetUnitAsync(UnitId id, CancellationToken ct = default) => Task.FromResult(_units.TryGetValue(id.Value, out var value) ? Clone(value) : null);
    public Task<MetricUnit?> FindUnitByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(_units.Values.FirstOrDefault(u => u.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase)) is { } u ? Clone(u) : null);

    public Task AddUnitAsync(MetricUnit unit, CancellationToken ct = default)
    {
        if (_units.ContainsKey(unit.Id.Value) || _units.Values.Any(u => u.Code.Equals(unit.Code, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Unit code or ID already exists.");
        _units[unit.Id.Value] = Clone(unit);
        return Task.CompletedTask;
    }

    public Task UpdateUnitAsync(MetricUnit unit, CancellationToken ct = default)
    {
        if (!_units.TryGetValue(unit.Id.Value, out var current)) throw new InvalidOperationException("Unit not found.");
        if (unit.Version <= current.Version) throw new InvalidOperationException("VERSION_CONFLICT");
        if (_units.Values.Any(u => u.Id != unit.Id && u.Code.Equals(unit.Code, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Unit code already exists.");
        _units[unit.Id.Value] = Clone(unit);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MetricUnit>> GetAllUnitsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MetricUnit>>(_units.Values.Select(Clone).ToList());

    public Task AddCompatibilityAsync(MetricUnitCompatibility compat, CancellationToken ct = default)
    {
        var key = (compat.MetricId.Value, compat.UnitId.Value);
        if (_compatibilities.ContainsKey(key)) throw new InvalidOperationException("Compatibility pair already exists.");
        if (!_metrics.ContainsKey(compat.MetricId.Value) || !_units.ContainsKey(compat.UnitId.Value)) throw new InvalidOperationException("Metric and Unit must exist.");
        if (compat.IsCanonical && _compatibilities.Values.Any(c => c.MetricId == compat.MetricId && c.IsCanonical)) throw new InvalidOperationException("Only one canonical Unit is allowed per Metric.");
        _compatibilities[key] = Clone(compat);
        return Task.CompletedTask;
    }

    public Task UpdateCompatibilityAsync(MetricUnitCompatibility compat, CancellationToken ct = default)
    {
        var key = (compat.MetricId.Value, compat.UnitId.Value);
        if (!_compatibilities.TryGetValue(key, out var current)) throw new InvalidOperationException("Compatibility not found.");
        if (compat.Version <= current.Version) throw new InvalidOperationException("VERSION_CONFLICT");
        if (compat.IsCanonical && _compatibilities.Values.Any(c => c.MetricId == compat.MetricId && c.UnitId != compat.UnitId && c.IsCanonical)) throw new InvalidOperationException("Only one canonical Unit is allowed per Metric.");
        _compatibilities[key] = Clone(compat);
        return Task.CompletedTask;
    }

    public Task<MetricUnitCompatibility?> GetCompatibilityAsync(MetricId metricId, UnitId unitId, CancellationToken ct = default) => Task.FromResult(_compatibilities.TryGetValue((metricId.Value, unitId.Value), out var c) ? Clone(c) : null);
    public Task<IReadOnlyList<MetricUnitCompatibility>> GetCompatibilitiesForMetricAsync(MetricId metricId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MetricUnitCompatibility>>(_compatibilities.Values.Where(c => c.MetricId == metricId).Select(Clone).ToList());
    public Task<MetricUnitCompatibility?> GetCanonicalUnitAsync(MetricId metricId, CancellationToken ct = default) => Task.FromResult(_compatibilities.Values.FirstOrDefault(c => c.MetricId == metricId && c.IsCanonical) is { } c ? Clone(c) : null);

    public Task<DataSource?> GetDataSourceAsync(DataSourceId id, CancellationToken ct = default) => Task.FromResult(_dataSources.TryGetValue(id.Value, out var source) ? Clone(source) : null);
    public Task<DataSource?> FindDataSourceByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(_dataSources.Values.FirstOrDefault(s => s.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase)) is { } s ? Clone(s) : null);

    public Task AddDataSourceAsync(DataSource source, CancellationToken ct = default)
    {
        if (_dataSources.ContainsKey(source.Id.Value) || _dataSources.Values.Any(s => s.Code.Equals(source.Code, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Data source code or ID already exists.");
        _dataSources[source.Id.Value] = Clone(source);
        return Task.CompletedTask;
    }

    public Task UpdateDataSourceAsync(DataSource source, CancellationToken ct = default)
    {
        if (!_dataSources.TryGetValue(source.Id.Value, out var current)) throw new InvalidOperationException("Data source not found.");
        if (source.Version <= current.Version) throw new InvalidOperationException("VERSION_CONFLICT");
        _dataSources[source.Id.Value] = Clone(source);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DataSource>> GetAllDataSourcesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DataSource>>(_dataSources.Values.Select(Clone).ToList());
    public Task<bool> HasDependentRunOrMeasurementAsync(DataSourceId id, CancellationToken ct = default) => Task.FromResult(_sourceDependencies.TryGetValue(id.Value, out var d) && d.HasOperationalDependency);
    public Task<CatalogDependencySnapshot> GetDataSourceDependencySnapshotAsync(DataSourceId id, CancellationToken ct = default) => Task.FromResult(_sourceDependencies.GetValueOrDefault(id.Value) ?? new CatalogDependencySnapshot());

    public Task<CatalogDeletionDecision> DeleteDataSourceAsync(DataSourceId id, CancellationToken ct = default)
    {
        if (!_dataSources.TryGetValue(id.Value, out var source)) return Task.FromResult(CatalogDeletionDecision.NotFound());
        var deps = _sourceDependencies.GetValueOrDefault(id.Value) ?? new CatalogDependencySnapshot();
        if (_mappings.Values.Any(mapping => mapping.DataSourceId == id)) deps = deps with { MappingUsage = true };
        if (source.Status != SourceStatus.Draft) return Task.FromResult(CatalogDeletionDecision.InvalidState("Only Draft data sources may be physically deleted."));
        if (deps.HasOperationalDependency) return Task.FromResult(CatalogDeletionDecision.DependentHistory());
        _dataSources.Remove(id.Value);
        return Task.FromResult(CatalogDeletionDecision.Allowed());
    }

    public Task<SourcePointMapping?> GetMappingAsync(MappingId id, CancellationToken ct = default) => Task.FromResult(_mappings.TryGetValue(id.Value, out var mapping) ? Clone(mapping) : null);

    public Task AddMappingAsync(SourcePointMapping mapping, CancellationToken ct = default)
    {
        if (_mappings.ContainsKey(mapping.Id.Value)) throw new InvalidOperationException("Mapping ID already exists.");
        if (!_dataSources.ContainsKey(mapping.DataSourceId.Value)) throw new InvalidOperationException("Data source must exist.");
        if (mapping.IsActive && _mappings.Values.Any(m => mapping.OverlapsWith(m))) throw new InvalidOperationException("Active mapping periods overlap.");
        _mappings[mapping.Id.Value] = Clone(mapping);
        return Task.CompletedTask;
    }

    public Task UpdateMappingAsync(SourcePointMapping mapping, CancellationToken ct = default)
    {
        if (!_mappings.TryGetValue(mapping.Id.Value, out var current)) throw new InvalidOperationException("Mapping not found.");
        if (mapping.Version <= current.Version) throw new InvalidOperationException("VERSION_CONFLICT");
        if (mapping.IsActive && _mappings.Values.Any(m => mapping.OverlapsWith(m))) throw new InvalidOperationException("Active mapping periods overlap.");
        _mappings[mapping.Id.Value] = Clone(mapping);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SourcePointMapping>> GetMappingsForPointAsync(string pointId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SourcePointMapping>>(_mappings.Values.Where(m => m.PointId.Equals(pointId.Trim(), StringComparison.Ordinal)).Select(Clone).ToList());
    public Task<IReadOnlyList<SourcePointMapping>> GetMappingsForSourceAsync(DataSourceId id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SourcePointMapping>>(_mappings.Values.Where(m => m.DataSourceId == id).Select(Clone).ToList());
    public Task<CatalogDependencySnapshot> GetMappingDependencySnapshotAsync(MappingId id, CancellationToken ct = default) => Task.FromResult(_mappingDependencies.GetValueOrDefault(id.Value) ?? new CatalogDependencySnapshot());

    public Task<CatalogDeletionDecision> DeleteMappingAsync(MappingId id, CancellationToken ct = default)
    {
        if (!_mappings.TryGetValue(id.Value, out var mapping)) return Task.FromResult(CatalogDeletionDecision.NotFound());
        var deps = _mappingDependencies.GetValueOrDefault(id.Value) ?? new CatalogDependencySnapshot();
        if (mapping.Status != MappingStatus.Draft) return Task.FromResult(CatalogDeletionDecision.InvalidState("Only Draft mappings may be physically deleted."));
        if (deps.HasOperationalDependency) return Task.FromResult(CatalogDeletionDecision.DependentHistory());
        _mappings.Remove(id.Value);
        return Task.FromResult(CatalogDeletionDecision.Allowed());
    }

    // Used only to model a provider returning corrupted duplicate rows for the Missing/Multiple contract test.
    public void SeedRawMappingForEligibility(SourcePointMapping mapping) => _mappings[mapping.Id.Value] = Clone(mapping);

    public Task<ICatalogTransaction> BeginTransactionAsync(CancellationToken ct = default) => Task.FromResult<ICatalogTransaction>(new FakeCatalogTransaction(this));

    private static Metric Clone(Metric value) => new(value.Id, value.Code, value.Name, value.Status, value.Version);
    private static MetricUnit Clone(MetricUnit value) => new(value.Id, value.Code, value.Symbol, value.Status, value.Version);
    private static MetricUnitCompatibility Clone(MetricUnitCompatibility value) => new(value.MetricId, value.UnitId, value.IsCanonical, value.Version);
    private static DataSource Clone(DataSource value) => new(value.Id, value.Code, value.Name, value.SourceType, value.Status, value.Version);
    private static SourcePointMapping Clone(SourcePointMapping value) => new(value.Id, value.DataSourceId, value.PointId, value.Status, value.EffectiveFrom, value.EffectiveTo, value.Version);
}

public sealed class FakeCatalogEligibilityQueryRepository : ICatalogEligibilityQueryRepository, ISourceMappingSnapshotQuery
{
    private readonly ICatalogCommandRepository _repo;
    public FakeCatalogEligibilityQueryRepository(ICatalogCommandRepository repo) => _repo = repo;

    public async Task<MetricUnitEligibility> GetMetricUnitEligibilityAsync(MetricId metricId, UnitId unitId, CancellationToken ct = default)
    {
        var metric = await _repo.GetMetricAsync(metricId, ct);
        if (metric is null) return new(false, false, false, false, false, 0, MetricUnitEligibilityOutcome.MissingMetric);
        var unit = await _repo.GetUnitAsync(unitId, ct);
        if (unit is null) return new(false, metric.IsActive(), false, false, false, metric.Version, MetricUnitEligibilityOutcome.MissingUnit);
        var compat = await _repo.GetCompatibilityAsync(metricId, unitId, ct);
        if (!metric.IsActive()) return new(true, false, unit.IsActive(), compat is not null, compat?.IsCanonical ?? false, compat?.Version ?? metric.Version, MetricUnitEligibilityOutcome.InactiveMetric);
        if (!unit.IsActive()) return new(true, true, false, compat is not null, compat?.IsCanonical ?? false, compat?.Version ?? unit.Version, MetricUnitEligibilityOutcome.InactiveUnit);
        if (compat is null) return new(true, true, true, false, false, 0, MetricUnitEligibilityOutcome.Incompatible);
        return new(true, true, true, true, compat.IsCanonical, compat.Version, MetricUnitEligibilityOutcome.Eligible);
    }

    public async Task<MetricUnitEligibility?> GetCanonicalUnitEligibilityAsync(MetricId metricId, CancellationToken ct = default)
    {
        var canonical = await _repo.GetCanonicalUnitAsync(metricId, ct);
        return canonical is null ? null : await GetMetricUnitEligibilityAsync(metricId, canonical.UnitId, ct);
    }

    public async Task<SourceMappingEligibility> GetActiveMappingEligibilityAsync(string pointId, DateTime at, CancellationToken ct = default)
    {
        var mappings = await _repo.GetMappingsForPointAsync(pointId, ct);
        var utcAt = at.Kind == DateTimeKind.Utc ? at : DateTime.SpecifyKind(at, DateTimeKind.Utc);
        var active = mappings.Where(m => m.IsActive && m.EffectiveFrom <= utcAt && (m.EffectiveTo is null || m.EffectiveTo > utcAt)).ToList();
        if (active.Count == 0) return new(false, "No active mapping", null, null, null, null, null, null, pointId, 0, MappingEligibilityOutcome.Missing);
        if (active.Count > 1) return new(false, "Multiple active mappings", null, null, null, null, null, null, pointId, 0, MappingEligibilityOutcome.Multiple);
        var mapping = active[0];
        var source = await _repo.GetDataSourceAsync(mapping.DataSourceId, ct);
        if (source is null || source.Status != SourceStatus.Active) return new(false, "Source is not active", mapping.Id, mapping.DataSourceId, source?.Status, mapping.Status, mapping.EffectiveFrom, mapping.EffectiveTo, pointId, mapping.Version, MappingEligibilityOutcome.Missing);
        return new(true, null, mapping.Id, mapping.DataSourceId, source.Status, mapping.Status, mapping.EffectiveFrom, mapping.EffectiveTo, pointId, mapping.Version, MappingEligibilityOutcome.Eligible);
    }

    public async Task<IReadOnlyList<SourceMappingEligibility>> GetMappingHistoryAsync(string pointId, CancellationToken ct = default)
    {
        var mappings = await _repo.GetMappingsForPointAsync(pointId, ct);
        var results = new List<SourceMappingEligibility>();
        foreach (var mapping in mappings)
        {
            var source = await _repo.GetDataSourceAsync(mapping.DataSourceId, ct);
            results.Add(new(true, null, mapping.Id, mapping.DataSourceId, source?.Status, mapping.Status, mapping.EffectiveFrom, mapping.EffectiveTo, pointId, mapping.Version, MappingEligibilityOutcome.Eligible));
        }
        return results;
    }

    public async Task<CatalogSourceMappingSnapshot?> GetSourceMappingSnapshotAsync(MappingId mappingId, CancellationToken ct = default)
    {
        var mapping = await _repo.GetMappingAsync(mappingId, ct);
        if (mapping is null) return null;
        var source = await _repo.GetDataSourceAsync(mapping.DataSourceId, ct);
        if (source is null) return null;
        return new CatalogSourceMappingSnapshot(mapping.Id, mapping.DataSourceId, mapping.PointId, source.Status,
            mapping.Status, mapping.EffectiveFrom, mapping.EffectiveTo, mapping.Version);
    }
}
