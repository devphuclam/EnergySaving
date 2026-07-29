using IUMP.Modules.Operations.Contracts;

namespace IUMP.Modules.Operations.Application;

public sealed record HealthJobExecutionResult(
    bool Succeeded,
    bool Retryable = false,
    string? RedactedError = null)
{
    public static HealthJobExecutionResult Success() => new(true);
    public static HealthJobExecutionResult Retry(string error) => new(false, true, error);
    public static HealthJobExecutionResult Failure(string error) => new(false, false, error);
}

/// <summary>
/// Host-facing provider port.  A composition root supplies an adapter that
/// loads the current Point/Source snapshot and invokes Telemetry's
/// SourceHealthService inside its existing transaction.  Operations itself
/// never queries another module's tables.
/// </summary>
public interface ISourceHealthJobHandler
{
    Task<HealthJobExecutionResult> EvaluateAsync(
        Guid pointId,
        DateTime nowUtc,
        CancellationToken ct = default);
}

public sealed record SourceHealthJobCycleResult(
    int Scheduled,
    int Claimed,
    int Completed,
    int Retried,
    int Failed,
    int Reclaimed);

public sealed class SourceHealthJobs
{
    public const string JobTypeName = "PointSourceHealthEvaluation";

    private readonly IDurableJobScheduler _scheduler;
    private readonly IJobClaimRepository _claims;
    private readonly ISourceHealthJobHandler _handler;
    private readonly ITelemetryUtcClock _clock;

    public SourceHealthJobs(
        IDurableJobScheduler scheduler,
        IJobClaimRepository claims,
        ISourceHealthJobHandler handler,
        ITelemetryUtcClock clock)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _claims = claims ?? throw new ArgumentNullException(nameof(claims));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<JobScheduleResult> ScheduleAsync(
        Guid pointId,
        DateTime availableAtUtc,
        CancellationToken ct = default)
    {
        if (pointId == Guid.Empty) throw new ArgumentException("Point is required.", nameof(pointId));
        if (availableAtUtc.Kind != DateTimeKind.Utc) throw new InvalidOperationException("JOB_TIMESTAMP_INVALID");
        var key = new IdempotencyKey($"point:{pointId:D}:source-health");
        var payload = SafeJobPayload.Create($"pointId={pointId:D};purpose=source-health");
        return _scheduler.EnqueueAsync(new JobType(JobTypeName), key, payload, availableAtUtc, ct);
    }

    public async Task<SourceHealthJobCycleResult> RunDueAsync(
        string owner,
        int maxCount = 100,
        CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var claims = await _claims.ClaimDueAsync(now, owner, maxCount, ct);
        var completed = 0;
        var retried = 0;
        var failed = 0;
        foreach (var claim in claims)
        {
            var pointId = ParsePointId(claim.Job.Payload.Value);
            HealthJobExecutionResult result;
            try
            {
                result = await _handler.EvaluateAsync(pointId, now, ct);
            }
            catch (Exception ex)
            {
                result = HealthJobExecutionResult.Retry(Redact(ex.Message));
            }

            if (result.Succeeded)
            {
                var completion = await _claims.CompleteAsync(claim, now, ct);
                if (completion.Succeeded) completed++;
                else failed++;
                continue;
            }

            if (result.Retryable)
            {
                var retry = await _claims.RescheduleAsync(
                    claim,
                    now.AddSeconds(30),
                    Redact(result.RedactedError),
                    now,
                    ct);
                if (retry.Succeeded) retried++;
                else failed++;
                continue;
            }

            var terminal = await _claims.FailAsync(
                claim, Redact(result.RedactedError), now, ct);
            if (terminal.Succeeded) failed++;
        }

        return new SourceHealthJobCycleResult(0, claims.Count, completed, retried, failed, 0);
    }

    public async Task<int> ReconcileExpiredAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var expired = await _claims.ListExpiredAsync(now, ct);
        var reclaimed = 0;
        foreach (var job in expired)
        {
            // The release operation is idempotent because the lease token and
            // optimistic version are copied from the expired snapshot.
            var claim = new JobClaim(
                job,
                job.LeaseOwner ?? "reconciler",
                job.LeaseToken ?? Guid.Empty,
                job.LeaseExpiresAtUtc ?? now);
            var result = await _claims.ReleaseAsync(claim, now, now, ct);
            if (result.Succeeded) reclaimed++;
        }
        return reclaimed;
    }

    private static Guid ParsePointId(string payload)
    {
        var marker = "pointId=";
        var start = payload.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) throw new InvalidOperationException("HEALTH_JOB_PAYLOAD_INVALID");
        start += marker.Length;
        var end = payload.IndexOf(';', start);
        var value = end < 0 ? payload[start..] : payload[start..end];
        return Guid.TryParse(value, out var pointId) && pointId != Guid.Empty
            ? pointId
            : throw new InvalidOperationException("HEALTH_JOB_PAYLOAD_INVALID");
    }

    private static string Redact(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "HEALTH_JOB_FAILED" :
        value.Replace("password", "[redacted]", StringComparison.OrdinalIgnoreCase)
             .Replace("secret", "[redacted]", StringComparison.OrdinalIgnoreCase)
             .Replace("token", "[redacted]", StringComparison.OrdinalIgnoreCase)
             .Replace("credential", "[redacted]", StringComparison.OrdinalIgnoreCase)
             .Replace("connection", "[redacted]", StringComparison.OrdinalIgnoreCase);
}

public interface ITelemetryUtcClock
{
    DateTime UtcNow { get; }
}
