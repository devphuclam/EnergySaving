using IUMP.Api.Infrastructure;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Audit.Application;
using IUMP.Modules.Audit.Contracts;
using IUMP.Tests.Unit.Fakes;

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
        await AuditValidationAndKeysetPagingAreStrict(failures);
        OperationalRunListingPreservesPausedState(failures);
        DashboardNeverLeaksCountsWithoutScope(failures);
        DashboardSummaryContractIsPublicAndScoped(failures);
        return failures;
    }

    private static void OperationalRunListingPreservesPausedState(ICollection<string> failures)
    {
        TestCount++;
        var repository = new FakeAcquisitionRunRepositories();
        var now = DateTime.UtcNow;
        var source = Guid.NewGuid();
        SimulatorRun Run(Guid id, SimulatorRunStatus status, Guid sourceId) => new(
            id, sourceId, 1, Guid.NewGuid(), 1, "Constant", 1, status, 1, 0, 0, 0,
            null, null, now, now, status == SimulatorRunStatus.Paused ? now : null,
            null, status == SimulatorRunStatus.Stopped ? now : null,
            "actor", "operator", "corr", null);
        repository.Seed(Run(Guid.NewGuid(), SimulatorRunStatus.Running, source));
        repository.Seed(Run(Guid.NewGuid(), SimulatorRunStatus.Paused, source));
        repository.Seed(Run(Guid.NewGuid(), SimulatorRunStatus.Stopped, source));
        var operational = repository.ListOperationalAsync().GetAwaiter().GetResult();
        Check(operational.Count == 2 && operational.Any(item => item.Status == SimulatorRunStatus.Paused) &&
              operational.All(item => item.Status is SimulatorRunStatus.Running or SimulatorRunStatus.Paused),
            "T065 corrective: Dashboard operational run listing must include Paused and exclude Stopped.", failures);
        var otherSource = repository.ListOperationalForSourcesAsync(new[] { Guid.NewGuid() })
            .GetAwaiter().GetResult();
        Check(otherSource.Count == 0,
            "T065 corrective: scoped operational run listing must not leak another Source.", failures);
    }

    private static async Task AuditValidationAndKeysetPagingAreStrict(
        ICollection<string> failures)
    {
        TestCount++;
        var row = new AuditEventRecord(
            Guid.NewGuid(), Guid.NewGuid(), "Configuration.v1", "Source", "one",
            "Update", "one", DateTime.UtcNow.AddMinutes(-3), DateTime.UtcNow,
            null, null, null, new Dictionary<string, object?>(),
            new Dictionary<string, object?>(), "site-1", null, null);
        var repository = new CountingAuditRepository(row);
        var service = new AuditQueryService(repository, new AuditAuthorization());
        var caller = new AuditCaller(false, true, new HashSet<string> { "site-1" }, new HashSet<string>());

        foreach (var pageSize in new[] { 0, -1, 101 })
        {
            var invalid = await service.QueryAsync(
                new AuditQueryRequest(null, null, null, null, null, 1, pageSize), caller);
            Check(invalid.ErrorCode == "VALIDATION" && invalid.Items.Count == 0,
                $"T065 corrective: pageSize={pageSize} must be rejected as VALIDATION.", failures);
        }
        var malformed = await service.QueryAsync(
            new AuditQueryRequest(null, null, null, null, null, 1, 25)
            { KeysetCursor = "malformed-cursor" }, caller);
        Check(malformed.ErrorCode == "VALIDATION" && malformed.Items.Count == 0,
            "T065 corrective: malformed cursor must be rejected as VALIDATION.", failures);
        Check(repository.QueryCount == 0,
            "T065 corrective: invalid Audit requests must not query the repository.", failures);
        var outOfRangeCursor = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            $"{long.MaxValue}:{Guid.NewGuid():D}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var outOfRange = await service.QueryAsync(
            new AuditQueryRequest(null, null, null, null, null, 1, 25)
            { KeysetCursor = outOfRangeCursor }, caller);
        Check(outOfRange.ErrorCode == "VALIDATION" && outOfRange.Items.Count == 0,
            "T065 corrective: an out-of-range decoded cursor must be rejected as VALIDATION.", failures);

        var ordered = new[]
        {
            row with { AuditEventId = Guid.Parse("00000000-0000-0000-0000-000000000003"), OccurredAtUtc = DateTime.UtcNow.AddMinutes(-1), Summary = "newest" },
            row with { AuditEventId = Guid.Parse("00000000-0000-0000-0000-000000000002"), OccurredAtUtc = DateTime.UtcNow.AddMinutes(-2), Summary = "middle" },
            row with { AuditEventId = Guid.Parse("00000000-0000-0000-0000-000000000001"), OccurredAtUtc = DateTime.UtcNow.AddMinutes(-3), Summary = "oldest" }
        };
        repository = new CountingAuditRepository(ordered);
        service = new AuditQueryService(repository, new AuditAuthorization());
        var first = await service.QueryAsync(
            new AuditQueryRequest(null, null, null, null, null, 1, 2), caller);
        Check(first.Items.Count == 2 && first.NextCursor is not null,
            "T065 corrective: pageSize+1 rows must produce a continuation cursor.", failures);
        var last = await service.QueryAsync(
            new AuditQueryRequest(null, null, null, null, null, 1, 5), caller);
        Check(last.Items.Count == 3 && last.NextCursor is null,
            "T065 corrective: a last page must not fabricate a continuation cursor.", failures);
        var continuation = await service.QueryAsync(
            new AuditQueryRequest(null, null, null, null, null, 1, 2)
            { KeysetCursor = first.NextCursor }, caller);
        Check(continuation.Items.Count == 1 && continuation.Items[0].Summary == "oldest" &&
              continuation.NextCursor is null,
            "T065 corrective: keyset continuation must not duplicate or skip the final row.", failures);
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

    private sealed class CountingAuditRepository : IAuditQueryRepository
    {
        private readonly IReadOnlyList<AuditEventRecord> _rows;
        public CountingAuditRepository(params AuditEventRecord[] rows) => _rows = rows;
        public int QueryCount { get; private set; }
        public Task<IReadOnlyList<AuditEventRecord>> QueryAsync(
            AuditQueryRequest request, CancellationToken ct = default)
        {
            QueryCount++;
            return Task.FromResult(_rows);
        }
    }
}
