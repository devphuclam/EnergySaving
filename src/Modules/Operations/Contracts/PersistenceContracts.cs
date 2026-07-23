namespace IUMP.Modules.Operations.Contracts;

public readonly record struct JobId(Guid Value);

public readonly record struct JobType(string Value);

public readonly record struct IdempotencyKey(string Value);

public enum JobState
{
    Pending,
    Leased,
    Completed,
    Failed
}

public interface IOperationsStore
{
    ValueTask EnqueueJobAsync(
        JobId id,
        JobType jobType,
        IdempotencyKey idempotencyKey,
        CancellationToken cancellationToken);
}
