using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Organization;

public static class PointActivationTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        Check(failures, "Admin activation", AdminActivation);
        Check(failures, "Scoped Engineer activation", ScopedEngineerActivation);
        Check(failures, "Inactive reactivation", InactiveReactivation);
        Check(failures, "Active no-op", ActiveIsNoOp);
        Check(failures, "Decommissioned rejection", DecommissionedIsRejected);
        Check(failures, "Prerequisite rejection", PrerequisitesAreEnforced);
        Check(failures, "Provider recheck", ProviderVersionConflictRollsBack);
        Check(failures, "Outbox atomicity", OutboxFailureRollsBack);
        return failures;
    }

    private static void Check(List<string> failures, string name, Func<string?> test)
    {
        try { if (test() is { } failure) failures.Add($"{name}: {failure}"); }
        catch (Exception ex) { failures.Add($"{name}: unexpected {ex.GetType().Name}: {ex.Message}"); }
    }

    private static string? AdminActivation()
    {
        var fx = BuildFixture(PointStatus.Draft, new("admin", "admin@test", true, new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>()));
        var result = Execute(fx);
        if (!result.IsSuccess || result.Outcome != ActivationOutcome.Allowed) return $"expected success, got {result.ErrorCode}";
        var point = fx.Repo.GetPointAsync(fx.Point.Id).GetAwaiter().GetResult()!;
        if (point.Status != PointStatus.Active || point.Version != 2) return "Point must become Active at version 2.";
        if (fx.Repo.GetLifecycleForPointAsync(point.Id.ToString()).GetAwaiter().GetResult().Count != 1) return "exactly one lifecycle row required.";
        if (fx.Outbox.Count != 1) return "exactly one outbox envelope required.";
        var envelope = fx.Outbox.Enqueued[0];
        if (envelope.EventType != "PointStatusChanged.v1" || envelope.Action != "Activated") return "activation envelope contract mismatch.";
        return null;
    }

    private static string? ScopedEngineerActivation()
    {
        var fx = BuildFixture(PointStatus.Draft, new("eng", "engineer@test", true, new[] { "Engineer" }, new[] { "site" }, Array.Empty<string>()));
        var caller = fx.Caller with { SiteScopes = new[] { fx.Site.Id.ToString() } };
        fx.Authorization = new FakeOrganizationAuthorization(caller);
        var result = Execute(fx, "eng");
        return result.IsSuccess ? null : $"scoped Engineer denied with {result.ErrorCode}";
    }

    private static string? InactiveReactivation()
    {
        var fx = BuildFixture(PointStatus.Inactive, AdminCaller());
        var result = Execute(fx);
        if (!result.IsSuccess || result.NewStatus != PointStatus.Active) return "Inactive Point must reactivate.";
        if (fx.Outbox.Enqueued[0].Action != "Reactivated") return "reactivation action must be Reactivated.";
        return null;
    }

    private static string? ActiveIsNoOp()
    {
        var fx = BuildFixture(PointStatus.Active, AdminCaller());
        var result = Execute(fx);
        if (result.IsSuccess || result.Outcome != ActivationOutcome.NoOp) return "Active Point must return a silent no-op outcome.";
        if (fx.Outbox.Count != 0) return "Active no-op must not enqueue.";
        return null;
    }

    private static string? DecommissionedIsRejected()
    {
        var fx = BuildFixture(PointStatus.Decommissioned, AdminCaller());
        var result = Execute(fx);
        return result.Outcome == ActivationOutcome.InvalidState && result.ErrorCode == "INVALID_STATE" ? null : "Decommissioned Point must return INVALID_STATE.";
    }

    private static string? PrerequisitesAreEnforced()
    {
        var fx = BuildFixture(PointStatus.Draft, AdminCaller());
        fx.Identity.Snapshot = fx.Identity.Snapshot with { HasTrustedAreaScope = false };
        var ownerResult = Execute(fx);
        if (ownerResult.ErrorCode != "DATA_OWNER_INELIGIBLE") return "ineligible Data Owner must be rejected.";

        fx = BuildFixture(PointStatus.Draft, AdminCaller());
        fx.Catalog.Snapshot = fx.Catalog.Snapshot! with { ActiveMappingCount = 0 };
        var mappingResult = Execute(fx);
        if (mappingResult.ErrorCode != "MAPPING_MISSING") return "missing Mapping must be rejected.";

        fx = BuildFixture(PointStatus.Draft, AdminCaller(), assetStatus: AssetStatus.Inactive);
        var parentResult = Execute(fx);
        return parentResult.ErrorCode == "PARENT_NOT_ACTIVE" ? null : "inactive parent Asset must be rejected.";
    }

    private static string? ProviderVersionConflictRollsBack()
    {
        var fx = BuildFixture(PointStatus.Draft, AdminCaller());
        fx.Identity.ChangeOnSecondRead = true;
        var result = Execute(fx);
        var point = fx.Repo.GetPointAsync(fx.Point.Id).GetAwaiter().GetResult()!;
        return result.Outcome == ActivationOutcome.ProviderVersionConflict && point.Status == PointStatus.Draft && fx.Outbox.Count == 0
            ? null : "provider version drift must rollback without side effects.";
    }

    private static string? OutboxFailureRollsBack()
    {
        var fx = BuildFixture(PointStatus.Draft, AdminCaller());
        fx.Outbox.FailOnEnqueue = true;
        var result = Execute(fx);
        var point = fx.Repo.GetPointAsync(fx.Point.Id).GetAwaiter().GetResult()!;
        return result.ErrorCode == "OUTBOX_WRITE_FAILED" && point.Status == PointStatus.Draft && point.Version == 1 &&
            fx.Repo.GetLifecycleForPointAsync(point.Id.ToString()).GetAwaiter().GetResult().Count == 0
            ? null : "outbox failure must rollback Point and lifecycle.";
    }

    private static ActivationResult Execute(Fixture fx, string actor = "admin") =>
        ActivateMeasurementPoint.ExecuteAsync(fx.Point.Id, 1,
            new OrganizationCommandContext(actor, "phase5-correlation", "phase5-causation"),
            fx.Repo, fx.Identity, fx.Catalog, fx.Authorization, fx.Outbox, fx.Host).GetAwaiter().GetResult();

    private static OrganizationCallerSnapshot AdminCaller() =>
        new("admin", "admin@test", true, new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>());

    private sealed class Fixture
    {
        public required FakeOrganizationCommandRepository Repo { get; init; }
        public required Site Site { get; init; }
        public required MeasurementPoint Point { get; init; }
        public required FakeActivationIdentityQuery Identity { get; init; }
        public required FakeActivationCatalogQuery Catalog { get; init; }
        public required FakeTransactionalOutboxWriter Outbox { get; init; }
        public required HostTransactionCoordinator Host { get; init; }
        public required OrganizationCallerSnapshot Caller { get; init; }
        public required FakeOrganizationAuthorization Authorization { get; set; }
    }

    private static Fixture BuildFixture(PointStatus pointStatus, OrganizationCallerSnapshot caller, AssetStatus assetStatus = AssetStatus.Active)
    {
        var repo = new FakeOrganizationCommandRepository();
        var site = new Site(SiteId.New(), "P5-SITE", "Phase 5 Site", null, "UTC", SiteStatus.Active, 1);
        var area = new Area(AreaId.New(), site.Id, "P5-AREA", "Phase 5 Area", null, AreaStatus.Active, 1);
        var asset = new Asset(AssetId.New(), site.Id, area.Id, "P5-ASSET", "Phase 5 Asset", null, assetStatus, 1);
        var point = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "P5-POINT", "activation test",
            "metric-1", "unit-1", "owner-user", 60, 300, pointStatus, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();
        repo.AddAreaAsync(area).GetAwaiter().GetResult();
        repo.AddAssetAsync(asset).GetAwaiter().GetResult();
        repo.AddPointAsync(point).GetAwaiter().GetResult();
        var identity = new FakeActivationIdentityQuery();
        var catalog = new FakeActivationCatalogQuery
        {
            Snapshot = new ActivationCatalogSnapshot("metric-1", 1, "Active", "unit-1", 1, "Active", true, 1,
                "mapping-1", 1, "Active", "source-1", 1, "Active", "Simulator",
                DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1), 1)
        };
        var authorization = new FakeOrganizationAuthorization(caller);
        return new Fixture { Repo = repo, Site = site, Point = point, Identity = identity, Catalog = catalog,
            Outbox = new FakeTransactionalOutboxWriter(), Host = new HostTransactionCoordinator(), Caller = caller, Authorization = authorization };
    }
}
