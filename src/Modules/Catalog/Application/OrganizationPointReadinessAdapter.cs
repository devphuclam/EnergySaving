using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Organization.Contracts;

namespace IUMP.Modules.Catalog.Application;

public sealed class OrganizationPointReadinessAdapter : ICatalogPointReadinessQuery
{
    private readonly IOrganizationQueryRepository _organization;

    public OrganizationPointReadinessAdapter(IOrganizationQueryRepository organization) => _organization = organization;

    public async Task<PointReadinessSnapshot?> GetPointReadinessAsync(string pointId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(pointId, out var pointGuid)) return Missing(pointId);
        var point = await _organization.GetPointSnapshotAsync(pointGuid, ct);
        if (point is null) return Missing(pointId);
        var asset = await _organization.GetAssetSnapshotAsync(point.AssetId, ct);
        if (asset is null || asset.SiteId != point.SiteId || asset.AreaId != point.AreaId) return Missing(pointId);
        var area = await _organization.GetAreaSnapshotAsync(point.AreaId, ct);
        if (area is null || area.SiteId != point.SiteId) return Missing(pointId);
        var site = await _organization.GetSiteSnapshotAsync(point.SiteId, ct);
        if (site is null || site.Id != point.SiteId) return Missing(pointId);

        var configurationReady = point.ExpectedIntervalSeconds > 0 &&
            point.NoDataAfterSeconds > point.ExpectedIntervalSeconds &&
            !string.IsNullOrWhiteSpace(point.MetricId) && !string.IsNullOrWhiteSpace(point.UnitId) &&
            !string.IsNullOrWhiteSpace(point.DataOwnerUserId) &&
            !string.Equals(point.Status.ToString(), "Decommissioned", StringComparison.Ordinal);
        var producingReady = configurationReady &&
            string.Equals(site.Status.ToString(), "Active", StringComparison.Ordinal) &&
            string.Equals(area.Status.ToString(), "Active", StringComparison.Ordinal) &&
            string.Equals(asset.Status.ToString(), "Active", StringComparison.Ordinal) &&
            string.Equals(point.Status.ToString(), "Active", StringComparison.Ordinal);
        var providerVersion = new[] { point.Version, asset.Version, area.Version, site.Version }.Max();
        var versions = new ReadinessVersionTuple(point.Version, asset.Version, area.Version, site.Version);
        return new PointReadinessSnapshot(point.Id.ToString("D"), site.Id.ToString("D"), area.Id.ToString("D"),
            true, configurationReady, producingReady, providerVersion, versions);
    }

    private static PointReadinessSnapshot Missing(string pointId) =>
        new(pointId, string.Empty, null, false, false, false, 0);
}
