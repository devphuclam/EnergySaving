using IUMP.Modules.Organization.Domain;

namespace IUMP.Modules.Organization.Contracts;

public sealed record SiteSnapshot(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string Timezone,
    SiteStatus Status,
    long Version,
    int AreaCount = 0);

public sealed record AreaSnapshot(
    Guid Id,
    Guid SiteId,
    string Code,
    string Name,
    string? Description,
    AreaStatus Status,
    long Version,
    int AssetCount = 0);

public sealed record AssetSnapshot(
    Guid Id,
    Guid SiteId,
    Guid AreaId,
    string Code,
    string Name,
    string? Description,
    AssetStatus Status,
    long Version,
    int PointCount = 0);

public sealed record PointSnapshot(
    Guid Id,
    Guid SiteId,
    Guid AreaId,
    Guid AssetId,
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

public sealed record OrganizationQueryScope(
    bool IsGlobal,
    IReadOnlyCollection<Guid> SiteIds,
    IReadOnlyCollection<Guid> AreaIds)
{
    public static OrganizationQueryScope Global() => new(true, Array.Empty<Guid>(), Array.Empty<Guid>());
}

public sealed record AreaAncestrySnapshot(Guid AreaId, Guid SiteId);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public interface IOrganizationQueryRepository
{
    Task<SiteSnapshot?> GetSiteSnapshotAsync(Guid id, CancellationToken ct = default);
    Task<SiteSnapshot?> FindSiteByCodeAsync(string code, CancellationToken ct = default);
    Task<PagedResult<SiteSnapshot>> GetSitesAsync(OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default);

    Task<AreaSnapshot?> GetAreaSnapshotAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<AreaSnapshot>> GetAreasForSiteAsync(Guid siteId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default);

    Task<AssetSnapshot?> GetAssetSnapshotAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<AssetSnapshot>> GetAssetsForAreaAsync(Guid areaId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default);

    Task<PointSnapshot?> GetPointSnapshotAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<PointSnapshot>> GetPointsForAssetAsync(Guid assetId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default);
    Task<PagedResult<PointSnapshot>> GetPointsForSiteAsync(Guid siteId, OrganizationQueryScope scope, ScopeFilter filter, CancellationToken ct = default);

    Task<bool> SiteExistsAsync(Guid id, CancellationToken ct = default);
    Task<long> GetSiteVersionAsync(Guid id, CancellationToken ct = default);
    Task<AreaAncestrySnapshot?> GetAreaAncestryAsync(Guid areaId, CancellationToken ct = default);
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
