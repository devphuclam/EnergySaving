namespace IUMP.BuildingBlocks.Persistence;

public interface IHostTransaction : IAsyncDisposable
{
    Guid TransactionId { get; }
    string IsolationIntent { get; }
    bool IsCompleted { get; }
}
