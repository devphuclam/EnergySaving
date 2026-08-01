using IUMP.Api.Infrastructure;
using IUMP.Composition.Postgres;
using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Modules.Telemetry.Application;
using IUMP.Modules.Telemetry.Contracts;
using IUMP.Modules.Telemetry.Domain;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IUMP.Tests.Integration.OperationalWorkspace;

/// T058: deterministic PostgreSQL-backed selector/latest/health contract.
/// Fixtures are isolated by unique owner codes and are created only through
/// module-owned repositories and application services (never ad-hoc SQL).
public static class LatestHealthTests
{
    private const int PointCount = 506;
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }

    public static async Task<IReadOnlyList<string>> RunAsync(IServiceProvider root)
    {
        var failures = new List<string>();
        TestCount = 0;
        AssertionCount = 0;
        try
        {
            var configuration = PostgresRuntimeConfiguration.CreateRuntime();
            Check(configuration.Host == PostgresRuntimeConfiguration.ApprovedLocalHost &&
                  configuration.Port == PostgresRuntimeConfiguration.ApprovedLocalPort &&
                  configuration.Database == PostgresRuntimeConfiguration.ApprovedLocalDatabase,
                "T058 must target only 127.0.0.1:5433/iump_dev", failures);

            using var scope = root.CreateScope();
            var services = scope.ServiceProvider;
            var fixture = await CreateFixtureAsync(services);
            var port = services.GetRequiredService<ITelemetryWorkspaceQueryPort>();
            var admin = new ServerPrincipal(Guid.NewGuid(), "phase4-admin", new HashSet<string>(), new HashSet<string>(), true);
            var engineer = new ServerPrincipal(Guid.NewGuid(), "phase4-engineer",
                new HashSet<string> { fixture.SiteId.ToString("D") }, new HashSet<string>(), false);
            var areaEngineer = new ServerPrincipal(Guid.NewGuid(), "phase4-area-engineer", new HashSet<string>(),
                new HashSet<string> { fixture.AreaId.ToString("D") }, false);
            var outside = new ServerPrincipal(Guid.NewGuid(), "phase4-outside",
                new HashSet<string> { Guid.NewGuid().ToString("D") }, new HashSet<string>(), false);

            var before = await CaptureOwnerStateAsync(services, fixture);
            var adminSites = await port.GetOptionsAsync(admin,
                new(TelemetryOptionLevel.Sites));
            var engineerSites = await port.GetOptionsAsync(engineer,
                new(TelemetryOptionLevel.Sites));
            var areaSites = await port.GetOptionsAsync(areaEngineer,
                new(TelemetryOptionLevel.Sites));
            var outsideSites = await port.GetOptionsAsync(outside,
                new(TelemetryOptionLevel.Sites));
            Check(adminSites.Sites.Any(value => value.SiteId == fixture.SiteId) &&
                  adminSites.Sites.Any(value => value.SiteId == fixture.OutOfScopeSiteId) &&
                  engineerSites.Sites.Any(value => value.SiteId == fixture.SiteId) &&
                  engineerSites.Sites.All(value => value.SiteId != fixture.OutOfScopeSiteId) &&
                  areaSites.Sites.Any(value => value.SiteId == fixture.SiteId) &&
                  outsideSites.Sites.All(value => value.SiteId != fixture.SiteId),
                "admin/Site-scope/Area-scope/out-of-scope Site selectors were not isolated", failures);

            var areas = await port.GetOptionsAsync(engineer,
                new(TelemetryOptionLevel.Areas, SiteId: fixture.SiteId));
            var assets = await port.GetOptionsAsync(engineer,
                new(TelemetryOptionLevel.Assets, SiteId: fixture.SiteId, AreaId: fixture.AreaId));
            Check(areas.Areas.Count(value => value.AreaId == fixture.AreaId) == 1 &&
                  assets.Assets.Count(value => value.AssetId == fixture.AssetId) == 1,
                "parent selectors must be independent of Point paging", failures);

            var firstPage = await port.GetOptionsAsync(engineer,
                new(TelemetryOptionLevel.Points, 1, 100, fixture.SiteId, fixture.AreaId, fixture.AssetId));
            var repeatedFirstPage = await port.GetOptionsAsync(engineer,
                new(TelemetryOptionLevel.Points, 1, 100, fixture.SiteId, fixture.AreaId, fixture.AssetId));
            var sixthPage = await port.GetOptionsAsync(engineer,
                new(TelemetryOptionLevel.Points, 6, 100, fixture.SiteId, fixture.AreaId, fixture.AssetId));
            var seventhPage = await port.GetOptionsAsync(engineer,
                new(TelemetryOptionLevel.Points, 7, 100, fixture.SiteId, fixture.AreaId, fixture.AssetId));
            Check(firstPage.ScopedCount == PointCount && firstPage.Points.Count == 100,
                "the scoped Point count/page one must include all 506 fixture Points", failures);
            Check(firstPage.SelectedPointId is null && repeatedFirstPage.SelectedPointId is null &&
                  firstPage.Points.Select(point => point.PointId)
                      .SequenceEqual(repeatedFirstPage.Points.Select(point => point.PointId)),
                "Point ordering must be deterministic and must never imply a selection", failures);
            Check(sixthPage.ScopedCount == PointCount && sixthPage.Points.Count == 6 && sixthPage.Page == 6,
                "the final partial Point page must remain reachable beyond the old 500-row cap", failures);
            Check(seventhPage.ScopedCount == PointCount && seventhPage.Points.Count == 0 && seventhPage.Page == 7,
                "an empty page beyond the final result must preserve the authorized filtered total", failures);
            var searched = await port.GetOptionsAsync(engineer,
                new(TelemetryOptionLevel.Points, 1, 100, fixture.SiteId, fixture.AreaId,
                    fixture.AssetId, fixture.SearchCode));
            Check(searched.ScopedCount == 1 && searched.Points.Single().PointId == fixture.SearchPointId,
                "server-side Point search must run inside the selected authorized hierarchy", failures);
            var outsidePoints = await port.GetOptionsAsync(outside,
                new(TelemetryOptionLevel.Points, 1, 100, fixture.SiteId, fixture.AreaId, fixture.AssetId));
            Check(outsidePoints.Points.Count == 0 && outsidePoints.ScopedCount == 0,
                "out-of-scope Point paging must not disclose counts or rows", failures);

            var noMapping = await CurrentAsync(port, admin, fixture, fixture.NoMappingPointId);
            var noData = await CurrentAsync(port, admin, fixture, fixture.NoDataPointId);
            var overlapProtected = await CurrentAsync(port, admin, fixture, fixture.OverlapProtectedPointId);
            var zero = await CurrentAsync(port, admin, fixture, fixture.ZeroPointId);
            var data = await CurrentAsync(port, admin, fixture, fixture.DataPointId);
            Check(noMapping.DataState == TelemetryDataState.NotConfigured && !noMapping.HasData,
                "a Point with no active Mapping must be NotConfigured", failures);
            Check(noData.DataState == TelemetryDataState.NoData && !noData.HasData && noData.Value is null,
                "a Point with one active Mapping and no measurement must be NoData", failures);
            Check(fixture.OverlappingMappingRejected &&
                  overlapProtected.DataState == TelemetryDataState.NoData && !overlapProtected.HasData,
                "the canonical exclusion invariant must reject an ambiguous active Mapping without damaging the first relationship", failures);
            Check(zero.DataState == TelemetryDataState.Data && zero.HasData && zero.Value == 0d,
                "accepted zero must remain numeric data rather than NoData", failures);
            Check(data.DataState == TelemetryDataState.Data && data.Value == 42d &&
                  data.Run?.RunId == fixture.RunId && data.Run.Generated == 3 &&
                  data.Run.Accepted == 2 && data.Run.Rejected == 1,
                "rejected-newer/stale measurements or an unrelated Run displaced the selected current value/counters", failures);
            Check(data.Point.Metric == fixture.MetricCode && data.Point.Unit == fixture.UnitSymbol &&
                  data.Quality == "Good" &&
                  data.SourceTimestampUtc == fixture.DataSourceTimestampUtc &&
                  data.ReceivedAtUtc == fixture.DataReceivedAtUtc,
                "canonical Metric/Unit, quality, and source/received timestamps were not preserved", failures);
            Check(data.Source?.SourceId == fixture.DataSourceId &&
                  data.Health?.PointId == fixture.DataPointId &&
                  data.Health.SourceId == fixture.DataSourceId && data.Health.Status == "Online" &&
                  data.Run?.Status == "Running",
                "Source Health or Run status did not belong to the selected Point relationship", failures);

            var mismatchFailedSafely = false;
            try
            {
                _ = await port.GetCurrentAsync(new(fixture.SiteId, fixture.AreaId,
                    fixture.OutOfScopeAssetId, fixture.DataPointId), engineer);
            }
            catch (RuntimeScopeDeniedException) { mismatchFailedSafely = true; }
            catch (TelemetryHierarchyConflictException) { mismatchFailedSafely = true; }
            Check(mismatchFailedSafely, "a Point/hierarchy mismatch must fail without returning metadata", failures);

            var denied = false;
            try
            {
                _ = await port.GetCurrentAsync(new(fixture.OutOfScopeSiteId, fixture.OutOfScopeAreaId,
                    fixture.OutOfScopeAssetId, fixture.OutOfScopePointId), engineer);
            }
            catch (RuntimeScopeDeniedException) { denied = true; }
            Check(denied,
                "the Engineer's real out-of-scope Point under another Site must be indistinguishable from not found",
                failures);

            var after = await CaptureOwnerStateAsync(services, fixture);
            Check(before == after,
                "selector/current reads changed Organization, Catalog, or Run owner state", failures);
            TestCount = 13;
        }
        catch (Exception exception)
        {
            failures.Add($"T058 unexpected {exception.GetType().Name}: {exception.Message}");
        }
        return failures;
    }

    private static Task<TelemetryWorkspaceCurrent> CurrentAsync(
        ITelemetryWorkspaceQueryPort port, ServerPrincipal principal, Fixture fixture, Guid pointId) =>
        port.GetCurrentAsync(new(fixture.SiteId, fixture.AreaId, fixture.AssetId, pointId), principal);

    private static async Task<Fixture> CreateFixtureAsync(IServiceProvider services)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var ownerId = Guid.NewGuid();
        var siteId = SiteId.New();
        var areaId = AreaId.New();
        var assetId = AssetId.New();
        var outOfScopeSiteId = SiteId.New();
        var outOfScopeAreaId = AreaId.New();
        var outOfScopeAssetId = AssetId.New();
        var outOfScopePointId = PointId.New();
        var metricId = MetricId.New();
        var unitId = UnitId.New();
        var organization = services.GetRequiredService<IOrganizationCommandRepository>();
        var catalog = services.GetRequiredService<ICatalogCommandRepository>();
        await organization.AddSiteAsync(new(siteId, $"P4S{suffix}", $"Phase 4 Site {suffix}", null,
            "UTC", SiteStatus.Active, 2));
        await organization.AddAreaAsync(new(areaId, siteId, $"P4A{suffix}", $"Phase 4 Area {suffix}", null,
            AreaStatus.Active, 2));
        await organization.AddAssetAsync(new(assetId, siteId, areaId, $"P4X{suffix}",
            $"Phase 4 Asset {suffix}", null, AssetStatus.Active, 2));
        await organization.AddSiteAsync(new(outOfScopeSiteId, $"P4O{suffix}",
            $"Phase 4 Out-of-scope Site {suffix}", null, "UTC", SiteStatus.Active, 2));
        await organization.AddAreaAsync(new(outOfScopeAreaId, outOfScopeSiteId, $"P4OA{suffix}",
            $"Phase 4 Out-of-scope Area {suffix}", null, AreaStatus.Active, 2));
        await organization.AddAssetAsync(new(outOfScopeAssetId, outOfScopeSiteId, outOfScopeAreaId,
            $"P4OX{suffix}", $"Phase 4 Out-of-scope Asset {suffix}", null, AssetStatus.Active, 2));
        await catalog.AddMetricAsync(new(metricId, $"P4M{suffix}", $"Phase 4 Metric {suffix}", MetricStatus.Active, 1));
        await catalog.AddUnitAsync(new(unitId, $"P4U{suffix}", "p4u", MetricUnitStatus.Active, 1));
        await catalog.AddCompatibilityAsync(new(metricId, unitId, true, 1));
        await organization.AddPointAsync(new(outOfScopePointId, outOfScopeSiteId, outOfScopeAreaId,
            outOfScopeAssetId, $"P4OP{suffix}", "Phase 4 Out-of-scope Point",
            metricId.Value.ToString("D"), unitId.Value.ToString("D"), ownerId.ToString("D"),
            10, 30, PointStatus.Active, 2));

        var pointIds = new List<Guid>(PointCount);
        for (var index = 0; index < PointCount; index++)
        {
            var id = PointId.New();
            pointIds.Add(id.Value);
            await organization.AddPointAsync(new(id, siteId, areaId, assetId,
                $"P4{suffix}{index:D4}", $"Phase 4 Point {index:D4}", metricId.Value.ToString("D"),
                unitId.Value.ToString("D"), ownerId.ToString("D"), 10, 30, PointStatus.Active, 2));
        }

        var now = DateTime.UtcNow;
        var noDataSource = await AddSourceAsync(catalog, siteId.Value, suffix, "NODATA");
        var ambiguousSourceA = await AddSourceAsync(catalog, siteId.Value, suffix, "AMBA");
        var ambiguousSourceB = await AddSourceAsync(catalog, siteId.Value, suffix, "AMBB");
        var dataSource = await AddSourceAsync(catalog, siteId.Value, suffix, "DATA");
        var unrelatedSource = await AddSourceAsync(catalog, siteId.Value, suffix, "OTHER");
        var noDataMapping = await AddMappingAsync(catalog, noDataSource, pointIds[1], now);
        _ = await AddMappingAsync(catalog, ambiguousSourceA, pointIds[2], now);
        var overlappingMappingRejected = false;
        try { _ = await AddMappingAsync(catalog, ambiguousSourceB, pointIds[2], now); }
        catch (InvalidOperationException exception) when (exception.Message == "CATALOG_CONFLICT")
        {
            overlappingMappingRejected = true;
        }
        var zeroMapping = await AddMappingAsync(catalog, dataSource, pointIds[3], now);
        var dataMapping = await AddMappingAsync(catalog, dataSource, pointIds[4], now);
        var unrelatedMapping = await AddMappingAsync(catalog, unrelatedSource, pointIds[5], now);

        var configurationId = Guid.NewGuid();
        var configurationRepository = services.GetRequiredService<IAcquisitionConfigurationRepository>();
        await configurationRepository.CreateAsync(
            new(configurationId, dataSource.Value, 1, 1),
            new(configurationId, 1, 10, -100, 100, 42, SimulatorScenario.Normal,
                SimulatorConfigurationConstants.AlgorithmId, 1, ownerId.ToString("D"), "phase4-fixture",
                now, $"phase4-{suffix}", null));

        var runId = Guid.NewGuid();
        var runRepository = services.GetRequiredService<IAcquisitionRunRepository>();
        var runUnitOfWork = services.GetRequiredService<ISimulatorRunUnitOfWork>();
        var run = new SimulatorRun(runId, dataSource.Value, 2, configurationId, 1,
            SimulatorConfigurationConstants.AlgorithmId, 1, SimulatorRunStatus.Running, 1,
            3, 2, 1, null, null, now, now, null, null, null, ownerId.ToString("D"),
            "phase4-fixture", $"phase4-{suffix}", null);
        var states = new[]
        {
            PointState(runId, pointIds[3], zeroMapping, metricId.Value, unitId.Value, siteId.Value, areaId.Value, now),
            PointState(runId, pointIds[4], dataMapping, metricId.Value, unitId.Value, siteId.Value, areaId.Value, now)
        };
        await using (var transaction = await runUnitOfWork.BeginAsync())
        {
            await runRepository.CreateAsync(run, states, transaction);
            await transaction.CommitAsync();
        }

        var unrelatedRunId = Guid.NewGuid();
        await using (var transaction = await runUnitOfWork.BeginAsync())
        {
            await runRepository.CreateAsync(run with {
                RunId = unrelatedRunId, SourceId = unrelatedSource.Value,
                CreatedAtUtc = now.AddSeconds(1), StartedAtUtc = now.AddSeconds(1),
                CorrelationId = $"phase4-other-{suffix}"
            }, [PointState(unrelatedRunId, pointIds[5], unrelatedMapping, metricId.Value,
                unitId.Value, siteId.Value, areaId.Value, now)], transaction);
            await transaction.CommitAsync();
        }

        var ingestion = services.GetRequiredService<IngestMeasurement>();
        await IngestAsync(ingestion, dataSource.Value, runId, pointIds[3], zeroMapping, configurationId, 1,
            0, now.AddMinutes(-2), suffix);
        var dataMeasurementId = await IngestAsync(ingestion, dataSource.Value, runId, pointIds[4], dataMapping, configurationId, 1,
            42, now.AddMinutes(-2), suffix);
        await IngestAsync(ingestion, dataSource.Value, runId, pointIds[4], dataMapping, configurationId, 2,
            99, now.AddMinutes(-1), suffix, "wrong-unit");
        await IngestAsync(ingestion, dataSource.Value, runId, pointIds[4], dataMapping, configurationId, 3,
            12, now.AddMinutes(-3), suffix);
        await IngestAsync(ingestion, unrelatedSource.Value, unrelatedRunId, pointIds[5], unrelatedMapping,
            configurationId, 0, 777, now.AddSeconds(-30), suffix);
        var dataTimestamps = await ReadMeasurementTimestampsAsync(services, dataMeasurementId);

        return new(siteId.Value, areaId.Value, assetId.Value, outOfScopeSiteId.Value,
            outOfScopeAreaId.Value, outOfScopeAssetId.Value, outOfScopePointId.Value, pointIds[0],
            pointIds[1], pointIds[2], pointIds[3], pointIds[4], pointIds[^1],
            $"P4{suffix}{PointCount - 1:D4}", runId, dataSource.Value, $"P4M{suffix}", "p4u",
            noDataMapping.Value, overlappingMappingRejected,
            dataTimestamps.SourceTimestampUtc, dataTimestamps.ReceivedAtUtc);
    }

    private static async Task<DataSourceId> AddSourceAsync(
        ICatalogCommandRepository repository, Guid siteId, string suffix, string discriminator)
    {
        var id = DataSourceId.New();
        await repository.AddDataSourceAsync(new(id, $"P4{discriminator}{suffix}",
            $"Phase 4 {discriminator} {suffix}", SourceType.Simulator, SourceStatus.Active, 2, siteId));
        return id;
    }

    private static async Task<MappingId> AddMappingAsync(
        ICatalogCommandRepository repository, DataSourceId sourceId, Guid pointId, DateTime now)
    {
        var id = MappingId.New();
        await repository.AddMappingAsync(new(id, sourceId, pointId.ToString("D"), MappingStatus.Active,
            now.AddHours(-1), null, 2));
        return id;
    }

    private static SimulatorRunPointState PointState(
        Guid runId, Guid pointId, MappingId mappingId, Guid metricId, Guid unitId,
        Guid siteId, Guid areaId, DateTime now) =>
        new(runId, pointId, 2, mappingId.Value, 2, metricId, unitId, "p4u", 2, 0,
            Enumerable.Repeat((byte)1, 25).ToArray(), now, siteId.ToString("D"), areaId.ToString("D"),
            null, null, 0, null, 1);

    private static async Task<Guid> IngestAsync(
        IngestMeasurement ingestion, Guid sourceId, Guid runId, Guid pointId, MappingId mappingId,
        Guid configurationId, long sequence, double value, DateTime sourceTimestampUtc, string suffix,
        string unitCode = "p4u")
    {
        var measurementId = MeasurementIdentityVerifier.Create(sourceId, runId, pointId, mappingId.Value, sequence, 1);
        var request = new TelemetryMeasurementRequest(measurementId.ToString("D"), sourceId, runId,
            pointId, mappingId.Value, 2, sequence, SimulatorConfigurationConstants.AlgorithmId, 1,
            configurationId, 1, sourceTimestampUtc, value, unitCode, "IUMP.Worker.Simulator",
            $"phase4-{suffix}-{sequence}-{pointId:N}", $"phase4-lineage-{suffix}");
        _ = await ingestion.ExecuteAsync(request,
            new TrustedProducerContext(true, request.ProducerIdentity, "Simulator", 1));
        return measurementId;
    }

    private static async Task<(DateTime SourceTimestampUtc, DateTime ReceivedAtUtc)>
        ReadMeasurementTimestampsAsync(IServiceProvider services, Guid measurementId)
    {
        var dataSource = services.GetRequiredService<NpgsqlDataSource>();
        await using var command = dataSource.CreateCommand("""
            SELECT source_timestamp_utc, received_at_utc
            FROM telemetry.measurement_raw
            WHERE measurement_id = @measurement_id
            """);
        command.Parameters.AddWithValue("measurement_id", measurementId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("Accepted Phase 4 fixture measurement was not persisted.");
        return (reader.GetDateTime(0), reader.GetDateTime(1));
    }

    private static async Task<OwnerState> CaptureOwnerStateAsync(IServiceProvider services, Fixture fixture)
    {
        var organization = services.GetRequiredService<IOrganizationCommandRepository>();
        var catalog = services.GetRequiredService<ICatalogCommandRepository>();
        var runs = services.GetRequiredService<IAcquisitionRunRepository>();
        var dataSource = services.GetRequiredService<NpgsqlDataSource>();
        await using var command = dataSource.CreateCommand("""
            SELECT
              (SELECT count(*) FROM integration.command_idempotency),
              (SELECT count(*) FROM audit.audit_event),
              (SELECT count(*) FROM integration.outbox_event),
              (SELECT count(*) FROM acquisition.simulator_run),
              (SELECT count(*) FROM telemetry.measurement_identity),
              (SELECT count(*) FROM telemetry.measurement_raw),
              (SELECT count(*) FROM telemetry.point_latest),
              (SELECT count(*) FROM telemetry.point_source_status)
            """);
        await using var reader = await command.ExecuteReaderAsync();
        _ = await reader.ReadAsync();
        return new((await organization.GetPointsForSiteAsync(new SiteId(fixture.SiteId))).Count,
            (await catalog.GetMappingsForPointAsync(fixture.DataPointId.ToString("D"))).Count,
            (await runs.GetAsync(fixture.RunId))?.Version ?? 0,
            reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3),
            reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6), reader.GetInt64(7));
    }

    private static void Check(bool condition, string message, ICollection<string> failures)
    {
        AssertionCount++;
        if (!condition) failures.Add(message);
    }

    private sealed record Fixture(Guid SiteId, Guid AreaId, Guid AssetId, Guid OutOfScopeSiteId,
        Guid OutOfScopeAreaId, Guid OutOfScopeAssetId, Guid OutOfScopePointId,
        Guid NoMappingPointId, Guid NoDataPointId, Guid OverlapProtectedPointId, Guid ZeroPointId,
        Guid DataPointId, Guid SearchPointId, string SearchCode, Guid RunId, Guid DataSourceId,
        string MetricCode, string UnitSymbol, Guid NoDataMappingId, bool OverlappingMappingRejected,
        DateTime DataSourceTimestampUtc, DateTime DataReceivedAtUtc);
    private sealed record OwnerState(int PointCount, int MappingCount, long RunVersion,
        long CommandIdempotencyCount, long AuditCount, long OutboxCount, long RunCount,
        long MeasurementIdentityCount, long MeasurementRawCount, long LatestCount, long HealthCount);
}
