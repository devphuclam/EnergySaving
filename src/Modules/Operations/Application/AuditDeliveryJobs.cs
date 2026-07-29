namespace IUMP.Modules.Operations.Application;

using IUMP.Modules.Operations.Contracts;

public sealed class AuditDeliveryJobs
{
    private readonly IJobClaimRepository _repository;
    private readonly IAuditDeliveryOperationsRepository? _operations;

    public AuditDeliveryJobs(IJobClaimRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _operations = repository as IAuditDeliveryOperationsRepository;
    }

    public IReadOnlyList<TimeSpan> RetrySchedule { get; } = new[]
    {
        TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)
    };

    public async Task<AuditDeliveryReconciliationResult> ReconcileAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var claims = await _repository.ClaimDueAsync(nowUtc, "audit-reconciliation", 50, ct);
        var expired = await _repository.ListExpiredAsync(nowUtc, ct);
        var released = 0;
        foreach (var job in expired)
        {
            var token = job.LeaseToken;
            if (job.LeaseOwner is null || token is null || job.LeaseExpiresAtUtc is null) continue;
            var claim = new JobClaim(job, job.LeaseOwner, token.Value, job.LeaseExpiresAtUtc.Value);
            var result = await _repository.ReleaseAsync(claim, nowUtc.Add(RetrySchedule[0]), nowUtc, ct);
            if (result.Succeeded) released++;
        }
        var publishedWithoutAudit = _operations is null ? 0 :
            await _operations.CountPublishedWithoutAuditAsync(nowUtc, ct);
        return new AuditDeliveryReconciliationResult(claims.Count, released, publishedWithoutAudit);
    }

    public Task<JobOperationResult> ReplayAsync(JobId jobId, string operatorId, DateTime nowUtc,
        CancellationToken ct = default) => _operations is null
        ? Task.FromResult(new JobOperationResult(false, false, "OPERATOR_REPLAY_UNAVAILABLE"))
        : _operations.ReplayAsync(jobId, operatorId, nowUtc, ct);

    public TimeSpan NextRetry(int attemptCount)
    {
        var index = Math.Clamp(attemptCount - 1, 0, RetrySchedule.Count - 1);
        return RetrySchedule[index];
    }
}

public sealed record AuditDeliveryReconciliationResult(int Claimed, int Released, int PublishedWithoutAudit);
