using IUMP.Modules.Integration.Contracts;
using IUMP.BuildingBlocks.Persistence;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeIntegrationDeliveryRepositories : IIntegrationDeliveryRepository, IInboxStateRepository, ITransactionalInboxRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, OutboxDeliveryRecord> _outbox = new();
    private readonly Dictionary<(string, Guid), InboxDeliveryRecord> _inbox = new();

    public Task AddOutboxAsync(OutboxDeliveryRecord record, CancellationToken ct = default)
    { lock (_gate) { _outbox[record.EventId] = record; } return Task.CompletedTask; }

    public Task<OutboxDeliveryRecord?> ClaimAsync(DateTime nowUtc, string owner, TimeSpan lease, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = _outbox.Values.FirstOrDefault(value => (value.Status is DeliveryStatus.Pending or DeliveryStatus.Claimed) && value.AvailableAtUtc <= nowUtc && (value.LeaseUntilUtc is null || value.LeaseUntilUtc <= nowUtc));
            if (row is null) return Task.FromResult<OutboxDeliveryRecord?>(null);
            var claimed = row with { Status = DeliveryStatus.Claimed, LeaseOwner = owner, LeaseToken = Guid.NewGuid(), LeaseUntilUtc = nowUtc.AddSeconds(30), AttemptCount = row.AttemptCount + 1, Version = row.Version + 1 };
            _outbox[row.EventId] = claimed; return Task.FromResult<OutboxDeliveryRecord?>(claimed);
        }
    }

    public Task<OutboxDeliveryRecord?> RenewAsync(OutboxDeliveryRecord record, DateTime leaseUntilUtc, CancellationToken ct = default)
    { lock (_gate) { if (!_outbox.TryGetValue(record.EventId, out var current)) return Task.FromResult<OutboxDeliveryRecord?>(null); var next = current with { LeaseUntilUtc = leaseUntilUtc, Version = current.Version + 1 }; _outbox[record.EventId] = next; return Task.FromResult<OutboxDeliveryRecord?>(next); } }
    public Task MarkPublishedAsync(Guid eventId, CancellationToken ct = default) { lock (_gate) { if (_outbox.TryGetValue(eventId, out var row)) _outbox[eventId] = row with { Status = DeliveryStatus.Published, LeaseOwner = null, LeaseToken = null, LeaseUntilUtc = null, Version = row.Version + 1 }; } return Task.CompletedTask; }
    public Task RescheduleAsync(Guid eventId, DateTime availableAtUtc, string redactedError, CancellationToken ct = default) { lock (_gate) { if (_outbox.TryGetValue(eventId, out var row)) _outbox[eventId] = row with { Status = DeliveryStatus.Pending, AvailableAtUtc = availableAtUtc, Error = redactedError, LeaseOwner = null, LeaseToken = null, LeaseUntilUtc = null, Version = row.Version + 1 }; } return Task.CompletedTask; }
    public Task MarkFailedAsync(Guid eventId, string redactedError, DateTime nowUtc, CancellationToken ct = default) { lock (_gate) { if (_outbox.TryGetValue(eventId, out var row)) _outbox[eventId] = row with { Status = DeliveryStatus.Failed, Error = redactedError, LeaseOwner = null, LeaseToken = null, LeaseUntilUtc = null, Version = row.Version + 1 }; } return Task.CompletedTask; }
    public Task<OutboxDeliveryRecord?> GetAsync(Guid eventId, CancellationToken ct = default) { lock (_gate) return Task.FromResult(_outbox.TryGetValue(eventId, out var row) ? row : null); }

    public Task<InboxDeliveryRecord?> ClaimAsync(string consumerName, Guid eventId, string payloadHash, DateTime nowUtc, string owner, TimeSpan lease, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var key = (consumerName, eventId);
            if (_inbox.TryGetValue(key, out var current) && current.PayloadHash != payloadHash) throw new InvalidOperationException("INBOX_HASH_CONFLICT");
            if (_inbox.TryGetValue(key, out current) && current.Status == DeliveryStatus.Completed) return Task.FromResult<InboxDeliveryRecord?>(null);
            if (_inbox.TryGetValue(key, out current) && current.Status == DeliveryStatus.Claimed && current.LeaseUntilUtc > nowUtc)
                return Task.FromResult<InboxDeliveryRecord?>(null);
            var row = current ?? new InboxDeliveryRecord(consumerName, eventId, payloadHash, nowUtc);
            var claimed = row with { Status = DeliveryStatus.Claimed, LeaseOwner = owner, LeaseToken = Guid.NewGuid(), LeaseUntilUtc = nowUtc.AddSeconds(30), AttemptCount = row.AttemptCount + 1, Version = row.Version + 1 };
            _inbox[key] = claimed; return Task.FromResult<InboxDeliveryRecord?>(claimed);
        }
    }
    public Task CompleteAsync(InboxDeliveryRecord record, CancellationToken ct = default) { lock (_gate) { _inbox[(record.ConsumerName, record.EventId)] = record with { Status = DeliveryStatus.Completed, LeaseOwner = null, LeaseToken = null, LeaseUntilUtc = null, Version = record.Version + 1 }; } return Task.CompletedTask; }
    public Task CompleteAsync(InboxDeliveryRecord record, IHostTransaction transaction, CancellationToken ct = default) => CompleteAsync(record, ct);
    public Task RescheduleAsync(InboxDeliveryRecord record, DateTime availableAtUtc, string redactedError, CancellationToken ct = default) { lock (_gate) { _inbox[(record.ConsumerName, record.EventId)] = record with { Status = DeliveryStatus.Pending, AvailableAtUtc = availableAtUtc, Error = redactedError, LeaseOwner = null, LeaseToken = null, LeaseUntilUtc = null, Version = record.Version + 1 }; } return Task.CompletedTask; }
    public Task MarkFailedAsync(InboxDeliveryRecord record, string redactedError, DateTime nowUtc, CancellationToken ct = default) { lock (_gate) { _inbox[(record.ConsumerName, record.EventId)] = record with { Status = DeliveryStatus.Failed, Error = redactedError, LeaseOwner = null, LeaseToken = null, LeaseUntilUtc = null, Version = record.Version + 1 }; } return Task.CompletedTask; }
    public Task<InboxDeliveryRecord?> GetInboxAsync(string consumerName, Guid eventId, CancellationToken ct = default) { lock (_gate) return Task.FromResult(_inbox.TryGetValue((consumerName, eventId), out var row) ? row : null); }
}
