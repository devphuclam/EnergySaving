using IUMP.Modules.Organization.Contracts;

namespace IUMP.Modules.Organization.Application;

public interface IOrganizationScopeFilter
{
    OrganizationQueryScope Resolve(string userId, OrganizationCallerSnapshot caller);
    IReadOnlyCollection<string> ResolveSiteScopes(string userId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> siteScopes, IReadOnlyCollection<string> areaScopes);
}

public sealed class OrganizationScopeFilterService : IOrganizationScopeFilter
{
    public OrganizationQueryScope Resolve(string userId, OrganizationCallerSnapshot caller)
    {
        if (!caller.IsActive) return new OrganizationQueryScope(false, Array.Empty<Guid>(), Array.Empty<Guid>());
        if (caller.HasRole("Administrator")) return OrganizationQueryScope.Global();

        var siteIds = caller.SiteScopes
            .Select(ParseGuid)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var areaIds = caller.AreaScopes
            .Select(ParseGuid)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        return new OrganizationQueryScope(false, siteIds, areaIds);
    }

    public IReadOnlyCollection<string> ResolveSiteScopes(string userId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> siteScopes, IReadOnlyCollection<string> areaScopes)
    {
        if (roles.Any(r => string.Equals(r, "Administrator", StringComparison.OrdinalIgnoreCase)))
            return Array.Empty<string>();
        return siteScopes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static Guid? ParseGuid(string value) => Guid.TryParse(value, out var id) ? id : null;
}

public sealed class OrganizationQueryService
{
    private readonly IOrganizationQueryRepository _repository;
    private readonly IOrganizationCallerSnapshotProvider _callerProvider;
    private readonly IOrganizationScopeFilter _scopeFilter;

    public OrganizationQueryService(IOrganizationQueryRepository repository,
        IOrganizationCallerSnapshotProvider callerProvider,
        IOrganizationScopeFilter? scopeFilter = null)
    {
        _repository = repository;
        _callerProvider = callerProvider;
        _scopeFilter = scopeFilter ?? new OrganizationScopeFilterService();
    }

    public async Task<PagedResult<SiteSnapshot>> GetSitesAsync(string userId, ScopeFilter filter, CancellationToken ct = default)
    {
        var scope = await ResolveScope(userId, ct);
        return scope is null ? Empty<SiteSnapshot>(filter) : await _repository.GetSitesAsync(scope, filter, ct);
    }

    public async Task<SiteSnapshot?> GetSiteAsync(string userId, Guid siteId, CancellationToken ct = default)
    {
        var scope = await ResolveScope(userId, ct);
        var snapshot = await _repository.GetSiteSnapshotAsync(siteId, ct);
        return scope is not null && snapshot is not null && await IsVisibleAsync(scope, snapshot.Id, null, ct) ? snapshot : null;
    }

    public async Task<PagedResult<AreaSnapshot>> GetAreasAsync(string userId, Guid siteId, ScopeFilter filter, CancellationToken ct = default)
    {
        var scope = await ResolveScope(userId, ct);
        return scope is null ? Empty<AreaSnapshot>(filter) : await _repository.GetAreasForSiteAsync(siteId, scope, filter, ct);
    }

    public async Task<AreaSnapshot?> GetAreaAsync(string userId, Guid areaId, CancellationToken ct = default)
    {
        var scope = await ResolveScope(userId, ct);
        var snapshot = await _repository.GetAreaSnapshotAsync(areaId, ct);
        return scope is not null && snapshot is not null && IsVisible(scope, snapshot.SiteId, snapshot.Id) ? snapshot : null;
    }

    public async Task<PagedResult<AssetSnapshot>> GetAssetsAsync(string userId, Guid areaId, ScopeFilter filter, CancellationToken ct = default)
    {
        var scope = await ResolveScope(userId, ct);
        return scope is null ? Empty<AssetSnapshot>(filter) : await _repository.GetAssetsForAreaAsync(areaId, scope, filter, ct);
    }

    public async Task<AssetSnapshot?> GetAssetAsync(string userId, Guid assetId, CancellationToken ct = default)
    {
        var scope = await ResolveScope(userId, ct);
        var snapshot = await _repository.GetAssetSnapshotAsync(assetId, ct);
        return scope is not null && snapshot is not null && IsVisible(scope, snapshot.SiteId, snapshot.AreaId) ? snapshot : null;
    }

    public async Task<PagedResult<PointSnapshot>> GetPointsAsync(string userId, Guid assetId, ScopeFilter filter, CancellationToken ct = default)
    {
        var scope = await ResolveScope(userId, ct);
        return scope is null ? Empty<PointSnapshot>(filter) : await _repository.GetPointsForAssetAsync(assetId, scope, filter, ct);
    }

    public async Task<PagedResult<PointSnapshot>> GetPointsForSiteAsync(string userId, Guid siteId, ScopeFilter filter, CancellationToken ct = default)
    {
        var scope = await ResolveScope(userId, ct);
        return scope is null ? Empty<PointSnapshot>(filter) : await _repository.GetPointsForSiteAsync(siteId, scope, filter, ct);
    }

    public async Task<PointSnapshot?> GetPointAsync(string userId, Guid pointId, CancellationToken ct = default)
    {
        var scope = await ResolveScope(userId, ct);
        var snapshot = await _repository.GetPointSnapshotAsync(pointId, ct);
        return scope is not null && snapshot is not null && IsVisible(scope, snapshot.SiteId, snapshot.AreaId) ? snapshot : null;
    }

    private async Task<OrganizationQueryScope?> ResolveScope(string userId, CancellationToken ct)
    {
        var caller = await _callerProvider.ResolveAsync(userId, ct);
        return caller is null ? null : _scopeFilter.Resolve(userId, caller);
    }

    private static bool IsVisible(OrganizationQueryScope scope, Guid siteId, Guid? areaId) =>
        scope.IsGlobal || scope.SiteIds.Contains(siteId) || areaId.HasValue && scope.AreaIds.Contains(areaId.Value);

    private async Task<bool> IsVisibleAsync(OrganizationQueryScope scope, Guid siteId, Guid? areaId, CancellationToken ct)
    {
        if (IsVisible(scope, siteId, areaId)) return true;
        if (scope.AreaIds.Count > 0)
        {
            var areas = await _repository.GetAreasForSiteAsync(siteId, OrganizationQueryScope.Global(), new ScopeFilter(1, 200), ct);
            return areaId.HasValue
                ? areas.Items.Any(area => area.Id == areaId.Value && scope.AreaIds.Contains(area.Id))
                : areas.Items.Any(area => scope.AreaIds.Contains(area.Id));
        }
        return false;
    }

    private static PagedResult<T> Empty<T>(ScopeFilter filter) =>
        new(Array.Empty<T>(), 0, Math.Max(1, filter.Page), Math.Clamp(filter.PageSize, 1, 200));
}
