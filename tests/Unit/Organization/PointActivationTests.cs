using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Organization;

public static class PointActivationTests
{
    public static int TestCount;
    public static int AssertionCount;

    public static List<string> Run()
    {
        TestCount = 0;
        AssertionCount = 0;
        var f = new List<string>();
        // -- authorization --
        AuthCase(f, "Admin Draft", () => Success(PointStatus.Draft, AdminCaller(), "Activated"));
        AuthCase(f, "scoped Engineer Draft", () => ScopedEngineerSuccess());
        AuthCase(f, "Inactive reactivation", () => Success(PointStatus.Inactive, AdminCaller(), "Reactivated"));
        AuthCase(f, "unscoped Engineer denied", () => Denied(new("eng", "eng", true, new[] { "Engineer" }, [], []), "NOT_FOUND"));
        foreach (var role in new[] { "Operator", "Manager", "Viewer" })
            AuthCase(f, $"{role} denied", () => Denied(new("u", role, true, new[] { role }, [], []), "FORBIDDEN"));
        AuthCase(f, "inactive caller", () => Denied(new("admin", "admin", false, new[] { "Administrator" }, [], []), "FORBIDDEN"));
        AuthCase(f, "missing caller", () => MissingCallerFailure());
        AuthCase(f, "spoofed scope", () => Denied(new("eng", "eng", true, new[] { "Engineer" }, [Guid.NewGuid().ToString()], []), "NOT_FOUND"));
        // -- preflight --
        AuthCase(f, "missing Point", () => MissingPointFailure());
        AuthCase(f, "Active no-op", () => NoOpSuccess());
        AuthCase(f, "Decommissioned invalid", () => StateFailure(PointStatus.Decommissioned, "INVALID_STATE"));
        AuthCase(f, "stale version", () => StaleVersionFailure());
        AuthCase(f, "inactive Site", () => ParentFailure(SiteStatus.Inactive, AreaStatus.Active, AssetStatus.Active));
        AuthCase(f, "inactive Area", () => ParentFailure(SiteStatus.Active, AreaStatus.Inactive, AssetStatus.Active));
        AuthCase(f, "inactive Asset", () => ParentFailure(SiteStatus.Active, AreaStatus.Active, AssetStatus.Inactive));
        AuthCase(f, "inconsistent ancestry", () => InconsistentAncestryFailure());
        AuthCase(f, "invalid intervals", () => InvalidIntervalFailure());
        // -- owner validation --
        AuthCase(f, "owner missing", () => OwnerFailure(s => s with { Exists = false }));
        AuthCase(f, "owner inactive", () => OwnerFailure(s => s with { IsActive = false }));
        AuthCase(f, "owner no site scope", () => OwnerFailure(s => s with { HasTrustedSiteScope = false }));
        AuthCase(f, "owner no area scope", () => OwnerFailure(s => s with { HasTrustedAreaScope = false }));
        AuthCase(f, "owner wrong SiteId", () => OwnerFailure(s => s with { TrustedSiteId = "wrong-site" }));
        AuthCase(f, "owner wrong AreaId", () => OwnerFailure(s => s with { TrustedAreaId = "wrong-area" }));
        AuthCase(f, "owner UserVersion=0", () => OwnerFailure(s => s with { UserVersion = 0 }));
        AuthCase(f, "owner ScopeVersion=0", () => OwnerFailure(s => s with { ScopeVersion = 0 }));
        AuthCase(f, "owner forbidden capability", () => OwnerFailure(s => s with { HasForbiddenCapability = true }));
        AuthCase(f, "owner drift", () => ProviderDriftFailure(owner: true));
        // -- catalog validation --
        AuthCase(f, "Metric missing", () => CatalogFailure(s => s! with { MetricStatus = "Missing" }, "METRIC_NOT_FOUND"));
        AuthCase(f, "Metric inactive", () => CatalogFailure(s => s! with { MetricStatus = "Inactive" }, "METRIC_INACTIVE"));
        AuthCase(f, "MetricVersion=0", () => CatalogFailure(s => s! with { MetricVersion = 0 }, "METRIC_NOT_FOUND"));
        AuthCase(f, "Unit missing", () => CatalogFailure(s => s! with { UnitStatus = "Missing" }, "UNIT_NOT_FOUND"));
        AuthCase(f, "Unit inactive", () => CatalogFailure(s => s! with { UnitStatus = "Inactive" }, "UNIT_INACTIVE"));
        AuthCase(f, "UnitVersion=0", () => CatalogFailure(s => s! with { UnitVersion = 0 }, "UNIT_NOT_FOUND"));
        AuthCase(f, "incompatible Unit", () => CatalogFailure(s => s! with { IsCompatible = false }, "UNIT_INCOMPATIBLE"));
        AuthCase(f, "CompatibilityVersion=0", () => CatalogFailure(s => s! with { CompatibilityVersion = 0 }, "UNIT_INCOMPATIBLE"));
        AuthCase(f, "missing CompatibilityIdentity", () => CatalogFailure(s => s! with { CompatibilityIdentity = "" }, "UNIT_INCOMPATIBLE"));
        AuthCase(f, "inactive CompatibilityStatus", () => CatalogFailure(s => s! with { CompatibilityStatus = "Inactive" }, "UNIT_INCOMPATIBLE"));
        AuthCase(f, "mapping missing", () => CatalogFailure(s => s! with { ActiveMappingCount = 0 }, "MAPPING_MISSING"));
        AuthCase(f, "mapping multiple", () => CatalogFailure(s => s! with { ActiveMappingCount = 2 }, "MAPPING_MULTIPLE"));
        AuthCase(f, "MappingVersion=0", () => CatalogFailure(s => s! with { MappingVersion = 0 }, "MAPPING_MISSING"));
        AuthCase(f, "mapping inactive", () => CatalogFailure(s => s! with { MappingStatus = "Inactive" }, "MAPPING_MISSING"));
        AuthCase(f, "mapping point mismatch", () => CatalogFailure(s => s! with { MappingPointId = "other-id", PointId = "other-id" }, "MAPPING_POINT_MISMATCH"));
        AuthCase(f, "future mapping", () => CatalogFailure(s => s! with { EffectiveFromUtc = DateTime.UtcNow.AddMinutes(5) }, "MAPPING_MISSING"));
        AuthCase(f, "expired mapping", () => CatalogFailure(s => s! with { EffectiveToUtc = DateTime.UtcNow.AddMinutes(-1) }, "MAPPING_MISSING"));
        AuthCase(f, "Source inactive", () => CatalogFailure(s => s! with { SourceStatus = "Inactive" }, "SOURCE_NOT_ACTIVE"));
        AuthCase(f, "Source non-Simulator", () => CatalogFailure(s => s! with { SourceType = "Modbus" }, "SOURCE_NOT_ACTIVE"));
        AuthCase(f, "SourceVersion=0", () => CatalogFailure(s => s! with { SourceVersion = 0 }, "SOURCE_NOT_ACTIVE"));
        AuthCase(f, "Catalog drift", () => ProviderDriftFailure(owner: false));
        AuthCase(f, "repeat activation", () => RepeatActivationNoOp());
        // -- iam side-effect check --
        AuthCase(f, "no IAM mutation", () => NoIamMutationCheck());
        return f;
    }

