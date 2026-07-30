using IUMP.Modules.Catalog.Domain;

namespace IUMP.Modules.Catalog.Contracts;

/// <summary>
/// Contract-only host facade for runtime catalog operations. Domain identifiers
/// and entities remain owned and constructed inside the Catalog module.
/// </summary>
public sealed class CatalogRuntimeGateway(ICatalogCommandRepository repository)
{
    public async Task<IReadOnlyList<object>> GetMetricsAsync(CancellationToken ct = default) =>
        (await repository.GetAllMetricsAsync(ct)).Cast<object>().ToArray();

    public async Task<IReadOnlyList<object>> GetUnitsAsync(CancellationToken ct = default) =>
        (await repository.GetAllUnitsAsync(ct)).Cast<object>().ToArray();

    public async Task<IReadOnlyList<object>> GetDataSourcesAsync(CancellationToken ct = default) =>
        (await GetDataSourceSnapshotsAsync(ct)).Cast<object>().ToArray();

    public async Task<IReadOnlyList<object>> GetMappingsAsync(CancellationToken ct = default)
        => (await GetMappingSnapshotsAsync(ct)).Cast<object>().ToArray();

    public async Task<IReadOnlyList<CatalogRuntimeDataSource>> GetDataSourceSnapshotsAsync(
        CancellationToken ct = default) =>
        (await repository.GetAllDataSourcesAsync(ct))
            .Select(value => new CatalogRuntimeDataSource(
                value.Id.Value, value.Code, value.Name,
                value.SourceType.ToString(), value.Status.ToString(), value.Version,
                value.SiteId))
            .ToArray();

    public async Task<IReadOnlyList<CatalogRuntimeMapping>> GetMappingSnapshotsAsync(
        CancellationToken ct = default)
    {
        var values = new List<CatalogRuntimeMapping>();
        foreach (var source in await repository.GetAllDataSourcesAsync(ct))
            values.AddRange((await repository.GetMappingsForSourceAsync(source.Id, ct))
                .Select(value => new CatalogRuntimeMapping(
                    value.Id.Value, value.DataSourceId.Value, Guid.Parse(value.PointId),
                    value.Status.ToString(), value.EffectiveFrom, value.EffectiveTo,
                    value.Version)));
        return values;
    }

    public async Task<IReadOnlyList<object>> GetCompatibilitiesAsync(
        Guid metricId, CancellationToken ct = default) =>
        (await repository.GetCompatibilitiesForMetricAsync(new MetricId(metricId), ct))
            .Cast<object>().ToArray();

    public async Task<object?> GetDataSourceAsync(
        Guid sourceId,
        CancellationToken ct = default) =>
        await GetDataSourceSnapshotAsync(sourceId, ct);

    public async Task<CatalogRuntimeDataSource?> GetDataSourceSnapshotAsync(
        Guid sourceId,
        CancellationToken ct = default)
    {
        var value = await repository.GetDataSourceAsync(new DataSourceId(sourceId), ct);
        return value is null ? null : new CatalogRuntimeDataSource(
            value.Id.Value, value.Code, value.Name, value.SourceType.ToString(),
            value.Status.ToString(), value.Version, value.SiteId);
    }

    public async Task<object?> GetMappingAsync(Guid mappingId, CancellationToken ct = default)
        => await GetMappingSnapshotAsync(mappingId, ct);

    public async Task<CatalogRuntimeMapping?> GetMappingSnapshotAsync(
        Guid mappingId,
        CancellationToken ct = default)
    {
        var value = await repository.GetMappingAsync(new MappingId(mappingId), ct);
        return value is null ? null : new CatalogRuntimeMapping(
            value.Id.Value, value.DataSourceId.Value, Guid.Parse(value.PointId),
            value.Status.ToString(), value.EffectiveFrom, value.EffectiveTo,
            value.Version);
    }

    public async Task<CatalogRuntimeMutation> CreateMetricAsync(
        string code, string name, CancellationToken ct = default)
    {
        var metric = new Metric(MetricId.New(), code, name, MetricStatus.Active, 1);
        await repository.AddMetricAsync(metric, ct);
        return new("Metric", metric.Id.Value, metric.Version);
    }

