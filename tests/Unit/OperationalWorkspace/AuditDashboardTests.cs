using IUMP.Api.Infrastructure;
using IUMP.Modules.Audit.Application;
using IUMP.Modules.Audit.Contracts;

namespace IUMP.Tests.Unit.OperationalWorkspace;

/// <summary>Phase 5 RED seam: these assertions describe the new Audit/Dashboard read contract.</summary>
public static class AuditDashboardTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }

    public static async Task<IReadOnlyList<string>> Run()
    {
        TestCount = 0;
        AssertionCount = 0;
        var failures = new List<string>();
        await AuditResultsAreRedactedAndCorrelationIsPermissionGated(failures);
        DashboardNeverLeaksCountsWithoutScope(failures);
        DashboardSummaryContractIsPublicAndScoped(failures);
        return failures;
    }

    private static async Task AuditResultsAreRedactedAndCorrelationIsPermissionGated(
        ICollection<string> failures)
    {
        TestCount++;
        var row = new AuditEventRecord(
            Guid.NewGuid(), Guid.NewGuid(), "Configuration.v1", "Source", "source-1",
            "Update", "Updated Source", DateTime.UtcNow, DateTime.UtcNow, "corr-sensitive",
            "actor-1", "engineer", new Dictionary<string, object?>
            {
                ["password"] = "must-not-leave-server",
                ["metadata"] = System.Text.Json.JsonDocument.Parse(
                    "{\"password\":\"nested-secret\",\"displayName\":\"safe\"}").RootElement.Clone(),
                ["displayName"] = "safe"
            }, new Dictionary<string, object?>
            {
                ["token"] = "must-not-leave-server",
                ["displayName"] = "safe-after"
            }, "site-1", "area-1", null);
        var repository = new SingleAuditRepository(row);
        var service = new AuditQueryService(repository, new AuditAuthorization());
        var toUtc = DateTime.UtcNow.AddMinutes(1);
        var result = await service.QueryAsync(
            new AuditQueryRequest("Source", "Update", "actor-1", null,
                DateTime.UtcNow.AddMinutes(-1), 1, 25)
            {
                ToUtc = toUtc, EntityId = "source-1", SiteId = "site-1", AreaId = "area-1"
            },
            new AuditCaller(false, true, new HashSet<string> { "site-1" },
                new HashSet<string> { "area-1" }));

        Check(result.Items.Count == 1, "T065: an authorized scoped Audit row must be visible.", failures);
        var visible = result.Items.SingleOrDefault();
        Check(visible is not null && visible.CorrelationId is null,
            "T065: correlation IDs require the explicit Administrator-only permission.", failures);
        Check(visible is not null && !ContainsSensitive(visible.Before) &&
              !ContainsSensitive(visible.After),
            "T065: Audit before/after values must be redacted before the browser contract.", failures);
        Check(repository.LastRequest is { } captured && captured.ToUtc == toUtc &&
              captured.EntityId == "source-1" && captured.SiteId == "site-1" &&
              captured.AreaId == "area-1" && captured.ObjectType == "Source" &&
              captured.Action == "Update",
            "T065: Audit time, entity, scope, object type, and action filters must reach the owner query.", failures);

        var administratorResult = await service.QueryAsync(
            new AuditQueryRequest(null, null, null, null, null, 1, 25),
            AuditCaller.Administrator());
        Check(administratorResult.Items.SingleOrDefault()?.CorrelationId == "corr-sensitive",
            "T065: Administrator audit permission must allow correlation review.", failures);
        var outOfScope = await service.QueryAsync(
            new AuditQueryRequest(null, null, null, null, null, 1, 25) { SiteId = "other-site" },
            new AuditCaller(false, true, new HashSet<string> { "site-1" }, new HashSet<string>()));
        Check(outOfScope.Items.Count == 0,
            "T065: an out-of-scope Site filter must not fall back to the caller's other scope.", failures);
        await service.QueryAsync(
            new AuditQueryRequest(null, null, null, "corr-sensitive", null, 1, 25),
            new AuditCaller(false, true, new HashSet<string> { "site-1" }, new HashSet<string>()));
        Check(repository.LastRequest?.CorrelationId is null,
            "T065: non-Administrator correlation filters must not be forwarded to the owner query.", failures);
    }

    private static void DashboardNeverLeaksCountsWithoutScope(ICollection<string> failures)
    {
        TestCount++;
        var status = OperationalWorkspaceStatusBuilder.Build(
            false, false, true, 8, 3, true, Array.Empty<WorkspaceSiteSummary>());
        Check(status.Landing == WorkspaceLanding.NoAuthorizedScope,
            "T065: a caller without scope must receive NoAuthorizedScope.", failures);
        Check(status.OperationalChainCount == 0 && status.IncompleteChainCount == 0,
              "T065: a caller without scope must not receive global chain counts.", failures);
    }

    private static void DashboardSummaryContractIsPublicAndScoped(ICollection<string> failures)
    {
        TestCount++;
        var snapshot = new OperationalDashboardSnapshot(
            OperationalDashboardState.Ready,
            WorkspaceRoleMode.Engineer,
            new(1, [new { SiteId = "site-1", Code = "SITE-1" }]),
            new(1, [new { SourceId = "source-1", Code = "SRC-1" }]),
            new(1, [new { PointId = "point-1", Code = "POINT-1" }]),
            new(0, []), new(0, []), new(0, []), new(0, null), new([], null),
            new("Available", false), new("Available", null, null));
        Check(snapshot.State == OperationalDashboardState.Ready &&
              snapshot.Sites.Count == snapshot.Sites.Items.Count &&
              snapshot.Sources.Count == snapshot.Sources.Items.Count &&
              snapshot.Points.Count == snapshot.Points.Items.Count,
            "T065: authorized Dashboard summaries must expose scoped public items with matching counts.", failures);
    }

    private static bool ContainsSensitive(IReadOnlyDictionary<string, object?> values) =>
        values.Keys.Any(key => key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("credential", StringComparison.OrdinalIgnoreCase)) ||
        values.Values.Any(value => value is System.Text.Json.JsonElement element &&
            element.ToString().Contains("password", StringComparison.OrdinalIgnoreCase));

    private static void Check(bool condition, string failure, ICollection<string> failures)
    {
        AssertionCount++;
        if (!condition) failures.Add(failure);
    }

    private sealed class SingleAuditRepository(AuditEventRecord row) : IAuditQueryRepository
    {
        public AuditQueryRequest? LastRequest { get; private set; }

        public Task<IReadOnlyList<AuditEventRecord>> QueryAsync(
            AuditQueryRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.FromResult<IReadOnlyList<AuditEventRecord>>([row]);
        }
    }
}
