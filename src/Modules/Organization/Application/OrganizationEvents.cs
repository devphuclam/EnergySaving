using System.Collections.ObjectModel;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;

namespace IUMP.Modules.Organization.Application;

public static class OrganizationEvents
{
    public static OwnerEventEnvelope BuildPointStatusChanged(
        MeasurementPoint point,
        PointStatus oldStatus,
        PointStatus newStatus,
        OrganizationCommandContext ctx,
        OrganizationCallerSnapshot actor)
    {
        var before = Snapshot(point, oldStatus, point.Version - 1);
        var after = Snapshot(point, newStatus, point.Version);
        var action = oldStatus == PointStatus.Inactive ? "Reactivated" : "Activated";
        var correlationId = ctx.CorrelationId ?? Guid.NewGuid().ToString("D");
        return new OwnerEventEnvelope(
            Guid.NewGuid(), "PointStatusChanged.v1", 1, "IUMP.Organization", "MeasurementPoint",
            point.Id.ToString(), point.Version, ctx.ActorUserId, actor.Username,
            new ReadOnlyDictionary<string, object?>(before), new ReadOnlyDictionary<string, object?>(after),
            action, $"Point {action.ToLowerInvariant()}.", DateTime.UtcNow,
            correlationId, ctx.CausationId,
            point.SiteId.ToString(), point.AreaId.ToString());
    }

    private static Dictionary<string, object?> Snapshot(MeasurementPoint point, PointStatus status, long version) => new(StringComparer.Ordinal)
    {
        ["pointId"] = point.Id.ToString(),
        ["siteId"] = point.SiteId.ToString(),
        ["areaId"] = point.AreaId.ToString(),
        ["assetId"] = point.AssetId.ToString(),
        ["status"] = status.ToString(),
        ["version"] = version
    };
}
