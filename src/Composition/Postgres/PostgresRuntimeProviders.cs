using IUMP.BuildingBlocks.Persistence;
using IUMP.Api.Infrastructure;
using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Acquisition.Application;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Acquisition.Domain;
using IUMP.Modules.IAM.Contracts;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Telemetry.Application;
using IUMP.Modules.Telemetry.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IUMP.Composition.Postgres;

public sealed class PostgresIdentitySnapshotProvider(
    IdentityRuntimeGateway identities) :
    IOrganizationCallerSnapshotProvider,
    IRunCallerSnapshotProvider,
    IConfigurationCallerSnapshotProvider
{
    public async Task<OrganizationCallerSnapshot?> ResolveAsync(
        string userId,
        CancellationToken ct = default)
    {
        var identity = await ReadAsync(userId, ct);
        return identity is null ? null : new OrganizationCallerSnapshot(
            identity.UserId, identity.Username, identity.IsActive,
            identity.Roles, identity.SiteScopes, identity.AreaScopes);
    }

    async Task<RunCallerSnapshot?> IRunCallerSnapshotProvider.ResolveAsync(
        string userId,
        CancellationToken ct)
    {
        var identity = await ReadAsync(userId, ct);
        return identity is null ? null : new RunCallerSnapshot(
            identity.UserId, identity.Username, identity.IsActive,
            identity.Roles, identity.SiteScopes, identity.AreaScopes);
    }

    async Task<ConfigurationCallerSnapshot?> IConfigurationCallerSnapshotProvider.ResolveAsync(
        string userId,
        CancellationToken ct)
    {
        var identity = await ReadAsync(userId, ct);
        return identity is null ? null : new ConfigurationCallerSnapshot(
            identity.UserId, identity.Username, identity.IsActive,
            identity.Roles, identity.SiteScopes, identity.AreaScopes);
    }

    private async Task<IdentitySnapshot?> ReadAsync(
        string userId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var id)) return null;
        var user = await identities.ResolveAsync(id.ToString("D"), ct);
        if (user is null) return null;
        return new IdentitySnapshot(
            user.UserId.ToString("D"),
            user.Username,
            user.IsActive,
            user.Roles,
            user.SiteScopes,
            user.AreaScopes);
    }

    private sealed record IdentitySnapshot(
        string UserId,
        string Username,
        bool IsActive,
        IReadOnlyCollection<string> Roles,
        IReadOnlyCollection<string> SiteScopes,
        IReadOnlyCollection<string> AreaScopes);
}

public sealed class RuntimeUtcClock :
    IUMP.Modules.Acquisition.Contracts.IUtcClock,
    ITelemetryUtcClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class PostgresImmutableConfigurationQuery(
    IAcquisitionConfigurationRepository configurations) :
    IImmutableSimulatorConfigurationQuery
{
    public async Task<ImmutableConfigurationSnapshot?> GetVersionAsync(
        Guid configurationId,
        long configurationVersion,
        CancellationToken ct = default)
    {
        var value = await configurations.GetVersionAsync(
            configurationId, configurationVersion, ct);
        return value is null ? null : new ImmutableConfigurationSnapshot(
            value.ConfigurationId, value.ConfigurationVersion,
            value.MinimumValue, value.MaximumValue);
    }
}

