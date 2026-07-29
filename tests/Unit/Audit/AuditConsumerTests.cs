using IUMP.Modules.Audit.Application;
using IUMP.Modules.Audit.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Audit;

public static class AuditConsumerTests
{
    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        var repository = new FakeAuditAppendRepository();
        var consumer = new AuditEventConsumer(repository);
        var envelope = AuditEventEnvelope.Create(Guid.NewGuid(), "Site.Created.v1", "Site", "1", "Create", "created", DateTime.UtcNow, "corr");
        await consumer.ConsumeAsync(envelope, CancellationToken.None);
        await consumer.ConsumeAsync(envelope, CancellationToken.None);
        if (repository.Rows.Count != 1) failures.Add("source event append must be idempotent");
        if (repository.Rows[0].Summary.Contains("password", StringComparison.OrdinalIgnoreCase)) failures.Add("audit output must redact secrets");
        return failures;
    }
}
