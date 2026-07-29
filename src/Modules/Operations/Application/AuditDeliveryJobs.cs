namespace IUMP.Modules.Operations.Application;

using IUMP.Modules.Operations.Contracts;

public sealed class AuditDeliveryJobs
{
    private readonly IJobClaimRepository? _repository;

    public AuditDeliveryJobs(IJobClaimRepository repository) => _repository = repository;

    // Kept for deterministic provider-neutral tests that only exercise retry policy.
    public AuditDeliveryJobs(object repository) => _repository = repository as IJobClaimRepository;

    public IReadOnlyList<TimeSpan> RetrySchedule { get; } = new[]
    {
        TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)
    };

    public async Task ReconcileAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        // Reconciliation is intentionally provider-neutral; the adapter supplies the due rows.
        if (_repository is not null)
            _ = await _repository.ClaimDueAsync(nowUtc, "audit-reconciliation", 50, ct);
        await Task.CompletedTask;
    }

    public TimeSpan NextRetry(int attemptCount)
    {
        var index = Math.Clamp(attemptCount - 1, 0, RetrySchedule.Count - 1);
        return RetrySchedule[index];
    }
}