    private static void AuthCase(List<string> failures, string name, Func<string?> test)
    {
        TestCount++;
        AssertionCount++;
        try { if (test() is { } err) failures.Add($"{name}: {err}"); } catch (Exception ex) { failures.Add($"{name}: {ex.Message}"); }
    }

    private static string? Success(PointStatus status, OrganizationCallerSnapshot caller, string action)
    {
        var fx = Build(status, caller);
        var r = Exec(fx);
        var p = fx.Repo.GetPointAsync(fx.Point.Id).GetAwaiter().GetResult()!;
        if (!r.IsSuccess || r.Outcome != ActivationOutcome.Allowed || p.Status != PointStatus.Active) return $"expected success, got {r.ErrorCode}";
        if (fx.Outbox.Enqueued.Count != 1 || fx.Outbox.Enqueued[0].Action != action) return "expected one outbox with correct action";
        if (fx.Repo.GetLifecycleForPointAsync(p.Id.ToString()).GetAwaiter().GetResult().Count != 1) return "expected one lifecycle entry";
        return null;
    }

    private static string? ScopedEngineerSuccess()
    {
        var fx = Build(PointStatus.Draft, new("eng", "eng", true, new[] { "Engineer" }, [], []));
        fx.Authorization = new FakeOrganizationAuthorization(fx.Caller with { SiteScopes = [fx.Site.Id.ToString()] });
        var r = Exec(fx, "eng");
        return r.IsSuccess ? null : $"expected success, got {r.ErrorCode}";
    }

