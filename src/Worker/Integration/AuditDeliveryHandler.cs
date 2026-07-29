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
            await consumer.ConsumeAsync(envelope, ct);
            await delivery.CompleteAsync(inbox, ct);
            if (hostTransaction is IHostTransactionController controller) await controller.CommitAsync(ct);
            return true;
        }
        catch (Exception)
        {
            if (hostTransaction is IHostTransactionController controller)
                await controller.RollbackAsync(CancellationToken.None);
            await delivery.MarkFailedAsync(inbox, "AUDIT_DELIVERY_FAILED", DateTime.UtcNow, ct);
            return false;
        }
        finally
        {
            if (hostTransaction is not null) await hostTransaction.DisposeAsync();
        }
    }
}
