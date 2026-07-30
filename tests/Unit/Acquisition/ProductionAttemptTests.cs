using IUMP.Modules.Acquisition.Application;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Acquisition.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Acquisition;

public static class ProductionAttemptTests
{
    public static int TestCount { get; private set; }
    public static int CheckCount { get; private set; }

    public static List<string> Run()
    {
        TestCount = 0;
        CheckCount = 0;
        var failures = new List<string>();
        RunAsync(failures).GetAwaiter().GetResult();
        return failures;
    }

    private static async Task RunAsync(List<string> failures)
    {
        var runId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        var repositories = new FakeAcquisitionRunRepositories();
        repositories.Seed(Phase6Fixtures.Run(runId), Phase6Fixtures.Point(runId));
        var configurations = await ConfigurationRepositoryAsync();
        var generator = new CountingSimulatorValueGenerator(new DeterministicGenerator());
        var identities = new CountingMeasurementIdentityFactory(new MeasurementIdentity());
        var service = new ProductionAttemptService(
            repositories, repositories, configurations, repositories, generator, identities,
            new FakeUtcClock(Phase6Fixtures.Now));

        TestCount++;
        var reserved = await service.ReserveAsync(runId, Phase6Fixtures.PointId, "corr-reserve", "lineage");
        Check(!reserved.ExistingPending && !reserved.UniquenessWinnerReloaded,
            "new slot wins reservation", failures);
        Check(reserved.Attempt.Status == SimulatorProductionAttemptStatus.Pending &&
              reserved.Attempt.Version > 0 && reserved.Attempt.CompletedAtUtc is null,
            "reservation stores a positive-version Pending attempt", failures);
        Check(reserved.Attempt.Payload.RunId == runId &&
              reserved.Attempt.Payload.PointId == Phase6Fixtures.PointId &&
              reserved.Attempt.Payload.SourceSequence == 0 &&
              reserved.Attempt.Payload.CorrelationId == "corr-reserve" &&
              reserved.Attempt.Payload.LineageId == "lineage",
            "Pending payload is complete and immutable", failures);
        var afterReserveRun = await repositories.GetAsync(runId);
        var afterReservePoint = await repositories.GetPointStateAsync(runId, Phase6Fixtures.PointId);
        Check(afterReserveRun!.GeneratedCount == 1 && afterReserveRun.Version == 2,
            "reservation increments Generated and Run version exactly once", failures);
        Check(afterReservePoint!.NextSourceSequence == 1 &&
              !afterReservePoint.PrngState.SequenceEqual(Phase6Fixtures.Point(runId).PrngState),
            "reservation advances cursor and PRNG state exactly once", failures);
        Check(generator.GenerateCount == 1 && identities.CreateCount == 1,
            "new slot invokes generator and identity exactly once", failures);
        Check((await repositories.GetAsync(runId, Phase6Fixtures.PointId, 0))?.Payload ==
              reserved.Attempt.Payload,
            "committed Pending can be loaded by primary identity", failures);

        TestCount++;
        var pendingAgain = await service.ReserveAsync(
            runId, Phase6Fixtures.PointId, "different-correlation", "different-lineage");
        Check(pendingAgain.ExistingPending && pendingAgain.Attempt.Payload == reserved.Attempt.Payload,
            "existing Pending is authoritative on retry", failures);
        Check(generator.GenerateCount == 1 && identities.CreateCount == 1 &&
              (await repositories.GetAsync(runId))!.GeneratedCount == 1,
            "Pending retry does not regenerate identity/value or increment Generated", failures);

        TestCount++;
        var accepted = new TelemetryDispatchResult(
            TelemetryAttemptOutcome.Accepted, ProductionFinalClassification.Accepted, true, true, null, null,
            reserved.Attempt.Payload.MeasurementId, "Good", null, Phase6Fixtures.Now,
            "original-correlation", "original-lineage");
        var finalized = await service.FinalizeAsync(runId, Phase6Fixtures.PointId, 0, accepted);
        Check(finalized.FirstTransition && !finalized.Replay &&
              finalized.Attempt.Status == SimulatorProductionAttemptStatus.Completed,
            "first terminal transition completes Pending exactly once", failures);
        Check(finalized.Attempt.TelemetryOutcome == TelemetryAttemptOutcome.Accepted &&
              finalized.Attempt.FinalClassification == ProductionFinalClassification.Accepted &&
              finalized.Attempt.LatestAdvanced == true &&
              finalized.Attempt.CompletedAtUtc?.Kind == DateTimeKind.Utc,
            "finalization persists outcome, classification, Latest result, and UTC completion", failures);
        var afterFinalize = await repositories.GetAsync(runId);
        Check(afterFinalize!.AcceptedCount == 1 && afterFinalize.RejectedCount == 0 &&
              afterFinalize.GeneratedCount == 1,
            "first Accepted finalization increments only Accepted and never Generated", failures);
        var replay = await service.FinalizeAsync(runId, Phase6Fixtures.PointId, 0, accepted);
        Check(replay.Replay && !replay.FirstTransition &&
              (await repositories.GetAsync(runId))!.AcceptedCount == 1,
            "identical terminal replay is an idempotent counter no-op", failures);
        var conflict = false;
        try
        {
            await service.FinalizeAsync(runId, Phase6Fixtures.PointId, 0,
                new TelemetryDispatchResult(TelemetryAttemptOutcome.Rejected,
                    ProductionFinalClassification.Rejected, false, false, "REJECTED", "INVALID",
                    null, null, null, Phase6Fixtures.Now, "different-correlation", "different-lineage"));
        }
        catch (InvalidOperationException ex)
        {
            conflict = ex.Message == "TERMINAL_RESULT_CONFLICT";
        }
        Check(conflict && (await repositories.GetAsync(runId))!.AcceptedCount == 1,
            "different terminal replay raises invariant conflict without counter mutation", failures);

        var invalidRunId = Guid.Parse("cccccccc-1111-4111-8111-cccccccccccc");
        var invalidRepositories = new FakeAcquisitionRunRepositories();
        invalidRepositories.Seed(
            Phase6Fixtures.Run(invalidRunId), Phase6Fixtures.Point(invalidRunId));
        invalidRepositories.SeedAttempt(Phase6Fixtures.Pending(invalidRunId));
        var invalidService = new ProductionAttemptService(
            invalidRepositories, invalidRepositories, configurations, invalidRepositories,
            new DeterministicGenerator(), new MeasurementIdentity(),
            new FakeUtcClock(Phase6Fixtures.Now));
        var invalidResults = new[]
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
                false, false, null, null)
        };
        foreach (var invalidResult in invalidResults)
        {
            TestCount++;
            var invalidRejected = false;
            try
            {
                await invalidService.FinalizeAsync(
                    invalidRunId, Phase6Fixtures.PointId, 0, invalidResult);
            }
            catch (InvalidOperationException ex)
            {
                invalidRejected = ex.Message == TelemetryDispatchResultValidator.InvalidCode;
            }
            var unchanged = await invalidRepositories.GetAsync(
                invalidRunId, Phase6Fixtures.PointId, 0);
            Check(invalidRejected && unchanged is
                  {
                      Status: SimulatorProductionAttemptStatus.Pending,
                      CompletedAtUtc: null,
                      Version: 1
                  } &&
                  (await invalidRepositories.GetAsync(invalidRunId)) is
                  { AcceptedCount: 0, RejectedCount: 0 },
                "invalid terminal pair returns TERMINAL_RESULT_INVALID without mutation", failures);
        }

        TestCount++;
        var duplicateRunId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
        var duplicateRepositories = new FakeAcquisitionRunRepositories();
        duplicateRepositories.Seed(Phase6Fixtures.Run(duplicateRunId),
            Phase6Fixtures.Point(duplicateRunId));
        duplicateRepositories.SeedAttempt(Phase6Fixtures.Pending(duplicateRunId));
        var duplicateService = new ProductionAttemptService(
            duplicateRepositories, duplicateRepositories, configurations, duplicateRepositories,
            new DeterministicGenerator(), new MeasurementIdentity(),
            new FakeUtcClock(Phase6Fixtures.Now));
        var duplicateResult = new TelemetryDispatchResult(
            TelemetryAttemptOutcome.Duplicate, ProductionFinalClassification.Accepted, true, false,
            "DUPLICATE", null, Phase6Fixtures.Pending(duplicateRunId).Payload.MeasurementId,
            "Good", null, Phase6Fixtures.Now, "original-correlation", "original-lineage");
        var duplicate = await duplicateService.FinalizeAsync(
            duplicateRunId, Phase6Fixtures.PointId, 0, duplicateResult);
        Check(duplicate.FirstTransition &&
              duplicate.Attempt.TelemetryOutcome == TelemetryAttemptOutcome.Duplicate &&
              duplicate.Attempt.FinalClassification == ProductionFinalClassification.Accepted,
            "Duplicate stores the original stable final classification", failures);
        Check((await duplicateRepositories.GetAsync(duplicateRunId))!.AcceptedCount == 1 &&
              (await duplicateRepositories.GetAsync(duplicateRunId))!.RejectedCount == 0,
            "Duplicate increments the original classification exactly once", failures);
        await duplicateService.FinalizeAsync(
            duplicateRunId, Phase6Fixtures.PointId, 0, duplicateResult);
        Check((await duplicateRepositories.GetAsync(duplicateRunId))!.AcceptedCount == 1,
            "Duplicate replay never increments a second time", failures);

        TestCount++;
        var raceRunId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");
        var raceRepositories = new FakeAcquisitionRunRepositories
        {
            SimulateReserveUniquenessRace = true
        };
        raceRepositories.Seed(Phase6Fixtures.Run(raceRunId), Phase6Fixtures.Point(raceRunId));
        var raceGenerator = new CountingSimulatorValueGenerator(new DeterministicGenerator());
        var raceService = new ProductionAttemptService(
            raceRepositories, raceRepositories, configurations, raceRepositories, raceGenerator,
            new MeasurementIdentity(), new FakeUtcClock(Phase6Fixtures.Now));
        var race = await raceService.ReserveAsync(
            raceRunId, Phase6Fixtures.PointId, "corr-race", "lineage-race");
        Check(race.UniquenessWinnerReloaded && !race.ExistingPending,
            "uniqueness-race loser reloads the committed winner", failures);
        var racePoint = await raceRepositories.GetPointStateAsync(
            raceRunId, Phase6Fixtures.PointId);
        Check((await raceRepositories.GetAsync(raceRunId)) is
              { GeneratedCount: 1, Version: 2 } &&
              racePoint is { NextSourceSequence: 1, Version: 2 } &&
              !racePoint.PrngState.SequenceEqual(Phase6Fixtures.Point(raceRunId).PrngState),
            "race winner atomically commits Pending, PRNG, cursor and Generated once", failures);
        Check(raceGenerator.GenerateCount == 1 &&
              (await raceRepositories.GetAsync(raceRunId, Phase6Fixtures.PointId, 0)) is not null,
            "race preserves exactly the winner Pending attempt", failures);
        var raceAccepted = accepted with
        {
            PersistedMeasurementId = race.Attempt.Payload.MeasurementId
        };
        await raceService.FinalizeAsync(
            raceRunId, Phase6Fixtures.PointId, 0, raceAccepted);
        Check((await raceRepositories.GetAsync(raceRunId)) is
              { GeneratedCount: 1, AcceptedCount: 1, RejectedCount: 0 },
            "race winner finalizes Accepted without a second state advance", failures);

        TestCount++;
        var rollbackRunId = Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff");
        var rollbackRepositories = new FakeAcquisitionRunRepositories { FailNextCommit = true };
        rollbackRepositories.Seed(Phase6Fixtures.Run(rollbackRunId),
            Phase6Fixtures.Point(rollbackRunId));
        var rollbackService = new ProductionAttemptService(
            rollbackRepositories, rollbackRepositories, configurations, rollbackRepositories,
            new DeterministicGenerator(), new MeasurementIdentity(),
            new FakeUtcClock(Phase6Fixtures.Now));
        var reserveFailed = false;
        try
        {
            await rollbackService.ReserveAsync(
                rollbackRunId, Phase6Fixtures.PointId, "corr-rollback", "lineage-rollback");
        }
        catch (InvalidOperationException ex)
        {
            reserveFailed = ex.Message == "COMMIT_FAILED";
        }
        Check(reserveFailed, "reservation commit failure is surfaced", failures);
        Check(await rollbackRepositories.GetAsync(rollbackRunId, Phase6Fixtures.PointId, 0) is null &&
              (await rollbackRepositories.GetAsync(rollbackRunId))!.GeneratedCount == 0,
            "reservation rollback publishes no attempt or counter change", failures);
        Check((await rollbackRepositories.GetPointStateAsync(rollbackRunId, Phase6Fixtures.PointId))!
              .NextSourceSequence == 0,
            "reservation rollback leaves cursor unchanged", failures);

        TestCount++;
        var finalizeRunId = Guid.Parse("87654321-4321-4321-8321-210987654321");
        var finalizeRepositories = new FakeAcquisitionRunRepositories();
        finalizeRepositories.Seed(Phase6Fixtures.Run(finalizeRunId),
            Phase6Fixtures.Point(finalizeRunId));
        var finalizePending = Phase6Fixtures.Pending(finalizeRunId);
        finalizeRepositories.SeedAttempt(finalizePending);
        finalizeRepositories.FailNextCommit = true;
        var finalizeService = new ProductionAttemptService(
            finalizeRepositories, finalizeRepositories, configurations, finalizeRepositories,
            new DeterministicGenerator(), new MeasurementIdentity(),
            new FakeUtcClock(Phase6Fixtures.Now));
        var finalizeFailed = false;
        try
        {
            await finalizeService.FinalizeAsync(
                finalizeRunId, Phase6Fixtures.PointId, 0,
                accepted with { PersistedMeasurementId = finalizePending.Payload.MeasurementId });
        }
        catch (InvalidOperationException ex)
        {
            finalizeFailed = ex.Message == "COMMIT_FAILED";
        }
        var rolledBackAttempt = await finalizeRepositories.GetAsync(
            finalizeRunId, Phase6Fixtures.PointId, 0);
        Check(finalizeFailed && rolledBackAttempt!.Status == SimulatorProductionAttemptStatus.Pending,
            "finalization rollback leaves attempt Pending", failures);
        Check((await finalizeRepositories.GetAsync(finalizeRunId))!.AcceptedCount == 0,
            "finalization rollback leaves counters unchanged", failures);
    }

    public static async Task<FakeAcquisitionConfigurationRepository> ConfigurationRepositoryAsync()
    {
        var repository = new FakeAcquisitionConfigurationRepository();
        await repository.CreateAsync(
            new SimulatorConfigurationHead(
                Phase6Fixtures.ConfigurationId, Phase6Fixtures.SourceId, 1, 1),
            Phase6Fixtures.Configuration(1));
        for (var version = 2L; version <= 7; version++)
        {
            await repository.AppendVersionAsync(
                Phase6Fixtures.ConfigurationId, version - 1,
                Phase6Fixtures.Configuration(version));
        }
        return repository;
    }

    private static void Check(bool condition, string message, List<string> failures)
    {
        CheckCount++;
        if (!condition) failures.Add($"T112: {message}.");
    }
}
