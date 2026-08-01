using IUMP.Modules.Acquisition.Contracts;
using IUMP.Tests.Integration.Acquisition;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeUtcClock : IUtcClock
{
    public FakeUtcClock(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(utcNow));
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; private set; }
    public void Advance(TimeSpan amount) => UtcNow = UtcNow.Add(amount);
}

public sealed class FakeRunAttemptRepositoryTestProviderFactory :
    IRunAttemptRepositoryTestProviderFactory
{
    public IRunAttemptRepositoryTestProvider Create() =>
        new FakeRunAttemptRepositoryTestProvider();

    private sealed class FakeRunAttemptRepositoryTestProvider :
        IRunAttemptRepositoryTestProvider
    {
        private readonly FakeAcquisitionRunRepositories _repositories = new();

        public IAcquisitionRunRepository Runs => _repositories;
        public ISimulatorProductionAttemptRepository Attempts => _repositories;
        public ISimulatorRunUnitOfWork UnitOfWork => _repositories;
        public void FailNextCommit() => _repositories.FailNextCommit = true;
        public void SimulateReserveUniquenessRace() =>
            _repositories.SimulateReserveUniquenessRace = true;
        public Task AttemptPinnedMutationAsync(
            SimulatorRunPointState replacement,
            ISimulatorRunTransaction transaction) =>
            _repositories.AttemptPinnedMutationAsync(replacement, transaction);
        public Task AttemptPayloadMutationAsync(
            SimulatorProductionAttempt replacement,
            ISimulatorRunTransaction transaction) =>
            _repositories.AttemptPayloadMutationAsync(replacement, transaction);
    }
}

public sealed class FakeRunCallerSnapshotProvider : IRunCallerSnapshotProvider
{
    public Dictionary<string, RunCallerSnapshot> Callers { get; } = new(StringComparer.Ordinal);
    public Task<RunCallerSnapshot?> ResolveAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult(Callers.GetValueOrDefault(userId));
}

public sealed class FakeSimulatorStartSnapshotProvider : ISimulatorStartSnapshotProvider
{
    public SimulatorStartSnapshot? Snapshot { get; set; }
    public bool ReturnSnapshotForAnySource { get; set; }
    public bool RecheckResult { get; set; } = true;
    public SimulatorStartSnapshot? SnapshotOnRecheck { get; set; }
    public int ResolveCount { get; private set; }
    public int RecheckCount { get; private set; }
    public SimulatorStartSelection? LastSelection { get; private set; }
    public IReadOnlyList<SimulatorStartLock> LastRecheckLockTrace { get; private set; } =
        Array.Empty<SimulatorStartLock>();

    public Task<SimulatorStartSnapshot?> ResolveAsync(Guid sourceId, DateTime atUtc,
        SimulatorStartSelection? selection = null, CancellationToken ct = default)
    {
        ResolveCount++;
        LastSelection = selection;
        return Task.FromResult(
            ReturnSnapshotForAnySource || Snapshot?.SourceId == sourceId ? Snapshot : null);
    }

    public Task<bool> RecheckAsync(
        SimulatorStartSnapshot snapshot,
        ISimulatorRunTransaction transaction,
        DateTime atUtc,
        CancellationToken ct = default)
    {
        RecheckCount++;
        if (transaction.IsCompleted)
            throw new InvalidOperationException("TRANSACTION_REQUIRED");
        LastRecheckLockTrace = transaction.LockTrace.ToList();
        if (SnapshotOnRecheck is not null) Snapshot = SnapshotOnRecheck;
        return Task.FromResult(RecheckResult);
    }
}

public sealed class FakeSimulatorProductionEligibility : ISimulatorProductionEligibility
{
    public bool IsActive { get; set; } = true;
    public string? ErrorCode { get; set; }
    public Func<SimulatorRun, SimulatorRunPointState, (bool IsActive, string? ErrorCode)>?
        Selector { get; set; }
    public List<Guid> CheckedPointIds { get; } = new();
    public Task<(bool IsActive, string? ErrorCode)> IsPinnedInputActiveAsync(
        SimulatorRun run, SimulatorRunPointState pointState, CancellationToken ct = default)
    {
        CheckedPointIds.Add(pointState.PointId);
        return Task.FromResult(Selector?.Invoke(run, pointState) ?? (IsActive, ErrorCode));
    }
}

