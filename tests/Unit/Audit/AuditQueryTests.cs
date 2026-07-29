using IUMP.Modules.Audit.Application;
using IUMP.Modules.Audit.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Audit;

public static class AuditQueryTests
{
    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        var repository = new FakeAuditAppendRepository();
        var consumer = new AuditEventConsumer(repository);
        await consumer.ConsumeAsync(AuditEventEnvelope.Create(Guid.NewGuid(), "Point.Updated.v1", "Point", "1", "Update", "updated", DateTime.UtcNow, "corr"), CancellationToken.None);
        var service = new AuditQueryService(repository, new AuditAuthorization());
        var result = await service.QueryAsync(new AuditQueryRequest(null, null, null, null, null, 1, 20), AuditCaller.Administrator(), CancellationToken.None);
        if (result.Items.Count != 1) failures.Add("Administrator must receive global audit rows");
        var denied = await service.QueryAsync(new AuditQueryRequest(null, null, null, null, null, 1, 20), AuditCaller.Viewer(), CancellationToken.None);
        if (denied.Items.Count != 0 || denied.ErrorCode != "FORBIDDEN") failures.Add("unscoped viewer must be denied without leakage");
        return failures;
    }
}
