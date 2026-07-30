using System.Security.Cryptography;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;
using IUMP.Modules.IAM.Contracts;
using IUMP.Modules.IAM.Domain;
using IUMP.Modules.Operations.Contracts;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Modules.Telemetry.Application;
using IUMP.Modules.Telemetry.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IUMP.Tests.Integration.Runtime;

/// <summary>
/// Focused executable evidence for PostgreSQL leaf tasks that were previously
/// represented only by provider-neutral contract sources.
/// </summary>
public static class PostgresRuntimeLeafTests
{
    public static async Task<IReadOnlyList<string>> RunAsync(
        IServiceProvider services)
    {
        var failures = new List<string>();
        await RunCaseAsync("T031 IAM", () => IamAsync(services, failures), failures);
        await RunCaseAsync("T052 Catalog", () => CatalogAsync(services, failures), failures);
        await RunCaseAsync("T074 Organization", () => OrganizationAsync(services, failures), failures);
        await RunCaseAsync("T090 Configuration", () => ConfigurationAsync(services, failures), failures);
        await RunCaseAsync("T127 Run/attempt", () => RunAttemptAsync(services, failures), failures);
        await RunCaseAsync("T148/T166 Telemetry", () => TelemetryAndHealthAsync(services, failures), failures);
        await RunCaseAsync("T166/T206 Operations", () => OperationsAsync(services, failures), failures);
        return failures;
    }