public sealed class FakeTelemetryIngestionClient : ITelemetryIngestionClient
{
    public List<SimulatorProductionPayload> Payloads { get; } = new();
    public Func<SimulatorProductionPayload, CanonicalTelemetryIngestionResult> CanonicalResultFactory { get; set; } =
        CanonicalTelemetryFixtures.Accepted;
    public bool ThrowTransient { get; set; }
    public Func<SimulatorProductionPayload, Exception?>? FailureSelector { get; set; }
    public TimeSpan DispatchDelay { get; set; }
    public Func<bool>? TransactionActiveProbe { get; set; }
    public bool ObservedActiveTransaction { get; private set; }

    public async Task<CanonicalTelemetryIngestionResult> DispatchCanonicalAsync(
        SimulatorProductionPayload payload, CancellationToken ct = default)
    {
        ObservedActiveTransaction |= TransactionActiveProbe?.Invoke() == true;
        Payloads.Add(payload with { });
        if (DispatchDelay > TimeSpan.Zero)
            await Task.Delay(DispatchDelay, ct);
        if (FailureSelector?.Invoke(payload) is { } failure) throw failure;
        if (ThrowTransient) throw new TimeoutException("TRANSIENT_TELEMETRY");
        return CanonicalResultFactory(payload);
    }
}

public static class CanonicalTelemetryFixtures
{
    private static readonly DateTime CompletedAtUtc =
        new(2026, 7, 28, 6, 0, 0, DateTimeKind.Utc);

    public static CanonicalTelemetryIngestionResult Accepted(
        SimulatorProductionPayload payload) => new(
            CanonicalTelemetryDisposition.Accepted,
            new CanonicalTelemetryOriginalResult(
                ProductionFinalClassification.Accepted, true, payload.MeasurementId,
                "Good", null, null, true, CompletedAtUtc,
                "fixture-original-correlation", "fixture-original-lineage"),
            null, payload.CorrelationId);
}

public sealed class CountingSimulatorValueGenerator : ISimulatorValueGenerator
{
    private readonly ISimulatorValueGenerator _inner;
    public CountingSimulatorValueGenerator(ISimulatorValueGenerator inner) => _inner = inner;
    public int InitializeCount { get; private set; }
    public int GenerateCount { get; private set; }

    public byte[] Initialize(ulong seed, Guid pointId, Guid configurationId,
        long configurationVersion, int algorithmVersion)
    {
        InitializeCount++;
        return _inner.Initialize(seed, pointId, configurationId, configurationVersion, algorithmVersion);
    }

    public DeterministicGeneration Generate(byte[] state, SimulatorScenario scenario,
        double minimumValue, double maximumValue)
    {
        GenerateCount++;
        return _inner.Generate(state, scenario, minimumValue, maximumValue);
    }
}

public sealed class CountingMeasurementIdentityFactory : IMeasurementIdentityFactory
{
    private readonly IMeasurementIdentityFactory _inner;
    public CountingMeasurementIdentityFactory(IMeasurementIdentityFactory inner) => _inner = inner;
    public int CreateCount { get; private set; }
    public List<Guid> CreatedPointIds { get; } = new();

    public Guid Create(Guid sourceId, Guid runId, Guid pointId, Guid mappingId,
        long sourceSequence, int algorithmVersion)
    {
        CreateCount++;
        CreatedPointIds.Add(pointId);
        return _inner.Create(sourceId, runId, pointId, mappingId, sourceSequence, algorithmVersion);
    }
}

