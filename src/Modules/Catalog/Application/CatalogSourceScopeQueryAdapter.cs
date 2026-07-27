using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;

namespace IUMP.Modules.Catalog.Application;

public sealed class CatalogSourceScopeQueryAdapter : ICatalogSourceScopeQuery
{
    private readonly ICatalogCommandRepository _catalog;
    private readonly ICatalogPointReadinessQuery _readiness;

    public CatalogSourceScopeQueryAdapter(ICatalogCommandRepository catalog, ICatalogPointReadinessQuery readiness)
    {
        _catalog = catalog;
        _readiness = readiness;
    }

    public async Task<CatalogSourceScopeSnapshot?> GetSourceScopeAsync(Guid sourceId, CancellationToken ct = default)
    {
        var source = await _catalog.GetDataSourceAsync(new DataSourceId(sourceId), ct);
        if (source is null) return null;

        var mappings = await _catalog.GetMappingsForSourceAsync(source.Id, ct);
        var nonSuperseded = mappings.Where(m => m.Status != MappingStatus.Superseded).ToList();

        var mappedScopes = new List<CatalogSourceMappedScopeSnapshot>();
        foreach (var mapping in nonSuperseded)
        {
            var readiness = await _readiness.GetPointReadinessAsync(mapping.PointId, ct);
            if (readiness is null || !readiness.Exists)
            {
                // Fail-closed: unresolved or inconsistent Point readiness
                // denies configuration access. No empty fallback SiteId/AreaId.
                return null;
            }
            // SiteId is non-nullable in readiness; AreaId is nullable metadata.
            mappedScopes.Add(new CatalogSourceMappedScopeSnapshot(
                mapping.Id, mapping.Version, mapping.PointId,
                readiness.SiteId, readiness.AreaId ?? string.Empty, readiness.ReadinessVersions));
        }

        return new CatalogSourceScopeSnapshot(
            source.Id.Value,
            true,
            source.SourceType.ToString(),
            source.Status.ToString(),
            source.Version,
            mappedScopes.AsReadOnly());
    }
}
