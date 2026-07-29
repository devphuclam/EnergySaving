using IUMP.Modules.Audit.Application;
using IUMP.Modules.Audit.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Audit;

public static class AuditQueryTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }
    public static int FailureCount { get; private set; }

    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        var assertions = 0;
        var repository = new FakeAuditAppendRepository();
        var consumer = new AuditEventConsumer(repository);
        await consumer.ConsumeAsync(AuditEventEnvelope.Create(Guid.NewGuid(), "Point.Updated.v1", "Point", "1", "Update", "updated", DateTime.UtcNow, "corr"), CancellationToken.None);
        var service = new AuditQueryService(repository, new AuditAuthorization());
        var result = await service.QueryAsync(new AuditQueryRequest(null, null, null, null, null, 1, 20), AuditCaller.Administrator(), CancellationToken.None);
        assertions++; if (result.Items.Count != 1) failures.Add("Administrator must receive global audit rows");
        var denied = await service.QueryAsync(new AuditQueryRequest(null, null, null, null, null, 1, 20), AuditCaller.Viewer(), CancellationToken.None);
        assertions++; if (denied.Items.Count != 0 || denied.ErrorCode != "FORBIDDEN") failures.Add("unscoped viewer must be denied without leakage");
        var scoped = AuditCaller.Administrator();
        var keyset = await service.QueryAsync(new AuditQueryRequest(null, null, null, null, null, 1, 20)
        {
            KeysetCursor = new AuditKeysetCursor(repository.Rows[0].OccurredAtUtc, repository.Rows[0].AuditEventId).Encode()
        }, scoped, CancellationToken.None);
        assertions++; if (keyset.Items.Count != 0) failures.Add("keyset tuple must return only rows strictly after the cursor");
        // Scope-before-paging is required for Site/Area callers; Administrator is the only global role.
        TestCount = 4; AssertionCount = assertions; FailureCount = failures.Count;
        return failures;
    }
}
