namespace IUMP.BuildingBlocks.Persistence;

public interface IHostTransactionParticipant
{
    ValueTask AcquireLockAsync(IHostTransaction transaction, LockRequest request, CancellationToken ct = default);
}