    public async Task<CatalogRuntimeMutation> CreateUnitAsync(
        string code, string symbol, CancellationToken ct = default)
    {
        var unit = new MetricUnit(UnitId.New(), code, symbol, MetricUnitStatus.Active, 1);
        await repository.AddUnitAsync(unit, ct);
        return new("Unit", unit.Id.Value, unit.Version);
    }

    public async Task<CatalogRuntimeMutation> CreateSourceAsync(
        string code,
        string name,
        CancellationToken ct = default,
        Guid? siteId = null)
    {
        var source = new DataSource(
            DataSourceId.New(), code, name, SourceType.Simulator,
            SourceStatus.Draft, 1, siteId);
        await repository.AddDataSourceAsync(source, ct);
        return new("DataSource", source.Id.Value, source.Version);
    }

    public async Task<CatalogRuntimeMutation> CreateCompatibilityAsync(
        Guid metricId,
        Guid unitId,
        bool isCanonical,
        CancellationToken ct = default)
    {
        var value = new MetricUnitCompatibility(
            new MetricId(metricId), new UnitId(unitId), isCanonical, 1);
        await repository.AddCompatibilityAsync(value, ct);
        return new("MetricUnitCompatibility", metricId, value.Version);
    }

    public async Task<CatalogRuntimeMutation> CreateMappingAsync(
        Guid sourceId,
        Guid pointId,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        CancellationToken ct = default)
    {
        var value = new SourcePointMapping(
            MappingId.New(), new DataSourceId(sourceId), pointId.ToString("D"),
            MappingStatus.Draft, effectiveFromUtc, effectiveToUtc, 1);
        await repository.AddMappingAsync(value, ct);
        return new("SourcePointMapping", value.Id.Value, value.Version);
    }

    public async Task<CatalogRuntimeMutation?> TransitionSourceAsync(
        Guid sourceId,
        long expectedVersion,
        string action,
        CancellationToken ct = default)
    {
        var value = await repository.GetDataSourceAsync(new DataSourceId(sourceId), ct);
        if (value is null) return null;
        if (value.Version != expectedVersion)
            throw new InvalidOperationException("VERSION_CONFLICT");
        var target = action switch
        {
            "activate" => SourceStatus.Active,
            "suspend" => SourceStatus.Suspended,
            "decommission" => SourceStatus.Decommissioned,
            _ => throw new InvalidOperationException("SOURCE_ACTION_INVALID")
        };
        if (!value.TryTransitionTo(target))
            throw new InvalidOperationException("PRECONDITION_FAILED");
        await repository.UpdateDataSourceAsync(value, ct);
        return new("DataSource", value.Id.Value, value.Version);
    }

    public async Task<CatalogRuntimeMutation?> TransitionMappingAsync(
        Guid mappingId,
        long expectedVersion,
        string action,
        CancellationToken ct = default)
    {
        var value = await repository.GetMappingAsync(new MappingId(mappingId), ct);
        if (value is null) return null;
        if (value.Version != expectedVersion)
            throw new InvalidOperationException("VERSION_CONFLICT");
        var changed = action switch
        {
            "activate" => value.TryActivate(),
            "inactivate" => value.TryInactivate(),
            "supersede" => value.TrySupersede(),
            _ => throw new InvalidOperationException("MAPPING_ACTION_INVALID")
        };
        if (!changed) throw new InvalidOperationException("PRECONDITION_FAILED");
        await repository.UpdateMappingAsync(value, ct);
        return new("SourcePointMapping", value.Id.Value, value.Version);
    }

    public async Task<CatalogRuntimeMutation?> UpdateMetricAsync(
        Guid metricId,
        long expectedVersion,
        string name,
        CancellationToken ct = default)
    {
        var value = await repository.GetMetricAsync(new MetricId(metricId), ct);
        if (value is null) return null;
        if (value.Version != expectedVersion)
            throw new InvalidOperationException("VERSION_CONFLICT");
        if (!value.TryUpdate(name))
            throw new InvalidOperationException("NO_OP");
        await repository.UpdateMetricAsync(value, ct);
        return new("Metric", value.Id.Value, value.Version);
    }

