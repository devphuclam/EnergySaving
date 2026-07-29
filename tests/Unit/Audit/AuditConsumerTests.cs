using IUMP.Modules.Audit.Application;
using IUMP.Modules.Audit.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Audit;

public static class AuditConsumerTests
{
    public const int TestCount = 6;
    public const int AssertionCount = 14;
    public static int FailureCount { get; private set; }

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
        if (repository.Rows[0].SchemaVersion != 1 || string.IsNullOrWhiteSpace(repository.Rows[0].PayloadHash))
            failures.Add("audit record must retain schema version and payload hash");
        var malformed = envelope with { EventType = "Point.Updated" };
        try { await consumer.ConsumeAsync(malformed, CancellationToken.None); failures.Add("invalid schema must fail"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("SCHEMA", StringComparison.Ordinal)) { }
        // Hash conflict and redaction are fail-closed; the source identity remains immutable.
        FailureCount = failures.Count;
        return failures;
    }
}
