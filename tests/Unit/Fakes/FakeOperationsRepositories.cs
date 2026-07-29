using IUMP.Modules.Operations.Contracts;
using IUMP.Modules.Telemetry.Contracts;

namespace IUMP.Tests.Unit.Fakes;

/// <summary>
/// Deterministic, provider-neutral Operations fake used by Phase 8 contract
/// tests.  Every read returns a copy and every mutation advances the optimistic
/// version, so tests cannot accidentally depend on shared mutable state.
/// </summary>
public sealed class FakeOperationsRepositories : IDurableJobScheduler, IJobClaimRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Type, string Key), DurableJob> _jobs = new();
    private readonly Dictionary<Guid, JobId> _completedClaims = new();
    private int _nextId;

    public DateTime CreatedAtUtc { get; set; } = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public int Count
    {
        get { lock (_gate) return _jobs.Count; }
    }

    public Task<JobScheduleResult> EnqueueAsync(
        JobType jobType,
        IdempotencyKey idempotencyKey,
        SafeJobPayload safePayload,
        DateTime availableAtUtc,
        CancellationToken ct = default)
    {
        if (availableAtUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("JOB_TIMESTAMP_INVALID");
        var canonicalPayload = SafeJobPayload.Create(safePayload.Value);
        if (!string.Equals(canonicalPayload.Fingerprint, safePayload.Fingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("JOB_PAYLOAD_FINGERPRINT_INVALID");
        lock (_gate)
        {
            var key = (jobType.Value, idempotencyKey.Value);
            if (_jobs.TryGetValue(key, out var existing))
            {
                if (existing.Payload.Fingerprint == safePayload.Fingerprint)
                    return Task.FromResult(JobScheduleResult.Existing(existing.Copy()));
                return Task.FromResult(JobScheduleResult.Conflicting(existing.Copy()));
            }

            var now = CreatedAtUtc;
            var job = new DurableJob(
                new JobId(new Guid($"00000000-0000-0000-0000-{++_nextId:000000000000}")),
                jobType,
                idempotencyKey,
                safePayload with { },
                JobState.Pending,
                availableAtUtc,
                0,
                null,
                null,
                null,
                1,
                null,
                now,
                now,
                null);
            _jobs.Add(key, job);
            return Task.FromResult(JobScheduleResult.CreatedJob(job.Copy()));
        }
    }

    public Task<JobScheduleResult> GetAsync(
        JobType jobType,
        IdempotencyKey idempotencyKey,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_jobs.TryGetValue((jobType.Value, idempotencyKey.Value), out var job))
                throw new KeyNotFoundException("JOB_NOT_FOUND");
            return Task.FromResult(JobScheduleResult.Existing(job.Copy()));
        }
    }

    public Task<bool> CancelAsync(
        JobId jobId,
        long expectedVersion,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            var job = Find(jobId);
            if (job.Version != expectedVersion || job.Status is JobState.Completed or JobState.Failed)
                return Task.FromResult(false);
            Replace(job with
            {
                Status = JobState.Failed,
                RedactedError = "CANCELLED",
                Version = job.Version + 1,
                UpdatedAtUtc = nowUtc,
                CompletedAtUtc = null,
                LeaseOwner = null,
                LeaseToken = null,
                LeaseExpiresAtUtc = null
            });
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<JobClaim>> ClaimDueAsync(
        DateTime nowUtc,
        string owner,
        int maxCount = 1,
        CancellationToken ct = default)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new InvalidOperationException("JOB_TIMESTAMP_INVALID");
        if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("Owner is required.", nameof(owner));
        lock (_gate)
        {
            var candidates = _jobs.Values
                .Where(job => job.Status == JobState.Pending && job.AvailableAtUtc <= nowUtc)
                .OrderBy(job => job.AvailableAtUtc)
                .ThenBy(job => job.CreatedAtUtc)
                .ThenBy(job => job.Id.Value)
                .Take(Math.Max(0, maxCount))
                .ToList();
            var claims = new List<JobClaim>(candidates.Count);
            foreach (var job in candidates)
            {
                var token = DeterministicToken(job.Id, job.Version + 1);
                var leased = job with
                {
                    Status = JobState.Leased,
                    LeaseOwner = owner,
                    LeaseToken = token,
                    LeaseExpiresAtUtc = nowUtc.AddSeconds(30),
                    AttemptCount = job.AttemptCount + 1,
                    Version = job.Version + 1,
                    UpdatedAtUtc = nowUtc
                };
                Replace(leased);
                claims.Add(new JobClaim(leased.Copy(), owner, token, leased.LeaseExpiresAtUtc!.Value));
            }
            return Task.FromResult<IReadOnlyList<JobClaim>>(claims);
        }
    }

    public Task<JobOperationResult> RenewAsync(JobClaim claim, DateTime nowUtc, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var current = Find(claim.Job.Id);
            if (!Owns(current, claim, nowUtc)) return Task.FromResult(Failure(current, "JOB_LEASE_INVALID"));
            var renewed = current with
            {
                LeaseExpiresAtUtc = nowUtc.AddSeconds(30),
                Version = current.Version + 1,
                UpdatedAtUtc = nowUtc
            };
            Replace(renewed);
            return Task.FromResult(Success(renewed));
        }
    }

    public Task<JobOperationResult> CompleteAsync(JobClaim claim, DateTime nowUtc, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var current = Find(claim.Job.Id);
            if (current.Status == JobState.Completed &&
                _completedClaims.TryGetValue(claim.Token, out var completedId) && completedId == current.Id)
                return Task.FromResult(new JobOperationResult(true, true, "ALREADY_COMPLETED", current.Copy()));
            if (!Owns(current, claim, nowUtc)) return Task.FromResult(Failure(current, "JOB_LEASE_INVALID"));
            var completed = current with
            {
                Status = JobState.Completed,
                LeaseOwner = null,
                LeaseToken = null,
                LeaseExpiresAtUtc = null,
                CompletedAtUtc = nowUtc,
                Version = current.Version + 1,
                UpdatedAtUtc = nowUtc
            };
            Replace(completed);
            _completedClaims[claim.Token] = current.Id;
            return Task.FromResult(Success(completed));
        }
    }

    public Task<JobOperationResult> RescheduleAsync(
        JobClaim claim,
        DateTime availableAtUtc,
        string redactedError,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            var current = Find(claim.Job.Id);
            if (!Owns(current, claim, nowUtc)) return Task.FromResult(Failure(current, "JOB_LEASE_INVALID"));
            var next = current with
            {
                Status = JobState.Pending,
                AvailableAtUtc = availableAtUtc,
                LeaseOwner = null,
                LeaseToken = null,
                LeaseExpiresAtUtc = null,
                RedactedError = Redact(redactedError),
                Version = current.Version + 1,
                UpdatedAtUtc = nowUtc
            };
            Replace(next);
            return Task.FromResult(Success(next));
        }
    }

    public Task<JobOperationResult> FailAsync(
        JobClaim claim,
        string redactedError,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            var current = Find(claim.Job.Id);
            if (!Owns(current, claim, nowUtc)) return Task.FromResult(Failure(current, "JOB_LEASE_INVALID"));
            var failed = current with
            {
                Status = JobState.Failed,
                LeaseOwner = null,
                LeaseToken = null,
                LeaseExpiresAtUtc = null,
                RedactedError = Redact(redactedError),
                Version = current.Version + 1,
                UpdatedAtUtc = nowUtc
            };
            Replace(failed);
            return Task.FromResult(Success(failed));
        }
    }

    public Task<JobOperationResult> ReleaseAsync(
        JobClaim claim,
        DateTime availableAtUtc,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            var current = Find(claim.Job.Id);
            var ownsExpired = current.Status == JobState.Leased &&
                current.Version == claim.Job.Version && current.LeaseOwner == claim.Owner &&
                current.LeaseToken == claim.Token && current.LeaseExpiresAtUtc <= nowUtc;
            if (!ownsExpired) return Task.FromResult(Failure(current, "JOB_LEASE_INVALID"));
            var released = current with
            {
                Status = JobState.Pending,
                AvailableAtUtc = availableAtUtc,
                LeaseOwner = null,
                LeaseToken = null,
                LeaseExpiresAtUtc = null,
                RedactedError = "LEASE_RELEASED",
                Version = current.Version + 1,
                UpdatedAtUtc = nowUtc
            };
            Replace(released);
            return Task.FromResult(Success(released));
        }
    }

    public Task<IReadOnlyList<DurableJob>> ListExpiredAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var expired = _jobs.Values
                .Where(job => job.Status == JobState.Leased && job.LeaseExpiresAtUtc <= nowUtc)
                .OrderBy(job => job.AvailableAtUtc)
                .Select(job => job.Copy())
                .ToList();
            return Task.FromResult<IReadOnlyList<DurableJob>>(expired);
        }
    }

    public IReadOnlyList<DurableJob> Snapshot()
    {
        lock (_gate) return _jobs.Values.OrderBy(job => job.Id.Value).Select(job => job.Copy()).ToList();
    }

    private DurableJob Find(JobId id) =>
        _jobs.Values.FirstOrDefault(job => job.Id == id) ?? throw new KeyNotFoundException("JOB_NOT_FOUND");

    private void Replace(DurableJob replacement)
    {
        var key = (replacement.JobType.Value, replacement.IdempotencyKey.Value);
        _jobs[key] = replacement;
    }

    private static bool Owns(DurableJob current, JobClaim claim, DateTime nowUtc) =>
        current.Status == JobState.Leased && current.Version == claim.Job.Version &&
        current.LeaseOwner == claim.Owner && current.LeaseToken == claim.Token &&
        current.LeaseExpiresAtUtc > nowUtc;

    private static JobOperationResult Success(DurableJob job) =>
        new(true, false, "OK", job.Copy());
    private static JobOperationResult Failure(DurableJob job, string code) =>
        new(false, false, code, job.Copy());

    private static Guid DeterministicToken(JobId id, long version) =>
        GuidUtility.Create(GuidUtility.Namespace, $"{id.Value:D}:{version}");

    private static string Redact(string value) =>
        string.IsNullOrWhiteSpace(value) ? "JOB_FAILED" :
        value.Replace("password", "[redacted]", StringComparison.OrdinalIgnoreCase)
             .Replace("secret", "[redacted]", StringComparison.OrdinalIgnoreCase)
             .Replace("token", "[redacted]", StringComparison.OrdinalIgnoreCase)
             .Replace("credential", "[redacted]", StringComparison.OrdinalIgnoreCase)
             .Replace("connection", "[redacted]", StringComparison.OrdinalIgnoreCase);

    private static class GuidUtility
    {
        public static readonly Guid Namespace = Guid.Parse("02e993bb-c767-5ff6-963f-530e1dfdff6b");
        public static Guid Create(Guid ns, string name)
        {
            var bytes = ns.ToByteArray().Concat(System.Text.Encoding.UTF8.GetBytes(name)).ToArray();
            var hash = System.Security.Cryptography.SHA256.HashData(bytes);
            return new Guid(hash.AsSpan(0, 16));
        }
    }
}