    public async Task<CatalogRuntimeMutation?> UpdateUnitAsync(
        Guid unitId,
        long expectedVersion,
        string symbol,
        CancellationToken ct = default)
    {
        var value = await repository.GetUnitAsync(new UnitId(unitId), ct);
        if (value is null) return null;
        if (value.Version != expectedVersion)
            throw new InvalidOperationException("VERSION_CONFLICT");
        if (!value.TryUpdate(symbol))
            throw new InvalidOperationException("NO_OP");
        await repository.UpdateUnitAsync(value, ct);
        return new("Unit", value.Id.Value, value.Version);
    }

    public async Task<CatalogRuntimeMutation?> UpdateSourceAsync(
        Guid sourceId,
        long expectedVersion,
        string name,
        CancellationToken ct = default)
    {
        var value = await repository.GetDataSourceAsync(new DataSourceId(sourceId), ct);
        if (value is null) return null;
        if (value.Version != expectedVersion)
            throw new InvalidOperationException("VERSION_CONFLICT");
        if (!value.TryUpdate(name))
            throw new InvalidOperationException(
                value.IsDecommissioned ? "PRECONDITION_FAILED" : "NO_OP");
        await repository.UpdateDataSourceAsync(value, ct);
        return new("DataSource", value.Id.Value, value.Version);
    }

    public async Task<CatalogRuntimeMutation?> UpdateMappingAsync(
        Guid mappingId,
        long expectedVersion,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        CancellationToken ct = default)
    {
        var value = await repository.GetMappingAsync(new MappingId(mappingId), ct);
        if (value is null) return null;
        if (value.Version != expectedVersion)
            throw new InvalidOperationException("VERSION_CONFLICT");
        if (!value.TryUpdatePeriod(effectiveFromUtc, effectiveToUtc))
            throw new InvalidOperationException(
                value.IsSuperseded || value.IsActive ? "PRECONDITION_FAILED" : "NO_OP");
        await repository.UpdateMappingAsync(value, ct);
        return new("SourcePointMapping", value.Id.Value, value.Version);
    }

    public async Task<CatalogRuntimeDeletion> DeleteSourceAsync(
        Guid sourceId,
        long expectedVersion,
        CancellationToken ct = default)
    {
        var value = await repository.GetDataSourceAsync(new DataSourceId(sourceId), ct);
        if (value is null) return new(false, "NOT_FOUND", "Source was not found.");
        if (value.Version != expectedVersion)
            return new(false, "VERSION_CONFLICT", "ExpectedVersion is stale.");
        var decision = await repository.DeleteDataSourceAsync(new DataSourceId(sourceId), ct);
        return new(decision.IsAllowed, decision.Code, decision.Error);
    }

    public async Task<CatalogRuntimeDeletion> DeleteMappingAsync(
        Guid mappingId,
        long expectedVersion,
        CancellationToken ct = default)
    {
        var value = await repository.GetMappingAsync(new MappingId(mappingId), ct);
        if (value is null) return new(false, "NOT_FOUND", "Mapping was not found.");
        if (value.Version != expectedVersion)
            return new(false, "VERSION_CONFLICT", "ExpectedVersion is stale.");
        var decision = await repository.DeleteMappingAsync(new MappingId(mappingId), ct);
        return new(decision.IsAllowed, decision.Code, decision.Error);
    }
}

public sealed record CatalogRuntimeMutation(string EntityType, Guid Id, long Version);
public sealed record CatalogRuntimeDeletion(bool IsAllowed, string Code, string? Error);
public sealed record CatalogRuntimeDataSource(
    Guid Id,
    string Code,
    string Name,
    string SourceType,
    string Status,
    long Version,
    Guid? SiteId);
public sealed record CatalogRuntimeMapping(
    Guid Id,
    Guid DataSourceId,
    Guid PointId,
    string Status,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    long Version);
