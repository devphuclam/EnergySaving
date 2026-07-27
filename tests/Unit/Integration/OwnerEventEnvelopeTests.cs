using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Integration;

public static class OwnerEventEnvelopeTests
{
    private static readonly string[] PointKeys = { "pointId", "siteId", "areaId", "assetId", "status", "version" };

    public static List<string> Run()
    {
        var failures = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var site = new Site(SiteId.New(), "EVT-SITE", "Event Site", null, "UTC", SiteStatus.Active, 1);
        var area = new Area(AreaId.New(), site.Id, "EVT-AREA", "Event Area", null, AreaStatus.Active, 1);
        var asset = new Asset(AssetId.New(), site.Id, area.Id, "EVT-ASSET", "Event Asset", null, AssetStatus.Active, 1);
        var point = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "EVT-PT", "event test",
            "metric", "unit", "owner", 60, 300, PointStatus.Draft, 1);
        var caller = new OrganizationCallerSnapshot("admin", "admin@test", true, new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>());
        var ctx = new OrganizationCommandContext("admin", "event-correlation", "event-causation");
        point.TryActivate();
        var envelope = OrganizationEvents.BuildPointStatusChanged(point, PointStatus.Draft, PointStatus.Active, ctx, caller);

        if (envelope.EventType != "PointStatusChanged.v1") failures.Add("event type must be PointStatusChanged.v1.");
        if (envelope.SchemaVersion != 1) failures.Add("schema version must be 1.");
        if (envelope.Producer != "IUMP.Organization") failures.Add("producer must be IUMP.Organization.");
        if (envelope.AggregateType != "MeasurementPoint") failures.Add("aggregate type must be MeasurementPoint.");
        if (envelope.AggregateId != point.Id.ToString() || envelope.AggregateVersion != 2) failures.Add("aggregate identity/version mismatch.");
        if (envelope.ActorId != "admin" || envelope.ActorUsername != "admin@test") failures.Add("trusted actor identity missing.");
        if (envelope.Action != "Activated") failures.Add("action must be Activated.");
        if (envelope.CorrelationId != "event-correlation" || envelope.CausationId != "event-causation") failures.Add("correlation/causation not propagated.");
        if (envelope.OccurredAt.Kind != DateTimeKind.Utc) failures.Add("occurred-at must be UTC.");
        if (envelope.SiteId != site.Id.ToString() || envelope.AreaId != area.Id.ToString()) failures.Add("trusted Site/Area IDs missing.");
        AssertKeys(failures, envelope.Before, "Before");
        AssertKeys(failures, envelope.After, "After");
        if (envelope.Before["status"]?.ToString() != "Draft" || envelope.After["status"]?.ToString() != "Active") failures.Add("status snapshots are incorrect.");
        if (envelope.Before["version"] is not long beforeVersion || beforeVersion != 1) failures.Add("before version must be prior version.");
        if (envelope.After["version"] is not long afterVersion || afterVersion != 2) failures.Add("after version must be aggregate version.");
        if (envelope.Before.Keys.Concat(envelope.After.Keys).Any(k => k.Contains("password", StringComparison.OrdinalIgnoreCase) || k.Contains("secret", StringComparison.OrdinalIgnoreCase) || k.Contains("token", StringComparison.OrdinalIgnoreCase)))
            failures.Add("event snapshots must not contain secrets.");
        return failures;
    }

    private static void AssertKeys(List<string> failures, IReadOnlyDictionary<string, object?> snapshot, string name)
    {
        if (snapshot.Count != PointKeys.Length || PointKeys.Any(k => !snapshot.ContainsKey(k)) || snapshot.Keys.Any(k => !PointKeys.Contains(k)))
            failures.Add($"{name} must contain exactly the six safe Point fields.");
    }
}
