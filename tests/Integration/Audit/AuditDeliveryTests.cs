using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Audit.Contracts;
using IUMP.Modules.Integration.Contracts;
using IUMP.Worker.Integration;
using Microsoft.Extensions.DependencyInjection;

namespace IUMP.Tests.Integration.Audit;

public static class AuditDeliveryTests
{
    public static async Task<IReadOnlyList<string>> RunAsync(IServiceProvider services)
    {
        var failures = new List<string>();
        var consumer = services.GetRequiredService<IAuditEventConsumer>();
        var transactional =
            services.GetRequiredService<ITransactionalAuditEventConsumer>();
        var query = services.GetRequiredService<IAuditQueryRepository>();
        var transactions = services.GetRequiredService<IHostTransactionFactory>();
        var inbox = services.GetRequiredService<IIntegrationDeliveryRepository>();
        var inboxState = services.GetRequiredService<IInboxStateRepository>();

        var sourceEventId = Guid.NewGuid();
        var correlation = $"audit-{Guid.NewGuid():N}";
        var envelope = AuditEventEnvelope.Create(
            sourceEventId, "AuditDeliveryVerified.v1", "RuntimeProbe",
            sourceEventId.ToString("D"), "Verified",
            "Audit delivery integration verified.", DateTime.UtcNow,
            correlation);
        var started = DateTime.UtcNow;
        var first = await consumer.ConsumeAsync(envelope);
        var replay = await consumer.ConsumeAsync(envelope);
        var rows = await query.QueryAsync(
            new AuditQueryRequest(
                "RuntimeProbe", null, null, correlation, null, 1, 20));
        Check(first.AuditEventId == replay.AuditEventId,
            "T220 source-event replay must deduplicate to the original Audit row",
            failures);
        Check(rows.Any(value => value.SourceEventId == sourceEventId) &&
            DateTime.UtcNow - started <= TimeSpan.FromSeconds(5),
            "T220 Audit evidence must be query-visible within five seconds",
            failures);

        var conflicting = envelope with { Summary = "Different payload." };
        var conflictObserved = false;
        try
        {
            _ = await consumer.ConsumeAsync(conflicting);
        }
        catch (InvalidOperationException exception)
        {
            conflictObserved = exception.Message.Contains(
                "CONFLICT", StringComparison.OrdinalIgnoreCase);
        }
        Check(conflictObserved,
            "T220 same source event with a different hash must conflict", failures);

        var rollbackEvent = Guid.NewGuid();
        var rollbackCorrelation = $"audit-rollback-{Guid.NewGuid():N}";
        await using (var transaction = await transactions.BeginAsync())
        {
            _ = await transactional.ConsumeAsync(
                AuditEventEnvelope.Create(
                    rollbackEvent, "AuditRollbackVerified.v1", "RuntimeProbe",
                    rollbackEvent.ToString("D"), "Verified",
                    "Audit rollback verification.", DateTime.UtcNow,
                    rollbackCorrelation),
                transaction);
            await ((IHostTransactionController)transaction).RollbackAsync();
        }
        var rollbackRows = await query.QueryAsync(
            new AuditQueryRequest(
                "RuntimeProbe", null, null, rollbackCorrelation, null, 1, 20));
        Check(rollbackRows.Count == 0,
            "T220 transactional Audit append must roll back atomically", failures);

        var inboxEvent = Guid.NewGuid();
        var claimed = await inbox.ClaimAsync(
            "audit-integration", inboxEvent, "hash-a", DateTime.UtcNow,
            "worker-a", TimeSpan.FromSeconds(30));
        Check(claimed is not null,
            "T220 inbox delivery must be claimable", failures);
        var duplicateWhileLive = await inbox.ClaimAsync(
            "audit-integration", inboxEvent, "hash-a", DateTime.UtcNow,
            "worker-b", TimeSpan.FromSeconds(30));
        Check(duplicateWhileLive is null,
            "T220 live inbox lease must prevent duplicate processing", failures);
        var inboxConflict = false;
        try
        {
            _ = await inbox.ClaimAsync(
                "audit-integration", inboxEvent, "hash-b", DateTime.UtcNow,
                "worker-c", TimeSpan.FromSeconds(30));
        }
        catch (InvalidOperationException exception)
        {
            inboxConflict = exception.Message.Contains(
                "HASH_CONFLICT", StringComparison.OrdinalIgnoreCase);
        }
        Check(inboxConflict,
            "T220 inbox payload hash conflict must fail closed", failures);
        await inbox.MarkFailedAsync(
            claimed!, "redacted-test-error", DateTime.UtcNow);
        var failed = await inboxState.GetInboxAsync(
            "audit-integration", inboxEvent);
        Check(failed?.Status == DeliveryStatus.Failed &&
            failed.Error == "redacted-test-error",
            "T220 poison inbox delivery must persist Failed with safe error",
            failures);

        await ExecuteDispatcherChainAsync(services, failures);
        return failures;
    }