public sealed class PostgresSimulatorStartSnapshotProvider(
    NpgsqlDataSource dataSource) : ISimulatorStartSnapshotProvider
{
    public async Task<SimulatorStartSnapshot?> ResolveAsync(
        Guid sourceId,
        DateTime atUtc,
        CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await ReadAsync(connection, null, sourceId, atUtc, ct);
    }

    public async Task<bool> RecheckAsync(
        SimulatorStartSnapshot snapshot,
        ISimulatorRunTransaction transaction,
        DateTime atUtc,
        CancellationToken ct = default)
    {
        var postgres = PostgresTransactionResolver.Require(transaction);
        var current = await ReadAsync(
            postgres.Connection, postgres.Transaction, snapshot.SourceId, atUtc, ct);
        return current is not null && HeaderEqual(snapshot, current) &&
            snapshot.Points.SequenceEqual(current.Points);
    }

    private static async Task<SimulatorStartSnapshot?> ReadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid sourceId,
        DateTime atUtc,
        CancellationToken ct)
    {
        await using var header = new NpgsqlCommand("""
            SELECT ds.id,ds.source_type,ds.status,ds.version,
                   c.configuration_id,c.current_configuration_version,
                   v.interval_seconds,v.minimum_value,v.maximum_value,v.deterministic_seed,
                   v.scenario_type,v.algorithm_id,v.algorithm_version
            FROM catalog.data_sources ds
            JOIN acquisition.simulator_configuration c ON c.source_id=ds.id
            JOIN acquisition.simulator_configuration_version v
              ON v.configuration_id=c.configuration_id
             AND v.configuration_version=c.current_configuration_version
            WHERE ds.id=@source_id
            """, connection, transaction);
        header.Parameters.AddWithValue("source_id", sourceId);
        SimulatorStartSnapshot? result;
        await using (var reader = await header.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return null;
            result = new SimulatorStartSnapshot(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                reader.GetInt64(3), reader.GetGuid(4), reader.GetInt64(5),
                reader.GetInt32(6), reader.GetDouble(7), reader.GetDouble(8),
                checked((ulong)reader.GetInt64(9)),
                Enum.Parse<SimulatorScenario>(reader.GetString(10), false),
                reader.GetString(11), reader.GetInt32(12), []);
        }

        await using var points = new NpgsqlCommand("""
            SELECT p.id,p.version,p.status,
                   s.id,s.version,s.status,
                   ar.id,ar.version,ar.status,
                   a.id,a.version,a.status,
                   m.mapping_id,m.version,m.status,m.effective_from,m.effective_to,
                   p.metric_id,p.unit_id,u.symbol
            FROM catalog.source_point_mapping m
            JOIN organization.measurement_points p
              ON p.id=m.point_id::uuid
            JOIN organization.sites s ON s.id=p.site_id
            JOIN organization.areas ar ON ar.id=p.area_id
            JOIN organization.assets a ON a.id=p.asset_id
            JOIN catalog.units u ON u.id=p.unit_id::uuid
            WHERE m.data_source_id=@source_id
              AND m.status='Active'
              AND m.effective_from<=@at_utc
              AND (m.effective_to IS NULL OR m.effective_to>@at_utc)
            ORDER BY p.id,m.mapping_id
            """, connection, transaction);
        points.Parameters.AddWithValue("source_id", sourceId);
        points.Parameters.AddWithValue("at_utc", atUtc.ToUniversalTime());
        await using var pointReader = await points.ExecuteReaderAsync(ct);
        var values = new List<SimulatorStartPointSnapshot>();
        while (await pointReader.ReadAsync(ct))
            values.Add(new SimulatorStartPointSnapshot(
                pointReader.GetGuid(0), pointReader.GetInt64(1), pointReader.GetString(2),
                pointReader.GetGuid(3).ToString("D"), pointReader.GetInt64(4),
                pointReader.GetString(5), pointReader.GetGuid(6).ToString("D"),
                pointReader.GetInt64(7), pointReader.GetString(8),
                pointReader.GetGuid(9), pointReader.GetInt64(10), pointReader.GetString(11),
                pointReader.GetGuid(12), pointReader.GetInt64(13), pointReader.GetString(14),
                pointReader.GetDateTime(15).ToUniversalTime(),
                pointReader.IsDBNull(16) ? null : pointReader.GetDateTime(16).ToUniversalTime(),
                Guid.Parse(pointReader.GetString(17)), Guid.Parse(pointReader.GetString(18)),
                pointReader.GetString(19)));
        return result with { Points = values };
    }

    private static bool HeaderEqual(
        SimulatorStartSnapshot expected,
        SimulatorStartSnapshot current) =>
        expected.SourceId == current.SourceId &&
        expected.SourceType == current.SourceType &&
        expected.SourceStatus == current.SourceStatus &&
        expected.SourceVersion == current.SourceVersion &&
        expected.ConfigurationId == current.ConfigurationId &&
        expected.ConfigurationVersion == current.ConfigurationVersion &&
        expected.IntervalSeconds == current.IntervalSeconds &&
        expected.MinimumValue == current.MinimumValue &&
        expected.MaximumValue == current.MaximumValue &&
        expected.DeterministicSeed == current.DeterministicSeed &&
        expected.Scenario == current.Scenario &&
        expected.AlgorithmId == current.AlgorithmId &&
        expected.AlgorithmVersion == current.AlgorithmVersion;
}

