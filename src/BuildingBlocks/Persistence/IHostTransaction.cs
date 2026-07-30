namespace IUMP.BuildingBlocks.Persistence;

public interface IHostTransaction : IAsyncDisposable
{
    Guid TransactionId { get; }
    string IsolationIntent { get; }
    bool IsCompleted { get; }
}

/// Provider-neutral wrapper seam used when a transaction coordinator borrows an
/// already-open host transaction owned by an outer idempotent command.
public interface IHostTransactionAccessor
{
    IHostTransaction InnerTransaction { get; }
}