    private static async Task IamAsync(
        IServiceProvider services,
        List<string> failures)
    {
        using var scope = services.CreateScope();
        var commands = scope.ServiceProvider.GetRequiredService<IIamCommandRepository>();
        var sessions = scope.ServiceProvider.GetRequiredService<IIamPrincipalSessionRepository>();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User(
            UserId.New(), $"runtime-{suffix}", "local-test-hash",
            UserStatus.Active, Role.Viewer);
        await commands.AddUserAsync(user);

        var duplicateRejected = false;
        try
        {
            await commands.AddUserAsync(new User(
                UserId.New(), user.Username, "other-local-test-hash",
                UserStatus.Active, Role.Viewer));
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }
        Check(duplicateRejected, "T031 duplicate username was not rejected", failures);

        var now = DateTime.UtcNow;
        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Guid.NewGuid().ToByteArray()));
        var session = new Session(
            SessionId.New(), user.Id, tokenHash, now,
            now.AddMinutes(20), now.AddHours(8));
        await sessions.AddSessionAsync(session);
        Check(
            (await sessions.FindSessionByTokenHashAsync(tokenHash))?.UserId == user.Id,
            "T031 token-hash lookup did not return the owning user",
            failures);
        await sessions.RevokeSessionAsync(session.Id, now.AddSeconds(1));
        Check(
            (await sessions.FindSessionByTokenHashAsync(tokenHash))?.IsRevoked == true,
            "T031 session revocation was not persisted",
            failures);

        var rolledBackUser = new User(
            UserId.New(), $"rollback-{suffix}", "local-test-hash",
            UserStatus.Active, Role.Engineer);
        var transaction = await commands.BeginTransactionAsync();
        await commands.AddUserAsync(rolledBackUser);
        await transaction.RollbackAsync();
        Check(
            await commands.GetUserAsync(rolledBackUser.Id) is null,
            "T031 IAM rollback published a staged user",
            failures);
    }

    private static async Task CatalogAsync(
        IServiceProvider services,
        List<string> failures)
    {
        using var scope = services.CreateScope();
        var commands = scope.ServiceProvider.GetRequiredService<ICatalogCommandRepository>();
        var suffix = Guid.NewGuid().ToString("N");
        var source = new DataSource(
            DataSourceId.New(), $"LEAF_{suffix}", "Runtime leaf source",
            SourceType.Simulator, SourceStatus.Active, 1);
        await commands.AddDataSourceAsync(source);

        var duplicateRejected = false;
        try
        {
            await commands.AddDataSourceAsync(new DataSource(
                DataSourceId.New(), source.Code.ToLowerInvariant(),
                "Duplicate runtime leaf source", SourceType.Simulator,
                SourceStatus.Draft, 1));
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }
        Check(duplicateRejected, "T052 duplicate source code was not rejected", failures);

        var pointId = Guid.NewGuid().ToString("D");
        var effective = new DateTime(2002, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await commands.AddMappingAsync(new SourcePointMapping(
            MappingId.New(), source.Id, pointId, MappingStatus.Active,
            effective, effective.AddDays(2), 1));
        var overlapRejected = false;
        try
        {
            await commands.AddMappingAsync(new SourcePointMapping(
                MappingId.New(), source.Id, pointId, MappingStatus.Active,
                effective.AddDays(1), effective.AddDays(3), 1));
        }
        catch (InvalidOperationException)
        {
            overlapRejected = true;
        }
        Check(overlapRejected, "T052 active Mapping overlap was not rejected", failures);

        var dependencies = await commands.GetDataSourceDependencySnapshotAsync(source.Id);
        Check(
            dependencies.MappingUsage &&
            !dependencies.SimulatorRun &&
            !dependencies.Measurement &&
            !dependencies.CurrentProjection &&
            !dependencies.ScheduledJob,
            "T052 dependency query did not isolate Mapping usage",
            failures);

        var rolledBack = new DataSource(
            DataSourceId.New(), $"ROLLBACK_{suffix}", "Rollback source",
            SourceType.Simulator, SourceStatus.Draft, 1);
        var transaction = await commands.BeginTransactionAsync();
        await commands.AddDataSourceAsync(rolledBack);
        await transaction.RollbackAsync();
        Check(
            await commands.GetDataSourceAsync(rolledBack.Id) is null,
            "T052 Catalog rollback published a staged source",
            failures);
    }

    private static async Task OrganizationAsync(
        IServiceProvider services,
        List<string> failures)
    {
        using var scope = services.CreateScope();
        var commands = scope.ServiceProvider.GetRequiredService<IOrganizationCommandRepository>();
        var suffix = Guid.NewGuid().ToString("N");
        var site = new Site(
            SiteId.New(), $"LEAF-{suffix}", "Runtime leaf Site", null,
            "UTC", SiteStatus.Draft, 1);
        await commands.AddSiteAsync(site);

        var duplicateRejected = false;
        try
        {
            await commands.AddSiteAsync(new Site(
                SiteId.New(), site.Code.ToLowerInvariant(), "Duplicate Site",
                null, "UTC", SiteStatus.Draft, 1));
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }
        Check(duplicateRejected, "T074 duplicate Site code was not rejected", failures);

        Check(site.TryActivate(), "T074 Site activation setup failed", failures);
        await commands.UpdateSiteAsync(site);
        var staleRejected = false;
        try
        {
            await commands.UpdateSiteAsync(new Site(
                site.Id, site.Code, "Stale update", null, "UTC",
                SiteStatus.Active, 2));
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains("VERSION_CONFLICT", StringComparison.Ordinal))
        {
            staleRejected = true;
        }
        Check(staleRejected, "T074 stale optimistic update was not rejected", failures);

        var area = new Area(
            AreaId.New(), site.Id, $"AREA-{suffix}", "Runtime leaf Area",
            null, AreaStatus.Active, 1);
        var asset = new Asset(
            AssetId.New(), site.Id, area.Id, $"ASSET-{suffix}",
            "Runtime leaf Asset", null, AssetStatus.Active, 1);
        var point = new MeasurementPoint(
            PointId.New(), site.Id, area.Id, asset.Id, $"POINT-{suffix}",
            null, Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"), 60, 300, PointStatus.Active, 1);
        await commands.AddAreaAsync(area);
        await commands.AddAssetAsync(asset);
        await commands.AddPointAsync(point);

        async Task<bool> DecommissionAsync()
        {
            using var contenderScope = services.CreateScope();
            var contender = contenderScope.ServiceProvider
                .GetRequiredService<IOrganizationCommandRepository>();
            var current = await contender.GetPointAsync(point.Id);
            if (current is null || !current.TryDecommission()) return false;
            try
            {
                await contender.UpdatePointAsync(current);
                return true;
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains(
                    "VERSION_CONFLICT", StringComparison.Ordinal))
            {
                return false;
            }
            catch (PostgresException exception)
                when (exception.SqlState is
                    PostgresErrorCodes.SerializationFailure or
                    PostgresErrorCodes.DeadlockDetected)
            {
                return false;
            }
        }

        var decommissionRace = await Task.WhenAll(
            DecommissionAsync(), DecommissionAsync());
        var decommissioned = await commands.GetPointAsync(point.Id);
        Check(
            decommissionRace.Count(value => value) == 1 &&
            decommissioned is { Status: PointStatus.Decommissioned, Version: 2 },
            "T074 concurrent Point decommission did not produce one winner",
            failures);

        var rolledBack = new Site(
            SiteId.New(), $"ROLLBACK-{suffix}", "Rollback Site", null,
            "UTC", SiteStatus.Draft, 1);
        var transaction = await commands.BeginTransactionAsync();
        await commands.AddSiteAsync(rolledBack);
        await transaction.RollbackAsync();
        Check(
            await commands.GetSiteAsync(rolledBack.Id) is null,
            "T074 Organization rollback published a staged Site",
            failures);
    }

    private static async Task ConfigurationAsync(
        IServiceProvider services,
        List<string> failures)
    {
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<IAcquisitionConfigurationRepository>();
        var configurationId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var first = ConfigurationVersion(
            configurationId, 1, 10, 2, 2, SimulatorScenario.Constant, createdAt);
        var transaction = await repository.BeginTransactionAsync();
        await repository.CreateAsync(
            new SimulatorConfigurationHead(configurationId, sourceId, 1, 1),
            first);
        await transaction.CommitAsync();
        transaction.Dispose();

        var append = await repository.BeginTransactionAsync();
        await repository.AppendVersionAsync(
            configurationId, 1,
            ConfigurationVersion(
                configurationId, 2, 20, 1, 3,
                SimulatorScenario.Normal, createdAt.AddSeconds(1)));
        await append.CommitAsync();
        append.Dispose();

        var versions = await repository.ListVersionsAsync(configurationId);
        Check(
            versions.Count == 2 &&
            versions[0].ConfigurationVersion == 1 &&
            versions[0].MinimumValue == first.MinimumValue &&
            versions[1].ConfigurationVersion == 2,
            "T090 configuration history was not ordered and immutable",
            failures);

        var staleRejected = false;
        try
        {
            await repository.AppendVersionAsync(
                configurationId, 1,
                ConfigurationVersion(
                    configurationId, 3, 30, 4, 4,
                    SimulatorScenario.Constant, createdAt.AddSeconds(2)));
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains("VERSION_CONFLICT", StringComparison.Ordinal))
        {
            staleRejected = true;
        }
        Check(
            staleRejected && (await repository.ListVersionsAsync(configurationId)).Count == 2,
            "T090 stale configuration append changed history",
            failures);

        var rolledBackId = Guid.NewGuid();
        var rollback = await repository.BeginTransactionAsync();
        await repository.CreateAsync(
            new SimulatorConfigurationHead(rolledBackId, Guid.NewGuid(), 1, 1),
            ConfigurationVersion(
                rolledBackId, 1, 10, 1, 1,
                SimulatorScenario.Constant, createdAt));
        await rollback.RollbackAsync();
        rollback.Dispose();
        Check(
            await repository.GetHeadAsync(rolledBackId) is null,
            "T090 configuration rollback published a new head",
            failures);
    }

    private static async Task TelemetryAndHealthAsync(
        IServiceProvider services,
        List<string> failures)
    {
        using var scope = services.CreateScope();
        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<ITelemetryFlowUnitOfWork>();
        var ingestion = scope.ServiceProvider
            .GetRequiredService<ITelemetryIngestionRepository>();
        var queries = scope.ServiceProvider
            .GetRequiredService<ITelemetryQueryRepository>();
        var health = scope.ServiceProvider
            .GetRequiredService<ISourceHealthProjectionRepository>();

        var accepted = TelemetryFixture(accepted: true, sequence: 1);
        await using (var transaction = await unitOfWork.BeginRepeatableReadAsync())
        {
            await ingestion.StageTerminalAsync(accepted.Terminal, transaction);
            await ingestion.StageRawAsync(accepted.Raw!, transaction);
            await transaction.CommitAsync();
        }
        Check(
            (await ingestion.GetTerminalAsync(accepted.Terminal.MeasurementId))
                ?.FinalClassification == TelemetryFinalClassification.Accepted &&
            (await queries.GetMeasurementAsync(accepted.Terminal.MeasurementId)) is not null,
            "T148 Accepted terminal and raw were not committed atomically",
            failures);

        var rejected = TelemetryFixture(accepted: false, sequence: 2);
        await using (var transaction = await unitOfWork.BeginRepeatableReadAsync())
        {
            await ingestion.StageTerminalAsync(rejected.Terminal, transaction);
            await transaction.CommitAsync();
        }
        Check(
            (await ingestion.GetTerminalAsync(rejected.Terminal.MeasurementId))
                ?.FinalClassification == TelemetryFinalClassification.Rejected &&
            (await queries.GetMeasurementAsync(rejected.Terminal.MeasurementId)) is null,
            "T148 Rejected terminal persisted an unexpected raw measurement",
            failures);

        var rollbackFixture = TelemetryFixture(accepted: true, sequence: 3);
        await using (var transaction = await unitOfWork.BeginRepeatableReadAsync())
        {
            await ingestion.StageTerminalAsync(rollbackFixture.Terminal, transaction);
            await ingestion.StageRawAsync(rollbackFixture.Raw!, transaction);
            await transaction.RollbackAsync();
        }
        Check(
            await ingestion.GetTerminalAsync(rollbackFixture.Terminal.MeasurementId) is null &&
            await queries.GetMeasurementAsync(rollbackFixture.Terminal.MeasurementId) is null,
            "T148 Telemetry rollback published terminal or raw data",
            failures);

        var pointId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var evaluated = new DateTime(2004, 1, 1, 0, 10, 0, DateTimeKind.Utc);
        var service = new SourceHealthService(health);
        var noDataInput = new SourceHealthEvaluationInput(
            pointId, sourceId, Guid.NewGuid().ToString("D"), null,
            "Active", "Active", "Running", 0, 0, 0, null,
            60, 300, 1, 1, 1);
        await using (var transaction = await unitOfWork.BeginRepeatableReadAsync())
        {
            var result = await service.EvaluateAsync(noDataInput, transaction, evaluated);
            await transaction.CommitAsync();
            Check(
                result.Changed && result.Current.Status == SourceHealthStatus.NoData,
                "T166 initial Source Health state was not NoData",
                failures);
        }

        var onlineInput = noDataInput with
        {
            LastAcceptedReceivedAtUtc = evaluated.AddSeconds(-30),
            GeneratedCount = 1,
            AcceptedCount = 1,
            ProviderVersion = 2
        };
        await using (var transaction = await unitOfWork.BeginRepeatableReadAsync())
        {
            var result = await service.EvaluateAsync(onlineInput, transaction, evaluated);
            await transaction.CommitAsync();
            Check(
                result.Changed && result.Current.Status == SourceHealthStatus.Online,
                "T166 Source Health did not recover to Online",
                failures);
        }

        using var restartScope = services.CreateScope();
        var restarted = restartScope.ServiceProvider
            .GetRequiredService<ISourceHealthProjectionRepository>();
        Check(
            (await restarted.GetCurrentAsync(pointId)) is
                { Status: SourceHealthStatus.Online, ProviderVersion: 2 },
            "T166 Source Health did not survive a service-scope restart",
            failures);
    }

    private static async Task RunAttemptAsync(
        IServiceProvider services,
        List<string> failures)
    {
        using var scope = services.CreateScope();
        var runs = scope.ServiceProvider.GetRequiredService<IAcquisitionRunRepository>();
        var attempts = scope.ServiceProvider
            .GetRequiredService<ISimulatorProductionAttemptRepository>();
        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<ISimulatorRunUnitOfWork>();
        var now = new DateTime(2006, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var runId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var mappingId = Guid.NewGuid();
        var configurationId = Guid.NewGuid();
        var initialState = Enumerable.Repeat((byte)1, 25).ToArray();
        var resultingState = Enumerable.Repeat((byte)2, 25).ToArray();
        var run = new SimulatorRun(
            runId, sourceId, 1, configurationId, 1,
            SimulatorConfigurationConstants.AlgorithmId, 1,
            SimulatorRunStatus.Running, 1, 0, 0, 0,
            null, null, now, now, null, null, null,
            "runtime-leaf-user", "runtime-leaf-user",
            $"runtime-leaf-{runId:N}", null);
        var point = new SimulatorRunPointState(
            runId, pointId, 1, mappingId, 1, Guid.NewGuid(), Guid.NewGuid(),
            "kW", 1, 0, initialState, now, Guid.NewGuid().ToString("D"),
            null, null, null, 0, null, 1);
        await using (var transaction = await unitOfWork.BeginAsync())
        {
            await runs.CreateAsync(run, [point], transaction);
            await transaction.CommitAsync();
        }

        var firstLease = await runs.ClaimDuePointAsync(
            runId, pointId, "runtime-leaf-worker", now, now.AddSeconds(30));
        var liveLeaseBlocked = await runs.ClaimDuePointAsync(
            runId, pointId, "runtime-leaf-other", now.AddSeconds(1), now.AddSeconds(31));
        var reclaimed = await runs.ClaimDuePointAsync(
            runId, pointId, "runtime-leaf-restart", now.AddSeconds(30), now.AddSeconds(60));
        Check(
            firstLease is not null && liveLeaseBlocked is null &&
            reclaimed is not null && reclaimed.Token != firstLease.Token,
            "T127 Run-Point lease was not exclusively reclaimed at expiry",
            failures);
        if (reclaimed is not null)
            await runs.ReleaseLeaseAsync(reclaimed);

        var transition = new SimulatorRunPointReservationTransition(
            runId, pointId, 1, 1, 0, resultingState, 1, now.AddSeconds(60));
        var payload = new SimulatorProductionPayload(
            Guid.NewGuid(), sourceId, runId, pointId, mappingId, 1, 0,
            SimulatorConfigurationConstants.AlgorithmId, 1,
            configurationId, 1, now, 12.5, "kW",
            "IUMP.Worker.Simulator", $"runtime-leaf-{runId:N}",
            $"runtime-leaf-lineage-{runId:N}");
        var attempt = new SimulatorProductionAttempt(
            runId, pointId, 0, payload,
            SimulatorProductionAttemptStatus.Pending,
            null, null, null, null, null, null, null, null, null,
            now, null, null, null, 1);
        await using (var transaction = await unitOfWork.BeginAsync())
        {
            var won = await attempts.TryReserveAsync(attempt, transition, transaction);
            if (won)
                await runs.StageReservationAsync(transition, transaction);
            await transaction.CommitAsync();
            Check(won, "T127 initial attempt reservation did not win", failures);
        }

        await using (var transaction = await unitOfWork.BeginAsync())
        {
            var duplicateWon = await attempts.TryReserveAsync(
                attempt with
                {
                    Payload = attempt.Payload with { MeasurementId = Guid.NewGuid() }
                },
                transition,
                transaction);
            await transaction.RollbackAsync();
            Check(!duplicateWon, "T127 duplicate Run/Point/sequence slot won", failures);
        }

        var savedRun = await runs.GetAsync(runId);
        var savedPoint = await runs.GetPointStateAsync(runId, pointId);
        Check(
            savedRun is { GeneratedCount: 1, Version: 2 } &&
            savedPoint is { NextSourceSequence: 1, Version: 2 } &&
            savedPoint.PrngState.SequenceEqual(resultingState),
            "T127 cursor, PRNG state, and Generated counter were not atomic",
            failures);
    }

    private static async Task OperationsAsync(
        IServiceProvider services,
        List<string> failures)
    {
        using var scope = services.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<IDurableJobScheduler>();
        var claims = scope.ServiceProvider.GetRequiredService<IJobClaimRepository>();
        var suffix = Guid.NewGuid().ToString("N");
        var jobType = new JobType($"Leaf-{suffix}");
        var key = new IdempotencyKey($"leaf:{suffix}");
        var payload = SafeJobPayload.Create($"purpose=runtime-leaf;id={suffix}");
        var now = new DateTime(2003, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var created = await scheduler.EnqueueAsync(jobType, key, payload, now);
        var replay = await scheduler.EnqueueAsync(jobType, key, payload, now);
        var conflict = await scheduler.EnqueueAsync(
            jobType, key, SafeJobPayload.Create($"purpose=changed;id={suffix}"), now);
        Check(
            created.Created && replay.Equivalent && conflict.Conflict,
            "T206 Operations enqueue replay/conflict contract failed",
            failures);

        var claimed = (await claims.ClaimDueAsync(now, $"leaf-{suffix}", 1))
            .SingleOrDefault(value => value.Job.Id == created.Job.Id);
        Check(claimed is not null, "T166/T206 due job was not claimed", failures);
        if (claimed is null) return;
        Check(
            claimed.LeaseExpiresAtUtc == now.AddSeconds(30),
            "T166/T206 claim lease was not 30 seconds",
            failures);

        var wrongToken = await claims.RenewAsync(
            claimed with { Token = Guid.NewGuid() }, now);
        Check(
            !wrongToken.Succeeded && wrongToken.Code == "LEASE_TOKEN_MISMATCH",
            "T206 wrong lease token was accepted",
            failures);

        var retryAt = now.AddSeconds(45);
        var rescheduled = await claims.RescheduleAsync(
            claimed, retryAt, "TRANSIENT_FAILURE", now);
        Check(
            rescheduled.Succeeded,
            "T166/T206 leased job was not rescheduled",
            failures);
        var reclaimed = (await claims.ClaimDueAsync(
            retryAt, $"leaf-restart-{suffix}", 1))
            .SingleOrDefault(value => value.Job.Id == created.Job.Id);
        Check(
            reclaimed is not null && reclaimed.Job.AttemptCount == 2,
            "T166/T206 retry was not reclaimed after restart",
            failures);
        if (reclaimed is null) return;

        var completed = await claims.CompleteAsync(reclaimed, retryAt.AddSeconds(1));
        var completionReplay = await claims.CompleteAsync(reclaimed, retryAt.AddSeconds(1));
        Check(
            completed.Succeeded && completionReplay.Succeeded &&
            completionReplay.Idempotent,
            "T206 completion replay was not idempotent",
            failures);
    }

    private static SimulatorConfigurationVersion ConfigurationVersion(
        Guid id,
        long version,
        int interval,
        double minimum,
        double maximum,
        SimulatorScenario scenario,
        DateTime createdAt) =>
        new(
            id, version, interval, minimum, maximum, (ulong)version, scenario,
            SimulatorConfigurationConstants.AlgorithmId,
            SimulatorConfigurationConstants.AlgorithmVersion,
            "runtime-leaf-user", "runtime-leaf-user", createdAt,
            $"runtime-leaf-{id:N}", $"runtime-leaf-{version}");

    private static (TelemetryTerminalResult Terminal, RawMeasurement? Raw)
        TelemetryFixture(bool accepted, long sequence)
    {
        var measurementId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var mappingId = Guid.NewGuid();
        var configurationId = Guid.NewGuid();
        var timestamp = new DateTime(2005, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddSeconds(sequence);
        var terminal = new TelemetryTerminalResult(
            measurementId, sourceId, runId, pointId, mappingId, 1, sequence,
            SimulatorConfigurationConstants.AlgorithmId, 1, configurationId, 1,
            accepted
                ? TelemetryFinalClassification.Accepted
                : TelemetryFinalClassification.Rejected,
            accepted, accepted ? measurementId : null,
            accepted ? MeasurementQuality.Good : null,
            null, accepted ? null : "POINT_INACTIVE",
            accepted ? true : null, timestamp, $"runtime-leaf-{measurementId:N}",
            $"runtime-leaf-{sequence}",
            SHA256.HashData(measurementId.ToByteArray()));
        var raw = accepted
            ? new RawMeasurement(
                measurementId, sourceId, runId, pointId, mappingId, 1,
                sequence, timestamp, timestamp, timestamp, sequence, "kW",
                MeasurementQuality.Good, null,
                terminal.OriginalCorrelationId, terminal.OriginalLineageId)
            : null;
        return (terminal, raw);
    }

    private static async Task RunCaseAsync(
        string name,
        Func<Task> action,
        List<string> failures)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            failures.Add(
                $"{name} unexpected {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static void Check(
        bool condition,
        string message,
        List<string> failures)
    {
        if (!condition) failures.Add(message);
    }
}