public sealed class PostgresSimulatorRunOwnerEventWriter(
    ITransactionalOutboxWriter outbox) : ISimulatorRunOwnerEventWriter
{
    public ValueTask StageAsync(
        SimulatorRunOwnerEvent ownerEvent,
        ISimulatorRunTransaction transaction,
        CancellationToken ct = default)
    {
        var site = ownerEvent.SiteIds.FirstOrDefault();
        var envelope = new OwnerEventEnvelope(
            ownerEvent.EventId, ownerEvent.EventType, ownerEvent.SchemaVersion,
            ownerEvent.Producer, ownerEvent.AggregateType,
            ownerEvent.AggregateId.ToString("D"), ownerEvent.AggregateVersion,
            ownerEvent.ActorId, ownerEvent.ActorUsername,
            ownerEvent.Before, ownerEvent.After, ownerEvent.Action, ownerEvent.Summary,
            ownerEvent.OccurredAtUtc, ownerEvent.CorrelationId, ownerEvent.CausationId,
            site, null);
        return outbox.EnqueueAsync(envelope, transaction, ct);
    }
}

public sealed class PostgresTelemetryProviderSnapshotQuery(
    NpgsqlDataSource dataSource) : ITelemetryProviderSnapshotQuery
{
    public async Task<TelemetryProviderSnapshot?> GetAsync(
        TelemetryMeasurementRequest request,
        DateTime receivedAtUtc,
        CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await ReadAsync(connection, null, request, receivedAtUtc, ct);
    }

    public async Task<TelemetryProviderRecheckResult> RecheckAsync(
        TelemetryProviderSnapshot snapshot,
        ITelemetryFlowTransaction transaction,
        CancellationToken ct = default)
    {
        var request = new TelemetryMeasurementRequest(
            Guid.Empty.ToString("D"), snapshot.SourceId, Guid.Empty, snapshot.PointId,
            snapshot.MappingId, snapshot.MappingVersion, 0,
            SimulatorConfigurationConstants.AlgorithmId,
            SimulatorConfigurationConstants.AlgorithmVersion,
            Guid.Empty, 1, DateTime.UtcNow, 0, snapshot.UnitCode,
            "IUMP.Worker.Simulator", "recheck", "recheck");
        var current = await GetAsync(request, DateTime.UtcNow, ct) ?? snapshot with
        {
            PointExists = false
        };
        return TelemetryProviderRecheckResult.Compare(snapshot, current);
    }

    private static async Task<TelemetryProviderSnapshot?> ReadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        TelemetryMeasurementRequest request,
        DateTime atUtc,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT p.id,p.status,p.version,p.metric_id,p.unit_id,
                   s.id,s.status,s.version,
                   ar.id,ar.status,ar.version,
                   a.id,a.status,a.version,
                   ds.id,ds.source_type,ds.status,ds.version,
                   m.mapping_id,m.status,m.version,m.effective_from,m.effective_to,m.point_id,
                   metric.status,metric.version,
                   u.status,u.version,u.symbol,
                   c.version
            FROM organization.measurement_points p
            JOIN organization.sites s ON s.id=p.site_id
            JOIN organization.areas ar ON ar.id=p.area_id
            JOIN organization.assets a ON a.id=p.asset_id
            JOIN catalog.source_point_mapping m
              ON m.mapping_id=@mapping_id AND m.point_id=p.id::text
            JOIN catalog.data_sources ds ON ds.id=m.data_source_id
            JOIN catalog.metrics metric ON metric.id=p.metric_id::uuid
            JOIN catalog.units u ON u.id=p.unit_id::uuid
            LEFT JOIN catalog.metric_unit_compatibilities c
              ON c.metric_id=metric.id AND c.unit_id=u.id
            WHERE p.id=@point_id AND ds.id=@source_id
            """, connection, transaction);
        command.Parameters.AddWithValue("point_id", request.PointId);
        command.Parameters.AddWithValue("source_id", request.SourceId);
        command.Parameters.AddWithValue("mapping_id", request.MappingId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var pointStatus = reader.GetString(1);
        var siteStatus = reader.GetString(6);
        var areaStatus = reader.GetString(9);
        var assetStatus = reader.GetString(12);
        var sourceStatus = reader.GetString(16);
        var mappingStatus = reader.GetString(19);
        var metricStatus = reader.GetString(24);
        var unitStatus = reader.GetString(26);
        var effectiveFrom = reader.GetDateTime(21).ToUniversalTime();
        DateTime? effectiveTo = reader.IsDBNull(22)
            ? null : reader.GetDateTime(22).ToUniversalTime();
        var compatible = !reader.IsDBNull(29);
        var effective = effectiveFrom <= atUtc &&
            (effectiveTo is null || effectiveTo > atUtc);
        var metricId = Guid.Parse(reader.GetString(3));
        var unitId = Guid.Parse(reader.GetString(4));
        return new TelemetryProviderSnapshot(
            reader.GetGuid(0), true, pointStatus == "Active",
            siteStatus == "Active", areaStatus == "Active", assetStatus == "Active",
            reader.GetInt64(7), reader.GetInt64(10), reader.GetInt64(13),
            reader.GetInt64(2), reader.GetGuid(14), reader.GetString(15), true,
            sourceStatus == "Active", reader.GetInt64(17), reader.GetGuid(18), true,
            mappingStatus == "Active", effective, Guid.Parse(reader.GetString(23)),
            reader.GetInt64(20), true, metricId == Guid.Parse(reader.GetString(3)),
            metricStatus == "Active", reader.GetInt64(25), true,
            unitStatus == "Active", compatible, reader.GetString(28), reader.GetInt64(27),
            $"{metricId:D}:{unitId:D}", compatible ? 1 : 0,
            compatible ? "Active" : "Inactive",
            reader.GetGuid(5).ToString("D"), reader.GetGuid(8).ToString("D"),
            reader.GetGuid(5).ToString("D"), siteStatus,
            reader.GetGuid(8).ToString("D"), areaStatus,
            reader.GetGuid(11).ToString("D"), assetStatus,
            pointStatus, sourceStatus, mappingStatus,
            metricId.ToString("D"), metricStatus,
            unitId.ToString("D"), unitStatus, effectiveFrom, effectiveTo);
    }
}

public sealed class PostgresTelemetryIngestionClient(
    IngestMeasurement ingestion) : ITelemetryIngestionClient
{
    public async Task<CanonicalTelemetryIngestionResult> DispatchCanonicalAsync(
        SimulatorProductionPayload payload,
        CancellationToken ct = default)
    {
        var request = new TelemetryMeasurementRequest(
            payload.MeasurementId.ToString("D"), payload.SourceId, payload.RunId,
            payload.PointId, payload.MappingId, payload.MappingVersion,
            payload.SourceSequence, payload.AlgorithmId, payload.AlgorithmVersion,
            payload.ConfigurationId, payload.ConfigurationVersion,
            payload.SourceTimestampUtc, payload.NumericValue, payload.UnitCode,
            payload.ProducerIdentity, payload.CorrelationId, payload.LineageId);
        var result = await ingestion.ExecuteAsync(request,
            new TrustedProducerContext(
                true, payload.ProducerIdentity, "Simulator", 1), ct);
        var terminal = result.OriginalResult;
        if (terminal is null)
            return new CanonicalTelemetryIngestionResult(
                CanonicalTelemetryDisposition.Rejected,
                new CanonicalTelemetryOriginalResult(
                    ProductionFinalClassification.Rejected, false, null,
                    null, null, result.ErrorCode ?? "TELEMETRY_FAILED", null,
                    DateTime.UtcNow, payload.CorrelationId, payload.LineageId),
                result.ErrorCode, payload.CorrelationId);
        var classification = terminal.FinalClassification ==
            TelemetryFinalClassification.Accepted
                ? ProductionFinalClassification.Accepted
                : ProductionFinalClassification.Rejected;
        return new CanonicalTelemetryIngestionResult(
            result.Disposition switch
            {
                TelemetryDisposition.Accepted => CanonicalTelemetryDisposition.Accepted,
                TelemetryDisposition.Rejected => CanonicalTelemetryDisposition.Rejected,
                TelemetryDisposition.Duplicate => CanonicalTelemetryDisposition.Duplicate,
                _ => CanonicalTelemetryDisposition.Rejected
            },
            new CanonicalTelemetryOriginalResult(
                classification, terminal.MeasurementPersisted,
                terminal.PersistedMeasurementId, terminal.QualityCode?.ToString(),
                terminal.ReasonCode, terminal.RejectionCode, terminal.LatestAdvanced,
                terminal.CompletedAtUtc, terminal.OriginalCorrelationId,
                terminal.OriginalLineageId),
            result.ErrorCode, payload.CorrelationId);
    }
}

public sealed class PostgresSimulatorProductionEligibility(
    NpgsqlDataSource dataSource) : ISimulatorProductionEligibility
{
    public async Task<(bool IsActive, string? ErrorCode)> IsPinnedInputActiveAsync(
        SimulatorRun run,
        SimulatorRunPointState pointState,
        CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT ds.status,m.status,p.status,s.status,ar.status,a.status
            FROM catalog.data_sources ds
            JOIN catalog.source_point_mapping m
              ON m.mapping_id=@mapping_id AND m.data_source_id=ds.id
            JOIN organization.measurement_points p
              ON p.id=@point_id AND p.id::text=m.point_id
            JOIN organization.sites s ON s.id=p.site_id
            JOIN organization.areas ar ON ar.id=p.area_id
            JOIN organization.assets a ON a.id=p.asset_id
            WHERE ds.id=@source_id
            """, connection);
        command.Parameters.AddWithValue("source_id", run.SourceId);
        command.Parameters.AddWithValue("mapping_id", pointState.MappingId);
        command.Parameters.AddWithValue("point_id", pointState.PointId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return (false, "PROVIDER_MISSING");
        if (reader.GetString(0) != "Active") return (false, "SOURCE_INACTIVE");
        if (reader.GetString(1) != "Active") return (false, "MAPPING_INACTIVE");
        if (reader.GetString(2) != "Active") return (false, "POINT_INACTIVE");
        if (reader.GetString(3) != "Active" ||
            reader.GetString(4) != "Active" ||
            reader.GetString(5) != "Active")
            return (false, "ANCESTOR_INACTIVE");
        return (true, null);
    }
}

