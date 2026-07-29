namespace IUMP.Modules.Operations.Contracts;

public sealed record SafeJobPayload(string Value, string Fingerprint)
{
    public static SafeJobPayload Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A safe payload is required.", nameof(value));
        if (value.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("host=", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("postgres", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("JOB_PAYLOAD_SENSITIVE");

        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value));
        return new SafeJobPayload(value, Convert.ToHexString(bytes));
    }
}

public sealed record DurableJob(
    JobId Id,
    JobType JobType,
    IdempotencyKey IdempotencyKey,
    SafeJobPayload Payload,
    JobState Status,
    DateTime AvailableAtUtc,
    int AttemptCount,
    string? LeaseOwner,
    Guid? LeaseToken,
    DateTime? LeaseExpiresAtUtc,
    long Version,
    string? RedactedError,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? CompletedAtUtc)
{
    public DurableJob Copy() => this with { Payload = Payload with { } };
}

public sealed record JobScheduleResult(
    DurableJob Job,
    bool Created,
    bool Equivalent,
    bool Conflict,
    string Code)
{
    public static JobScheduleResult CreatedJob(DurableJob job) =>
        new(job, true, false, false, "CREATED");
    public static JobScheduleResult Existing(DurableJob job) =>
        new(job, false, true, false, "EXISTING");
    public static JobScheduleResult Conflicting(DurableJob job) =>
        new(job, false, false, true, "JOB_IDEMPOTENCY_CONFLICT");
}

public interface IDurableJobScheduler
{
    Task<JobScheduleResult> EnqueueAsync(
        JobType jobType,
        IdempotencyKey idempotencyKey,
        SafeJobPayload safePayload,
        DateTime availableAtUtc,
        CancellationToken ct = default);

    Task<JobScheduleResult> GetAsync(
        JobType jobType,
        IdempotencyKey idempotencyKey,
        CancellationToken ct = default);

    Task<bool> CancelAsync(
        JobId jobId,
        long expectedVersion,
        DateTime nowUtc,
        CancellationToken ct = default);
}
