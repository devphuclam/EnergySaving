namespace IUMP.Modules.Operations.Contracts;

public sealed record JobClaim(
    DurableJob Job,
    string Owner,
    Guid Token,
    DateTime LeaseExpiresAtUtc)
{
    public DurableJob Snapshot => Job with
    {
        LeaseOwner = Owner,
        LeaseToken = Token,
        LeaseExpiresAtUtc = LeaseExpiresAtUtc
    };
}

public sealed record JobOperationResult(
    bool Succeeded,
    bool Idempotent,
    string Code,
    DurableJob? Job = null);

public sealed record JobRetryPolicy(
    int MaxAttempts = 10,
    TimeSpan LeaseDuration = default,
    TimeSpan RetryDelay = default)
{
    public TimeSpan EffectiveLeaseDuration =>
        LeaseDuration == default ? TimeSpan.FromSeconds(30) : LeaseDuration;
    public TimeSpan EffectiveRetryDelay =>
        RetryDelay == default ? TimeSpan.FromSeconds(30) : RetryDelay;
}

public interface IJobClaimRepository
{
    Task<IReadOnlyList<JobClaim>> ClaimDueAsync(
        DateTime nowUtc,
        string owner,
        int maxCount = 1,
        CancellationToken ct = default);

    Task<JobOperationResult> RenewAsync(
        JobClaim claim,
        DateTime nowUtc,
        CancellationToken ct = default);

    Task<JobOperationResult> CompleteAsync(
        JobClaim claim,
        DateTime nowUtc,
        CancellationToken ct = default);

    Task<JobOperationResult> RescheduleAsync(
        JobClaim claim,
        DateTime availableAtUtc,
        string redactedError,
        DateTime nowUtc,
        CancellationToken ct = default);

    Task<JobOperationResult> FailAsync(
        JobClaim claim,
        string redactedError,
        DateTime nowUtc,
        CancellationToken ct = default);

    Task<JobOperationResult> ReleaseAsync(
        JobClaim claim,
        DateTime availableAtUtc,
        DateTime nowUtc,
        CancellationToken ct = default);

    Task<IReadOnlyList<DurableJob>> ListExpiredAsync(
        DateTime nowUtc, CancellationToken ct = default);
}
