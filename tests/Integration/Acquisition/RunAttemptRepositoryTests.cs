using IUMP.Modules.Acquisition.Contracts;

namespace IUMP.Tests.Integration.Acquisition;

public interface IRunAttemptRepositoryTestProviderFactory
{
    IRunAttemptRepositoryTestProvider Create();
}

public interface IRunAttemptRepositoryTestProvider
{
    IAcquisitionRunRepository Runs { get; }
    ISimulatorProductionAttemptRepository Attempts { get; }
    ISimulatorRunUnitOfWork UnitOfWork { get; }
    void FailNextCommit();
    void SimulateReserveUniquenessRace();
    Task AttemptPinnedMutationAsync(
        SimulatorRunPointState replacement,
        ISimulatorRunTransaction transaction);
    Task AttemptPayloadMutationAsync(
        SimulatorProductionAttempt replacement,
        ISimulatorRunTransaction transaction);
}

public sealed class RunAttemptRepositoryContractRunner
{
    private static readonly DateTime Now =
        new(2026, 7, 28, 3, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SourceId =
        Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid ConfigurationId =
        Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
    private static readonly Guid PointId =
        Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly Guid MappingId =
        Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private readonly IRunAttemptRepositoryTestProviderFactory _factory;
    private readonly List<string> _failures = new();

    public RunAttemptRepositoryContractRunner(IRunAttemptRepositoryTestProviderFactory factory) =>
        _factory = factory;

    public IReadOnlyList<string> Failures => _failures;
    public int TestCount { get; private set; }
    public int AssertionCount { get; private set; }

    public async Task RunAllAsync()
    {
        await CreatePinnedStateAndRollbackAsync();
        await StatusVersionAndRecoveryAsync();
        await LeaseLifecycleAsync();
        await ReservationAtomicityAndRollbackAsync();
        await ReservationRaceAsync();
        await PinnedStateAndOptimisticConflictAsync();
        await AcceptedFinalizeReplayConflictAsync();
        await RejectedAndDuplicateClassificationAsync();
        await InvalidTerminalResultsAsync();
        await CommitFailureIsAtomicAsync();
        await FinalizationCommitFailureAsync();
    }

    private async Task CreatePinnedStateAndRollbackAsync()
    {
        var provider = _factory.Create();
        var runId = Guid.Parse("10000000-0000-4000-8000-000000000001");
        var run = Run(runId);
        var point = Point(runId);
        await CreateAsync(provider, run, point);
        TestCount++;
        Assert((await provider.Runs.GetAsync(runId)) == run,
            "created Run is readable through the public port");
        var storedPoints = await provider.Runs.ListPointStatesAsync(runId);
        Assert(storedPoints.Count == 1 &&
               storedPoints[0].MappingId == MappingId &&
               storedPoints[0].ConfigurationSnapshotEquivalent(point),
            "pinned Run-Point state is complete and deep-copied");
        storedPoints[0].PrngState[0] ^= 0xff;
        Assert((await provider.Runs.GetPointStateAsync(runId, PointId))!.PrngState[0] == 0,
            "read models cannot mutate committed PRNG state");

        var rollbackRunId = Guid.Parse("10000000-0000-4000-8000-000000000002");
        await using var rollback = await provider.UnitOfWork.BeginAsync();
        await provider.Runs.CreateAsync(Run(rollbackRunId), [Point(rollbackRunId)], rollback);
        await rollback.RollbackAsync();
        Assert(await provider.Runs.GetAsync(rollbackRunId) is null,
            "Run plus pinned states roll back atomically");
    }

    private async Task StatusVersionAndRecoveryAsync()
    {
        var provider = _factory.Create();
        var runningId = Guid.Parse("20000000-0000-4000-8000-000000000001");
        var pausedId = Guid.Parse("20000000-0000-4000-8000-000000000002");
        var stoppedId = Guid.Parse("20000000-0000-4000-8000-000000000003");
        await CreateAsync(provider, Run(runningId), Point(runningId));
        await CreateAsync(provider, Run(pausedId), Point(pausedId));
        await ChangeStatusAsync(provider, pausedId, 1, SimulatorRunStatus.Paused);
        await CreateAsync(provider, Run(stoppedId), Point(stoppedId));
        await ChangeStatusAsync(provider, stoppedId, 1, SimulatorRunStatus.Stopped);
        TestCount++;
        var running = await provider.Runs.ListRunningAsync();
        Assert(running.Count == 1 && running[0].RunId == runningId,
            "restart recovery includes Running and excludes Paused/Stopped");
        Assert((await provider.Runs.GetAsync(pausedId))?.Version == 2,
            "accepted status transition advances optimistic version");
        var stale = false;
        await using var tx = await provider.UnitOfWork.BeginAsync();
        try
        {
            await provider.Runs.ChangeStatusAsync(
                pausedId, 1, SimulatorRunStatus.Running, Now, null, null, tx);
        }
        catch (InvalidOperationException ex)
        {
            stale = ex.Message == "VERSION_CONFLICT";
        }
        await tx.RollbackAsync();
        Assert(stale && (await provider.Runs.GetAsync(pausedId))?.Status ==
               SimulatorRunStatus.Paused,
            "stale ExpectedVersion fails without status mutation");
    }

    private async Task LeaseLifecycleAsync()
    {
        var provider = _factory.Create();
        var runId = Guid.Parse("30000000-0000-4000-8000-000000000001");
        await CreateAsync(provider, Run(runId), Point(runId));
        TestCount++;
        var first = await provider.Runs.ClaimDuePointAsync(
            runId, PointId, "worker-a", Now, Now.AddSeconds(30));
        Assert(first is not null, "due Running Run-Point can be claimed");
        Assert(await provider.Runs.ClaimDuePointAsync(
            runId, PointId, "worker-b", Now.AddSeconds(10), Now.AddSeconds(40)) is null,
            "unexpired lease cannot be stolen");
        var renewed = await provider.Runs.RenewLeaseAsync(first!, Now.AddSeconds(45));
        Assert(renewed is not null && renewed.Version > first!.Version,
            "lease owner/token/version can renew");
        var reclaimed = await provider.Runs.ClaimDuePointAsync(
            runId, PointId, "worker-b", Now.AddSeconds(46), Now.AddSeconds(76));
        Assert(reclaimed is not null && reclaimed.Owner == "worker-b" &&
               reclaimed.Token != renewed!.Token,
            "expired lease is reclaimable with a fresh token");
        await provider.Runs.ReleaseLeaseAsync(reclaimed!);
        Assert((await provider.Runs.GetPointStateAsync(runId, PointId))?.LeaseOwner is null,
            "lease release clears owner and token");
    }

    private async Task ReservationAtomicityAndRollbackAsync()
    {
        var provider = _factory.Create();
        var runId = Guid.Parse("40000000-0000-4000-8000-000000000001");
        var originalPoint = Point(runId);
        await CreateAsync(provider, Run(runId), originalPoint);
        var attempt = Pending(runId, 0, Guid.Parse("40000000-0000-5000-8000-000000000001"));
        var reservationTransition =
            Transition(originalPoint, Enumerable.Repeat((byte)7, 25).ToArray());
        await using (var tx = await provider.UnitOfWork.BeginAsync())
        {
            Assert(await provider.Attempts.TryReserveAsync(attempt, reservationTransition, tx),
                "new unique attempt reservation wins");
            await provider.Runs.StageReservationAsync(reservationTransition, tx);
            await tx.CommitAsync();
        }
        TestCount++;
        Assert((await provider.Attempts.GetPendingAsync(runId, PointId))?.Payload ==
               attempt.Payload, "existing Pending lookup returns immutable payload");
        Assert((await provider.Runs.GetAsync(runId))?.GeneratedCount == 1 &&
               (await provider.Runs.GetPointStateAsync(runId, PointId))?.NextSourceSequence == 1,
            "attempt, PRNG, cursor and Generated publish atomically");
        var payloadMutationRejected = false;
        await using (var immutableTx = await provider.UnitOfWork.BeginAsync())
        {
            try
            {
                await provider.AttemptPayloadMutationAsync(
                    attempt with
                    {
                        Payload = attempt.Payload with
                        {
                            MeasurementId = Guid.NewGuid(),
                            CorrelationId = "mutated"
                        }
                    },
                    immutableTx);
            }
            catch (InvalidOperationException ex)
            {
                payloadMutationRejected = ex.Message == "ATTEMPT_PAYLOAD_IMMUTABLE";
            }
            await immutableTx.RollbackAsync();
        }
        TestCount++;
        Assert(payloadMutationRejected &&
               (await provider.Attempts.GetAsync(runId, PointId, 0))?.Payload == attempt.Payload,
            "attempt payload mutation is rejected without committed change");

        var rollbackProvider = _factory.Create();
        var rollbackRunId = Guid.Parse("40000000-0000-4000-8000-000000000002");
        var rollbackPoint = Point(rollbackRunId);
        await CreateAsync(rollbackProvider, Run(rollbackRunId), rollbackPoint);
        var rollbackTransition =
            Transition(rollbackPoint, Enumerable.Repeat((byte)9, 25).ToArray());
        await using (var tx = await rollbackProvider.UnitOfWork.BeginAsync())
        {
            await rollbackProvider.Attempts.TryReserveAsync(
                Pending(rollbackRunId, 0,
                    Guid.Parse("40000000-0000-5000-8000-000000000002")),
                rollbackTransition, tx);
            await rollbackProvider.Runs.StageReservationAsync(rollbackTransition, tx);
            await tx.RollbackAsync();
        }
        Assert(await rollbackProvider.Attempts.GetPendingAsync(rollbackRunId, PointId) is null &&
               (await rollbackProvider.Runs.GetAsync(rollbackRunId))?.GeneratedCount == 0 &&
               (await rollbackProvider.Runs.GetPointStateAsync(rollbackRunId, PointId))?
               .NextSourceSequence == 0,
            "reserve rollback publishes no attempt/state/cursor/counter changes");
    }

    private async Task ReservationRaceAsync()
    {
        var provider = _factory.Create();
        var runId = Guid.Parse("50000000-0000-4000-8000-000000000001");
        await CreateAsync(provider, Run(runId), Point(runId));
        var winner = Pending(
            runId, 0, Guid.Parse("50000000-0000-5000-8000-000000000001"));
        var winnerState = Enumerable.Repeat((byte)5, 25).ToArray();
        var winnerTransition =
            Transition(Point(runId), winnerState, Now.AddSeconds(60));
        provider.SimulateReserveUniquenessRace();
        await using var tx = await provider.UnitOfWork.BeginAsync();
        var won = await provider.Attempts.TryReserveAsync(winner, winnerTransition, tx);
        await tx.RollbackAsync();
        TestCount++;
        Assert(!won, "uniqueness race reports local reservation loss");
        Assert((await provider.Attempts.GetAsync(runId, PointId, 0))?.Payload ==
               winner.Payload, "race winner is reloadable by exact slot");
        var committedRun = await provider.Runs.GetAsync(runId);
        var committedPoint = await provider.Runs.GetPointStateAsync(runId, PointId);
        Assert(committedRun is { GeneratedCount: 1, Version: 2 } &&
               committedPoint is { NextSourceSequence: 1, Version: 2 } &&
               committedPoint.PrngState.SequenceEqual(winnerState) &&
               committedPoint.NextDueAtUtc == Now.AddSeconds(60),
            "committed race winner atomically publishes Pending, PRNG, cursor, due time and Generated");
        var accepted = await FinalizeAsync(provider, runId,
            new TelemetryDispatchResult(
                TelemetryAttemptOutcome.Accepted, ProductionFinalClassification.Accepted,
                true, true, null, null));
        Assert(accepted.FirstTransition &&
               (await provider.Runs.GetAsync(runId)) is
               { GeneratedCount: 1, AcceptedCount: 1, RejectedCount: 0 },
            "race loser performs no second advancement and winner finalizes within counter constraints");
    }

    private async Task PinnedStateAndOptimisticConflictAsync()
    {
        var provider = _factory.Create();
        var runId = Guid.Parse("51000000-0000-4000-8000-000000000001");
        var original = Point(runId);
        await CreateAsync(provider, Run(runId), original);
        var pinnedMutations = new Func<SimulatorRunPointState, SimulatorRunPointState>[]
        {
            value => value with { RunId = Guid.NewGuid() },
            value => value with { PointId = Guid.NewGuid() },
            value => value with { PointVersionAtStart = 99 },
            value => value with { MappingId = Guid.NewGuid() },
            value => value with { MappingVersion = 99 },
            value => value with { MetricId = Guid.NewGuid() },
            value => value with { UnitId = Guid.NewGuid() },
            value => value with { UnitCode = "MUTATED" },
            value => value with { SourceVersion = 99 },
            value => value with { SiteId = "mutated-site" },
            value => value with { AreaId = "mutated-area" }
        };
        var transitionProperties = typeof(SimulatorRunPointReservationTransition)
            .GetProperties().Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        var pinnedProperties = new[]
        {
            nameof(SimulatorRunPointState.PointVersionAtStart),
            nameof(SimulatorRunPointState.MappingId),
            nameof(SimulatorRunPointState.MappingVersion),
            nameof(SimulatorRunPointState.MetricId),
            nameof(SimulatorRunPointState.UnitId),
            nameof(SimulatorRunPointState.UnitCode),
            nameof(SimulatorRunPointState.SourceVersion),
            nameof(SimulatorRunPointState.SiteId),
            nameof(SimulatorRunPointState.AreaId)
        };
        TestCount++;
        Assert(pinnedProperties.All(property => !transitionProperties.Contains(property)),
            "reservation transition exposes none of the pinned update fields");
        foreach (var mutate in pinnedMutations)
        {
            var rejected = false;
            await using var tx = await provider.UnitOfWork.BeginAsync();
            try
            {
                await provider.AttemptPinnedMutationAsync(mutate(original), tx);
            }
            catch (InvalidOperationException ex)
            {
                rejected = ex.Message == "PINNED_STATE_IMMUTABLE";
            }
            await tx.RollbackAsync();
            TestCount++;
            Assert(rejected &&
                   (await provider.Runs.GetPointStateAsync(runId, PointId))!
                   .PinnedSnapshotEquivalent(original),
                "each pinned Run-Point field mutation is rejected without committed change");
        }
        await using (var tx = await provider.UnitOfWork.BeginAsync())
        {
            await provider.Runs.StageReservationAsync(
                Transition(original, Enumerable.Repeat((byte)6, 25).ToArray()), tx);
            await tx.CommitAsync();
        }
        TestCount++;
        var committed = await provider.Runs.GetPointStateAsync(runId, PointId)
            ?? throw new InvalidOperationException("TEST_RUN_POINT_NOT_FOUND");
        Assert(committed.PinnedSnapshotEquivalent(original),
            "reservation transition cannot supply or mutate any pinned Run-Point field");

        var conflict = false;
        TestCount++;
        await using (var tx = await provider.UnitOfWork.BeginAsync())
        {
            try
            {
                await provider.Runs.StageReservationAsync(
                    Transition(committed, new byte[25]) with
                    {
                        ExpectedRunVersion = 2,
                        ExpectedPointStateVersion = 1
                    }, tx);
            }
            catch (InvalidOperationException ex)
            {
                conflict = ex.Message == "RUN_POINT_VERSION_CONFLICT";
            }
            await tx.RollbackAsync();
        }
        Assert(conflict &&
               (await provider.Runs.GetPointStateAsync(runId, PointId))!.Version == committed.Version,
            "Run-Point optimistic version conflict rejects without committed mutation");
    }

    private async Task AcceptedFinalizeReplayConflictAsync()
    {
        var provider = _factory.Create();
        var runId = Guid.Parse("60000000-0000-4000-8000-000000000001");
        await CreateAsync(provider, Run(runId), Point(runId));
        await ReserveOnlyAsync(provider, Pending(
            runId, 0, Guid.Parse("60000000-0000-5000-8000-000000000001")));
        var accepted = new TelemetryDispatchResult(
            TelemetryAttemptOutcome.Accepted, ProductionFinalClassification.Accepted,
            true, true, null, null);
        var first = await FinalizeAsync(provider, runId, accepted);
        TestCount++;
        Assert(first.FirstTransition && !first.Replay &&
               first.Attempt.Status == SimulatorProductionAttemptStatus.Completed,
            "Pending completes on first finalization");
        Assert((await provider.Runs.GetAsync(runId))?.AcceptedCount == 1 &&
               (await provider.Runs.GetAsync(runId))?.RejectedCount == 0,
            "Accepted first transition increments only Accepted");
        TestCount++;
        var replay = await FinalizeAsync(provider, runId, accepted);
        Assert(replay.Replay && !replay.FirstTransition &&
               (await provider.Runs.GetAsync(runId))?.AcceptedCount == 1,
            "identical terminal replay is a counter no-op");
        TestCount++;
        var conflict = false;
        await using var tx = await provider.UnitOfWork.BeginAsync();
        try
        {
            await provider.Attempts.FinalizeAsync(
                runId, PointId, 0,
                new TelemetryDispatchResult(
                     TelemetryAttemptOutcome.Rejected, ProductionFinalClassification.Rejected,
                    false, false, "REJECTED", "INVALID"), Now, tx);
        }
        catch (InvalidOperationException ex)
        {
            conflict = ex.Message == "TERMINAL_RESULT_CONFLICT";
        }
        await tx.RollbackAsync();
        Assert(conflict && (await provider.Runs.GetAsync(runId))?.AcceptedCount == 1,
            "different terminal replay conflicts without mutation");
    }

    private async Task RejectedAndDuplicateClassificationAsync()
    {
        var rejectedProvider = _factory.Create();
        var rejectedRunId = Guid.Parse("70000000-0000-4000-8000-000000000001");
        await CreateAsync(rejectedProvider, Run(rejectedRunId), Point(rejectedRunId));
        await ReserveOnlyAsync(rejectedProvider, Pending(
            rejectedRunId, 0, Guid.Parse("70000000-0000-5000-8000-000000000001")));
        await FinalizeAsync(rejectedProvider, rejectedRunId,
            new TelemetryDispatchResult(
                TelemetryAttemptOutcome.Rejected, ProductionFinalClassification.Rejected,
                false, false, "REJECTED", "OUT_OF_RANGE"));
        TestCount++;
        Assert((await rejectedProvider.Runs.GetAsync(rejectedRunId))?.RejectedCount == 1 &&
               (await rejectedProvider.Runs.GetAsync(rejectedRunId))?.AcceptedCount == 0,
            "Rejected first transition increments only Rejected");

        TestCount++;
        var duplicateProvider = _factory.Create();
        var duplicateRunId = Guid.Parse("70000000-0000-4000-8000-000000000002");
        await CreateAsync(duplicateProvider, Run(duplicateRunId), Point(duplicateRunId));
        await ReserveOnlyAsync(duplicateProvider, Pending(
            duplicateRunId, 0, Guid.Parse("70000000-0000-5000-8000-000000000002")));
        var duplicate = await FinalizeAsync(duplicateProvider, duplicateRunId,
            new TelemetryDispatchResult(
                TelemetryAttemptOutcome.Duplicate, ProductionFinalClassification.Accepted,
                true, false, "DUPLICATE", null));
        Assert(duplicate.Attempt.TelemetryOutcome == TelemetryAttemptOutcome.Duplicate &&
               duplicate.Attempt.FinalClassification == ProductionFinalClassification.Accepted,
            "Duplicate preserves the original final classification");
        Assert((await duplicateProvider.Runs.GetAsync(duplicateRunId))?.AcceptedCount == 1,
            "Duplicate increments its original classification once");

        TestCount++;
        var rejectedDuplicateProvider = _factory.Create();
        var rejectedDuplicateRunId = Guid.Parse("70000000-0000-4000-8000-000000000003");
        await CreateAsync(
            rejectedDuplicateProvider, Run(rejectedDuplicateRunId), Point(rejectedDuplicateRunId));
        await ReserveOnlyAsync(rejectedDuplicateProvider, Pending(
            rejectedDuplicateRunId, 0,
            Guid.Parse("70000000-0000-5000-8000-000000000003")));
        var rejectedDuplicate = await FinalizeAsync(
            rejectedDuplicateProvider,
            rejectedDuplicateRunId,
            new TelemetryDispatchResult(
                TelemetryAttemptOutcome.Duplicate, ProductionFinalClassification.Rejected,
                false, false, "DUPLICATE", "OUT_OF_RANGE"));
        Assert(rejectedDuplicate.Attempt is
               {
                   TelemetryOutcome: TelemetryAttemptOutcome.Duplicate,
                   FinalClassification: ProductionFinalClassification.Rejected,
                   LatestAdvanced: false,
                   RejectionCode: "OUT_OF_RANGE"
               } &&
               (await rejectedDuplicateProvider.Runs.GetAsync(rejectedDuplicateRunId)) is
               { AcceptedCount: 0, RejectedCount: 1 },
            "Duplicate preserves an original Rejected terminal result and classification");
    }

    private async Task CommitFailureIsAtomicAsync()
    {
        var provider = _factory.Create();
        var runId = Guid.Parse("80000000-0000-4000-8000-000000000001");
        var point = Point(runId);
        await CreateAsync(provider, Run(runId), point);
        provider.FailNextCommit();
        var failed = false;
        await using var tx = await provider.UnitOfWork.BeginAsync();
        try
        {
            var transition = Transition(point, new byte[25]);
            await provider.Attempts.TryReserveAsync(
                Pending(runId, 0, Guid.Parse("80000000-0000-5000-8000-000000000001")),
                transition, tx);
            await provider.Runs.StageReservationAsync(transition, tx);
            await tx.CommitAsync();
        }
        catch (InvalidOperationException ex)
        {
            failed = ex.Message == "COMMIT_FAILED";
            await tx.RollbackAsync();
        }
        TestCount++;
        Assert(failed, "commit failure is surfaced");
        Assert(await provider.Attempts.GetPendingAsync(runId, PointId) is null &&
               (await provider.Runs.GetAsync(runId))?.GeneratedCount == 0 &&
               (await provider.Runs.GetPointStateAsync(runId, PointId))?.NextSourceSequence == 0,
            "failed commit has no partial publication");
    }

    private async Task InvalidTerminalResultsAsync()
    {
        var invalid = new[]
        {
            new TelemetryDispatchResult(
                TelemetryAttemptOutcome.Accepted, ProductionFinalClassification.Rejected,
                false, false, null, "INVALID"),
            new TelemetryDispatchResult(
                TelemetryAttemptOutcome.Rejected, ProductionFinalClassification.Accepted,
                false, false, null, null),
            new TelemetryDispatchResult(
                TelemetryAttemptOutcome.Rejected, ProductionFinalClassification.Rejected,
                false, true, null, "INVALID"),
            new TelemetryDispatchResult(
                TelemetryAttemptOutcome.Rejected, ProductionFinalClassification.Rejected,
                false, false, null, " "),
            new TelemetryDispatchResult(
                (TelemetryAttemptOutcome)999, ProductionFinalClassification.Accepted,
                false, false, null, null),
            new TelemetryDispatchResult(
                TelemetryAttemptOutcome.Accepted, (ProductionFinalClassification)999,
                false, false, null, null),
            new TelemetryDispatchResult(
                TelemetryAttemptOutcome.Duplicate, ProductionFinalClassification.Accepted,
                true, false, null, "UNEXPECTED"),
            new TelemetryDispatchResult(
                TelemetryAttemptOutcome.Duplicate, ProductionFinalClassification.Rejected,
                false, true, null, "OUT_OF_RANGE"),
            new TelemetryDispatchResult(
                TelemetryAttemptOutcome.Duplicate, ProductionFinalClassification.Rejected,
                false, false, null, null)
        };
        foreach (var (result, index) in invalid.Select((value, index) => (value, index)))
        {
            var provider = _factory.Create();
            var runId = Guid.Parse($"90000000-0000-4000-8000-{index + 1:D12}");
            await CreateAsync(provider, Run(runId), Point(runId));
            await ReserveOnlyAsync(provider, Pending(
                runId, 0, Guid.Parse($"90000000-0000-5000-8000-{index + 1:D12}")));
            var rejected = false;
            await using var tx = await provider.UnitOfWork.BeginAsync();
            try
            {
                await provider.Attempts.FinalizeAsync(runId, PointId, 0, result, Now, tx);
            }
            catch (InvalidOperationException ex)
            {
                rejected = ex.Message == TelemetryDispatchResultValidator.InvalidCode;
            }
            await tx.RollbackAsync();
            TestCount++;
            var unchanged = await provider.Attempts.GetAsync(runId, PointId, 0);
            Assert(rejected && unchanged is
                   {
                       Status: SimulatorProductionAttemptStatus.Pending,
                       CompletedAtUtc: null,
                       Version: 1
                   } &&
                   (await provider.Runs.GetAsync(runId)) is
                   { AcceptedCount: 0, RejectedCount: 0 },
                $"invalid terminal result {index + 1} is rejected without attempt or counter mutation");
        }
    }

    private async Task FinalizationCommitFailureAsync()
    {
        var provider = _factory.Create();
        var runId = Guid.Parse("a0000000-0000-4000-8000-000000000001");
        await CreateAsync(provider, Run(runId), Point(runId));
        await ReserveOnlyAsync(provider, Pending(
            runId, 0, Guid.Parse("a0000000-0000-5000-8000-000000000001")));
        provider.FailNextCommit();
        var failed = false;
        try
        {
            await FinalizeAsync(provider, runId,
                new TelemetryDispatchResult(
                    TelemetryAttemptOutcome.Accepted, ProductionFinalClassification.Accepted,
                    true, true, null, null));
        }
        catch (InvalidOperationException ex)
        {
            failed = ex.Message == "COMMIT_FAILED";
        }
        TestCount++;
        Assert(failed &&
               (await provider.Attempts.GetAsync(runId, PointId, 0)) is
               {
                   Status: SimulatorProductionAttemptStatus.Pending,
                   CompletedAtUtc: null,
                   Version: 1
               } &&
               (await provider.Runs.GetAsync(runId)) is
               { AcceptedCount: 0, RejectedCount: 0 },
            "finalization commit failure rolls back terminal fields, version and counters");
    }

    private static async Task CreateAsync(
        IRunAttemptRepositoryTestProvider provider,
        SimulatorRun run,
        SimulatorRunPointState point)
    {
        await using var tx = await provider.UnitOfWork.BeginAsync();
        await provider.Runs.CreateAsync(run, [point], tx);
        await tx.CommitAsync();
    }

    private static async Task ChangeStatusAsync(
        IRunAttemptRepositoryTestProvider provider,
        Guid runId,
        long expectedVersion,
        SimulatorRunStatus status)
    {
        await using var tx = await provider.UnitOfWork.BeginAsync();
        await provider.Runs.ChangeStatusAsync(
            runId, expectedVersion, status, Now, null, null, tx);
        await tx.CommitAsync();
    }

    private static async Task ReserveOnlyAsync(
        IRunAttemptRepositoryTestProvider provider,
        SimulatorProductionAttempt attempt)
    {
        var point = await provider.Runs.GetPointStateAsync(attempt.RunId, attempt.PointId)
            ?? throw new InvalidOperationException("TEST_RUN_POINT_NOT_FOUND");
        var run = await provider.Runs.GetAsync(attempt.RunId)
            ?? throw new InvalidOperationException("TEST_RUN_NOT_FOUND");
        var transition = Transition(point, Enumerable.Repeat((byte)3, 25).ToArray()) with
        {
            ExpectedRunVersion = run.Version
        };
        await using var tx = await provider.UnitOfWork.BeginAsync();
        if (!await provider.Attempts.TryReserveAsync(attempt, transition, tx))
            throw new InvalidOperationException("TEST_RESERVATION_CONFLICT");
        await provider.Runs.StageReservationAsync(transition, tx);
        await tx.CommitAsync();
    }

    private static async Task<AttemptFinalizeResult> FinalizeAsync(
        IRunAttemptRepositoryTestProvider provider,
        Guid runId,
        TelemetryDispatchResult result)
    {
        var run = await provider.Runs.GetAsync(runId)
            ?? throw new InvalidOperationException("TEST_RUN_NOT_FOUND");
        await using var tx = await provider.UnitOfWork.BeginAsync();
        var finalized = await provider.Attempts.FinalizeAsync(
            runId, PointId, 0, result, Now, tx);
        if (finalized.FirstTransition)
            await provider.Runs.StageFinalCounterAsync(
                runId, run.Version, result.FinalClassification, tx);
        await tx.CommitAsync();
        return finalized;
    }

    private void Assert(bool condition, string message)
    {
        AssertionCount++;
        if (!condition) _failures.Add($"T124: {message}.");
    }

    private static SimulatorRun Run(Guid runId) =>
        new(runId, runId, 3, ConfigurationId, 7, "IUMP-DETERMINISTIC-V1", 1,
            SimulatorRunStatus.Running, 1, 0, 0, 0, null, null, Now, Now,
            null, null, null, "runner", "runner", "corr", "cause");

    private static SimulatorRunPointState Point(Guid runId) =>
        new(runId, PointId, 5, MappingId, 4,
            Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"),
            Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"), "kW", 3, 0,
            new byte[25], Now, "site-a", "area-a", null, null, 0, null, 1);

    private static SimulatorRunPointReservationTransition Transition(
        SimulatorRunPointState point,
        byte[] state,
        DateTime? nextDueAtUtc = null) =>
        new(
            point.RunId,
            point.PointId,
            1,
            point.Version,
            point.NextSourceSequence,
            state,
            checked(point.NextSourceSequence + 1),
            nextDueAtUtc ?? Now);

    private static SimulatorProductionAttempt Pending(
        Guid runId, long sequence, Guid measurementId)
    {
        var payload = new SimulatorProductionPayload(
            measurementId, SourceId, runId, PointId, MappingId, 4, sequence,
            "IUMP-DETERMINISTIC-V1", 1, ConfigurationId, 7, Now, 12.3456, "kW",
            "IUMP.Worker.Simulator", "corr-payload", "lineage-payload");
        return new SimulatorProductionAttempt(
            runId, PointId, sequence, payload, SimulatorProductionAttemptStatus.Pending,
            null, null, null, null, null, null, null, null, null, Now, null, null, null, 1);
    }
}

internal static class RunAttemptRepositoryAssertions
{
    public static bool PinnedSnapshotEquivalent(
        this SimulatorRunPointState actual,
        SimulatorRunPointState expected) =>
        actual.RunId == expected.RunId &&
        actual.PointId == expected.PointId &&
        actual.PointVersionAtStart == expected.PointVersionAtStart &&
        actual.MappingId == expected.MappingId &&
        actual.MappingVersion == expected.MappingVersion &&
        actual.MetricId == expected.MetricId &&
        actual.UnitId == expected.UnitId &&
        actual.UnitCode == expected.UnitCode &&
        actual.SourceVersion == expected.SourceVersion &&
        actual.SiteId == expected.SiteId &&
        actual.AreaId == expected.AreaId;

    public static bool ConfigurationSnapshotEquivalent(
        this SimulatorRunPointState actual,
        SimulatorRunPointState expected) =>
        actual.PointId == expected.PointId &&
        actual.PointVersionAtStart == expected.PointVersionAtStart &&
        actual.MappingId == expected.MappingId &&
        actual.MappingVersion == expected.MappingVersion &&
        actual.MetricId == expected.MetricId &&
        actual.UnitId == expected.UnitId &&
        actual.UnitCode == expected.UnitCode &&
        actual.SourceVersion == expected.SourceVersion &&
        actual.PrngState.SequenceEqual(expected.PrngState);
}
