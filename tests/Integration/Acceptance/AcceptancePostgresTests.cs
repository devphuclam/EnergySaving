using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Telemetry.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Security.Cryptography;

namespace IUMP.Tests.Integration.Acceptance;

public static class AcceptancePostgresTests
{
    public static async Task<IReadOnlyList<string>> RunAsync(
        IServiceProvider services)
    {
        var failures = new List<string>();
        await ConfigurationAppendRaceAsync(services, failures);
        await MappingActivationRaceAsync(services, failures);
        await LatestRaceAsync(services, failures);
        return failures;
    }

    private static async Task ConfigurationAppendRaceAsync(
        IServiceProvider services,
        List<string> failures)
    {
        var configurationId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var created = DateTime.UtcNow;
        using (var setup = services.CreateScope())
        {
            var repository = setup.ServiceProvider
                .GetRequiredService<IAcquisitionConfigurationRepository>();
            var transaction = await repository.BeginTransactionAsync();
            await repository.CreateAsync(
                new SimulatorConfigurationHead(configurationId, sourceId, 1, 1),
                Version(configurationId, 1, 1, created));
            await transaction.CommitAsync();
            transaction.Dispose();
        }

        async Task<bool> AppendAsync(long seed)
        {
            using var scope = services.CreateScope();
            var repository = scope.ServiceProvider
                .GetRequiredService<IAcquisitionConfigurationRepository>();
            try
            {
                await repository.AppendVersionAsync(
                    configurationId, 1,
                    Version(configurationId, 2, seed, created.AddSeconds(seed)));
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

        var winners = await Task.WhenAll(AppendAsync(2), AppendAsync(3));
        using var verificationScope = services.CreateScope();
        var versions = await verificationScope.ServiceProvider
            .GetRequiredService<IAcquisitionConfigurationRepository>()
            .ListVersionsAsync(configurationId);
        if (winners.Count(value => value) != 1 ||
            versions.Count != 2 ||
            versions[0].ConfigurationVersion != 1 ||
            versions[1].ConfigurationVersion != 2)
            failures.Add(
                "T236 configuration append race must have one winner and immutable ordered history");
    }

    private static async Task MappingActivationRaceAsync(
        IServiceProvider services,
        List<string> failures)
    {
        var sourceId = DataSourceId.New();
        var mappingId = MappingId.New();
        using (var setup = services.CreateScope())
        {
            var catalog = setup.ServiceProvider
                .GetRequiredService<ICatalogCommandRepository>();
            await catalog.AddDataSourceAsync(new DataSource(
                sourceId, $"RACE_{Guid.NewGuid():N}", "Race source",
                SourceType.Simulator, SourceStatus.Draft, 1));
            await catalog.AddMappingAsync(new SourcePointMapping(
                mappingId, sourceId, Guid.NewGuid().ToString("D"),
                MappingStatus.Draft, DateTime.UtcNow.AddMinutes(-1), null, 1));
        }

        using var firstScope = services.CreateScope();
        using var secondScope = services.CreateScope();
        var firstRepo = firstScope.ServiceProvider
            .GetRequiredService<ICatalogCommandRepository>();
        var secondRepo = secondScope.ServiceProvider
            .GetRequiredService<ICatalogCommandRepository>();
        var first = await firstRepo.GetMappingAsync(mappingId);
        var second = await secondRepo.GetMappingAsync(mappingId);
        if (first is null || second is null || !first.TryActivate())
        {
            failures.Add("T236 configuration race setup failed");
            return;
        }
        var superseded = new SourcePointMapping(
            second.Id, second.DataSourceId, second.PointId,
            MappingStatus.Superseded, second.EffectiveFrom,
            second.EffectiveTo, second.Version + 1);

        async Task<bool> PersistAsync(
            ICatalogCommandRepository repository,
            SourcePointMapping mapping)
        {
            try
            {
                await repository.UpdateMappingAsync(mapping);
                return true;
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains(
                    "VERSION_CONFLICT", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var results = await Task.WhenAll(
            PersistAsync(firstRepo, first),
            PersistAsync(secondRepo, superseded));
        using var verificationScope = services.CreateScope();
        var saved = await verificationScope.ServiceProvider
            .GetRequiredService<ICatalogCommandRepository>()
            .GetMappingAsync(mappingId);
        if (results.Count(value => value) != 1 ||
            saved is not { Version: 2 } ||
            saved.Status is not (MappingStatus.Active or MappingStatus.Superseded))
            failures.Add(
                "T236 Mapping activate-vs-supersede race must have one winner and one version conflict");
    }

    private static async Task LatestRaceAsync(
        IServiceProvider services,
        List<string> failures)
    {
        var pointId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        async Task<bool> ApplyAsync(long sequence)
        {
            var measurementId = Guid.NewGuid();
            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var scope = services.CreateScope();
                var uow = scope.ServiceProvider
                    .GetRequiredService<ITelemetryFlowUnitOfWork>();
                var latest = scope.ServiceProvider
                    .GetRequiredService<IPointLatestProjectionRepository>();
                var ingestion = scope.ServiceProvider
                    .GetRequiredService<ITelemetryIngestionRepository>();
                await using var transaction =
                    await uow.BeginRepeatableReadAsync();
                try
                {
                    var sourceId = Guid.NewGuid();
                    var runId = Guid.NewGuid();
                    var mappingId = Guid.NewGuid();
                    var configurationId = Guid.NewGuid();
                    await ingestion.StageTerminalAsync(
                        new TelemetryTerminalResult(
                            measurementId, sourceId, runId, pointId, mappingId,
                            1, sequence, "IUMP-DETERMINISTIC-V1", 1,
                            configurationId, 1,
                            TelemetryFinalClassification.Accepted, true,
                            measurementId, MeasurementQuality.Good,
                            null, null, true, timestamp,
                            $"latest-race-{pointId:N}",
                            $"latest-race-{sequence}",
                            SHA256.HashData(measurementId.ToByteArray())),
                        transaction);
                    await ingestion.StageRawAsync(
                        new RawMeasurement(
                            measurementId, sourceId, runId, pointId,
                            mappingId, 1, sequence, timestamp, timestamp,
                            timestamp.AddTicks(sequence), sequence, "kW",
                            MeasurementQuality.Good, null,
                            $"latest-race-{pointId:N}", $"latest-race-{sequence}"),
                        transaction);
                    var result = await latest.CompareAndSetAsync(
                        new LatestProjectionCandidate(
                            measurementId, pointId, timestamp, sequence,
                            timestamp.AddTicks(sequence), MeasurementQuality.Good,
                            sequence, "kW", timestamp),
                        transaction);
                    await transaction.CommitAsync();
                    return result.Advanced;
                }
                catch (PostgresException exception)
                    when (exception.SqlState is
                        PostgresErrorCodes.SerializationFailure or
                        PostgresErrorCodes.DeadlockDetected)
                {
                    await transaction.RollbackAsync();
                    if (attempt == 2) return false;
                    await Task.Delay(
                        attempt switch { 0 => 50, 1 => 150, _ => 450 });
                }
            }
            return false;
        }

        _ = await Task.WhenAll(ApplyAsync(1), ApplyAsync(2));
        using var verificationScope = services.CreateScope();
        var current = await verificationScope.ServiceProvider
            .GetRequiredService<IPointLatestProjectionRepository>()
            .GetCurrentAsync(pointId);
        if (current?.SourceSequence != 2 || current.NumericValue != 2)
            failures.Add(
                "T236 Latest race must converge on the greatest ordering tuple");
    }

    private static SimulatorConfigurationVersion Version(
        Guid configurationId,
        long configurationVersion,
        long seed,
        DateTime createdAtUtc) =>
        new(
            configurationId, configurationVersion, 1, seed, seed,
            checked((ulong)seed), SimulatorScenario.Constant,
            SimulatorConfigurationConstants.AlgorithmId,
            SimulatorConfigurationConstants.AlgorithmVersion,
            "acceptance-race-user", "acceptance-race-user", createdAtUtc,
            $"acceptance-race-{configurationId:N}",
            $"acceptance-race-{configurationVersion}");
}
