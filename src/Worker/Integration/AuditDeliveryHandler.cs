using IUMP.Modules.Audit.Contracts;
using IUMP.Modules.Integration.Contracts;
using IUMP.BuildingBlocks.Persistence;

namespace IUMP.Worker.Integration;

public sealed class AuditDeliveryHandler(
    IIntegrationDeliveryRepository delivery,
    IAuditEventConsumer consumer,
    Func<OutboxDeliveryRecord, AuditEventEnvelope> envelopeFactory,
    IHostTransactionFactory? transactionFactory = null)
{
    public async Task<bool> HandleAsync(OutboxDeliveryRecord outbox, CancellationToken ct = default)
    {
        var envelope = envelopeFactory(outbox);
        var inbox = await delivery.ClaimAsync("Audit.v1", outbox.EventId, outbox.PayloadHash,
            DateTime.UtcNow, "audit-handler", TimeSpan.FromSeconds(30), ct);
        if (inbox is null) return true;
        IHostTransaction? hostTransaction = null;
        try
        {
            // Audit append is first and Integration inbox completion is last in one host transaction.
            hostTransaction = transactionFactory is null ? null : await transactionFactory.BeginAsync(ct);
            if (hostTransaction is not null && consumer is ITransactionalAuditEventConsumer transactionalConsumer)
                await transactionalConsumer.ConsumeAsync(envelope, hostTransaction, ct);
            else if (hostTransaction is not null)
                throw new InvalidOperationException("AUDIT_CONSUMER_TRANSACTION_REQUIRED");
            else
                await consumer.ConsumeAsync(envelope, ct);
            if (hostTransaction is not null && delivery is ITransactionalInboxRepository transactionalInbox)
                await transactionalInbox.CompleteAsync(inbox, hostTransaction, ct);
            else if (hostTransaction is not null)
                throw new InvalidOperationException("INBOX_TRANSACTION_REQUIRED");
            else
                await delivery.CompleteAsync(inbox, ct);
            if (hostTransaction is IHostTransactionController controller) await controller.CommitAsync(ct);
            return true;
        }
        catch (Exception)
        {
            if (hostTransaction is IHostTransactionController controller)
                await controller.RollbackAsync(CancellationToken.None);
            await delivery.RescheduleAsync(inbox, DateTime.UtcNow.AddSeconds(5), "AUDIT_DELIVERY_RETRY", ct);
            return false;
        }
        finally
        {
            if (hostTransaction is not null) await hostTransaction.DisposeAsync();
        }
    }
}
