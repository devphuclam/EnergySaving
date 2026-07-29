using IUMP.Modules.Audit.Contracts;
using IUMP.Modules.Integration.Contracts;

namespace IUMP.Worker.Integration;

public sealed class AuditDeliveryHandler(
    IIntegrationDeliveryRepository delivery,
    IAuditEventConsumer consumer,
    Func<OutboxDeliveryRecord, AuditEventEnvelope> envelopeFactory)
{
    public async Task<bool> HandleAsync(OutboxDeliveryRecord outbox, CancellationToken ct = default)
    {
        var envelope = envelopeFactory(outbox);
        var inbox = await delivery.ClaimAsync("Audit.v1", outbox.EventId, outbox.PayloadHash,
            DateTime.UtcNow, "audit-handler", TimeSpan.FromSeconds(30), ct);
        if (inbox is null) return true;
        try
        {
            await consumer.ConsumeAsync(envelope, ct);
            await delivery.CompleteAsync(inbox, ct);
            return true;
        }
        catch (Exception)
        {
            await delivery.MarkFailedAsync(inbox, "AUDIT_DELIVERY_FAILED", DateTime.UtcNow, ct);
            return false;
        }
    }
}
