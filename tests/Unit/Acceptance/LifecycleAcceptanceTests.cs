using IUMP.Modules.Catalog.Domain;
using IUMP.Modules.Organization.Domain;

namespace IUMP.Tests.Unit.Acceptance;

public static class LifecycleAcceptanceTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }
    public static int FailureCount { get; private set; }

    public static List<string> Run()
    {
        var failures = new List<string>();
        var assertions = 0;
        var site = new Site(SiteId.New(), "SITE", "Site", null, "UTC", SiteStatus.Active, 1);
        var area = new Area(AreaId.New(), site.Id, "AREA", "Area", null, AreaStatus.Active, 1);
        var asset = new Asset(AssetId.New(), site.Id, area.Id, "ASSET", "Asset", null, AssetStatus.Active, 1);
        var activePoint = new MeasurementPoint(PointId.New(), site.Id, area.Id, asset.Id, "POINT", null,
            "metric", "unit", "owner", 60, 300, PointStatus.Active, 1);

        var protectedDelete = LifecyclePolicy.DeleteDraft(hasMapping: true, hasPoint: false,
            hasSource: false, hasImmutableEvidence: false, auditOnly: false);
        Assert(protectedDelete.Code == "DEPENDENT_HISTORY" && !protectedDelete.Mutated,
            "dependency-protected delete must fail without cascade", failures, ref assertions);

        var activeChildDecision = DecommissionPolicy.EvaluateAsset(asset, [activePoint]);
        Assert(!activeChildDecision.IsAllowed && activeChildDecision.Code == "ACTIVE_CHILD_POINT" &&
               activePoint.Status == PointStatus.Active,
            "active Point dependency must block Asset decommission without child mutation", failures, ref assertions);

        Assert(site.TryInactivate() && site.Status == SiteStatus.Inactive &&
               activePoint.Status == PointStatus.Active,
            "deactivate must change only the selected aggregate", failures, ref assertions);

        var source = new DataSource(DataSourceId.New(), "SOURCE", "Source", SourceType.Simulator, SourceStatus.Active, 1);
        Assert(source.TryTransitionTo(SourceStatus.Suspended) && source.Status == SourceStatus.Suspended,
            "suspend semantics must preserve the Source while stopping production", failures, ref assertions);
        Assert(source.TryTransitionTo(SourceStatus.Decommissioned) &&
               !source.TryTransitionTo(SourceStatus.Active),
            "decommissioned Source must be terminal", failures, ref assertions);

        var mapping = new SourcePointMapping(MappingId.New(), DataSourceId.New(), "POINT",
            MappingStatus.Active, Utc(2026, 1, 1), null, 1);
        Assert(mapping.TryInactivate() && mapping.TrySupersede() && !mapping.TryActivate(),
            "superseded Mapping must not silently reactivate", failures, ref assertions);

        var pointDependency = DecommissionPolicy.EvaluatePoint(activePoint, hasRunningSimulator: true);
        Assert(!pointDependency.IsAllowed && pointDependency.Code == "RUNNING_SIMULATOR" &&
               activePoint.Status == PointStatus.Active,
            "active Source/Point production dependency must fail closed", failures, ref assertions);

        var auditOnly = LifecyclePolicy.DeleteDraft(false, false, false, false, auditOnly: true);
        Assert(auditOnly.Mutated && auditOnly.Code == "DELETED",
            "audit-only evidence must not block permitted Draft deletion", failures, ref assertions);

        var immutable = new EvidenceSnapshot(MeasurementCount: 3, AuditCount: 2);
        var before = immutable;
        LifecyclePolicy.DeactivateWithoutEvidenceMutation(immutable);
        Assert(immutable == before,
            "existing immutable measurements and Audit evidence must be retained", failures, ref assertions);

        var stale = LifecyclePolicy.ApplyExpectedVersion(currentVersion: 3, expectedVersion: 2);
        Assert(stale.Code == "VERSION_CONFLICT" && stale.Body == """{"errorCode":"VERSION_CONFLICT"}""",
            "stale expected version must return a safe conflict", failures, ref assertions);
        var missing = LifecyclePolicy.ApplyExpectedVersion(currentVersion: 3, expectedVersion: null);
        Assert(missing.Code == "EXPECTED_VERSION_REQUIRED" && !missing.Mutated,
            "lifecycle mutation must require ExpectedVersion", failures, ref assertions);

        var ledger = new ReplayLedger();
        var first = ledger.Execute("command-1", "fingerprint-a", () => new LifecycleResult(true, "DECOMMISSIONED", """{"status":"Decommissioned"}"""));
        var replay = ledger.Execute("command-1", "fingerprint-a", () => throw new InvalidOperationException("must not execute"));
        var conflict = ledger.Execute("command-1", "fingerprint-b", () => throw new InvalidOperationException("must not execute"));
        Assert(first == replay && ledger.MutationCount == 1,
            "exact idempotent replay must retain the original result and mutate once", failures, ref assertions);
        Assert(conflict.Code == "IDEMPOTENCY_CONFLICT" && !conflict.Mutated,
            "same command ID with different fingerprint must return safe conflict", failures, ref assertions);

        var unrelatedChildVersion = activePoint.Version;
        LifecyclePolicy.DeactivateWithoutEvidenceMutation(immutable);
        Assert(activePoint.Version == unrelatedChildVersion && activePoint.Status == PointStatus.Active,
            "lifecycle command must not mutate unrelated children", failures, ref assertions);

        TestCount = assertions;
        AssertionCount = assertions;
        FailureCount = failures.Count;
        return failures;
    }

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static void Assert(bool condition, string message, List<string> failures, ref int assertions)
    {
        assertions++;
        if (!condition) failures.Add($"T225-FAIL: {message}.");
    }

    private sealed record EvidenceSnapshot(int MeasurementCount, int AuditCount);
    private sealed record LifecycleResult(bool Mutated, string Code, string Body = "");

    private static class LifecyclePolicy
    {
        public static LifecycleResult DeleteDraft(bool hasMapping, bool hasPoint, bool hasSource,
            bool hasImmutableEvidence, bool auditOnly) =>
            hasMapping || hasPoint || hasSource || hasImmutableEvidence
                ? new(false, "DEPENDENT_HISTORY", """{"errorCode":"DEPENDENT_HISTORY"}""")
                : new(true, "DELETED");

        public static LifecycleResult ApplyExpectedVersion(long currentVersion, long? expectedVersion) =>
            expectedVersion is null
                ? new(false, "EXPECTED_VERSION_REQUIRED", """{"errorCode":"EXPECTED_VERSION_REQUIRED"}""")
                : expectedVersion != currentVersion
                    ? new(false, "VERSION_CONFLICT", """{"errorCode":"VERSION_CONFLICT"}""")
                    : new(true, "ACCEPTED");

        public static void DeactivateWithoutEvidenceMutation(EvidenceSnapshot evidence) { }
    }

    private sealed class ReplayLedger
    {
        private readonly Dictionary<string, (string Fingerprint, LifecycleResult Result)> _entries = [];
        public int MutationCount { get; private set; }

        public LifecycleResult Execute(string commandId, string fingerprint, Func<LifecycleResult> mutation)
        {
            if (_entries.TryGetValue(commandId, out var existing))
                return existing.Fingerprint == fingerprint
                    ? existing.Result
                    : new(false, "IDEMPOTENCY_CONFLICT", """{"errorCode":"IDEMPOTENCY_CONFLICT"}""");

            var result = mutation();
            MutationCount++;
            _entries.Add(commandId, (fingerprint, result));
            return result;
        }
    }
}
