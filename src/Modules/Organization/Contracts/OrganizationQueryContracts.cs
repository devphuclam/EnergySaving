using IUMP.Modules.Organization.Domain;

namespace IUMP.Modules.Organization.Contracts;

public sealed record SiteSnapshot(
    SiteId Id,
    string Code,
    string Name,
    string? Description,
    string Timezone,
    SiteStatus Status,
    long Version);

public sealed record AreaSnapshot(
    AreaId Id,
    SiteId SiteId,
    string Code,
    string Name,
    string? Description,
    AreaStatus Status,
    long Version);

public sealed record AssetSnapshot(
    AssetId Id,
    SiteId SiteId,
    AreaId AreaId,
    string Code,
    string Name,
    string? Description,
    AssetStatus Status,
    long Version);

public sealed record PointSnapshot(
    PointId Id,
    SiteId SiteId,
    AreaId AreaId,
    AssetId AssetId,
    string Code,
    string? Description,
    string MetricId,
    string UnitId,
    string DataOwnerUserId,
    int ExpectedIntervalSeconds,
    int NoDataAfterSeconds,
    PointStatus Status,
    long Version);

public sealed record ScopeFilter(int Page, int PageSize);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public interface IOrganizationQueryRepository
{
    Task<SiteSnapshot?> GetSiteSnapshotAsync(SiteId id, CancellationToken ct = default);
    Task<SiteSnapshot?> FindSiteByCodeAsync(string code, CancellationToken ct = default);
    Task<PagedResult<SiteSnapshot>> GetSitesAsync(IReadOnlyCollection<string> scopeSiteIds, ScopeFilter filter, CancellationToken ct = default);

    Task<AreaSnapshot?> GetAreaSnapshotAsync(AreaId id, CancellationToken ct = default);
    Task<PagedResult<AreaSnapshot>> GetAreasForSiteAsync(SiteId siteId, IReadOnlyCollection<string> scopeSiteIds, ScopeFilter filter, CancellationToken ct = default);

    Task<AssetSnapshot?> GetAssetSnapshotAsync(AssetId id, CancellationToken ct = default);
    Task<PagedResult<AssetSnapshot>> GetAssetsForAreaAsync(AreaId areaId, IReadOnlyCollection<string> scopeSiteIds, ScopeFilter filter, CancellationToken ct = default);

    Task<PointSnapshot?> GetPointSnapshotAsync(PointId id, CancellationToken ct = default);
    Task<PagedResult<PointSnapshot>> GetPointsForAssetAsync(AssetId assetId, IReadOnlyCollection<string> scopeSiteIds, ScopeFilter filter, CancellationToken ct = default);
    Task<PagedResult<PointSnapshot>> GetPointsForSiteAsync(SiteId siteId, IReadOnlyCollection<string> scopeSiteIds, ScopeFilter filter, CancellationToken ct = default);

    Task<bool> SiteExistsAsync(SiteId id, CancellationToken ct = default);
    Task<long> GetSiteVersionAsync(SiteId id, CancellationToken ct = default);
}

public sealed record OrganizationCallerSnapshot(
    string UserId,
    string Username,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> SiteScopes,
    IReadOnlyCollection<string> AreaScopes)
{
    public bool HasRole(string role) => Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
    public bool HasSiteScope(string siteId) => SiteScopes.Any(s => string.Equals(s, siteId, StringComparison.OrdinalIgnoreCase));
}

public sealed record OrganizationAuthorizationDecision(bool IsAllowed, string Code, string? Error)
{
    public static OrganizationAuthorizationDecision Allowed() => new(true, "Allowed", null);
    public static OrganizationAuthorizationDecision Forbidden(string? error = null) =>
        new(false, "Forbidden", error ?? "Caller is not authorized for this Organization mutation.");
    public static OrganizationAuthorizationDecision NotFound() =>
        new(false, "NotFound", "The target is not visible in the caller scope.");
}

public interface IOrganizationCallerSnapshotProvider
{
    Task<OrganizationCallerSnapshot?> ResolveAsync(string userId, CancellationToken ct = default);
}
