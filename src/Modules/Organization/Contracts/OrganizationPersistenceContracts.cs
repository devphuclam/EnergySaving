using IUMP.Modules.Organization.Domain;

namespace IUMP.Modules.Organization.Contracts;

public interface IOrganizationTransaction
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

public interface IOrganizationCommandRepository
{
    Task<Site?> GetSiteAsync(SiteId id, CancellationToken ct = default);
    Task<Site?> FindSiteByCodeAsync(string code, CancellationToken ct = default);
    Task AddSiteAsync(Site site, CancellationToken ct = default);
    Task UpdateSiteAsync(Site site, CancellationToken ct = default);
    Task<IReadOnlyList<Site>> GetAllSitesAsync(CancellationToken ct = default);

    Task<Area?> GetAreaAsync(AreaId id, CancellationToken ct = default);
    Task<Area?> FindAreaByCodeAsync(SiteId siteId, string code, CancellationToken ct = default);
    Task AddAreaAsync(Area area, CancellationToken ct = default);
    Task UpdateAreaAsync(Area area, CancellationToken ct = default);
    Task<IReadOnlyList<Area>> GetAreasForSiteAsync(SiteId siteId, CancellationToken ct = default);

    Task<Asset?> GetAssetAsync(AssetId id, CancellationToken ct = default);
    Task<Asset?> FindAssetByCodeAsync(AreaId areaId, string code, CancellationToken ct = default);
    Task AddAssetAsync(Asset asset, CancellationToken ct = default);
    Task UpdateAssetAsync(Asset asset, CancellationToken ct = default);
    Task<IReadOnlyList<Asset>> GetAssetsForAreaAsync(AreaId areaId, CancellationToken ct = default);
    Task<IReadOnlyList<Asset>> GetAssetsForSiteAsync(SiteId siteId, CancellationToken ct = default);

    Task<MeasurementPoint?> GetPointAsync(PointId id, CancellationToken ct = default);
    Task<MeasurementPoint?> FindPointByCodeAsync(SiteId siteId, string code, CancellationToken ct = default);
    Task AddPointAsync(MeasurementPoint point, CancellationToken ct = default);
    Task UpdatePointAsync(MeasurementPoint point, CancellationToken ct = default);
    Task<IReadOnlyList<MeasurementPoint>> GetPointsForAssetAsync(AssetId assetId, CancellationToken ct = default);
    Task<IReadOnlyList<MeasurementPoint>> GetPointsForSiteAsync(SiteId siteId, CancellationToken ct = default);

    Task AddLifecycleEntryAsync(PointLifecycleEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<PointLifecycleEntry>> GetLifecycleForPointAsync(string pointId, CancellationToken ct = default);

    Task<bool> IsPointCodeReservedAsync(SiteId siteId, string code, CancellationToken ct = default);
    Task<IOrganizationTransaction> BeginTransactionAsync(CancellationToken ct = default);
}
