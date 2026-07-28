using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Organization;

public static class PointActivationTests
{
    public const int CaseCount = 41;
    public static List<string> Run()
    {
        var failures = new List<string>();
        Case(failures, "Administrator Draft", () => Success(PointStatus.Draft, AdminCaller()));
        Case(failures, "scoped Engineer Draft", () => ScopedEngineer());
        Case(failures, "Inactive reactivation", () => Success(PointStatus.Inactive, AdminCaller(), "Reactivated"));
        Case(failures, "unscoped Engineer", () => Denied(new("eng", "eng", true, new[] { "Engineer" }, Array.Empty<string>(), Array.Empty<string>()), "NOT_FOUND"));
        foreach (var role in new[] { "Operator", "Manager", "Viewer" }) Case(failures, role, () => Denied(new("user", role, true, new[] { role }, Array.Empty<string>(), Array.Empty<string>()), "FORBIDDEN"));
        Case(failures, "inactive caller", () => Denied(new("admin", "admin", false, new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>()), "FORBIDDEN"));
        Case(failures, "missing caller", () => MissingCaller());
        Case(failures, "spoofed scope", () => Denied(new("eng", "eng", true, new[] { "Engineer" }, new[] { Guid.NewGuid().ToString() }, Array.Empty<string>()), "NOT_FOUND"));
        Case(failures, "missing point", () => MissingPoint());
        Case(failures, "Active no-op", () => NoOp());
        Case(failures, "Decommissioned", () => StateFailure(PointStatus.Decommissioned, "INVALID_STATE"));
        Case(failures, "stale version", () => StaleVersion());
        Case(failures, "inactive Site", () => ParentFailure(SiteStatus.Inactive, AreaStatus.Active, AssetStatus.Active));
        Case(failures, "inactive Area", () => ParentFailure(SiteStatus.Active, AreaStatus.Inactive, AssetStatus.Active));
        Case(failures, "inactive Asset", () => ParentFailure(SiteStatus.Active, AreaStatus.Active, AssetStatus.Inactive));
        Case(failures, "inconsistent ancestry", () => InconsistentAncestry());
        Case(failures, "invalid intervals", () => InvalidIntervals());
        Case(failures, "missing owner", () => OwnerFailure(s => s with { Exists = false }));
        Case(failures, "inactive owner", () => OwnerFailure(s => s with { IsActive = false }));
        Case(failures, "wrong owner scope", () => OwnerFailure(s => s with { HasTrustedAreaScope = false }));
        Case(failures, "forbidden owner capability", () => OwnerFailure(s => s with { HasForbiddenCapability = true }));
        Case(failures, "owner version drift", () => ProviderDrift(owner: true));
        Case(failures, "missing Metric", () => CatalogFailure(s => s! with { MetricStatus = "Missing" }, "METRIC_NOT_FOUND"));
        Case(failures, "catalog configured IDs preserved", () => CatalogConfiguredIdsPreserved());
        Case(failures, "inactive Metric", () => CatalogFailure(s => s! with { MetricStatus = "Inactive" }, "METRIC_INACTIVE"));
        Case(failures, "missing Unit", () => CatalogFailure(s => s! with { UnitStatus = "Missing" }, "UNIT_NOT_FOUND"));
        Case(failures, "inactive Unit", () => CatalogFailure(s => s! with { UnitStatus = "Inactive" }, "UNIT_INACTIVE"));
        Case(failures, "incompatible Unit", () => CatalogFailure(s => s! with { IsCompatible = false }, "UNIT_INCOMPATIBLE"));
        Case(failures, "inactive compatibility", () => CatalogFailure(s => s! with { CompatibilityStatus = "Inactive" }, "UNIT_INCOMPATIBLE"));
        Case(failures, "mapping missing", () => CatalogFailure(s => s! with { ActiveMappingCount = 0 }, "MAPPING_MISSING"));
        Case(failures, "mapping multiple", () => CatalogFailure(s => s! with { ActiveMappingCount = 2 }, "MAPPING_MULTIPLE"));
        Case(failures, "inactive mapping", () => CatalogFailure(s => s! with { MappingStatus = "Inactive" }, "MAPPING_MISSING"));
        Case(failures, "future mapping", () => CatalogFailure(s => s! with { EffectiveFromUtc = DateTime.UtcNow.AddMinutes(5) }, "MAPPING_MISSING"));
        Case(failures, "expired mapping", () => CatalogFailure(s => s! with { EffectiveToUtc = DateTime.UtcNow.AddMinutes(-1) }, "MAPPING_MISSING"));
        Case(failures, "mapping belongs to another Point", () => CatalogFailure(s => s! with { MappingPointId = Guid.NewGuid().ToString(), PointId = Guid.NewGuid().ToString() }, "MAPPING_POINT_MISMATCH"));
        Case(failures, "inactive Source", () => CatalogFailure(s => s! with { SourceStatus = "Inactive" }, "SOURCE_NOT_ACTIVE"));
        Case(failures, "non-Simulator Source", () => CatalogFailure(s => s! with { SourceType = "Modbus" }, "SOURCE_NOT_ACTIVE"));
        Case(failures, "catalog version drift", () => ProviderDrift(owner: false));
        Case(failures, "repeat activation", () => RepeatActivationNoOp());
        return failures;
    }

