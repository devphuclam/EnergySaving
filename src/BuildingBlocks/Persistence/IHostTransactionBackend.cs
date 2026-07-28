namespace IUMP.BuildingBlocks.Persistence;

public interface IHostTransactionBackend
{
    ValueTask<IHostTransaction> BeginAsync(CancellationToken ct = default);

    ValueTask CommitAsync(IHostTransaction transaction, CancellationToken ct = default);

    ValueTask RollbackAsync(IHostTransaction transaction, CancellationToken ct = default);
}
