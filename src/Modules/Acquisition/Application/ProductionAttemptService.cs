using IUMP.Modules.Acquisition.Contracts;

namespace IUMP.Modules.Acquisition.Application;

public sealed class ProductionAttemptService : IProductionAttemptService
{
    private readonly IAcquisitionRunRepository _runs;
    private readonly ISimulatorProductionAttemptRepository _attempts;
    private readonly IAcquisitionConfigurationRepository _configurations;
    private readonly ISimulatorRunUnitOfWork _unitOfWork;
    private readonly ISimulatorValueGenerator _generator;
    private readonly IMeasurementIdentityFactory _identities;
    private readonly IUtcClock _clock;

    public ProductionAttemptService(
        IAcquisitionRunRepository runs,
        ISimulatorProductionAttemptRepository attempts,
        IAcquisitionConfigurationRepository configurations,
        ISimulatorRunUnitOfWork unitOfWork,
        ISimulatorValueGenerator generator,
        IMeasurementIdentityFactory identities,
        IUtcClock clock)
    {
        _runs = runs;
        _attempts = attempts;
        _configurations = configurations;
        _unitOfWork = unitOfWork;
        _generator = generator;
        _identities = identities;
        _clock = clock;
    }

    public Task<SimulatorProductionAttempt?> LoadPendingAsync(
        Guid runId,
        Guid pointId,
        CancellationToken ct = default) =>
        _attempts.GetPendingAsync(runId, pointId, ct);