    private static async Task ExecuteDispatcherChainAsync(
        IServiceProvider services,
        List<string> failures)
    {
        var delivery =
            services.GetRequiredService<IIntegrationDeliveryRepository>();
        var inboxState =
            services.GetRequiredService<IInboxStateRepository>();
        var transactions =
            services.GetRequiredService<IHostTransactionFactory>();
        var outbox =
            services.GetRequiredService<ITransactionalOutboxWriter>();
        var transactionalAudit =
            services.GetRequiredService<ITransactionalAuditEventConsumer>();
        var query = services.GetRequiredService<IAuditQueryRepository>();
        var chainAt =
            new DateTime(2000, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        var eventId = Guid.NewGuid();
        var correlation = $"delivery-chain-{Guid.NewGuid():N}";
        await using (var transaction = await transactions.BeginAsync())
        {
            await outbox.EnqueueAsync(
                OwnerEnvelope(eventId, correlation), transaction);
            await ((IHostTransactionController)transaction).CommitAsync();
        }
        var registry = new RequiredConsumerRegistry();
        registry.RegisterTransactional(
            "AuditDeliveryChain.v1", "Audit.v1",
            async (record, transaction, ct) =>
            {
                await transactionalAudit.ConsumeAsync(
                    AuditEnvelope(record, correlation), transaction, ct);
                return true;
            });
        var dispatcher = new OutboxDispatcherWorker(
            delivery, registry, transactions);
        var dispatched = await dispatcher.DispatchOnceAsync(chainAt);
        var deliveredInbox = await inboxState.GetInboxAsync(
            "Audit.v1", eventId);
        var deliveredAudit = await query.QueryAsync(
            new AuditQueryRequest(
                "RuntimeProbe", null, null, correlation, null, 1, 20));
        Check(dispatched == 1 &&
            (await delivery.GetAsync(eventId))?.Status ==
                DeliveryStatus.Published &&
            deliveredInbox?.Status == DeliveryStatus.Completed &&
            deliveredAudit.Count(value =>
                value.SourceEventId == eventId) == 1,
            "T220 owner outbox must dispatch to atomic Audit append plus inbox completion",
            failures);

        var crashEvent = Guid.NewGuid();
        var crashCorrelation = $"delivery-crash-{Guid.NewGuid():N}";
        await using (var transaction = await transactions.BeginAsync())
        {
            await outbox.EnqueueAsync(
                OwnerEnvelope(crashEvent, crashCorrelation), transaction);
            await ((IHostTransactionController)transaction).CommitAsync();
        }
        var crashRegistry = new RequiredConsumerRegistry();
        crashRegistry.RegisterTransactional(
            "AuditDeliveryChain.v1", "Audit.v1",
            async (record, transaction, ct) =>
            {
                await transactionalAudit.ConsumeAsync(
                    AuditEnvelope(record, crashCorrelation),
                    transaction, ct);
                throw new InvalidOperationException(
                    "SIMULATED_CRASH_AFTER_AUDIT_APPEND");
            });
        var crashDispatcher = new OutboxDispatcherWorker(
            delivery, crashRegistry, transactions);
        var crashAt = chainAt.AddMinutes(1);
        _ = await crashDispatcher.DispatchOnceAsync(crashAt);
        var afterCrashAudit = await query.QueryAsync(
            new AuditQueryRequest(
                "RuntimeProbe", null, null, crashCorrelation,
                null, 1, 20));
        var afterCrashInbox = await inboxState.GetInboxAsync(
            "Audit.v1", crashEvent);
        Check(afterCrashAudit.Count == 0 &&
            afterCrashInbox?.Status == DeliveryStatus.Claimed,
            "T220 crash between Audit append and inbox completion must roll back Audit and leave a reclaimable inbox",
            failures);

        var recoveryRegistry = new RequiredConsumerRegistry();
        recoveryRegistry.RegisterTransactional(
            "AuditDeliveryChain.v1", "Audit.v1",
            async (record, transaction, ct) =>
            {
                await transactionalAudit.ConsumeAsync(
                    AuditEnvelope(record, crashCorrelation),
                    transaction, ct);
                return true;
            });
        var recoveryDispatcher = new OutboxDispatcherWorker(
            delivery, recoveryRegistry, transactions);
        _ = await recoveryDispatcher.DispatchOnceAsync(
            crashAt.AddSeconds(31));
        var recoveredAudit = await query.QueryAsync(
            new AuditQueryRequest(
                "RuntimeProbe", null, null, crashCorrelation,
                null, 1, 20));
        Check((await delivery.GetAsync(crashEvent))?.Status ==
                DeliveryStatus.Published &&
            (await inboxState.GetInboxAsync("Audit.v1", crashEvent))?.Status ==
                DeliveryStatus.Completed &&
            recoveredAudit.Count(value =>
                value.SourceEventId == crashEvent) == 1,
            "T220 restart must reclaim the incomplete consumer and append Audit exactly once",
            failures);

        var poisonEvent = Guid.NewGuid();
        await using (var transaction = await transactions.BeginAsync())
        {
            await outbox.EnqueueAsync(
                OwnerEnvelope(
                    poisonEvent,
                    $"delivery-poison-{Guid.NewGuid():N}"),
                transaction);
            await ((IHostTransactionController)transaction).CommitAsync();
        }
        var poisonRegistry = new RequiredConsumerRegistry();
        poisonRegistry.RegisterTransactional(
            "AuditDeliveryChain.v1", "Audit.v1",
            (_, _, _) => throw new InvalidOperationException(
                "SAFE_POISON_TEST"));
        var poisonDispatcher = new OutboxDispatcherWorker(
            delivery, poisonRegistry, transactions);
        var poisonAt = chainAt.AddMinutes(2);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            _ = await poisonDispatcher.DispatchOnceAsync(poisonAt);
            poisonAt = poisonAt.AddSeconds(31);
        }
        Check((await delivery.GetAsync(poisonEvent))?.Status ==
                DeliveryStatus.Failed &&
            (await delivery.GetAsync(poisonEvent))?.Error ==
                "DELIVERY_EXHAUSTED",
            "T220 retry exhaustion must persist a safe Failed terminal state",
            failures);
    }

    private static OwnerEventEnvelope OwnerEnvelope(
        Guid eventId,
        string correlation) =>
        new(eventId, "AuditDeliveryChain.v1", 1, "IUMP.Tests",
            "RuntimeProbe", eventId.ToString("D"), 1,
            Guid.NewGuid().ToString("D"), "integration-test",
            new Dictionary<string, object?>(),
            new Dictionary<string, object?>
            {
                ["status"] = "verified"
            },
            "Verified", "Audit delivery chain verified.",
            new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            correlation, null, null, null);

    private static AuditEventEnvelope AuditEnvelope(
        OutboxDeliveryRecord record,
        string correlation) =>
        AuditEventEnvelope.Create(
            record.EventId, record.EventType, "RuntimeProbe",
            record.EventId.ToString("D"), "Verified",
            "Audit delivery chain verified.", DateTime.UtcNow,
            correlation);

    private static void Check(bool condition, string message, List<string> failures)
    {
        if (!condition) failures.Add(message);
    }
}