    private static string? Denied(OrganizationCallerSnapshot caller, string code)
    {
        var fx = Build(PointStatus.Draft, caller);
        var r = Exec(fx, caller.UserId);
        return r.ErrorCode == code && Unchanged(fx) ? null : $"expected {code}, got {r.ErrorCode}";
    }

    private static string? MissingCallerFailure()
    {
        var fx = Build(PointStatus.Draft, AdminCaller());
        fx.Authorization = new FakeOrganizationAuthorization(null);
        var r = Exec(fx);
        return r.ErrorCode == "FORBIDDEN" && Unchanged(fx) ? null : "missing caller must fail closed.";
    }

    private static string? MissingPointFailure()
    {
        var fx = Build(PointStatus.Draft, AdminCaller());
        var r = ActivateMeasurementPoint.ExecuteAsync(PointId.New(), 1, Ctx(), fx.Repo, fx.Iam, fx.Org, fx.Cat, fx.Authorization, fx.Outbox, fx.Coord).GetAwaiter().GetResult();
        return r.ErrorCode == "NOT_FOUND" ? null : "missing Point must be NOT_FOUND.";
    }

    private static string? NoOpSuccess()
    {
        var fx = Build(PointStatus.Active, AdminCaller());
        var r = Exec(fx);
        return r.IsSuccess && r.Outcome == ActivationOutcome.NoOp && r.ErrorCode == "NO_OP" && Unchanged(fx) ? null : "Active must be successful NO_OP.";
    }

    private static string? StateFailure(PointStatus state, string code)
    {
        var fx = Build(state, AdminCaller());
        var r = Exec(fx);
        return r.ErrorCode == code && Unchanged(fx) ? null : $"expected {code}, got {r.ErrorCode}";
    }

    private static string? StaleVersionFailure()
    {
        var fx = Build(PointStatus.Draft, AdminCaller());
        var r = ActivateMeasurementPoint.ExecuteAsync(fx.Point.Id, 99, Ctx(), fx.Repo, fx.Iam, fx.Org, fx.Cat, fx.Authorization, fx.Outbox, fx.Coord).GetAwaiter().GetResult();
        return r.ErrorCode == "VERSION_CONFLICT" && Unchanged(fx) ? null : "stale version must fail without mutation.";
    }

    private static string? ParentFailure(SiteStatus site, AreaStatus area, AssetStatus asset)
    {
        var fx = Build(PointStatus.Draft, AdminCaller(), site, area, asset);
        var r = Exec(fx);
        return r.ErrorCode == "PARENT_NOT_ACTIVE" && Unchanged(fx) ? null : $"expected PARENT_NOT_ACTIVE, got {r.ErrorCode}";
    }