    public async Task<AttemptReserveResult> ReserveAsync(
        Guid runId,
        Guid pointId,
        string correlationId,
        string lineageId,
        CancellationToken ct = default)
    {
        var existing = await _attempts.GetPendingAsync(runId, pointId, ct);
        if (existing is not null)
            return new AttemptReserveResult(existing, true, false);

        var run = await _runs.GetAsync(runId, ct) ?? throw new InvalidOperationException("RUN_NOT_FOUND");
        if (run.Status != SimulatorRunStatus.Running)
            throw new InvalidOperationException("RUN_NOT_RUNNING");
        var point = await _runs.GetPointStateAsync(runId, pointId, ct)
            ?? throw new InvalidOperationException("RUN_POINT_NOT_FOUND");
        var configuration = await _configurations.GetVersionAsync(run.ConfigurationId,
            run.ConfigurationVersion, ct) ?? throw new InvalidOperationException("CONFIGURATION_NOT_FOUND");
        var generated = _generator.Generate(point.PrngState, configuration.ScenarioType,
            configuration.MinimumValue, configuration.MaximumValue);
        var now = _clock.UtcNow;
        var sequence = point.NextSourceSequence;
        var measurementId = _identities.Create(run.SourceId, run.RunId, point.PointId,
            point.MappingId, sequence, run.AlgorithmVersion);
        var payload = new SimulatorProductionPayload(
            measurementId, run.SourceId, run.RunId, point.PointId, point.MappingId,
            point.MappingVersion, sequence, run.AlgorithmId, run.AlgorithmVersion,
            run.ConfigurationId, run.ConfigurationVersion, now, generated.Value, point.UnitCode,
            "IUMP.Worker.Simulator", correlationId, lineageId);
        var attempt = new SimulatorProductionAttempt(
            run.RunId, point.PointId, sequence, payload, SimulatorProductionAttemptStatus.Pending,
            null, null, null, null, null, now, null, 1);
        var transition = new SimulatorRunPointReservationTransition(
            run.RunId,
            point.PointId,
            run.Version,
            point.Version,
            sequence,
            generated.State.ToArray(),
            checked(sequence + 1),
            now.AddSeconds(configuration.IntervalSeconds));

        await using var tx = await _unitOfWork.BeginAsync(ct);
        try
        {
            await tx.LockAsync(SimulatorStartLockTarget.AcquisitionRun,
                $"{run.RunId:D}/{point.PointId:D}", ct);
            if (!await _attempts.TryReserveAsync(attempt, transition, tx, ct))
            {
                await tx.RollbackAsync(CancellationToken.None);
                var winner = await _attempts.GetAsync(runId, pointId, sequence, ct)
                    ?? throw new InvalidOperationException("RESERVATION_WINNER_NOT_FOUND");
                return new AttemptReserveResult(winner, false, true);
            }
            await _runs.StageReservationAsync(transition, tx, ct);
            await tx.CommitAsync(ct);
            return new AttemptReserveResult(attempt, false, false);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<AttemptFinalizeResult> FinalizeAsync(
        Guid runId,
        Guid pointId,
        long sourceSequence,
        TelemetryDispatchResult result,
        CancellationToken ct = default)
    {
        TelemetryDispatchResultValidator.EnsureValid(result);
        var run = await _runs.GetAsync(runId, ct) ?? throw new InvalidOperationException("RUN_NOT_FOUND");
        await using var tx = await _unitOfWork.BeginAsync(ct);
        try
        {
            await tx.LockAsync(SimulatorStartLockTarget.AcquisitionRun,
                $"{runId:D}/{pointId:D}/{sourceSequence}", ct);
            var finalized = await _attempts.FinalizeAsync(runId, pointId, sourceSequence, result,
                _clock.UtcNow, tx, ct);
            if (finalized.FirstTransition)
                await _runs.StageFinalCounterAsync(runId, run.Version, result.FinalClassification, tx, ct);
            await tx.CommitAsync(ct);
            return finalized;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}

public sealed class SimulatorProductionCoordinator : ISimulatorProductionCoordinator
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    private static readonly HashSet<string> OwnerErrorCodes = new(StringComparer.Ordinal)
    {
        "SOURCE_INACTIVE",
        "MAPPING_INACTIVE",
        "POINT_INACTIVE",
        "ANCESTOR_INACTIVE"
    };
    private static readonly RunCallerSnapshot WorkerActor =
        new("iump-worker", "IUMP Worker", true, ["System"], []);
    private readonly IAcquisitionRunRepository _runs;
    private readonly ISimulatorRunUnitOfWork _unitOfWork;
    private readonly ISimulatorRunOwnerEventWriter _events;
    private readonly IProductionAttemptService _attempts;
    private readonly ITelemetryIngestionClient _telemetry;
    private readonly ISimulatorProductionEligibility _eligibility;
    private readonly IUtcClock _clock;
    private readonly TimeSpan _leaseRenewalInterval;

    public SimulatorProductionCoordinator(
        IAcquisitionRunRepository runs,
        ISimulatorRunUnitOfWork unitOfWork,
        ISimulatorRunOwnerEventWriter events,
        IProductionAttemptService attempts,
        ITelemetryIngestionClient telemetry,
        ISimulatorProductionEligibility eligibility,
        IUtcClock clock,
        TimeSpan? leaseRenewalInterval = null)
    {
        _runs = runs;
        _unitOfWork = unitOfWork;
        _events = events;
        _attempts = attempts;
        _telemetry = telemetry;
        _eligibility = eligibility;
        _clock = clock;
        _leaseRenewalInterval = leaseRenewalInterval ?? TimeSpan.FromSeconds(10);
        if (_leaseRenewalInterval <= TimeSpan.Zero ||
            _leaseRenewalInterval >= LeaseDuration)
            throw new ArgumentOutOfRangeException(
                nameof(leaseRenewalInterval),
                "Lease renewal interval must be positive and shorter than the lease duration.");
    }

    public async Task<SimulatorProductionCycleResult> RunOnceAsync(
        string workerId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            throw new ArgumentException("WorkerId is required.", nameof(workerId));
        var runs = (await _runs.ListRunningAsync(ct)).OrderBy(run => run.RunId).ToList();
        var claimed = 0;
        var dispatched = 0;
        var finalized = 0;
        var failures = new List<SimulatorProductionFailure>();

        foreach (var run in runs)
        {
            var points = (await _runs.ListPointStatesAsync(run.RunId, ct))
                .OrderBy(point => point.NextDueAtUtc)
                .ThenBy(point => point.PointId)
                .ToList();
            foreach (var point in points)
            {
                if (ct.IsCancellationRequested) break;
                var now = _clock.UtcNow;
                if (point.NextDueAtUtc > now) continue;
                var lease = await _runs.ClaimDuePointAsync(
                    run.RunId, point.PointId, workerId, now, now.Add(LeaseDuration), ct);
                if (lease is null) continue;
                claimed++;
                var leaseGuard = new LeaseGuard(lease);
                using var heartbeatCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(ct);
                var heartbeat = MaintainLeaseAsync(leaseGuard, heartbeatCancellation.Token);
                try
                {
                    var pending = await _attempts.LoadPendingAsync(run.RunId, point.PointId, ct);
                    AttemptReserveResult reservation;
                    if (pending is not null)
                    {
                        reservation = new AttemptReserveResult(pending, true, false);
                    }
                    else
                    {
                        var eligibility = await _eligibility.IsPinnedInputActiveAsync(run, point, ct);
                        if (!eligibility.IsActive)
                        {
                            var ownerCode = NormalizeOwnerErrorCode(eligibility.ErrorCode);
                            await StopForOwnerDriftAsync(run.RunId, ownerCode, ct);
                            failures.Add(new SimulatorProductionFailure(
                                run.RunId, point.PointId, run.CorrelationId, ownerCode));
                            continue;
                        }

                        reservation = await _attempts.ReserveAsync(
                            run.RunId, point.PointId, run.CorrelationId,
                            $"{run.RunId:D}:{point.PointId:D}", ct);
                    }
                    var result = await _telemetry.DispatchAsync(reservation.Attempt.Payload, ct);
                    dispatched++;
                    if (leaseGuard.IsLost)
                        throw new InvalidOperationException("LEASE_LOST");
                    await _attempts.FinalizeAsync(
                        run.RunId, point.PointId, reservation.Attempt.SourceSequence, result, ct);
                    finalized++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (InvalidOperationException ex) when (ex.Message == "LEASE_LOST")
                {
                    failures.Add(new SimulatorProductionFailure(
                        run.RunId, point.PointId, run.CorrelationId, "LEASE_LOST"));
                }
                catch
                {
                    failures.Add(new SimulatorProductionFailure(
                        run.RunId, point.PointId, run.CorrelationId,
                        "PRODUCTION_POINT_FAILED"));
                }
                finally
                {
                    heartbeatCancellation.Cancel();
                    await heartbeat;
                    await _runs.ReleaseLeaseAsync(
                        leaseGuard.Current, CancellationToken.None);
                }
            }
            if (ct.IsCancellationRequested) break;
        }

        return new SimulatorProductionCycleResult(
            runs.Count, claimed, dispatched, finalized, failures.Count, failures);
    }

    private async Task StopForOwnerDriftAsync(
        Guid runId,
        string errorCode,
        CancellationToken ct)
    {
        var run = await _runs.GetAsync(runId, ct)
            ?? throw new InvalidOperationException("RUN_NOT_FOUND");
        if (run.Status == SimulatorRunStatus.Stopped) return;
        var points = await _runs.ListPointStatesAsync(runId, ct);
        var now = _clock.UtcNow;
        await using var tx = await _unitOfWork.BeginAsync(ct);
        try
        {
            await tx.LockAsync(
                SimulatorStartLockTarget.AcquisitionRun, runId.ToString("D"), ct);
            var stopped = await _runs.ChangeStatusAsync(
                runId, run.Version, SimulatorRunStatus.Stopped, now, errorCode,
                "Pinned owner state is no longer Active.", tx, ct);
            await tx.LockAsync(
                SimulatorStartLockTarget.IntegrationOutbox, runId.ToString("D"), ct);
            await _events.StageAsync(SimulatorRunEventFactory.Create(
                run, stopped, points.Select(point => point.SiteId), WorkerActor, "Stop", now,
                run.CorrelationId, run.CausationId), tx, ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static string NormalizeOwnerErrorCode(string? errorCode) =>
        errorCode is not null && OwnerErrorCodes.Contains(errorCode)
            ? errorCode
            : "ANCESTOR_INACTIVE";

    private async Task MaintainLeaseAsync(
        LeaseGuard guard,
        CancellationToken ct)
    {
        try
        {
            while (true)
            {
                await Task.Delay(_leaseRenewalInterval, ct);
                var renewed = await _runs.RenewLeaseAsync(
                    guard.Current, _clock.UtcNow.Add(LeaseDuration), ct);
                if (renewed is null)
                {
                    guard.MarkLost();
                    return;
                }
                guard.Update(renewed);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch
        {
            guard.MarkLost();
        }
    }

    private sealed class LeaseGuard
    {
        private readonly object _sync = new();
        private SimulatorRunLease _current;
        private bool _isLost;

        public LeaseGuard(SimulatorRunLease current) => _current = current;

        public SimulatorRunLease Current
        {
            get
            {
                lock (_sync) return _current;
            }
        }

        public bool IsLost
        {
            get
            {
                lock (_sync) return _isLost;
            }
        }

        public void Update(SimulatorRunLease renewed)
        {
            lock (_sync) _current = renewed;
        }

        public void MarkLost()
        {
            lock (_sync) _isLost = true;
        }
    }
}