public sealed class NoOpTelemetryFlowUnitOfWork : ITelemetryFlowUnitOfWork
{
    public ValueTask<ITelemetryFlowTransaction> BeginRepeatableReadAsync(CancellationToken ct = default) =>
        ValueTask.FromResult<ITelemetryFlowTransaction>(new NoOpTelemetryFlowTransaction());

    private sealed class NoOpTelemetryFlowTransaction : ITelemetryFlowTransaction
    {
        private readonly List<TelemetryFlowLock> _locks = [];
        public Guid TransactionId { get; } = Guid.NewGuid();
        public string IsolationIntent => "REPEATABLE READ";
        public bool IsCompleted { get; private set; }
        public IReadOnlyList<TelemetryFlowLock> LockTrace => _locks;
        public ValueTask AcquireLockAsync(TelemetryFlowLockTarget target, string key, CancellationToken ct = default)
        { _locks.Add(new TelemetryFlowLock(target, key)); return ValueTask.CompletedTask; }
        public ValueTask CommitAsync(CancellationToken ct = default) { IsCompleted = true; return ValueTask.CompletedTask; }
        public ValueTask RollbackAsync(CancellationToken ct = default) { IsCompleted = true; return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() { IsCompleted = true; return ValueTask.CompletedTask; }
    }
}
