using IUMP.Modules.Integration.Contracts;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeCommandIdempotencyStore : ICommandIdempotencyStore
{
    private readonly object _gate = new();
    private readonly Dictionary<(Guid, string, string), CommandIdempotencyRecord> _rows = new();

    public Task<CommandRegistrationResult> RegisterOrReadAsync(CommandIdentity identity, byte[] fingerprint,
        string? target, TimeSpan lease, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var key = (identity.CallerUserId, identity.OperationCode, identity.IdempotencyKey);
            if (!_rows.TryGetValue(key, out var existing))
            {
                var created = CommandIdempotencyRecord.Pending(identity, fingerprint, DateTime.UtcNow.Add(lease));
                _rows[key] = created;
                return Task.FromResult(new CommandRegistrationResult(created, true, false, false, false));
            }
            if (!existing.Fingerprint.SequenceEqual(fingerprint))
                return Task.FromResult(new CommandRegistrationResult(existing, false, false, true, false));
            var inProgress = existing.Status == CommandIdempotencyStatus.Pending && existing.IsLeaseLive(DateTime.UtcNow);
            return Task.FromResult(new CommandRegistrationResult(existing, false, !inProgress && existing.Status == CommandIdempotencyStatus.Completed, false, inProgress));
        }
    }

    public Task<CommandIdempotencyRecord?> GetAsync(Guid id, CancellationToken ct = default)
    {
        lock (_gate) return Task.FromResult(_rows.Values.FirstOrDefault(row => row.Id == id));
    }

    public Task<CommandIdempotencyRecord?> TryReclaimExpiredAsync(Guid id, long expectedVersion, string owner,
        DateTime leaseUntilUtc, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = _rows.Values.FirstOrDefault(value => value.Id == id);
            if (row is null || row.Version != expectedVersion || row.Status != CommandIdempotencyStatus.Pending || row.IsLeaseLive(DateTime.UtcNow)) return Task.FromResult<CommandIdempotencyRecord?>(null);
            var updated = row with { PendingOwner = owner, PendingUntilUtc = leaseUntilUtc, AttemptCount = row.AttemptCount + 1, Version = row.Version + 1, UpdatedAtUtc = DateTime.UtcNow };
            _rows[(row.Identity.CallerUserId, row.Identity.OperationCode, row.Identity.IdempotencyKey)] = updated;
            return Task.FromResult<CommandIdempotencyRecord?>(updated);
        }
    }

    public Task<CommandIdempotencyRecord?> CompleteAsync(Guid id, long expectedVersion, StoredHttpResult result,
        DateTime expiresAtUtc, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = _rows.Values.FirstOrDefault(value => value.Id == id);
            if (row is null || row.Version != expectedVersion) return Task.FromResult<CommandIdempotencyRecord?>(null);
            var updated = row.Complete(result.StatusCode, result.Body, result.ResourceReference, expiresAtUtc, result.Location, result.ETag, result.OriginalCorrelationId);
            _rows[(row.Identity.CallerUserId, row.Identity.OperationCode, row.Identity.IdempotencyKey)] = updated;
            return Task.FromResult<CommandIdempotencyRecord?>(updated);
        }
    }

    public Task<int> RemoveExpiredAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var expired = _rows.Where(pair => pair.Value.IsExpired(nowUtc)).Select(pair => pair.Key).ToArray();
            foreach (var key in expired) _rows.Remove(key);
            return Task.FromResult(expired.Length);
        }
    }
}
