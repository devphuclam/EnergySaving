using IUMP.Modules.Audit.Application;
using IUMP.Modules.Audit.Contracts;
using IUMP.Modules.Integration.Contracts;
using IUMP.Tests.Unit.Fakes;
using IUMP.Worker.Integration;

namespace IUMP.Tests.Unit.Audit;

public static class AuditConsumerTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }
    public static int FailureCount { get; private set; }

    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();
        var assertions = 0;
        var cases = 0;
        var repository = new FakeAuditAppendRepository();
        var consumer = new AuditEventConsumer(repository);
        var envelope = AuditEventEnvelope.Create(Guid.NewGuid(), "Site.Created.v1", "Site", "1", "Create", "created", DateTime.UtcNow, "corr") with
        {
            ActorId = "actor-1", ActorUsername = "admin", SiteId = "site-1", AreaId = "area-1", CausationId = "cause-1",
            Before = new Dictionary<string, object?> { ["password"] = "secret" },
            After = new Dictionary<string, object?> { ["status"] = "Draft" }
        };
        await consumer.ConsumeAsync(envelope, CancellationToken.None);
        await consumer.ConsumeAsync(envelope, CancellationToken.None);
        cases++;
        assertions++; if (repository.Rows.Count != 1) failures.Add("source event append must be idempotent");
        assertions++; if (repository.Rows[0].Summary.Contains("password", StringComparison.OrdinalIgnoreCase) || repository.Rows[0].Before["password"]?.ToString() != "[REDACTED]") failures.Add("audit output must redact secrets");
        assertions++; if (repository.Rows[0].SchemaVersion != 1 || string.IsNullOrWhiteSpace(repository.Rows[0].PayloadHash))
            failures.Add("audit record must retain schema version and payload hash");
        var malformed = envelope with { EventType = "Point.Updated" };
        try { await consumer.ConsumeAsync(malformed, CancellationToken.None); failures.Add("invalid schema must fail"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("SCHEMA", StringComparison.Ordinal)) { }
        cases++;
        var conflict = envelope with { Summary = "changed" };
        try { await consumer.ConsumeAsync(conflict, CancellationToken.None); failures.Add("different payload hash must conflict"); }
        catch (InvalidOperationException ex) when (ex.Message == "AUDIT_SOURCE_HASH_CONFLICT") { assertions++; }
        cases++;
        // Hash conflict and redaction are fail-closed; the source identity remains immutable.
        var delivery = new FakeIntegrationDeliveryRepositories();
        var txFactory = new FakePhase9TransactionFactory();
        var eventId = Guid.NewGuid();
        var outbox = new OutboxDeliveryRecord(eventId, "Audit.v1", 1, envelope.PayloadHash, DateTime.UtcNow);
        await delivery.AddOutboxAsync(outbox);
        var handler = new AuditDeliveryHandler(delivery, consumer,
            row => envelope with { SourceEventId = row.EventId }, txFactory);
        var handled = await handler.HandleAsync(outbox, CancellationToken.None);
        var inbox = await delivery.GetInboxAsync("Audit.v1", eventId);
        assertions++; if (!handled || repository.Rows.Count != 2) failures.Add("audit handler must append through the real consumer");
        assertions++; if (inbox?.Status != DeliveryStatus.Completed || txFactory.Last.CommitCount != 1 || txFactory.Last.RollbackCount != 0)
            failures.Add("audit append and inbox completion must commit exactly once in one transaction");
        cases++;

        // B. A failure after Audit staging must roll back the append and retain a retryable inbox row.
        var failedAudit = new FakeAuditAppendRepository();
        var failedDelivery = new FakeIntegrationDeliveryRepositories { FailTransactionalCompletion = true };
        var failedFactory = new FakePhase9TransactionFactory();
        var failedOutbox = new OutboxDeliveryRecord(Guid.NewGuid(), "Audit.v1", 1, envelope.PayloadHash, DateTime.UtcNow);
        var failedHandler = new AuditDeliveryHandler(failedDelivery, new AuditEventConsumer(failedAudit),
            row => envelope with { SourceEventId = row.EventId }, failedFactory);
        var failedHandled = await failedHandler.HandleAsync(failedOutbox, CancellationToken.None);
        var failedInbox = await failedDelivery.GetInboxAsync("Audit.v1", failedOutbox.EventId);
        assertions++; if (failedHandled || failedAudit.Rows.Count != 0 || failedFactory.Last.CommitCount != 0 ||
            failedFactory.Last.RollbackCount != 1 || failedInbox?.Status != DeliveryStatus.Pending)
            failures.Add("failure after Audit staging must roll back Audit and retain a retryable inbox row");
        cases++;

        // C. A host commit failure must publish neither participant and must retain retry state.
        var commitAudit = new FakeAuditAppendRepository();
        var commitDelivery = new FakeIntegrationDeliveryRepositories();
        var commitFactory = new FakePhase9TransactionFactory(failOnCommit: true);
        var commitOutbox = new OutboxDeliveryRecord(Guid.NewGuid(), "Audit.v1", 1, envelope.PayloadHash, DateTime.UtcNow);
        var commitHandler = new AuditDeliveryHandler(commitDelivery, new AuditEventConsumer(commitAudit),
            row => envelope with { SourceEventId = row.EventId }, commitFactory);
        var commitHandled = await commitHandler.HandleAsync(commitOutbox, CancellationToken.None);
        var commitInbox = await commitDelivery.GetInboxAsync("Audit.v1", commitOutbox.EventId);
        assertions++; if (commitHandled || commitAudit.Rows.Count != 0 || commitDelivery.InboxCompletedCount != 0 ||
            commitFactory.Last.CommitCount != 0 || commitFactory.Last.RollbackCount != 1 ||
            commitInbox?.Status != DeliveryStatus.Pending)
            failures.Add("commit failure must publish no Audit/inbox completion and retain retry state");
        cases++;

        TestCount = cases; AssertionCount = assertions;
        FailureCount = failures.Count;
        return failures;
    }
}
