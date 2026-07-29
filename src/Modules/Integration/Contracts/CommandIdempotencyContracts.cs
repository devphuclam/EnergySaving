namespace IUMP.Modules.Integration.Contracts;

using IUMP.BuildingBlocks.Persistence;

public interface ICommandIdempotencyStore
{
    Task<CommandRegistrationResult> RegisterOrReadAsync(
        CommandIdentity identity, byte[] fingerprint, string? target, TimeSpan lease, CancellationToken ct = default);

    Task<CommandIdempotencyRecord?> GetAsync(Guid id, CancellationToken ct = default);

    Task<CommandIdempotencyRecord?> TryReclaimExpiredAsync(
        Guid id, long expectedVersion, string owner, DateTime leaseUntilUtc, CancellationToken ct = default);

    Task<CommandIdempotencyRecord?> CompleteAsync(
        Guid id, long expectedVersion, StoredHttpResult result, DateTime expiresAtUtc, CancellationToken ct = default);

    Task<int> RemoveExpiredAsync(DateTime nowUtc, CancellationToken ct = default);
}

/// Optional host adapter seam: completion participates in the same owner/outbox transaction.
/// Provider-neutral contracts keep the API runnable without selecting a database package.
public interface ITransactionalCommandIdempotencyStore
{
    Task<CommandIdempotencyRecord?> CompleteInTransactionAsync(
        Guid id, long expectedVersion, StoredHttpResult result, DateTime expiresAtUtc,
        IHostTransaction transaction, CancellationToken ct = default);
}