    private static string? InconsistentAncestryFailure()
    {
        var fx = Build(PointStatus.Draft, AdminCaller());
        fx.Org.SnapshotOverride = fx.Org.SnapshotOverride! with { Asset = new Asset(fx.Asset.Id, SiteId.New(), fx.Asset.AreaId, "BAD", "bad", null, AssetStatus.Active, fx.Asset.Version) };
        var r = Exec(fx);
        return r.ErrorCode == "INVALID_STATE" && Unchanged(fx) ? null : "inconsistent ancestry must fail.";
    }

    private static string? InvalidIntervalFailure()
    {
        var fx = Build(PointStatus.Draft, AdminCaller());
        fx.Org.SnapshotOverride = fx.Org.SnapshotOverride! with { IntervalValidOverride = false };
        var r = Exec(fx);
        return r.ErrorCode == "INTERVAL_INVALID" && Unchanged(fx) ? null : "invalid intervals must fail without mutation.";
    }

    private static string? OwnerFailure(Func<ActivationDataOwnerSnapshot, ActivationDataOwnerSnapshot> change)
    {
        var fx = Build(PointStatus.Draft, AdminCaller());
        fx.Iam.Snapshot = change(fx.Iam.Snapshot);
        var r = Exec(fx);
        return r.ErrorCode == "DATA_OWNER_INELIGIBLE" && Unchanged(fx) ? null : $"owner failure got {r.ErrorCode}";
    }

    private static string? CatalogFailure(Func<ActivationCatalogSnapshot?, ActivationCatalogSnapshot?> change, string code)
    {
        var fx = Build(PointStatus.Draft, AdminCaller());
        fx.Cat.Snapshot = change(fx.Cat.Snapshot);
        var r = Exec(fx);
        return r.ErrorCode == code && Unchanged(fx) ? null : $"expected {code}, got {r.ErrorCode}";
    }

    private static string? ProviderDriftFailure(bool owner)
    {
        var fx = Build(PointStatus.Draft, AdminCaller());
        if (owner) fx.Iam.ChangeOnSecondRead = true;
        else fx.Cat.ChangeOnSecondRead = true;
        var r = Exec(fx);
        return r.Outcome == ActivationOutcome.ProviderVersionConflict && Unchanged(fx) ? null : "provider drift must rollback.";
    }

    private static string? RepeatActivationNoOp()
    {
        var fx = Build(PointStatus.Draft, AdminCaller());
        var first = Exec(fx);
        var v = fx.Repo.GetPointAsync(fx.Point.Id).GetAwaiter().GetResult()!.Version;
        var secondBackend = new FakeAtomicBackend(fx.Repo);
        var second = ActivateMeasurementPoint.ExecuteAsync(fx.Point.Id, v, Ctx(), fx.Repo, fx.Iam, fx.Org, fx.Cat, fx.Authorization, fx.Outbox, new HostTransactionCoordinator(secondBackend)).GetAwaiter().GetResult();
        var history = fx.Repo.GetLifecycleForPointAsync(fx.Point.Id.ToString()).GetAwaiter().GetResult();
        return first.IsSuccess && second.IsSuccess && second.Outcome == ActivationOutcome.NoOp && v == 2 && history.Count == 1 && fx.Outbox.Enqueued.Count == 1 ? null : $"repeat activation must be no-op (first={first.ErrorCode}, second={second.ErrorCode}, v={v}, hist={history.Count}, out={fx.Outbox.Enqueued.Count}).";
    }

    private static string? NoIamMutationCheck()
    {
        var fx = Build(PointStatus.Draft, AdminCaller());
        var iamSnapshot = fx.Iam.Snapshot with { };
        var before = fx.Iam.Snapshot;
        var r = Exec(fx);
        var after = fx.Iam.Snapshot;
        return r.IsSuccess && Equals(before, after) ? null : "activation must not mutate IAM data.";
    }

    // -- helpers --

