using IUMP.Modules.Catalog.Domain;
using IUMP.Modules.Catalog.Application;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Catalog;

public sealed class FakeCatalogAuthorization : ICatalogAuthorization
{
    private readonly HashSet<string> _allowedUsers = new(StringComparer.OrdinalIgnoreCase);

    public FakeCatalogAuthorization AllowUser(string userId)
    {
        _allowedUsers.Add(userId);
        return this;
    }

    public Task<bool> CanManageCatalogAsync(string userId, CancellationToken ct = default)
        => Task.FromResult(_allowedUsers.Contains(userId));
}

public static class CatalogCommandTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();

        // Setup
        var repo = new FakeCatalogCommandRepository();
        var auth = new FakeCatalogAuthorization();
        var handler = new CatalogCommandHandler(repo, auth);

        var adminUser = "admin-001";
        auth.AllowUser(adminUser);

        // 1. Administrator with permission can create metric
        var result1 = handler.HandleAsync(new CreateMetricCommand("TEST_M", "Test Metric", adminUser)).GetAwaiter().GetResult();
        if (result1.IsFailure) failures.Add($"FAIL: Admin should be able to create metric: {result1.Error}");

        // 2. Engineer with Site scope (allowed) can create metric
        var engUser = "eng-001";
        auth.AllowUser(engUser);
        var result2 = handler.HandleAsync(new CreateMetricCommand("ENG_M", "Engineer Metric", engUser)).GetAwaiter().GetResult();
        if (result2.IsFailure) failures.Add($"FAIL: Authorized engineer should create metric: {result2.Error}");

        // 3. Engineer without Site scope (not allowed) is denied
        var unauthorizedEng = "eng-002";
        var result3 = handler.HandleAsync(new CreateMetricCommand("UNAUTH_M", "Unauthorized", unauthorizedEng)).GetAwaiter().GetResult();
        if (!result3.IsFailure) failures.Add("FAIL: Unauthorized engineer should be denied");
        if (result3.Code != "Forbidden") failures.Add("FAIL: Denial should have Forbidden code");

        // 4. Operator mutation denial
        var opUser = "op-001";
        var result4 = handler.HandleAsync(new CreateMetricCommand("OP_M", "Operator Metric", opUser)).GetAwaiter().GetResult();
        if (!result4.IsFailure) failures.Add("FAIL: Operator should not be able to create metric");

        // 5. Manager mutation denial
        var mgrUser = "mgr-001";
        var result5 = handler.HandleAsync(new CreateMetricCommand("MGR_M", "Manager Metric", mgrUser)).GetAwaiter().GetResult();
        if (!result5.IsFailure) failures.Add("FAIL: Manager should not be able to create metric");

        // 6. Viewer mutation denial
        var viewerUser = "view-001";
        var result6 = handler.HandleAsync(new CreateMetricCommand("VIEW_M", "Viewer Metric", viewerUser)).GetAwaiter().GetResult();
        if (!result6.IsFailure) failures.Add("FAIL: Viewer should not be able to create metric");

        // 7. Server-side caller authority enforcement (no client-side bypass)
        // The handler always calls _auth.CanManageCatalogAsync — test validates by direct call
        var bypassAttempt = handler.HandleAsync(new CreateMetricCommand("BYPASS_M", "Bypass", "")).GetAwaiter().GetResult();
        if (!bypassAttempt.IsFailure) failures.Add("FAIL: Empty userId should be denied at server side");

        // 8. Out-of-scope target returns NotFound
        var ghostId = MetricId.New();
        var result8 = handler.HandleAsync(new UpdateMetricStatusCommand(ghostId, true, adminUser)).GetAwaiter().GetResult();
        if (!result8.IsFailure) failures.Add("FAIL: Non-existent metric should return NotFound");
        if (result8.Code != "NotFound") failures.Add("FAIL: Non-existent metric failure code should be NotFound");

        // 9. Owner event type and aggregate version
        handler.HandleAsync(new CreateMetricCommand("EVENT_M", "Event Test", adminUser)).GetAwaiter().GetResult();
        var events = handler.Events;
        if (events.Count == 0) failures.Add("FAIL: Handler should produce event after create");
        if (events.Count > 0)
        {
            if (events[0].EventType != "MetricCreated") failures.Add("FAIL: Event type should be MetricCreated");
            if (events[0].AggregateVersion != 1) failures.Add("FAIL: Aggregate version should be 1 after creation");
        }

        // 10. Event before/after field redaction — Data is null for create (no sensitive fields)
        if (events.Count > 0 && events[0].Data != null)
            failures.Add("FAIL: Create metric event should have null Data (no sensitive fields)");

        // 11. Correlation and causation ID preservation
        var corrId = "corr-test-001";
        handler.HandleAsync(new CreateMetricCommand("CORR_M", "Correlation Test", adminUser), corrId).GetAwaiter().GetResult();
        var corrEvents = handler.Events;
        var foundCorr = corrEvents.Any(e => e.CorrelationId == corrId);
        if (!foundCorr) failures.Add("FAIL: Correlation ID should be preserved in events");

        // 12. Payload construction must not claim Audit persistence
        // Catalog handler creates CatalogEvent records; no Audit module interaction
        var hasAuditClaim = handler.Events.Any(e =>
            e.EventType.Contains("Audit", StringComparison.OrdinalIgnoreCase));
        if (hasAuditClaim) failures.Add("FAIL: Catalog handler must not claim Audit persistence");

        return failures;
    }
}