    private static void Case(List<string> failures, string name, Func<string?> test) { try { if (test() is { } error) failures.Add($"{name}: {error}"); } catch (Exception ex) { failures.Add($"{name}: {ex.Message}"); } }
    private static string? Success(PointStatus status, OrganizationCallerSnapshot caller, string action = "Activated")
    {
        var f = Build(status, caller); var result = Execute(f); var point = f.Repo.GetPointAsync(f.Point.Id).GetAwaiter().GetResult()!;
        if (!result.IsSuccess || result.Outcome != ActivationOutcome.Allowed || point.Status != PointStatus.Active) return $"expected success, got {result.ErrorCode}";
        if (f.Outbox.Count != 1 || f.Outbox.Enqueued[0].Action != action || f.Repo.GetLifecycleForPointAsync(point.Id.ToString()).GetAwaiter().GetResult().Count != 1) return "one transition, history row and outbox row required.";
        return null;
    }
    private static string? ScopedEngineer() { var f = Build(PointStatus.Draft, new("eng", "eng", true, new[] { "Engineer" }, Array.Empty<string>(), Array.Empty<string>())); f.Authorization = new FakeOrganizationAuthorization(f.Caller with { SiteScopes = new[] { f.Site.Id.ToString() } }); return SuccessWithFixture(f); }
    private static string? SuccessWithFixture(Fixture f) { var r = Execute(f, "eng"); return r.IsSuccess && f.Outbox.Count == 1 ? null : $"expected success, got {r.ErrorCode}"; }
    private static string? Denied(OrganizationCallerSnapshot caller, string code) { var f = Build(PointStatus.Draft, caller); var r = Execute(f, caller.UserId); return r.ErrorCode == code && Unchanged(f) ? null : $"expected {code}, got {r.ErrorCode}"; }
    private static string? MissingCaller() { var f = Build(PointStatus.Draft, AdminCaller()); f.Authorization = new FakeOrganizationAuthorization(null); var r = Execute(f); return r.ErrorCode == "FORBIDDEN" && Unchanged(f) ? null : "missing caller must fail closed."; }
    private static string? MissingPoint() { var f = Build(PointStatus.Draft, AdminCaller()); var r = ActivateMeasurementPoint.ExecuteAsync(PointId.New(), 1, Context(), f.Repo, f.Identity, f.Organization, f.Catalog, f.Authorization, f.Outbox, f.Host).GetAwaiter().GetResult(); return r.ErrorCode == "NOT_FOUND" ? null : "missing Point must be NOT_FOUND."; }
    private static string? NoOp() { var f = Build(PointStatus.Active, AdminCaller()); var r = Execute(f); return r.IsSuccess && r.Outcome == ActivationOutcome.NoOp && r.ErrorCode == "NO_OP" && Unchanged(f) ? null : "Active must be successful NO_OP without mutation."; }
    private static string? StateFailure(PointStatus state, string code) { var f = Build(state, AdminCaller()); var r = Execute(f); return r.ErrorCode == code && Unchanged(f) ? null : $"expected {code}, got {r.ErrorCode}"; }
    private static string? StaleVersion() { var f = Build(PointStatus.Draft, AdminCaller()); var r = ActivateMeasurementPoint.ExecuteAsync(f.Point.Id, 99, Context(), f.Repo, f.Identity, f.Organization, f.Catalog, f.Authorization, f.Outbox, f.Host).GetAwaiter().GetResult(); return r.ErrorCode == "VERSION_CONFLICT" && Unchanged(f) ? null : "stale version must not mutate."; }
    private static string? ParentFailure(SiteStatus site, AreaStatus area, AssetStatus asset) { var f = Build(PointStatus.Draft, AdminCaller(), site, area, asset); var r = Execute(f); return r.ErrorCode == "PARENT_NOT_ACTIVE" && Unchanged(f) ? null : $"expected parent failure, got {r.ErrorCode}"; }
    private static string? InconsistentAncestry() { var f = Build(PointStatus.Draft, AdminCaller()); var s = f.Organization.SnapshotOverride!; f.Organization.SnapshotOverride = s with { Asset = new Asset(s.Asset.Id, SiteId.New(), s.Asset.AreaId, "BAD", "bad", null, AssetStatus.Active, s.Asset.Version) }; var r = Execute(f); return r.ErrorCode == "INVALID_STATE" && Unchanged(f) ? null : "inconsistent ancestry must fail."; }
    private static string? InvalidIntervals() { var f = Build(PointStatus.Draft, AdminCaller()); f.Organization.SnapshotOverride = f.Organization.SnapshotOverride! with { IntervalValidOverride = false }; var r = Execute(f); return r.ErrorCode == "INTERVAL_INVALID" && Unchanged(f) ? null : "invalid intervals must fail without mutation."; }
    private static string? OwnerFailure(Func<ActivationDataOwnerSnapshot, ActivationDataOwnerSnapshot> change) { var f = Build(PointStatus.Draft, AdminCaller()); f.Identity.Snapshot = change(f.Identity.Snapshot); var r = Execute(f); return r.ErrorCode == "DATA_OWNER_INELIGIBLE" && Unchanged(f) ? null : $"owner failure got {r.ErrorCode}"; }
    private static string? CatalogFailure(Func<ActivationCatalogSnapshot?, ActivationCatalogSnapshot?> change, string code) { var f = Build(PointStatus.Draft, AdminCaller()); f.Catalog.Snapshot = change(f.Catalog.Snapshot); var r = Execute(f); return r.ErrorCode == code && Unchanged(f) ? null : $"expected {code}, got {r.ErrorCode}"; }
    private static string? CatalogConfiguredIdsPreserved() { var f = Build(PointStatus.Draft, AdminCaller()); f.Catalog.Snapshot = f.Catalog.Snapshot! with { MetricId = "configured-metric", UnitId = "configured-unit" }; var r = Execute(f); return r.ErrorCode == "METRIC_NOT_FOUND" && Unchanged(f) ? null : "fake Catalog must preserve configured MetricId/UnitId facts."; }
    private static string? ProviderDrift(bool owner) { var f = Build(PointStatus.Draft, AdminCaller()); if (owner) f.Identity.ChangeOnSecondRead = true; else f.Catalog.ChangeOnSecondRead = true; var r = Execute(f); return r.Outcome == ActivationOutcome.ProviderVersionConflict && Unchanged(f) ? null : "provider drift must rollback."; }
    private static string? RepeatActivationNoOp() { var f = Build(PointStatus.Draft, AdminCaller()); var first = Execute(f); var beforeVersion = f.Repo.GetPointAsync(f.Point.Id).GetAwaiter().GetResult()!.Version; var second = ActivateMeasurementPoint.ExecuteAsync(f.Point.Id, beforeVersion, Context(), f.Repo, f.Identity, f.Organization, f.Catalog, f.Authorization, f.Outbox, new HostTransactionCoordinator()).GetAwaiter().GetResult(); var history = f.Repo.GetLifecycleForPointAsync(f.Point.Id.ToString()).GetAwaiter().GetResult(); return first.IsSuccess && second.IsSuccess && second.Outcome == ActivationOutcome.NoOp && beforeVersion == 2 && history.Count == 1 && f.Outbox.Count == 1 ? null : $"repeat activation must be a single transition and event (first={first.ErrorCode}, second={second.ErrorCode}, status={f.Repo.GetPointAsync(f.Point.Id).GetAwaiter().GetResult()!.Status}, version={beforeVersion}, history={history.Count}, outbox={f.Outbox.Count})."; }
    private static ActivationResult Execute(Fixture f, string actor = "admin") => ActivateMeasurementPoint.ExecuteAsync(f.Point.Id, 1, Context(actor), f.Repo, f.Identity, f.Organization, f.Catalog, f.Authorization, f.Outbox, f.Host).GetAwaiter().GetResult();
    private static OrganizationCommandContext Context(string actor = "admin") => new(actor, "phase5-correlation", null);
    private static bool Unchanged(Fixture f) { var p = f.Repo.GetPointAsync(f.Point.Id).GetAwaiter().GetResult()!; return p.Status == f.Point.Status && p.Version == f.Point.Version && f.Outbox.Count == 0 && f.Repo.GetLifecycleForPointAsync(p.Id.ToString()).GetAwaiter().GetResult().Count == 0; }
    private static OrganizationCallerSnapshot AdminCaller() => new("admin", "admin@test", true, new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>());

