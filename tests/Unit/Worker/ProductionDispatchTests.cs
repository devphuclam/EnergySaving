using IUMP.Modules.Acquisition.Application;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Acquisition.Domain;
using IUMP.Tests.Unit.Acquisition;
using IUMP.Tests.Unit.Fakes;
using IUMP.Worker;
using Microsoft.Extensions.Logging.Abstractions;

namespace IUMP.Tests.Unit.Worker;

public static class ProductionDispatchTests
{
    public static int TestCount { get; private set; }
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        TestCount = 8;
        CheckCount = 34;
        var failures = new List<string>();
        RunAsync(failures).GetAwaiter().GetResult();
        return failures;
    }

    private static async Task RunAsync(List<string> failures)
    {
        var configurations = await ProductionAttemptTests.ConfigurationRepositoryAsync();

        var pendingRunId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        var pendingRepositories = new FakeAcquisitionRunRepositories();
        var initialPoint = Phase6Fixtures.Point(pendingRunId);
        pendingRepositories.Seed(Phase6Fixtures.Run(pendingRunId), initialPoint);
        var pendingAttempt = Phase6Fixtures.Pending(pendingRunId);
        pendingRepositories.SeedAttempt(pendingAttempt);
        var pendingGenerator = new CountingSimulatorValueGenerator(new DeterministicGenerator());
        var pendingIdentities = new CountingMeasurementIdentityFactory(new MeasurementIdentity());
        var pendingClock = new FakeUtcClock(Phase6Fixtures.Now);
        var pendingService = new ProductionAttemptService(
            pendingRepositories, pendingRepositories, configurations, pendingRepositories,
            pendingGenerator, pendingIdentities, pendingClock);
        var pendingTelemetry = new FakeTelemetryIngestionClient
        {
            TransactionActiveProbe = () => pendingRepositories.IsTransactionActive
        };
        var pendingEligibility = new FakeSimulatorProductionEligibility
        {
            IsActive = false,
            ErrorCode = "POINT_INACTIVE"
        };
        var pendingWorker = Worker(
            pendingRepositories, pendingService, pendingTelemetry, pendingEligibility, pendingClock);

        var pendingCycle = await pendingWorker.RunOnceAsync("worker-a");
        Check(pendingCycle is { RunningRuns: 1, ClaimedPoints: 1, DispatchedAttempts: 1,
            FinalizedAttempts: 1, FailedPoints: 0 },
            "existing Pending is claimed, dispatched, and finalized", failures);
        Check(pendingTelemetry.Payloads.Count == 1 &&
              pendingTelemetry.Payloads[0] == pendingAttempt.Payload,
            "persisted Pending payload is dispatched byte-for-field exactly", failures);
        Check(pendingGenerator.GenerateCount == 0 && pendingIdentities.CreateCount == 0,
            "existing Pending never invokes generator or identity", failures);
        var pendingPointAfter = await pendingRepositories.GetPointStateAsync(
            pendingRunId, Phase6Fixtures.PointId);
        Check(pendingPointAfter!.NextSourceSequence == initialPoint.NextSourceSequence &&
              pendingPointAfter.PrngState.SequenceEqual(initialPoint.PrngState),
            "existing Pending leaves cursor and PRNG state unchanged", failures);
        var pendingRunAfter = await pendingRepositories.GetAsync(pendingRunId);
        Check(pendingRunAfter!.GeneratedCount == 0 && pendingRunAfter.AcceptedCount == 1,
            "existing Pending leaves Generated unchanged and finalizes one Accepted", failures);
        Check(pendingRunAfter.Status == SimulatorRunStatus.Running &&
              pendingCycle.FailedPoints == 0,
            "existing Pending dispatch precedes owner eligibility and is never stranded", failures);
        Check(!pendingTelemetry.ObservedActiveTransaction,
            "Telemetry dispatch occurs outside reservation/finalization transactions", failures);
        Check(pendingPointAfter.LeaseOwner is null && pendingPointAfter.LeaseToken is null,
            "successful dispatch releases the lease", failures);

        var newRunId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
        var newRepositories = new FakeAcquisitionRunRepositories();
        newRepositories.Seed(Phase6Fixtures.Run(newRunId), Phase6Fixtures.Point(newRunId));
        var newGenerator = new CountingSimulatorValueGenerator(new DeterministicGenerator());
        var newIdentities = new CountingMeasurementIdentityFactory(new MeasurementIdentity());
        var newClock = new FakeUtcClock(Phase6Fixtures.Now);
        var newService = new ProductionAttemptService(
            newRepositories, newRepositories, configurations, newRepositories,
            newGenerator, newIdentities, newClock);
        var newTelemetry = new FakeTelemetryIngestionClient
        {
            TransactionActiveProbe = () => newRepositories.IsTransactionActive
        };
        var newWorker = Worker(
            newRepositories, newService, newTelemetry,
            new FakeSimulatorProductionEligibility(), newClock);
        var newCycle = await newWorker.RunOnceAsync("worker-b");
        Check(newCycle.DispatchedAttempts == 1 && newCycle.FinalizedAttempts == 1,
            "new slot reserves before one dispatch and one finalization", failures);
        Check(newGenerator.GenerateCount == 1 && newIdentities.CreateCount == 1,
            "new slot invokes generator and identity exactly once", failures);
        var newRun = await newRepositories.GetAsync(newRunId);
        var newPoint = await newRepositories.GetPointStateAsync(newRunId, Phase6Fixtures.PointId);
        Check(newRun!.GeneratedCount == 1 && newRun.AcceptedCount == 1 &&
              newRun.RejectedCount == 0, "new slot increments Generated and Accepted once", failures);
        Check(newPoint!.NextSourceSequence == 1 &&
              !newPoint.PrngState.SequenceEqual(Phase6Fixtures.Point(newRunId).PrngState),
            "new slot persists cursor and resulting PRNG state", failures);
        Check((await newRepositories.GetAsync(newRunId, Phase6Fixtures.PointId, 0)) is
              { Status: SimulatorProductionAttemptStatus.Completed },
            "new slot inserts Pending before completing it", failures);
        Check(!newTelemetry.ObservedActiveTransaction,
            "new-slot Telemetry dispatch is outside the reservation transaction", failures);

        var crashRunId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
        var crashRepositories = new FakeAcquisitionRunRepositories();
        crashRepositories.Seed(Phase6Fixtures.Run(crashRunId), Phase6Fixtures.Point(crashRunId));
        var crashGenerator = new CountingSimulatorValueGenerator(new DeterministicGenerator());
        var crashIdentities = new CountingMeasurementIdentityFactory(new MeasurementIdentity());
        var crashClock = new FakeUtcClock(Phase6Fixtures.Now);
        var crashService = new ProductionAttemptService(
            crashRepositories, crashRepositories, configurations, crashRepositories,
            crashGenerator, crashIdentities, crashClock);
        var crashTelemetry = new FakeTelemetryIngestionClient { ThrowTransient = true };
        var crashWorker = Worker(
            crashRepositories, crashService, crashTelemetry,
            new FakeSimulatorProductionEligibility(), crashClock);
        var crashCycle = await crashWorker.RunOnceAsync("worker-crash");
        var persistedPending = await crashRepositories.GetAsync(
            crashRunId, Phase6Fixtures.PointId, 0);
        Check(crashCycle.FailedPoints == 1 &&
              persistedPending?.Status == SimulatorProductionAttemptStatus.Pending,
            "transient dispatch failure leaves the complete attempt Pending", failures);
        Check(crashGenerator.GenerateCount == 1 && crashIdentities.CreateCount == 1 &&
              (await crashRepositories.GetAsync(crashRunId))!.GeneratedCount == 1,
            "crash path reserves and advances state exactly once", failures);
        var crashPayload = persistedPending!.Payload;
        crashTelemetry.ThrowTransient = false;
        crashClock.Advance(TimeSpan.FromSeconds(60));
        var retryCycle = await crashWorker.RunOnceAsync("worker-crash-retry");
        Check(retryCycle.FinalizedAttempts == 1 &&
              crashTelemetry.Payloads.Count == 2 &&
              crashTelemetry.Payloads[1] == crashPayload,
            "crash retry dispatches the exact persisted payload", failures);
        Check(crashGenerator.GenerateCount == 1 && crashIdentities.CreateCount == 1 &&
              (await crashRepositories.GetAsync(crashRunId))!.GeneratedCount == 1,
            "crash retry never regenerates or increments Generated again", failures);

        var isolatedRunId = Guid.Parse("abcd0000-0000-4000-8000-000000000001");
        var isolatedRepositories = new FakeAcquisitionRunRepositories();
        var firstPoint = Phase6Fixtures.Point(isolatedRunId);
        var secondPointId = Guid.Parse("99999999-2222-4333-8444-555555555555");
        var secondPoint = firstPoint with
        {
            PointId = secondPointId,
            MappingId = Guid.Parse("99999999-cccc-4ccc-8ccc-cccccccccccc"),
            PrngState = new DeterministicGenerator().Initialize(
                42, secondPointId, Phase6Fixtures.ConfigurationId, 7, 1)
        };
        isolatedRepositories.Seed(
            Phase6Fixtures.Run(isolatedRunId), firstPoint, secondPoint);
        var isolatedClock = new FakeUtcClock(Phase6Fixtures.Now);
        var isolatedService = new ProductionAttemptService(
            isolatedRepositories, isolatedRepositories, configurations, isolatedRepositories,
            new DeterministicGenerator(), new MeasurementIdentity(), isolatedClock);
        var isolatedTelemetry = new FakeTelemetryIngestionClient
        {
            FailureSelector = payload => payload.PointId == firstPoint.PointId
                ? new TimeoutException("FIRST_POINT_TRANSIENT")
                : null
        };
        var isolatedWorker = Worker(
            isolatedRepositories, isolatedService, isolatedTelemetry,
            new FakeSimulatorProductionEligibility(), isolatedClock);
        var isolatedCycle = await isolatedWorker.RunOnceAsync("worker-isolated");
        Check(isolatedCycle.ClaimedPoints == 2 && isolatedCycle.FailedPoints == 1 &&
              isolatedCycle.FinalizedAttempts == 1,
            "one Point failure does not prevent an unrelated due Point from finalizing", failures);
        Check(isolatedCycle.Failures.Count == 1 &&
              isolatedCycle.Failures[0].PointId == firstPoint.PointId &&
              isolatedCycle.Failures[0].Code == "PRODUCTION_POINT_FAILED",
            "failed Point has an explicit classified outcome", failures);
        Check((await isolatedRepositories.GetAsync(isolatedRunId)) is
              { GeneratedCount: 2, AcceptedCount: 1 },
            "isolated failure preserves each Point reservation and successful counter", failures);

        var renewalRunId = Guid.Parse("abcd0000-0000-4000-8000-000000000003");
        var renewalRepositories = new FakeAcquisitionRunRepositories();
        renewalRepositories.Seed(
            Phase6Fixtures.Run(renewalRunId), Phase6Fixtures.Point(renewalRunId));
        var renewalClock = new FakeUtcClock(Phase6Fixtures.Now);
        var renewalService = new ProductionAttemptService(
            renewalRepositories, renewalRepositories, configurations, renewalRepositories,
            new DeterministicGenerator(), new MeasurementIdentity(), renewalClock);
        var delayedTelemetry = new FakeTelemetryIngestionClient
        {
            DispatchDelay = TimeSpan.FromMilliseconds(100)
        };
        var renewalWorker = Worker(
            renewalRepositories, renewalService, delayedTelemetry,
            new FakeSimulatorProductionEligibility(), renewalClock,
            TimeSpan.FromMilliseconds(5));
        var renewalCycleTask = renewalWorker.RunOnceAsync("worker-renewal");
        while (delayedTelemetry.Payloads.Count == 0)
            await Task.Delay(1);
        renewalClock.Advance(TimeSpan.FromSeconds(31));
        await Task.Delay(25);
        var competingLease = await renewalRepositories.ClaimDuePointAsync(
            renewalRunId, Phase6Fixtures.PointId, "worker-competitor",
            renewalClock.UtcNow, renewalClock.UtcNow.AddSeconds(30));
        var renewalCycle = await renewalCycleTask;
        Check(renewalRepositories.LeaseRenewalCount > 0 && competingLease is null,
            "heartbeat renews versioned lease before a second Worker can reclaim", failures);
        Check(renewalCycle.FinalizedAttempts == 1 &&
              (await renewalRepositories.GetAsync(
                  renewalRunId, Phase6Fixtures.PointId, 0))?.Status ==
              SimulatorProductionAttemptStatus.Completed,
            "delayed dispatch finalizes once under the renewed lease", failures);

        var cancelledRunId = Guid.Parse("abcd0000-0000-4000-8000-000000000002");
        var cancelledRepositories = new FakeAcquisitionRunRepositories();
        cancelledRepositories.Seed(
            Phase6Fixtures.Run(cancelledRunId), Phase6Fixtures.Point(cancelledRunId));
        var cancelledClock = new FakeUtcClock(Phase6Fixtures.Now);
        var cancelledService = new ProductionAttemptService(
            cancelledRepositories, cancelledRepositories, configurations, cancelledRepositories,
            new DeterministicGenerator(), new MeasurementIdentity(), cancelledClock);
        using var cancellation = new CancellationTokenSource();
        var cancelledTelemetry = new FakeTelemetryIngestionClient
        {
            FailureSelector = _ =>
            {
                cancellation.Cancel();
                return new OperationCanceledException(cancellation.Token);
            }
        };
        var cancelledWorker = Worker(
            cancelledRepositories, cancelledService, cancelledTelemetry,
            new FakeSimulatorProductionEligibility(), cancelledClock);
        var cancelled = false;
        try
        {
            await cancelledWorker.RunOnceAsync("worker-cancelled", cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        Check(cancelled, "cancellation is propagated instead of classified as a Point failure", failures);
        Check((await cancelledRepositories.GetPointStateAsync(
                  cancelledRunId, Phase6Fixtures.PointId))?.LeaseOwner is null,
            "cancellation releases the claimed lease with a non-cancelled cleanup token", failures);

        var idleRepositories = new FakeAcquisitionRunRepositories();
        var pausedRunId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");
        var stoppedRunId = Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff");
        idleRepositories.Seed(Phase6Fixtures.Run(pausedRunId, SimulatorRunStatus.Paused),
            Phase6Fixtures.Point(pausedRunId));
        idleRepositories.Seed(Phase6Fixtures.Run(stoppedRunId, SimulatorRunStatus.Stopped),
            Phase6Fixtures.Point(stoppedRunId));
        var idleGenerator = new CountingSimulatorValueGenerator(new DeterministicGenerator());
        var idleIdentities = new CountingMeasurementIdentityFactory(new MeasurementIdentity());
        var idleClock = new FakeUtcClock(Phase6Fixtures.Now);
        var idleService = new ProductionAttemptService(
            idleRepositories, idleRepositories, configurations, idleRepositories,
            idleGenerator, idleIdentities, idleClock);
        var idleTelemetry = new FakeTelemetryIngestionClient();
        var idleWorker = Worker(
            idleRepositories, idleService, idleTelemetry,
            new FakeSimulatorProductionEligibility(), idleClock);
        var idleCycle = await idleWorker.RunOnceAsync("worker-idle");
        Check(idleCycle.RunningRuns == 0 && idleCycle.ClaimedPoints == 0,
            "Paused and Stopped Runs are never claimed", failures);
        Check(idleGenerator.GenerateCount == 0 && idleIdentities.CreateCount == 0 &&
              idleTelemetry.Payloads.Count == 0,
            "Paused and Stopped Runs never generate, identify, or dispatch", failures);
        Check((await idleRepositories.GetAsync(pausedRunId))!.GeneratedCount == 0 &&
              (await idleRepositories.GetAsync(stoppedRunId))!.GeneratedCount == 0,
            "Paused and Stopped counters remain unchanged", failures);

        var ownerRunId = Guid.Parse("12345678-1234-4234-8234-123456789012");
        var ownerRepositories = new FakeAcquisitionRunRepositories();
        ownerRepositories.Seed(Phase6Fixtures.Run(ownerRunId), Phase6Fixtures.Point(ownerRunId));
        var ownerClock = new FakeUtcClock(Phase6Fixtures.Now);
        var ownerService = new ProductionAttemptService(
            ownerRepositories, ownerRepositories, configurations, ownerRepositories,
            new DeterministicGenerator(), new MeasurementIdentity(), ownerClock);
        var ownerWorker = Worker(
            ownerRepositories, ownerService, new FakeTelemetryIngestionClient(),
            new FakeSimulatorProductionEligibility
            {
                IsActive = false,
                ErrorCode = "POINT_INACTIVE"
            }, ownerClock);
        var ownerCycle = await ownerWorker.RunOnceAsync("worker-owner");
        var ownerRun = await ownerRepositories.GetAsync(ownerRunId);
        Check(ownerCycle.FailedPoints == 1 &&
              ownerRun?.Status == SimulatorRunStatus.Stopped &&
              ownerRun.LatestErrorCode == "POINT_INACTIVE",
            "pinned owner drift stops the Run with a stable error code", failures);
        Check(ownerRun!.GeneratedCount == 0,
            "owner drift stops before generation", failures);
        Check(ownerRepositories.CommittedEvents.Count == 1 &&
              ownerRepositories.CommittedEvents[0].Action == "Stop",
            "owner drift stages the safe Stop owner event atomically", failures);
        Check(ownerCycle.Failures.Count == 1 &&
              ownerCycle.Failures[0].CorrelationId == ownerRun.CorrelationId &&
              ownerCycle.Failures[0].Code == "POINT_INACTIVE",
            "Point failure returns a classified correlation-aware outcome", failures);
    }

    private static SimulatorProductionWorker Worker(
        FakeAcquisitionRunRepositories repositories,
        IProductionAttemptService attempts,
        ITelemetryIngestionClient telemetry,
        ISimulatorProductionEligibility eligibility,
        FakeUtcClock clock,
        TimeSpan? leaseRenewalInterval = null)
    {
        var coordinator = new SimulatorProductionCoordinator(
            repositories, repositories, repositories, attempts, telemetry, eligibility, clock,
            leaseRenewalInterval);
        return new SimulatorProductionWorker(
            coordinator, NullLogger<SimulatorProductionWorker>.Instance);
    }

    private static void Check(bool condition, string message, List<string> failures)
    {
        if (!condition) failures.Add($"T111: {message}.");
    }
}