public static class PostgresRuntimeProviderRegistration
{
    public static IServiceCollection AddIumpPostgresRuntimeProviders(
        this IServiceCollection services)
    {
        services.AddScoped<PostgresIdentitySnapshotProvider>();
        services.AddScoped<IOrganizationCallerSnapshotProvider>(provider =>
            provider.GetRequiredService<PostgresIdentitySnapshotProvider>());
        services.AddScoped<IRunCallerSnapshotProvider>(provider =>
            provider.GetRequiredService<PostgresIdentitySnapshotProvider>());
        services.AddScoped<IConfigurationCallerSnapshotProvider>(provider =>
            provider.GetRequiredService<PostgresIdentitySnapshotProvider>());
        services.AddSingleton<RuntimeUtcClock>();
        services.AddSingleton<IUMP.Modules.Acquisition.Contracts.IUtcClock>(provider =>
            provider.GetRequiredService<RuntimeUtcClock>());
        services.AddSingleton<ITelemetryUtcClock>(provider =>
            provider.GetRequiredService<RuntimeUtcClock>());
        services.AddScoped<IImmutableSimulatorConfigurationQuery,
            PostgresImmutableConfigurationQuery>();
        services.AddScoped<ISimulatorStartSnapshotProvider,
            PostgresSimulatorStartSnapshotProvider>();
        services.AddScoped<ISimulatorRunOwnerEventWriter,
            PostgresSimulatorRunOwnerEventWriter>();
        services.AddScoped<ITelemetryProviderSnapshotQuery,
            PostgresTelemetryProviderSnapshotQuery>();
        services.AddScoped<ISimulatorProductionEligibility,
            PostgresSimulatorProductionEligibility>();
        services.AddScoped<TelemetryPersistenceService>();
        services.AddScoped<IngestMeasurement>();
        services.AddScoped<ITelemetryIngestionClient,
            PostgresTelemetryIngestionClient>();
        services.AddScoped<ISimulatorValueGenerator, DeterministicGenerator>();
        services.AddScoped<IMeasurementIdentityFactory, MeasurementIdentity>();
        services.AddScoped<IProductionAttemptService, ProductionAttemptService>();
        services.AddScoped<ISimulatorProductionCoordinator,
            SimulatorProductionCoordinator>();
        services.AddScoped<SimulatorRunCommandService>();
        services.AddScoped<ISimulatorWorkspaceQueryPort,
            PostgresSimulatorWorkspaceQueryPort>();
        services.AddScoped<IOrganizationAuthorization,
            OrganizationRoleScopeAuthorization>();
        return services;
    }
}