    private sealed class Fixture
    {
        public required FakeOrganizationCommandRepository Repo { get; init; }
        public required Site Site { get; init; }
        public required MeasurementPoint Point { get; init; }
        public required FakeActivationIdentityQuery Identity { get; init; }
        public required FakeActivationOrganizationParticipant Organization { get; init; }
        public required FakeActivationCatalogQuery Catalog { get; init; }
        public required FakeTransactionalOutboxWriter Outbox { get; init; }
        public required HostTransactionCoordinator Host { get; init; }
        public required OrganizationCallerSnapshot Caller { get; init; }
        public required FakeOrganizationAuthorization Authorization { get; set; }
    }

    private static Fixture Build(PointStatus pointStatus, OrganizationCallerSnapshot caller, SiteStatus siteStatus = SiteStatus.Active, AreaStatus areaStatus = AreaStatus.Active, AssetStatus assetStatus = AssetStatus.Active)
    {
        var repo = new FakeOrganizationCommandRepository(); var site = new Site(SiteId.New(), "P5-SITE", "Phase 5", null, "UTC", siteStatus, 1); var area = new Area(AreaId.New(), site.Id, "P5-AREA", "Area", null, areaStatus, 1); var asset = new Asset(AssetId.New(), site.Id, area.Id, "P5-ASSET", "Asset", null, assetStatus, 1); var point = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "P5-POINT", "test", "metric-1", "unit-1", "owner-user", 60, 300, pointStatus, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult(); repo.AddAreaAsync(area).GetAwaiter().GetResult(); repo.AddAssetAsync(asset).GetAwaiter().GetResult(); repo.AddPointAsync(point).GetAwaiter().GetResult();
        var identity = new FakeActivationIdentityQuery { Snapshot = new ActivationDataOwnerSnapshot("owner-user", true, true, true, true, false, 1, 1, site.Id.ToString(), area.Id.ToString()) }; var organization = new FakeActivationOrganizationParticipant(repo); var catalog = new FakeActivationCatalogQuery { Snapshot = new ActivationCatalogSnapshot("metric-1", 1, "Active", "unit-1", 1, "Active", true, 1, "mapping-1", 1, "Active", "source-1", 1, "Active", "Simulator", DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1), 1, point.Id.ToString(), point.Id.ToString(), "metric-1|unit-1", "Active") }; var auth = new FakeOrganizationAuthorization(caller);
        organization.SnapshotOverride = new ActivationOrganizationSnapshot(point, site, area, asset);
        return new Fixture { Repo = repo, Site = site, Point = point, Identity = identity, Organization = organization, Catalog = catalog, Outbox = new FakeTransactionalOutboxWriter(), Host = new HostTransactionCoordinator(), Caller = caller, Authorization = auth };
    }
}