public sealed class FakeAcquisitionRunRepositories :
    IAcquisitionRunRepository,
    ISimulatorProductionAttemptRepository,
    ISimulatorRunUnitOfWork,
    ISimulatorRunOwnerEventWriter
{
    private State _committed = new();
    private FakeSimulatorRunTransaction? _active;

    public bool IsTransactionActive => _active is { IsFinished: false };
    public int LeaseRenewalCount { get; private set; }
    public bool FailNextCommit { get; set; }
    public bool SimulateReserveUniquenessRace { get; set; }
    public int CommittedPointCount => _committed.Points.Count;
    public int BeginCount { get; private set; }
    public IReadOnlyList<SimulatorRunOwnerEvent> CommittedEvents =>
        _committed.Events.Select(Clone).ToList();

    public ValueTask<ISimulatorRunTransaction> BeginAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_active is { IsFinished: false }) throw new InvalidOperationException("TRANSACTION_ALREADY_ACTIVE");
        BeginCount++;
        _active = new FakeSimulatorRunTransaction(this, Clone(_committed));
        return ValueTask.FromResult<ISimulatorRunTransaction>(_active);
    }

    public Task<SimulatorRun?> GetAsync(Guid runId, CancellationToken ct = default) =>
        Task.FromResult(_committed.Runs.GetValueOrDefault(runId) is { } run ? Clone(run) : null);

    public Task<SimulatorRun?> GetCurrentBySourceAsync(Guid sourceId, CancellationToken ct = default) =>
        Task.FromResult(_committed.Runs.Values
            .Where(run => run.SourceId == sourceId && run.Status != SimulatorRunStatus.Stopped)
            .OrderByDescending(run => run.CreatedAtUtc).Select(Clone).FirstOrDefault());

    public Task<IReadOnlyList<SimulatorRun>> ListRunningAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SimulatorRun>>(_committed.Runs.Values
            .Where(run => run.Status == SimulatorRunStatus.Running)
            .OrderBy(run => run.RunId).Select(Clone).ToList());

    public Task<IReadOnlyList<SimulatorRunPointState>> ListPointStatesAsync(Guid runId,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SimulatorRunPointState>>(_committed.Points.Values
            .Where(point => point.RunId == runId).OrderBy(point => point.PointId).Select(Clone).ToList());

    public Task<SimulatorRunPointState?> GetPointStateAsync(Guid runId, Guid pointId,
        CancellationToken ct = default) =>
        Task.FromResult(_committed.Points.GetValueOrDefault((runId, pointId)) is { } point
            ? Clone(point) : null);

    public Task CreateAsync(SimulatorRun run, IReadOnlyList<SimulatorRunPointState> points,
        ISimulatorRunTransaction transaction, CancellationToken ct = default)
    {
        var state = Workspace(transaction);
        if (state.Runs.Values.Any(current => current.SourceId == run.SourceId &&
            current.Status != SimulatorRunStatus.Stopped))
            throw new InvalidOperationException("RUN_ALREADY_EXISTS");
        if (points.Count == 0 || points.Any(point => point.RunId != run.RunId))
            throw new InvalidOperationException("RUN_POINTS_REQUIRED");
        state.Runs.Add(run.RunId, Clone(run));
        foreach (var point in points)
        {
            if (point.PrngState.Length != 25) throw new InvalidOperationException("PRNG_STATE_LENGTH");
            state.Points.Add((run.RunId, point.PointId), Clone(point));
        }
        return Task.CompletedTask;
    }

    public Task<SimulatorRun> ChangeStatusAsync(Guid runId, long expectedVersion,
        SimulatorRunStatus targetStatus, DateTime nowUtc, string? errorCode, string? errorMessage,
        ISimulatorRunTransaction transaction, CancellationToken ct = default)
    {
        var state = Workspace(transaction);
        var run = state.Runs.GetValueOrDefault(runId) ?? throw new InvalidOperationException("RUN_NOT_FOUND");
        if (run.Version != expectedVersion) throw new InvalidOperationException("VERSION_CONFLICT");
        var changed = run with
        {
            Status = targetStatus,
            Version = checked(run.Version + 1),
            PausedAtUtc = targetStatus == SimulatorRunStatus.Paused ? nowUtc : run.PausedAtUtc,
            ResumedAtUtc = targetStatus == SimulatorRunStatus.Running ? nowUtc : run.ResumedAtUtc,
            StoppedAtUtc = targetStatus == SimulatorRunStatus.Stopped ? nowUtc : run.StoppedAtUtc,
            LatestErrorCode = errorCode ?? run.LatestErrorCode,
            LatestErrorMessage = errorMessage ?? run.LatestErrorMessage
        };
        state.Runs[runId] = changed;
        return Task.FromResult(Clone(changed));
    }

    public Task<SimulatorRunLease?> ClaimDuePointAsync(Guid runId, Guid pointId, string owner,
        DateTime nowUtc, DateTime leaseUntilUtc, CancellationToken ct = default)
    {
        var run = _committed.Runs.GetValueOrDefault(runId);
        var key = (runId, pointId);
        var point = _committed.Points.GetValueOrDefault(key);
        if (run?.Status != SimulatorRunStatus.Running || point is null || point.NextDueAtUtc > nowUtc ||
            point.LeaseExpiresAtUtc is { } currentExpiry && currentExpiry > nowUtc)
            return Task.FromResult<SimulatorRunLease?>(null);
        var token = Guid.NewGuid();
        var version = checked(point.LeaseVersion + 1);
        _committed.Points[key] = point with
        {
            LeaseOwner = owner,
            LeaseToken = token,
            LeaseVersion = version,
            LeaseExpiresAtUtc = leaseUntilUtc,
            Version = checked(point.Version + 1)
        };
        return Task.FromResult<SimulatorRunLease?>(new(runId, pointId, owner, token, version, leaseUntilUtc));
    }

    public Task<SimulatorRunLease?> RenewLeaseAsync(SimulatorRunLease lease, DateTime leaseUntilUtc,
        CancellationToken ct = default)
    {
        var key = (lease.RunId, lease.PointId);
        var point = _committed.Points.GetValueOrDefault(key);
        if (point is null || point.LeaseToken != lease.Token || point.LeaseOwner != lease.Owner ||
            point.LeaseVersion != lease.Version)
            return Task.FromResult<SimulatorRunLease?>(null);
        var renewedVersion = checked(point.LeaseVersion + 1);
        _committed.Points[key] = point with
        {
            LeaseExpiresAtUtc = leaseUntilUtc,
            LeaseVersion = renewedVersion,
            Version = checked(point.Version + 1)
        };
        LeaseRenewalCount++;
        return Task.FromResult<SimulatorRunLease?>(
            lease with { Version = renewedVersion, ExpiresAtUtc = leaseUntilUtc });
    }

    public Task ReleaseLeaseAsync(SimulatorRunLease lease, CancellationToken ct = default)
    {
        var key = (lease.RunId, lease.PointId);
        var point = _committed.Points.GetValueOrDefault(key);
        if (point is not null && point.LeaseToken == lease.Token)
            _committed.Points[key] = point with
            {
                LeaseOwner = null,
                LeaseToken = null,
                LeaseExpiresAtUtc = null,
                LeaseVersion = checked(point.LeaseVersion + 1),
                Version = checked(point.Version + 1)
            };
        return Task.CompletedTask;
    }

    public Task StageReservationAsync(
        SimulatorRunPointReservationTransition transition,
        ISimulatorRunTransaction transaction,
        CancellationToken ct = default)
    {
        var state = Workspace(transaction);
        ApplyReservation(state, transition);
        return Task.CompletedTask;
    }

    private static void ApplyReservation(
        State state,
        SimulatorRunPointReservationTransition transition)
    {
        var run = state.Runs.GetValueOrDefault(transition.RunId)
            ?? throw new InvalidOperationException("RUN_NOT_FOUND");
        if (run.Version != transition.ExpectedRunVersion)
            throw new InvalidOperationException("VERSION_CONFLICT");
        var key = (transition.RunId, transition.PointId);
        var current = state.Points.GetValueOrDefault(key)
            ?? throw new InvalidOperationException("RUN_POINT_NOT_FOUND");
        if (current.Version != transition.ExpectedPointStateVersion)
            throw new InvalidOperationException("RUN_POINT_VERSION_CONFLICT");
        if (current.NextSourceSequence != transition.ExpectedNextSourceSequence ||
            transition.NextSourceSequence != checked(current.NextSourceSequence + 1))
            throw new InvalidOperationException("SEQUENCE_ADVANCE_INVALID");
        if (transition.ResultingPrngState.Length != 25)
            throw new InvalidOperationException("PRNG_STATE_LENGTH");
        state.Runs[transition.RunId] = run with
        {
            GeneratedCount = checked(run.GeneratedCount + 1),
            Version = checked(run.Version + 1)
        };
        state.Points[key] = current with
        {
            NextSourceSequence = transition.NextSourceSequence,
            PrngState = transition.ResultingPrngState.ToArray(),
            NextDueAtUtc = transition.NextDueAtUtc,
            Version = checked(current.Version + 1)
        };
    }

    public Task StageFinalCounterAsync(Guid runId, long expectedRunVersion,
        ProductionFinalClassification classification, ISimulatorRunTransaction transaction,
        CancellationToken ct = default)
    {
        var state = Workspace(transaction);
        var run = state.Runs.GetValueOrDefault(runId) ?? throw new InvalidOperationException("RUN_NOT_FOUND");
        if (run.Version != expectedRunVersion) throw new InvalidOperationException("VERSION_CONFLICT");
        state.Runs[runId] = classification switch
        {
            ProductionFinalClassification.Accepted => run with
            {
                AcceptedCount = checked(run.AcceptedCount + 1),
                Version = checked(run.Version + 1)
            },
            ProductionFinalClassification.Rejected => run with
            {
                RejectedCount = checked(run.RejectedCount + 1),
                Version = checked(run.Version + 1)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(classification))
        };
        return Task.CompletedTask;
    }

    public Task<SimulatorProductionAttempt?> GetPendingAsync(Guid runId, Guid pointId,
        CancellationToken ct = default) =>
        Task.FromResult(_committed.Attempts.Values
            .Where(attempt => attempt.RunId == runId && attempt.PointId == pointId &&
                attempt.Status == SimulatorProductionAttemptStatus.Pending)
            .OrderBy(attempt => attempt.SourceSequence).Select(Clone).FirstOrDefault());

    public Task<SimulatorProductionAttempt?> GetAsync(Guid runId, Guid pointId, long sourceSequence,
        CancellationToken ct = default) =>
        Task.FromResult(_committed.Attempts.GetValueOrDefault((runId, pointId, sourceSequence)) is { } attempt
            ? Clone(attempt) : null);

    public Task<IReadOnlyList<SimulatorProductionAttempt>> ListPendingAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SimulatorProductionAttempt>>(_committed.Attempts.Values
            .Where(attempt => attempt.Status == SimulatorProductionAttemptStatus.Pending)
            .OrderBy(attempt => attempt.RunId).ThenBy(attempt => attempt.PointId)
            .ThenBy(attempt => attempt.SourceSequence).Select(Clone).ToList());

    public Task<bool> TryReserveAsync(
        SimulatorProductionAttempt attempt,
        SimulatorRunPointReservationTransition transition,
        ISimulatorRunTransaction transaction, CancellationToken ct = default)
    {
        var key = (attempt.RunId, attempt.PointId, attempt.SourceSequence);
        if (SimulateReserveUniquenessRace)
        {
            SimulateReserveUniquenessRace = false;
            var winner = Clone(_committed);
            if (!winner.Attempts.ContainsKey(key))
                winner.Attempts[key] = Clone(attempt);
            ApplyReservation(winner, transition);
            _committed = winner;
            return Task.FromResult(false);
        }
        var state = Workspace(transaction);
        if (state.Attempts.ContainsKey(key) ||
            state.Attempts.Values.Any(current => current.Payload.MeasurementId == attempt.Payload.MeasurementId))
            return Task.FromResult(false);
        state.Attempts.Add(key, Clone(attempt));
        return Task.FromResult(true);
    }

    public Task<AttemptFinalizeResult> FinalizeAsync(Guid runId, Guid pointId, long sourceSequence,
        TelemetryDispatchResult result, DateTime completedAtUtc, ISimulatorRunTransaction transaction,
        CancellationToken ct = default)
    {
        var state = Workspace(transaction);
        var key = (runId, pointId, sourceSequence);
        var attempt = state.Attempts.GetValueOrDefault(key)
            ?? throw new InvalidOperationException("ATTEMPT_NOT_FOUND");
        if (attempt.Status == SimulatorProductionAttemptStatus.Completed)
        {
            var same = attempt.TelemetryOutcome == result.Outcome &&
                attempt.FinalClassification == result.FinalClassification &&
                attempt.MeasurementPersisted == result.MeasurementPersisted &&
                attempt.PersistedMeasurementId == result.PersistedMeasurementId &&
                attempt.QualityCode == result.QualityCode &&
                attempt.ReasonCode == result.ReasonCode &&
                attempt.LatestAdvanced == result.LatestAdvanced &&
                attempt.ErrorCode == result.ErrorCode &&
                attempt.RejectionCode == result.RejectionCode &&
                attempt.CompletedAtUtc == completedAtUtc &&
                attempt.OriginalCorrelationId == result.OriginalCorrelationId &&
                attempt.OriginalLineageId == result.OriginalLineageId;
            if (!same) throw new InvalidOperationException("TERMINAL_RESULT_CONFLICT");
            return Task.FromResult(new AttemptFinalizeResult(Clone(attempt), false, true));
        }
        TelemetryDispatchResultValidator.EnsureValid(result);
        var completed = attempt with
        {
            Status = SimulatorProductionAttemptStatus.Completed,
            TelemetryOutcome = result.Outcome,
            FinalClassification = result.FinalClassification,
            MeasurementPersisted = result.MeasurementPersisted,
            PersistedMeasurementId = result.PersistedMeasurementId,
            QualityCode = result.QualityCode,
            ReasonCode = result.ReasonCode,
            LatestAdvanced = result.LatestAdvanced,
            ErrorCode = result.ErrorCode,
            RejectionCode = result.RejectionCode,
            CompletedAtUtc = completedAtUtc,
            OriginalCorrelationId = result.OriginalCorrelationId,
            OriginalLineageId = result.OriginalLineageId,
            Version = checked(attempt.Version + 1)
        };
        state.Attempts[key] = completed;
        return Task.FromResult(new AttemptFinalizeResult(Clone(completed), true, false));
    }

    public ValueTask StageAsync(SimulatorRunOwnerEvent ownerEvent,
        ISimulatorRunTransaction transaction, CancellationToken ct = default)
    {
        Workspace(transaction).Events.Add(Clone(ownerEvent));
        return ValueTask.CompletedTask;
    }

    public void Seed(SimulatorRun run, params SimulatorRunPointState[] points)
    {
        _committed.Runs[run.RunId] = Clone(run);
        foreach (var point in points)
            _committed.Points[(point.RunId, point.PointId)] = Clone(point);
    }

    public void SeedAttempt(SimulatorProductionAttempt attempt) =>
        _committed.Attempts[(attempt.RunId, attempt.PointId, attempt.SourceSequence)] = Clone(attempt);

    public Task AttemptPinnedMutationAsync(
        SimulatorRunPointState replacement,
        ISimulatorRunTransaction transaction)
    {
        var current = Workspace(transaction).Points.GetValueOrDefault(
            (replacement.RunId, replacement.PointId));
        if (current is null ||
            current.RunId != replacement.RunId ||
            current.PointId != replacement.PointId ||
            current.PointVersionAtStart != replacement.PointVersionAtStart ||
            current.MappingId != replacement.MappingId ||
            current.MappingVersion != replacement.MappingVersion ||
            current.MetricId != replacement.MetricId ||
            current.UnitId != replacement.UnitId ||
            current.UnitCode != replacement.UnitCode ||
            current.SourceVersion != replacement.SourceVersion ||
            current.SiteId != replacement.SiteId ||
            current.AreaId != replacement.AreaId)
            throw new InvalidOperationException("PINNED_STATE_IMMUTABLE");
        return Task.CompletedTask;
    }

    public Task AttemptPayloadMutationAsync(
        SimulatorProductionAttempt replacement,
        ISimulatorRunTransaction transaction)
    {
        var current = Workspace(transaction).Attempts.GetValueOrDefault(
            (replacement.RunId, replacement.PointId, replacement.SourceSequence));
        if (current is null ||
            current.RunId != replacement.RunId ||
            current.PointId != replacement.PointId ||
            current.SourceSequence != replacement.SourceSequence ||
            current.Payload != replacement.Payload ||
            current.CreatedAtUtc != replacement.CreatedAtUtc)
            throw new InvalidOperationException("ATTEMPT_PAYLOAD_IMMUTABLE");
        return Task.CompletedTask;
    }

    private State Workspace(ISimulatorRunTransaction transaction)
    {
        if (transaction is not FakeSimulatorRunTransaction fake || fake.Owner != this || fake.IsFinished)
            throw new InvalidOperationException("TRANSACTION_REQUIRED");
        return fake.Workspace;
    }

    private void Commit(FakeSimulatorRunTransaction transaction)
    {
        if (FailNextCommit)
        {
            FailNextCommit = false;
            throw new InvalidOperationException("COMMIT_FAILED");
        }
        _committed = Clone(transaction.Workspace);
    }

    private sealed class State
    {
        public Dictionary<Guid, SimulatorRun> Runs { get; } = new();
        public Dictionary<(Guid RunId, Guid PointId), SimulatorRunPointState> Points { get; } = new();
        public Dictionary<(Guid RunId, Guid PointId, long Sequence), SimulatorProductionAttempt> Attempts { get; } = new();
        public List<SimulatorRunOwnerEvent> Events { get; } = new();
    }

    private static State Clone(State source)
    {
        var clone = new State();
        foreach (var (key, value) in source.Runs) clone.Runs[key] = Clone(value);
        foreach (var (key, value) in source.Points) clone.Points[key] = Clone(value);
        foreach (var (key, value) in source.Attempts) clone.Attempts[key] = Clone(value);
        clone.Events.AddRange(source.Events.Select(Clone));
        return clone;
    }

    private static SimulatorRun Clone(SimulatorRun run) => run with { };
    private static SimulatorRunPointState Clone(SimulatorRunPointState point) =>
        point with { PrngState = point.PrngState.ToArray() };
    private static SimulatorProductionAttempt Clone(SimulatorProductionAttempt attempt) =>
        attempt with { Payload = attempt.Payload with { } };
    private static SimulatorRunOwnerEvent Clone(SimulatorRunOwnerEvent ownerEvent) =>
        ownerEvent with
        {
            SiteIds = ownerEvent.SiteIds.ToList(),
            Before = new Dictionary<string, object?>(ownerEvent.Before, StringComparer.Ordinal),
            After = new Dictionary<string, object?>(ownerEvent.After, StringComparer.Ordinal)
        };

    private sealed class FakeSimulatorRunTransaction : ISimulatorRunTransaction
    {
        private readonly List<SimulatorStartLock> _locks = new();
        private SimulatorStartLockTarget? _lastTarget;
        public FakeSimulatorRunTransaction(FakeAcquisitionRunRepositories owner, State workspace)
        {
            Owner = owner;
            Workspace = workspace;
            TransactionId = Guid.NewGuid();
        }

        public FakeAcquisitionRunRepositories Owner { get; }
        public State Workspace { get; }
        public bool IsFinished { get; private set; }
        public Guid TransactionId { get; }
        public string IsolationIntent => "REPEATABLE READ";
        public bool IsCompleted => IsFinished;
        public IReadOnlyList<SimulatorStartLock> LockTrace => _locks.AsReadOnly();

        public ValueTask LockAsync(SimulatorStartLockTarget target, string key,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (IsFinished) throw new InvalidOperationException("TRANSACTION_COMPLETED");
            if (_lastTarget is { } last && target < last)
                throw new InvalidOperationException("LOCK_ORDER_VIOLATION");
            if (_locks.Any(item => item.Target == target && item.Key == key))
                throw new InvalidOperationException("LOCK_ORDER_VIOLATION");
            _locks.Add(new SimulatorStartLock(target, key));
            _lastTarget = target;
            return ValueTask.CompletedTask;
        }

        public ValueTask CommitAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (IsFinished) return ValueTask.CompletedTask;
            Owner.Commit(this);
            IsFinished = true;
            Owner._active = null;
            return ValueTask.CompletedTask;
        }

        public ValueTask RollbackAsync(CancellationToken ct = default)
        {
            if (IsFinished) return ValueTask.CompletedTask;
            IsFinished = true;
            Owner._active = null;
            return ValueTask.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (!IsFinished) await RollbackAsync(CancellationToken.None);
        }
    }
}
