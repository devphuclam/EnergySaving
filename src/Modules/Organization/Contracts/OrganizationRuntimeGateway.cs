using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Domain;

namespace IUMP.Modules.Organization.Contracts;

/// <summary>
/// Module-owned runtime command facade. The host coordinates idempotency and
/// outbox staging, while Organization retains authorization and business writes.
/// </summary>
public sealed class OrganizationRuntimeGateway(
    IOrganizationCommandRepository repository,
    IOrganizationAuthorization authorization)
{
    public async Task<OrganizationRuntimeMutation> CreateSiteAsync(
        string name,
        string actorUserId,
        CancellationToken ct = default)
    {
        if (await DeniedAsync(actorUserId, OrganizationResource.RootSite, null, ct) is { } denied)
            return denied;
        var site = new Site(
            SiteId.New(), Code(name, "SITE"), Text(name, "Site"),
            null, "UTC", SiteStatus.Draft, 1);
        if (await repository.FindSiteByCodeAsync(site.Code, ct) is not null)
            return OrganizationRuntimeMutation.Failure(409, "CONFLICT");
        await repository.AddSiteAsync(site, ct);
        return OrganizationRuntimeMutation.Success(
            "Site", site.Id.Value, site.Version, site.Id.Value, null);
    }

    public async Task<OrganizationRuntimeMutation> UpdateSiteAsync(
        Guid siteId,
        string name,
        long expectedVersion,
        string actorUserId,
        CancellationToken ct = default)
    {
        if (await DeniedAsync(
            actorUserId, OrganizationResource.RootSite, siteId, ct) is { } denied)
            return denied;
        var site = await repository.GetSiteAsync(new SiteId(siteId), ct);
        if (site is null) return OrganizationRuntimeMutation.Failure(404, "NOT_FOUND");
        if (site.Version != expectedVersion)
            return OrganizationRuntimeMutation.Failure(409, "VERSION_CONFLICT");
        _ = site.TryUpdate(Text(name, site.Name), site.Description, site.Timezone);
        await repository.UpdateSiteAsync(site, ct);
        return OrganizationRuntimeMutation.Success(
            "Site", site.Id.Value, site.Version, site.Id.Value, null);
    }

    public async Task<OrganizationRuntimeMutation> CreateAreaAsync(
        Guid siteId,
        string name,
        string actorUserId,
        CancellationToken ct = default)
    {
        if (await DeniedAsync(
            actorUserId, OrganizationResource.SiteChild, siteId, ct) is { } denied)
            return denied;
        var site = await repository.GetSiteAsync(new SiteId(siteId), ct);
        if (site is null) return OrganizationRuntimeMutation.Failure(404, "NOT_FOUND");
        var area = new Area(
            AreaId.New(), site.Id, Code(name, "AREA"), Text(name, "Area"),
            null, AreaStatus.Draft, 1);
        if (await repository.FindAreaByCodeAsync(site.Id, area.Code, ct) is not null)
            return OrganizationRuntimeMutation.Failure(409, "CONFLICT");
        await repository.AddAreaAsync(area, ct);
        return OrganizationRuntimeMutation.Success(
            "Area", area.Id.Value, area.Version, site.Id.Value, area.Id.Value);
    }

    public async Task<OrganizationRuntimeMutation> CreateAssetAsync(
        Guid areaId,
        string name,
        string actorUserId,
        CancellationToken ct = default)
    {
        var area = await repository.GetAreaAsync(new AreaId(areaId), ct);
        if (area is null) return OrganizationRuntimeMutation.Failure(404, "NOT_FOUND");
        if (await DeniedAsync(
            actorUserId, OrganizationResource.SiteChild, area.SiteId.Value, ct, area.Id.Value) is { } denied)
            return denied;
        var asset = new Asset(
            AssetId.New(), area.SiteId, area.Id, Code(name, "ASSET"),
            Text(name, "Asset"), null, AssetStatus.Draft, 1);
        if (await repository.FindAssetByCodeAsync(area.Id, asset.Code, ct) is not null)
            return OrganizationRuntimeMutation.Failure(409, "CONFLICT");
        await repository.AddAssetAsync(asset, ct);
        return OrganizationRuntimeMutation.Success(
            "Asset", asset.Id.Value, asset.Version,
            asset.SiteId.Value, asset.AreaId.Value);
    }

    public async Task<OrganizationRuntimeMutation> CreatePointAsync(
        Guid assetId,
        string name,
        Guid metricId,
        Guid unitId,
        Guid dataOwnerUserId,
        int expectedIntervalSeconds,
        int noDataAfterSeconds,
        string actorUserId,
        CancellationToken ct = default)
    {
        var asset = await repository.GetAssetAsync(new AssetId(assetId), ct);
        if (asset is null) return OrganizationRuntimeMutation.Failure(404, "NOT_FOUND");
        if (await DeniedAsync(
            actorUserId, OrganizationResource.SiteChild, asset.SiteId.Value, ct, asset.AreaId.Value) is { } denied)
            return denied;
        var point = new MeasurementPoint(
            PointId.New(), asset.SiteId, asset.AreaId, asset.Id,
            Code(name, "POINT"), null, metricId.ToString("D"), unitId.ToString("D"),
            dataOwnerUserId.ToString("D"), expectedIntervalSeconds, noDataAfterSeconds,
            PointStatus.Draft, 1);
        if (await repository.FindPointByCodeAsync(
            asset.SiteId, point.Code, ct) is not null)
            return OrganizationRuntimeMutation.Failure(409, "CONFLICT");
        await repository.AddPointAsync(point, ct);
        return OrganizationRuntimeMutation.Success(
            "MeasurementPoint", point.Id.Value, point.Version,
            point.SiteId.Value, point.AreaId.Value);
    }

    public async Task<OrganizationRuntimeMutation> TransitionSiteAsync(
        Guid siteId,
        long expectedVersion,
        string action,
        string actorUserId,
        CancellationToken ct = default)
    {
        if (await DeniedAsync(
            actorUserId, OrganizationResource.RootSite, siteId, ct) is { } denied)
            return denied;
        var site = await repository.GetSiteAsync(new SiteId(siteId), ct);
        if (site is null) return OrganizationRuntimeMutation.Failure(404, "NOT_FOUND");
        if (site.Version != expectedVersion)
            return OrganizationRuntimeMutation.Failure(409, "VERSION_CONFLICT");
        var changed = action == "activate" ? site.TryActivate() : site.TryInactivate();
        if (!changed) return OrganizationRuntimeMutation.Failure(409, "PRECONDITION_FAILED");
        await repository.UpdateSiteAsync(site, ct);
        return OrganizationRuntimeMutation.Success(
            "Site", site.Id.Value, site.Version, site.Id.Value, null);
    }

    public async Task<OrganizationRuntimeMutation> TransitionAreaAsync(
        Guid areaId,
        long expectedVersion,
        string action,
        string actorUserId,
        CancellationToken ct = default)
    {
        var area = await repository.GetAreaAsync(new AreaId(areaId), ct);
        if (area is null) return OrganizationRuntimeMutation.Failure(404, "NOT_FOUND");
        if (await DeniedAsync(
            actorUserId, OrganizationResource.SiteChild, area.SiteId.Value, ct, area.Id.Value) is { } denied)
            return denied;
        if (area.Version != expectedVersion)
            return OrganizationRuntimeMutation.Failure(409, "VERSION_CONFLICT");
        if (action == "activate")
        {
            var parent = await repository.GetSiteAsync(area.SiteId, ct);
            if (parent is null || !parent.IsActive)
                return OrganizationRuntimeMutation.Failure(409, "PARENT_NOT_ACTIVE");
        }
        var changed = action == "activate" ? area.TryActivate() : area.TryInactivate();
        if (!changed) return OrganizationRuntimeMutation.Failure(409, "PRECONDITION_FAILED");
        await repository.UpdateAreaAsync(area, ct);
        return OrganizationRuntimeMutation.Success(
            "Area", area.Id.Value, area.Version,
            area.SiteId.Value, area.Id.Value);
    }

    public async Task<OrganizationRuntimeMutation> TransitionAssetAsync(
        Guid assetId,
        long expectedVersion,
        string action,
        string actorUserId,
        CancellationToken ct = default)
    {
        var asset = await repository.GetAssetAsync(new AssetId(assetId), ct);
        if (asset is null) return OrganizationRuntimeMutation.Failure(404, "NOT_FOUND");
        if (await DeniedAsync(
            actorUserId, OrganizationResource.SiteChild, asset.SiteId.Value, ct, asset.AreaId.Value) is { } denied)
            return denied;
        if (asset.Version != expectedVersion)
            return OrganizationRuntimeMutation.Failure(409, "VERSION_CONFLICT");
        if (action == "activate")
        {
            var parent = await repository.GetAreaAsync(asset.AreaId, ct);
            if (parent is null || !parent.IsActive)
                return OrganizationRuntimeMutation.Failure(409, "PARENT_NOT_ACTIVE");
        }
        var changed = action == "activate" ? asset.TryActivate() : asset.TryInactivate();
        if (!changed) return OrganizationRuntimeMutation.Failure(409, "PRECONDITION_FAILED");
        await repository.UpdateAssetAsync(asset, ct);
        return OrganizationRuntimeMutation.Success(
            "Asset", asset.Id.Value, asset.Version,
            asset.SiteId.Value, asset.AreaId.Value);
    }

    public async Task<OrganizationRuntimeMutation> UpdateAreaAsync(
        Guid areaId,
        string name,
        long expectedVersion,
        string actorUserId,
        CancellationToken ct = default)
    {
        var area = await repository.GetAreaAsync(new AreaId(areaId), ct);
        if (area is null) return OrganizationRuntimeMutation.Failure(404, "NOT_FOUND");
        if (await DeniedAsync(
            actorUserId, OrganizationResource.SiteChild, area.SiteId.Value, ct, area.Id.Value) is { } denied)
            return denied;
        if (area.Version != expectedVersion)
            return OrganizationRuntimeMutation.Failure(409, "VERSION_CONFLICT");
        _ = area.TryUpdate(Text(name, area.Name), area.Description);
        await repository.UpdateAreaAsync(area, ct);
        return OrganizationRuntimeMutation.Success(
            "Area", area.Id.Value, area.Version, area.SiteId.Value, area.Id.Value);
    }

    public async Task<OrganizationRuntimeMutation> UpdateAssetAsync(
        Guid assetId,
        string name,
        long expectedVersion,
        string actorUserId,
        CancellationToken ct = default)
    {
        var asset = await repository.GetAssetAsync(new AssetId(assetId), ct);
        if (asset is null) return OrganizationRuntimeMutation.Failure(404, "NOT_FOUND");
        if (await DeniedAsync(
            actorUserId, OrganizationResource.SiteChild, asset.SiteId.Value, ct, asset.AreaId.Value) is { } denied)
            return denied;
        if (asset.Version != expectedVersion)
            return OrganizationRuntimeMutation.Failure(409, "VERSION_CONFLICT");
        _ = asset.TryUpdate(Text(name, asset.Name), asset.Description);
        await repository.UpdateAssetAsync(asset, ct);
        return OrganizationRuntimeMutation.Success(
            "Asset", asset.Id.Value, asset.Version,
            asset.SiteId.Value, asset.AreaId.Value);
    }

    public async Task<OrganizationRuntimeMutation> UpdatePointAsync(
        Guid pointId,
        string? description,
        Guid metricId,
        Guid unitId,
        Guid dataOwnerUserId,
        int expectedIntervalSeconds,
        int noDataAfterSeconds,
        long expectedVersion,
        string actorUserId,
        CancellationToken ct = default)
    {
        var point = await repository.GetPointAsync(new PointId(pointId), ct);
        if (point is null) return OrganizationRuntimeMutation.Failure(404, "NOT_FOUND");
        if (await DeniedAsync(
            actorUserId, OrganizationResource.SiteChild, point.SiteId.Value, ct, point.AreaId.Value) is { } denied)
            return denied;
        if (point.Version != expectedVersion)
            return OrganizationRuntimeMutation.Failure(409, "VERSION_CONFLICT");
        if (point.Status == PointStatus.Active)
            return OrganizationRuntimeMutation.Failure(409, "ACTIVE_POINT_REQUIRES_ORCHESTRATION");
        if (point.Status == PointStatus.Decommissioned)
            return OrganizationRuntimeMutation.Failure(409, "INVALID_STATE");
        _ = point.TryUpdateConfiguration(
            description, metricId.ToString("D"), unitId.ToString("D"),
            dataOwnerUserId.ToString("D"), expectedIntervalSeconds, noDataAfterSeconds);
        await repository.UpdatePointAsync(point, ct);
        return OrganizationRuntimeMutation.Success(
            "MeasurementPoint", point.Id.Value, point.Version,
            point.SiteId.Value, point.AreaId.Value);
    }

    public async Task<OrganizationRuntimeMutation> InactivatePointAsync(
        Guid pointId,
        long expectedVersion,
        string actorUserId,
        CancellationToken ct = default)
    {
        var point = await repository.GetPointAsync(new PointId(pointId), ct);
        if (point is null) return OrganizationRuntimeMutation.Failure(404, "NOT_FOUND");
        if (await DeniedAsync(
            actorUserId, OrganizationResource.SiteChild, point.SiteId.Value, ct, point.AreaId.Value) is { } denied)
            return denied;
        if (point.Version != expectedVersion)
            return OrganizationRuntimeMutation.Failure(409, "VERSION_CONFLICT");
        var previous = point.Status;
        if (!point.TryInactivate())
            return OrganizationRuntimeMutation.Failure(409, "PRECONDITION_FAILED");
        await repository.UpdatePointAsync(point, ct);
        await repository.AddLifecycleEntryAsync(new PointLifecycleEntry(
            Guid.NewGuid().ToString("D"), point.Id.ToString(), point.Version,
            previous, point.Status, actorUserId, actorUserId,
            "Inactivated by runtime command", DateTime.UtcNow, null, null), ct);
        return OrganizationRuntimeMutation.Success(
            "MeasurementPoint", point.Id.Value, point.Version,
            point.SiteId.Value, point.AreaId.Value);
    }

    private async Task<OrganizationRuntimeMutation?> DeniedAsync(
        string actorUserId,
        OrganizationResource resource,
        Guid? siteId,
        CancellationToken ct,
        Guid? areaId = null)
    {
        var decision = await authorization.AuthorizeTargetAsync(
            actorUserId, resource, siteId?.ToString("D"),
            areaId?.ToString("D"), ct);
        if (decision.IsAllowed) return null;
        return decision.Code.Equals("NotFound", StringComparison.OrdinalIgnoreCase)
            ? OrganizationRuntimeMutation.Failure(404, "NOT_FOUND")
            : OrganizationRuntimeMutation.Failure(403, "FORBIDDEN");
    }

    private static string Text(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string Code(string value, string fallback)
    {
        var source = Text(value, fallback).ToUpperInvariant();
        var chars = source.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        var result = new string(chars).Trim('_');
        return result.Length == 0 ? fallback : result[..Math.Min(result.Length, 48)];
    }
}

public sealed record OrganizationRuntimeMutation(
    bool IsSuccess,
    int StatusCode,
    string? ErrorCode,
    string? EntityType,
    Guid? Id,
    long? Version,
    Guid? SiteId,
    Guid? AreaId)
{
    public static OrganizationRuntimeMutation Success(
        string entityType,
        Guid id,
        long version,
        Guid siteId,
        Guid? areaId) =>
        new(true, 200, null, entityType, id, version, siteId, areaId);

    public static OrganizationRuntimeMutation Failure(int statusCode, string errorCode) =>
        new(false, statusCode, errorCode, null, null, null, null, null);
}