    private static ActivationResult Exec(Fixture fx, string actor = "admin") =>
        ActivateMeasurementPoint.ExecuteAsync(fx.Point.Id, 1, Ctx(actor), fx.Repo, fx.Iam, fx.Org, fx.Cat, fx.Authorization, fx.Outbox, fx.Coord).GetAwaiter().GetResult();

    private static OrganizationCommandContext Ctx(string actor = "admin") => new(actor, "p5-correlation", null);

    private static bool Unchanged(Fixture fx)
    {
        var p = fx.Repo.GetPointAsync(fx.Point.Id).GetAwaiter().GetResult()!;
        return p.Status == fx.Point.Status && p.Version == fx.Point.Version && fx.Outbox.Enqueued.Count == 0 && fx.Repo.GetLifecycleForPointAsync(p.Id.ToString()).GetAwaiter().GetResult().Count == 0;
    }

    private static OrganizationCallerSnapshot AdminCaller() => new("admin", "admin@test", true, ["Administrator"], [], []);

    private sealed class Fixture
    {
        public required FakeOrganizationCommandRepository Repo { get; init; }
        public required FakeAtomicBackend Backend { get; init; }
        public required Site Site { get; init; }
        public required Asset Asset { get; init; }
        public required MeasurementPoint Point { get; init; }
        public required FakeActivationIdentityQuery Iam { get; init; }
        public required FakeActivationOrganizationParticipant Org { get; init; }
        public required FakeActivationCatalogQuery Cat { get; init; }
        public required FakeTransactionalOutboxWriter Outbox { get; init; }
        public required HostTransactionCoordinator Coord { get; init; }
        public required OrganizationCallerSnapshot Caller { get; init; }
        public required IOrganizationAuthorization Authorization { get; set; }
    }

    private static Fixture Build(PointStatus pointStatus, OrganizationCallerSnapshot caller, SiteStatus siteStatus = SiteStatus.Active, AreaStatus areaStatus = AreaStatus.Active, AssetStatus assetStatus = AssetStatus.Active)
    {
        var repo = new FakeOrganizationCommandRepository();
        var backend = new FakeAtomicBackend(repo);
        var site = new Site(SiteId.New(), "P5-SITE", "Phase 5", null, "UTC", siteStatus, 1);
        var area = new Area(AreaId.New(), site.Id, "P5-AREA", "Area", null, areaStatus, 1);
        var asset = new Asset(AssetId.New(), site.Id, area.Id, "P5-ASSET", "Asset", null, assetStatus, 1);
        var point = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "P5-POINT", "test", "metric-1", "unit-1", "owner-user", 60, 300, pointStatus, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();
        repo.AddAreaAsync(area).GetAwaiter().GetResult();
        repo.AddAssetAsync(asset).GetAwaiter().GetResult();
        repo.AddPointAsync(point).GetAwaiter().GetResult();
        var iam = new FakeActivationIdentityQuery { Snapshot = new ActivationDataOwnerSnapshot("owner-user", true, true, true, true, false, 1, 1, site.Id.ToString(), area.Id.ToString()) };
        var org = new FakeActivationOrganizationParticipant(repo, backend);
        var cat = new FakeActivationCatalogQuery { Snapshot = new ActivationCatalogSnapshot("metric-1", 1, "Active", "unit-1", 1, "Active", true, 1, "mapping-1", 1, "Active", "source-1", 1, "Active", "Simulator", DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1), 1, point.Id.ToString(), point.Id.ToString(), "metric-1|unit-1", "Active") };
        var outbox = new FakeTransactionalOutboxWriter(backend);
        var coord = new HostTransactionCoordinator(backend);
        var auth = new FakeOrganizationAuthorization(caller);
        org.SnapshotOverride = new ActivationOrganizationSnapshot(point, site, area, asset);
        return new Fixture { Repo = repo, Backend = backend, Site = site, Asset = asset, Point = point, Iam = iam, Org = org, Cat = cat, Outbox = outbox, Coord = coord, Caller = caller, Authorization = auth };
    }
}
