namespace IUMP.BuildingBlocks.Persistence;

/// Provider-neutral composition seam for a single host transaction.
public interface IHostTransactionFactory
{
    ValueTask<IHostTransaction> BeginAsync(CancellationToken ct = default);
}

public interface IHostTransactionController
{
    ValueTask CommitAsync(CancellationToken ct = default);
    ValueTask RollbackAsync(CancellationToken ct = default);
}
