using IUMP.BuildingBlocks.Persistence;

namespace IUMP.Tests.Unit.Fakes;

public sealed class NullBackend : IHostTransactionBackend
{
    public static NullBackend Instance { get; } = new();

    private int _txCounter;

    public ValueTask<IHostTransaction> BeginAsync(CancellationToken ct = default)
    {
        var tx = new FakeHostTransaction(Guid.NewGuid());
        Interlocked.Increment(ref _txCounter);
        return ValueTask.FromResult<IHostTransaction>(tx);
    }

    public ValueTask CommitAsync(IHostTransaction transaction, CancellationToken ct = default)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask RollbackAsync(IHostTransaction transaction, CancellationToken ct = default)
    {
        return ValueTask.CompletedTask;
    }
}
