using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Organization.Contracts;
using Npgsql;

namespace IUMP.Modules.Catalog.Infrastructure;

public sealed class PostgresCatalogRepositories :
    ICatalogCommandRepository,
    ICatalogEligibilityQueryRepository,
    ISourceMappingSnapshotQuery,
    ICatalogPointReadinessQuery,
    ICatalogSourceScopeQuery,
    IActivationCatalogParticipant
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresTransactionContext _hostTransactions;
    private readonly AsyncLocal<TransactionHolder?> _state = new();

    public PostgresCatalogRepositories(
        NpgsqlDataSource dataSource,
        PostgresTransactionContext hostTransactions)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _hostTransactions = hostTransactions ?? throw new ArgumentNullException(nameof(hostTransactions));
    }

    public Task<Metric?> GetMetricAsync(MetricId id, CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT id, code, name, status, version FROM catalog.metrics WHERE id = @value
            """, command => command.Parameters.AddWithValue("value", id.Value), MapMetric, ct);

    public Task<Metric?> FindMetricByCodeAsync(string code, CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT id, code, name, status, version FROM catalog.metrics WHERE code = upper(@value)
            """, command => command.Parameters.AddWithValue("value", code), MapMetric, ct);

    public Task AddMetricAsync(Metric metric, CancellationToken ct = default) =>
        InsertAsync("""
            INSERT INTO catalog.metrics (id, code, name, status, version)
            VALUES (@id, @code, @name, @status, @version)
            """, command =>
        {
            command.Parameters.AddWithValue("id", metric.Id.Value);
            command.Parameters.AddWithValue("code", metric.Code);
            command.Parameters.AddWithValue("name", metric.Name);
            command.Parameters.AddWithValue("status", metric.Status.ToString());
            command.Parameters.AddWithValue("version", metric.Version);
        }, ct);

    public Task UpdateMetricAsync(Metric metric, CancellationToken ct = default) =>
        OptimisticUpdateAsync("""
            UPDATE catalog.metrics
            SET code=@code, name=@name, status=@status, version=@version, updated_at=now()
            WHERE id=@id AND version=@expected_version
            """, command =>
        {
            command.Parameters.AddWithValue("id", metric.Id.Value);
            command.Parameters.AddWithValue("code", metric.Code);
            command.Parameters.AddWithValue("name", metric.Name);
            command.Parameters.AddWithValue("status", metric.Status.ToString());
            command.Parameters.AddWithValue("version", metric.Version);
            command.Parameters.AddWithValue("expected_version", metric.Version - 1);
        }, ct);

    public async Task<IReadOnlyList<Metric>> GetAllMetricsAsync(CancellationToken ct = default) =>
        await QueryAsync("SELECT id, code, name, status, version FROM catalog.metrics ORDER BY code",
            null, MapMetric, ct);

    public Task<MetricUnit?> GetUnitAsync(UnitId id, CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT id, code, symbol, status, version FROM catalog.units WHERE id = @value
            """, command => command.Parameters.AddWithValue("value", id.Value), MapUnit, ct);

    public Task<MetricUnit?> FindUnitByCodeAsync(string code, CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT id, code, symbol, status, version FROM catalog.units WHERE code = upper(@value)
            """, command => command.Parameters.AddWithValue("value", code), MapUnit, ct);

    public Task AddUnitAsync(MetricUnit unit, CancellationToken ct = default) =>
        InsertAsync("""
            INSERT INTO catalog.units (id, code, symbol, status, version)
            VALUES (@id, @code, @symbol, @status, @version)
            """, command =>
        {
            command.Parameters.AddWithValue("id", unit.Id.Value);
            command.Parameters.AddWithValue("code", unit.Code);
            command.Parameters.AddWithValue("symbol", unit.Symbol);
            command.Parameters.AddWithValue("status", unit.Status.ToString());
            command.Parameters.AddWithValue("version", unit.Version);
        }, ct);

    public Task UpdateUnitAsync(MetricUnit unit, CancellationToken ct = default) =>
        OptimisticUpdateAsync("""
            UPDATE catalog.units
            SET code=@code, symbol=@symbol, status=@status, version=@version, updated_at=now()
            WHERE id=@id AND version=@expected_version
            """, command =>
        {
            command.Parameters.AddWithValue("id", unit.Id.Value);
            command.Parameters.AddWithValue("code", unit.Code);
            command.Parameters.AddWithValue("symbol", unit.Symbol);
            command.Parameters.AddWithValue("status", unit.Status.ToString());
            command.Parameters.AddWithValue("version", unit.Version);
            command.Parameters.AddWithValue("expected_version", unit.Version - 1);
        }, ct);

    public async Task<IReadOnlyList<MetricUnit>> GetAllUnitsAsync(CancellationToken ct = default) =>
        await QueryAsync("SELECT id, code, symbol, status, version FROM catalog.units ORDER BY code",
            null, MapUnit, ct);

    public Task AddCompatibilityAsync(MetricUnitCompatibility compatibility, CancellationToken ct = default) =>
        InsertAsync("""
            INSERT INTO catalog.metric_unit_compatibilities
                (metric_id, unit_id, is_canonical, version)
            VALUES (@metric_id, @unit_id, @is_canonical, @version)
            """, command => BindCompatibility(command, compatibility), ct);

    public Task UpdateCompatibilityAsync(MetricUnitCompatibility compatibility, CancellationToken ct = default) =>
        OptimisticUpdateAsync("""
            UPDATE catalog.metric_unit_compatibilities
            SET is_canonical=@is_canonical, version=@version, updated_at=now()
            WHERE metric_id=@metric_id AND unit_id=@unit_id AND version=@expected_version
            """, command =>
        {
            BindCompatibility(command, compatibility);
            command.Parameters.AddWithValue("expected_version", compatibility.Version - 1);
        }, ct);

    public Task<MetricUnitCompatibility?> GetCompatibilityAsync(
        MetricId metricId,
        UnitId unitId,
        CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT metric_id, unit_id, is_canonical, version
            FROM catalog.metric_unit_compatibilities
            WHERE metric_id=@metric_id AND unit_id=@unit_id
            """, command =>
        {
            command.Parameters.AddWithValue("metric_id", metricId.Value);
            command.Parameters.AddWithValue("unit_id", unitId.Value);
        }, MapCompatibility, ct);

    public async Task<IReadOnlyList<MetricUnitCompatibility>> GetCompatibilitiesForMetricAsync(
        MetricId metricId,
        CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT metric_id, unit_id, is_canonical, version
            FROM catalog.metric_unit_compatibilities
            WHERE metric_id=@metric_id ORDER BY unit_id
            """, command => command.Parameters.AddWithValue("metric_id", metricId.Value),
            MapCompatibility, ct);

    public Task<MetricUnitCompatibility?> GetCanonicalUnitAsync(
        MetricId metricId,
        CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT metric_id, unit_id, is_canonical, version
            FROM catalog.metric_unit_compatibilities
            WHERE metric_id=@metric_id AND is_canonical
            """, command => command.Parameters.AddWithValue("metric_id", metricId.Value),
            MapCompatibility, ct);

    public Task<DataSource?> GetDataSourceAsync(DataSourceId id, CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT id, code, name, source_type, status, version, site_id
            FROM catalog.data_sources WHERE id=@value
            """, command => command.Parameters.AddWithValue("value", id.Value), MapSource, ct);

    public Task<DataSource?> FindDataSourceByCodeAsync(string code, CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT id, code, name, source_type, status, version, site_id
            FROM catalog.data_sources WHERE code=upper(@value)
            """, command => command.Parameters.AddWithValue("value", code), MapSource, ct);

    public Task AddDataSourceAsync(DataSource source, CancellationToken ct = default) =>
        InsertAsync("""
            INSERT INTO catalog.data_sources
                (id, code, name, source_type, status, version, site_id)
            VALUES (@id, @code, @name, @source_type, @status, @version, @site_id)
            """, command => BindSource(command, source), ct);

    public Task UpdateDataSourceAsync(DataSource source, CancellationToken ct = default) =>
        OptimisticUpdateAsync("""
            UPDATE catalog.data_sources
            SET code=@code, name=@name, source_type=@source_type, status=@status,
                version=@version, site_id=@site_id, updated_at=now()
            WHERE id=@id AND version=@expected_version
            """, command =>
        {
            BindSource(command, source);
            command.Parameters.AddWithValue("expected_version", source.Version - 1);
        }, ct);

    public async Task<IReadOnlyList<DataSource>> GetAllDataSourcesAsync(CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT id, code, name, source_type, status, version, site_id
            FROM catalog.data_sources ORDER BY code
            """, null, MapSource, ct);

    public async Task<IReadOnlyList<DataSource>> GetDataSourcesForSitesAsync(
        IReadOnlyCollection<Guid> siteIds, CancellationToken ct = default)
    {
        if (siteIds.Count == 0) return [];
        return await QueryAsync("""
            SELECT id, code, name, source_type, status, version, site_id
            FROM catalog.data_sources
            WHERE site_id = ANY(@site_ids)
            ORDER BY code
            """, command => command.Parameters.AddWithValue("site_ids", siteIds.ToArray()), MapSource, ct);
    }

    public async Task<bool> HasDependentRunOrMeasurementAsync(
        DataSourceId id,
        CancellationToken ct = default) =>
        (await ScalarAsync<long>("""
            SELECT
              (SELECT count(*) FROM acquisition.simulator_run WHERE source_id=@id) +
              (SELECT count(*) FROM telemetry.measurement_identity WHERE source_id=@id)
            """, command => command.Parameters.AddWithValue("id", id.Value), ct)) > 0;

    public async Task<CatalogDependencySnapshot> GetDataSourceDependencySnapshotAsync(
        DataSourceId id,
        CancellationToken ct = default)
    {
        var values = await QuerySingleAsync("""
            SELECT
              EXISTS(SELECT 1 FROM catalog.source_point_mapping WHERE data_source_id=@id),
              EXISTS(SELECT 1 FROM acquisition.simulator_run WHERE source_id=@id),
              EXISTS(SELECT 1 FROM telemetry.measurement_identity WHERE source_id=@id),
              EXISTS(SELECT 1 FROM telemetry.point_source_status WHERE source_id=@id),
              EXISTS(SELECT 1 FROM operations.job
                     WHERE payload_json ->> 'sourceId' = @id_text
                       AND status IN ('Pending','Leased'))
            """, command =>
        {
            command.Parameters.AddWithValue("id", id.Value);
            command.Parameters.AddWithValue("id_text", id.Value.ToString("D"));
        }, reader => new CatalogDependencySnapshot(
            MappingUsage: reader.GetBoolean(0),
            SimulatorRun: reader.GetBoolean(1),
            Measurement: reader.GetBoolean(2),
            CurrentProjection: reader.GetBoolean(3),
            ScheduledJob: reader.GetBoolean(4)), ct);
        return values ?? new CatalogDependencySnapshot();
    }

    public async Task<CatalogDeletionDecision> DeleteDataSourceAsync(
        DataSourceId id,
        CancellationToken ct = default)
    {
        var source = await GetDataSourceAsync(id, ct);
        if (source is null) return CatalogDeletionDecision.NotFound();
        if (source.Status != SourceStatus.Draft)
            return CatalogDeletionDecision.InvalidState("Only Draft sources may be hard deleted.");
        var dependencies = await GetDataSourceDependencySnapshotAsync(id, ct);
        if (dependencies.HasOperationalDependency)
            return CatalogDeletionDecision.DependentHistory();
        var affected = await ExecuteCoreAsync(
            "DELETE FROM catalog.data_sources WHERE id=@id AND status='Draft'",
            command => command.Parameters.AddWithValue("id", id.Value), ct);
        return affected == 1 ? CatalogDeletionDecision.Allowed() : CatalogDeletionDecision.NotFound();
    }

    public Task<SourcePointMapping?> GetMappingAsync(MappingId id, CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT mapping_id, data_source_id, point_id, status, effective_from, effective_to, version
            FROM catalog.source_point_mapping WHERE mapping_id=@value
            """, command => command.Parameters.AddWithValue("value", id.Value), MapMapping, ct);

    public Task AddMappingAsync(SourcePointMapping mapping, CancellationToken ct = default) =>
        InsertAsync("""
            INSERT INTO catalog.source_point_mapping
                (mapping_id, data_source_id, point_id, status, effective_from, effective_to, version)
            VALUES (@id, @source_id, @point_id, @status, @effective_from, @effective_to, @version)
            """, command => BindMapping(command, mapping), ct);

    public Task UpdateMappingAsync(SourcePointMapping mapping, CancellationToken ct = default) =>
        OptimisticUpdateAsync("""
            UPDATE catalog.source_point_mapping
            SET status=@status, effective_from=@effective_from,
                effective_to=@effective_to, version=@version, updated_at=now()
            WHERE mapping_id=@id AND version=@expected_version
            """, command =>
        {
            BindMapping(command, mapping);
            command.Parameters.AddWithValue("expected_version", mapping.Version - 1);
        }, ct);

    public async Task<IReadOnlyList<SourcePointMapping>> GetMappingsForPointAsync(
        string pointId,
        CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT mapping_id, data_source_id, point_id, status, effective_from, effective_to, version
            FROM catalog.source_point_mapping
            WHERE point_id=@point_id ORDER BY effective_from, mapping_id
            """, command => command.Parameters.AddWithValue("point_id", pointId), MapMapping, ct);

    public async Task<IReadOnlyList<SourcePointMapping>> GetMappingsForSourceAsync(
        DataSourceId dataSourceId,
        CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT mapping_id, data_source_id, point_id, status, effective_from, effective_to, version
            FROM catalog.source_point_mapping
            WHERE data_source_id=@source_id ORDER BY effective_from, mapping_id
            """, command => command.Parameters.AddWithValue("source_id", dataSourceId.Value), MapMapping, ct);

    public async Task<CatalogDependencySnapshot> GetMappingDependencySnapshotAsync(
        MappingId id,
        CancellationToken ct = default)
    {
        var snapshot = await QuerySingleAsync("""
            SELECT
              EXISTS(SELECT 1 FROM acquisition.simulator_run_point_state WHERE mapping_id=@id),
              EXISTS(SELECT 1 FROM telemetry.measurement_identity WHERE mapping_id=@id),
              EXISTS(SELECT 1 FROM telemetry.point_latest WHERE mapping_id=@id)
            """, command => command.Parameters.AddWithValue("id", id.Value),
            reader => new CatalogDependencySnapshot(
                SimulatorRun: reader.GetBoolean(0),
                Measurement: reader.GetBoolean(1),
                CurrentProjection: reader.GetBoolean(2)), ct);
        return snapshot ?? new CatalogDependencySnapshot();
    }

    public async Task<CatalogDeletionDecision> DeleteMappingAsync(
        MappingId id,
        CancellationToken ct = default)
    {
        var mapping = await GetMappingAsync(id, ct);
        if (mapping is null) return CatalogDeletionDecision.NotFound();
        if (mapping.Status != MappingStatus.Draft)
            return CatalogDeletionDecision.InvalidState("Only Draft mappings may be hard deleted.");
        var dependencies = await GetMappingDependencySnapshotAsync(id, ct);
        if (dependencies.HasOperationalDependency)
            return CatalogDeletionDecision.DependentHistory();
        var affected = await ExecuteCoreAsync("""
            DELETE FROM catalog.source_point_mapping
            WHERE mapping_id=@id AND status='Draft'
            """, command => command.Parameters.AddWithValue("id", id.Value), ct);
        return affected == 1 ? CatalogDeletionDecision.Allowed() : CatalogDeletionDecision.NotFound();
    }

    public Task<ICatalogTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var holder = _state.Value ??= new TransactionHolder();
        if (holder.Current is not null)
            throw new InvalidOperationException("CATALOG_TRANSACTION_ALREADY_ACTIVE");
        return BeginTransactionCoreAsync(holder, ct);
    }

    private async Task<ICatalogTransaction> BeginTransactionCoreAsync(
        TransactionHolder holder,
        CancellationToken ct)
    {
        var connection = await _dataSource.OpenConnectionAsync(ct);
        var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead,
            ct);
        var state = new TransactionState(connection, transaction);
        holder.Current = state;
        return new CatalogTransaction(state, () => holder.Current = null);
    }

    public async Task<MetricUnitEligibility> GetMetricUnitEligibilityAsync(
        MetricId metricId,
        UnitId unitId,
        CancellationToken ct = default)
    {
        var result = await QuerySingleAsync("""
            SELECT m.id IS NOT NULL, u.id IS NOT NULL,
                   COALESCE(m.status='Active', false),
                   COALESCE(u.status='Active', false),
                   c.metric_id IS NOT NULL,
                   COALESCE(c.is_canonical, false),
                   COALESCE(c.version, GREATEST(COALESCE(m.version,0), COALESCE(u.version,0)))
            FROM (SELECT 1) seed
            LEFT JOIN catalog.metrics m ON m.id=@metric_id
            LEFT JOIN catalog.units u ON u.id=@unit_id
            LEFT JOIN catalog.metric_unit_compatibilities c
              ON c.metric_id=m.id AND c.unit_id=u.id
            """, command =>
        {
            command.Parameters.AddWithValue("metric_id", metricId.Value);
            command.Parameters.AddWithValue("unit_id", unitId.Value);
        }, reader =>
        {
            var metricExists = reader.GetBoolean(0);
            var unitExists = reader.GetBoolean(1);
            var metricActive = reader.GetBoolean(2);
            var unitActive = reader.GetBoolean(3);
            var compatible = reader.GetBoolean(4);
            var outcome = !metricExists ? MetricUnitEligibilityOutcome.MissingMetric
                : !unitExists ? MetricUnitEligibilityOutcome.MissingUnit
                : !metricActive ? MetricUnitEligibilityOutcome.InactiveMetric
                : !unitActive ? MetricUnitEligibilityOutcome.InactiveUnit
                : !compatible ? MetricUnitEligibilityOutcome.Incompatible
                : MetricUnitEligibilityOutcome.Eligible;
            return new MetricUnitEligibility(
                metricExists && unitExists,
                metricActive,
                unitActive,
                compatible,
                reader.GetBoolean(5),
                reader.GetInt64(6),
                outcome);
        }, ct);
        return result!;
    }

    public async Task<MetricUnitEligibility?> GetCanonicalUnitEligibilityAsync(
        MetricId metricId,
        CancellationToken ct = default)
    {
        var canonical = await GetCanonicalUnitAsync(metricId, ct);
        return canonical is null
            ? null
            : await GetMetricUnitEligibilityAsync(metricId, canonical.UnitId, ct);
    }

    public async Task<SourceMappingEligibility> GetActiveMappingEligibilityAsync(
        string pointId,
        DateTime at,
        CancellationToken ct = default)
    {
        var matches = await QueryMappingEligibilityAsync(pointId, at.ToUniversalTime(), onlyEffective: true, ct);
        if (matches.Count == 0)
            return new SourceMappingEligibility(false, "MAPPING_MISSING", null, null, null, null,
                null, null, pointId, 0, MappingEligibilityOutcome.Missing);
        if (matches.Count > 1)
            return new SourceMappingEligibility(true, "MAPPING_MULTIPLE", null, null, null, null,
                null, null, pointId, matches.Max(value => value.Version), MappingEligibilityOutcome.Multiple);
        return matches[0];
    }

    public async Task<IReadOnlyList<SourceMappingEligibility>> GetMappingHistoryAsync(
        string pointId,
        CancellationToken ct = default) =>
        await QueryMappingEligibilityAsync(pointId, null, onlyEffective: false, ct);

    public async Task<CatalogSourceMappingSnapshot?> GetSourceMappingSnapshotAsync(
        MappingId mappingId,
        CancellationToken ct = default) =>
        await QuerySingleAsync("""
            SELECT m.mapping_id, m.data_source_id, m.point_id, s.status, m.status,
                   m.effective_from, m.effective_to, m.version
            FROM catalog.source_point_mapping m
            JOIN catalog.data_sources s ON s.id=m.data_source_id
            WHERE m.mapping_id=@id
            """, command => command.Parameters.AddWithValue("id", mappingId.Value),
            reader => new CatalogSourceMappingSnapshot(
                new MappingId(reader.GetGuid(0)),
                new DataSourceId(reader.GetGuid(1)),
                reader.GetString(2),
                Enum.Parse<SourceStatus>(reader.GetString(3), false),
                Enum.Parse<MappingStatus>(reader.GetString(4), false),
                reader.GetDateTime(5).ToUniversalTime(),
                reader.IsDBNull(6) ? null : reader.GetDateTime(6).ToUniversalTime(),
                reader.GetInt64(7)), ct);

    public async Task<PointReadinessSnapshot?> GetPointReadinessAsync(
        string pointId,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(pointId, out var id)) return null;
        return await QuerySingleAsync("""
            SELECT p.id, p.site_id, p.area_id, p.status, p.version,
                   a.status, a.version, ar.status, ar.version, s.status, s.version,
                   EXISTS (
                     SELECT 1 FROM catalog.metrics m
                     JOIN catalog.units u ON u.id::text=p.unit_id
                     JOIN catalog.metric_unit_compatibilities c
                       ON c.metric_id=m.id AND c.unit_id=u.id
                     WHERE m.id::text=p.metric_id AND m.status='Active' AND u.status='Active'
                   ),
                   (SELECT count(*) FROM catalog.source_point_mapping m
                    WHERE m.point_id=p.id::text AND m.status='Active'
                      AND m.effective_from <= now()
                      AND (m.effective_to IS NULL OR m.effective_to > now()))
            FROM organization.measurement_points p
            JOIN organization.assets a ON a.id=p.asset_id
            JOIN organization.areas ar ON ar.id=p.area_id
            JOIN organization.sites s ON s.id=p.site_id
            WHERE p.id=@id
            """, command => command.Parameters.AddWithValue("id", id), reader =>
        {
            var configurationReady =
                reader.GetString(3) != "Decommissioned" &&
                reader.GetString(5) == "Active" &&
                reader.GetString(7) == "Active" &&
                reader.GetString(9) == "Active" &&
                reader.GetBoolean(11) &&
                reader.GetInt64(12) == 1;
            return new PointReadinessSnapshot(
                reader.GetGuid(0).ToString("D"),
                reader.GetGuid(1).ToString("D"),
                reader.GetGuid(2).ToString("D"),
                true,
                configurationReady,
                configurationReady && reader.GetString(3) == "Active",
                reader.GetInt64(4),
                new ReadinessVersionTuple(
                    reader.GetInt64(4),
                    reader.GetInt64(6),
                    reader.GetInt64(8),
                    reader.GetInt64(10)));
        }, ct);
    }

    public async Task<CatalogSourceScopeSnapshot?> GetSourceScopeAsync(
        Guid sourceId,
        CancellationToken ct = default)
    {
        var source = await GetDataSourceAsync(new DataSourceId(sourceId), ct);
        if (source is null) return null;
        var mapped = await QueryAsync("""
            SELECT m.mapping_id, m.version, m.point_id,
                   p.site_id, p.area_id, p.version, a.version, ar.version, s.version
            FROM catalog.source_point_mapping m
            JOIN organization.measurement_points p ON p.id::text=m.point_id
            JOIN organization.assets a ON a.id=p.asset_id
            JOIN organization.areas ar ON ar.id=p.area_id
            JOIN organization.sites s ON s.id=p.site_id
            WHERE m.data_source_id=@id
              AND m.status='Active'
              AND m.effective_from <= now()
              AND (m.effective_to IS NULL OR m.effective_to > now())
            ORDER BY m.mapping_id
            """, command => command.Parameters.AddWithValue("id", sourceId),
            reader => new CatalogSourceMappedScopeSnapshot(
                new MappingId(reader.GetGuid(0)),
                reader.GetInt64(1),
                reader.GetString(2),
                reader.GetGuid(3).ToString("D"),
                reader.GetGuid(4).ToString("D"),
                new ReadinessVersionTuple(
                    reader.GetInt64(5),
                    reader.GetInt64(6),
                    reader.GetInt64(7),
                    reader.GetInt64(8))), ct);
        return new CatalogSourceScopeSnapshot(
            sourceId, true, source.SourceType.ToString(), source.Status.ToString(), source.Version, mapped);
    }

    private async Task<IReadOnlyList<SourceMappingEligibility>> QueryMappingEligibilityAsync(
        string pointId,
        DateTime? at,
        bool onlyEffective,
        CancellationToken ct)
    {
        var sql = """
            SELECT m.mapping_id, m.data_source_id, s.status, m.status,
                   m.effective_from, m.effective_to, m.point_id, m.version
            FROM catalog.source_point_mapping m
            JOIN catalog.data_sources s ON s.id=m.data_source_id
            WHERE m.point_id=@point_id
            """;
        if (onlyEffective)
            sql += """
                 AND m.status='Active' AND s.status='Active'
                 AND m.effective_from <= @at
                 AND (m.effective_to IS NULL OR m.effective_to > @at)
                """;
        sql += " ORDER BY m.effective_from, m.mapping_id";
        return await QueryAsync(sql, command =>
        {
            command.Parameters.AddWithValue("point_id", pointId);
            if (onlyEffective) command.Parameters.AddWithValue("at", at!.Value);
        }, reader => new SourceMappingEligibility(
            true,
            null,
            new MappingId(reader.GetGuid(0)),
            new DataSourceId(reader.GetGuid(1)),
            Enum.Parse<SourceStatus>(reader.GetString(2), false),
            Enum.Parse<MappingStatus>(reader.GetString(3), false),
            reader.GetDateTime(4).ToUniversalTime(),
            reader.IsDBNull(5) ? null : reader.GetDateTime(5).ToUniversalTime(),
            reader.GetString(6),
            reader.GetInt64(7),
            MappingEligibilityOutcome.Eligible), ct);
    }

    public async ValueTask AcquireLockAsync(
        IHostTransaction transaction,
        LockRequest request,
        CancellationToken ct = default)
    {
        var postgres = PostgresTransactionResolver.Require(transaction);
        string sql;
        object value;
        switch (request.Target)
        {
            case LockTarget.CatalogMetric when Guid.TryParse(request.Id, out var metricId):
                sql = "SELECT id FROM catalog.metrics WHERE id=@value FOR UPDATE";
                value = metricId;
                break;
            case LockTarget.CatalogUnit when Guid.TryParse(request.Id, out var unitId):
                sql = "SELECT id FROM catalog.units WHERE id=@value FOR UPDATE";
                value = unitId;
                break;
            case LockTarget.CatalogMapping:
                sql = """
                    SELECT mapping_id FROM catalog.source_point_mapping
                    WHERE point_id=@value ORDER BY mapping_id FOR UPDATE
                    """;
                value = request.Id;
                break;
            default:
                throw new InvalidOperationException("CATALOG_LOCK_TARGET_INVALID");
        }
        await using var command = new NpgsqlCommand(sql, postgres.Connection, postgres.Transaction);
        command.Parameters.AddWithValue("value", value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) { }
    }

    public Task<ActivationCatalogSnapshot?> ReadActivationSnapshotAsync(
        IHostTransaction transaction,
        string pointId,
        string metricId,
        string unitId,
        DateTime atUtc,
        CancellationToken ct = default) =>
        ReadActivationSnapshotCoreAsync(transaction, pointId, metricId, unitId, atUtc, ct);

    public Task<ActivationCatalogSnapshot?> RecheckActivationSnapshotAsync(
        IHostTransaction transaction,
        string pointId,
        string metricId,
        string unitId,
        DateTime atUtc,
        CancellationToken ct = default) =>
        ReadActivationSnapshotCoreAsync(transaction, pointId, metricId, unitId, atUtc, ct);

    private static async Task<ActivationCatalogSnapshot?> ReadActivationSnapshotCoreAsync(
        IHostTransaction transaction,
        string pointId,
        string metricId,
        string unitId,
        DateTime atUtc,
        CancellationToken ct)
    {
        if (!Guid.TryParse(metricId, out var metricGuid) ||
            !Guid.TryParse(unitId, out var unitGuid))
            return null;
        var postgres = PostgresTransactionResolver.Require(transaction);
        await using var command = new NpgsqlCommand("""
            SELECT m.id,m.version,m.status,u.id,u.version,u.status,
                   (c.metric_id IS NOT NULL),COALESCE(c.version,0),
                   spm.mapping_id,spm.version,spm.status,
                   ds.id,ds.version,ds.status,ds.source_type,
                   spm.effective_from,spm.effective_to,
                   count(*) OVER()::int
            FROM catalog.metrics m
            JOIN catalog.units u ON u.id=@unit_id
            LEFT JOIN catalog.metric_unit_compatibilities c
              ON c.metric_id=m.id AND c.unit_id=u.id
            JOIN catalog.source_point_mapping spm
              ON spm.point_id=@point_id
             AND spm.status='Active'
             AND spm.effective_from<=@at_utc
             AND (spm.effective_to IS NULL OR spm.effective_to>@at_utc)
            JOIN catalog.data_sources ds ON ds.id=spm.data_source_id
            WHERE m.id=@metric_id
            ORDER BY spm.mapping_id
            """, postgres.Connection, postgres.Transaction);
        command.Parameters.AddWithValue("metric_id", metricGuid);
        command.Parameters.AddWithValue("unit_id", unitGuid);
        command.Parameters.AddWithValue("point_id", pointId);
        command.Parameters.AddWithValue("at_utc", atUtc.ToUniversalTime());
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new ActivationCatalogSnapshot(
            reader.GetGuid(0).ToString("D"), reader.GetInt64(1), reader.GetString(2),
            reader.GetGuid(3).ToString("D"), reader.GetInt64(4), reader.GetString(5),
            reader.GetBoolean(6), reader.GetInt64(7),
            reader.GetGuid(8).ToString("D"), reader.GetInt64(9), reader.GetString(10),
            reader.GetGuid(11).ToString("D"), reader.GetInt64(12), reader.GetString(13),
            reader.GetString(14), reader.GetDateTime(15).ToUniversalTime(),
            reader.IsDBNull(16) ? null : reader.GetDateTime(16).ToUniversalTime(),
            reader.GetInt32(17), pointId, pointId,
            $"{metricGuid:D}:{unitGuid:D}", reader.GetBoolean(6) ? "Active" : "Inactive");
    }

    private static Metric MapMetric(NpgsqlDataReader reader) => new(
        new MetricId(reader.GetGuid(0)),
        reader.GetString(1),
        reader.GetString(2),
        Enum.Parse<MetricStatus>(reader.GetString(3), false),
        reader.GetInt64(4));

    private static MetricUnit MapUnit(NpgsqlDataReader reader) => new(
        new UnitId(reader.GetGuid(0)),
        reader.GetString(1),
        reader.GetString(2),
        Enum.Parse<MetricUnitStatus>(reader.GetString(3), false),
        reader.GetInt64(4));

    private static MetricUnitCompatibility MapCompatibility(NpgsqlDataReader reader) => new(
        new MetricId(reader.GetGuid(0)),
        new UnitId(reader.GetGuid(1)),
        reader.GetBoolean(2),
        reader.GetInt64(3));

    private static DataSource MapSource(NpgsqlDataReader reader) => new(
        new DataSourceId(reader.GetGuid(0)),
        reader.GetString(1),
        reader.GetString(2),
        Enum.Parse<SourceType>(reader.GetString(3), false),
        Enum.Parse<SourceStatus>(reader.GetString(4), false),
        reader.GetInt64(5),
        reader.IsDBNull(6) ? null : reader.GetGuid(6));

    private static SourcePointMapping MapMapping(NpgsqlDataReader reader) => new(
        new MappingId(reader.GetGuid(0)),
        new DataSourceId(reader.GetGuid(1)),
        reader.GetString(2),
        Enum.Parse<MappingStatus>(reader.GetString(3), false),
        reader.GetDateTime(4).ToUniversalTime(),
        reader.IsDBNull(5) ? null : reader.GetDateTime(5).ToUniversalTime(),
        reader.GetInt64(6));

    private static void BindCompatibility(NpgsqlCommand command, MetricUnitCompatibility value)
    {
        command.Parameters.AddWithValue("metric_id", value.MetricId.Value);
        command.Parameters.AddWithValue("unit_id", value.UnitId.Value);
        command.Parameters.AddWithValue("is_canonical", value.IsCanonical);
        command.Parameters.AddWithValue("version", value.Version);
    }

    private static void BindSource(NpgsqlCommand command, DataSource value)
    {
        command.Parameters.AddWithValue("id", value.Id.Value);
        command.Parameters.AddWithValue("code", value.Code);
        command.Parameters.AddWithValue("name", value.Name);
        command.Parameters.AddWithValue("source_type", value.SourceType.ToString());
        command.Parameters.AddWithValue("status", value.Status.ToString());
        command.Parameters.AddWithValue("version", value.Version);
        command.Parameters.AddWithValue(
            "site_id", (object?)value.SiteId ?? DBNull.Value);
    }

    private static void BindMapping(NpgsqlCommand command, SourcePointMapping value)
    {
        command.Parameters.AddWithValue("id", value.Id.Value);
        command.Parameters.AddWithValue("source_id", value.DataSourceId.Value);
        command.Parameters.AddWithValue("point_id", value.PointId);
        command.Parameters.AddWithValue("status", value.Status.ToString());
        command.Parameters.AddWithValue("effective_from", value.EffectiveFrom);
        command.Parameters.AddWithValue("effective_to", (object?)value.EffectiveTo ?? DBNull.Value);
        command.Parameters.AddWithValue("version", value.Version);
    }

    private async Task InsertAsync(
        string sql,
        Action<NpgsqlCommand> bind,
        CancellationToken ct)
    {
        try { _ = await ExecuteCoreAsync(sql, bind, ct); }
        catch (PostgresException exception) when (
            exception.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ExclusionViolation)
        {
            throw new InvalidOperationException("CATALOG_CONFLICT", exception);
        }
    }

    private async Task OptimisticUpdateAsync(
        string sql,
        Action<NpgsqlCommand> bind,
        CancellationToken ct)
    {
        try
        {
            if (await ExecuteCoreAsync(sql, bind, ct) != 1)
                throw new InvalidOperationException("CATALOG_VERSION_CONFLICT");
        }
        catch (PostgresException exception) when (
            exception.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ExclusionViolation)
        {
            throw new InvalidOperationException("CATALOG_CONFLICT", exception);
        }
    }

    private async Task<int> ExecuteCoreAsync(
        string sql,
        Action<NpgsqlCommand> bind,
        CancellationToken ct)
    {
        var (connection, owns) = await AcquireAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(sql, connection, CurrentTransaction);
            bind(command);
            return await command.ExecuteNonQueryAsync(ct);
        }
        finally { if (owns) await connection.DisposeAsync(); }
    }

    private async Task<T> ScalarAsync<T>(
        string sql,
        Action<NpgsqlCommand> bind,
        CancellationToken ct)
    {
        var (connection, owns) = await AcquireAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(sql, connection, CurrentTransaction);
            bind(command);
            return (T)(await command.ExecuteScalarAsync(ct))!;
        }
        finally { if (owns) await connection.DisposeAsync(); }
    }

    private async Task<T?> QuerySingleAsync<T>(
        string sql,
        Action<NpgsqlCommand> bind,
        Func<NpgsqlDataReader, T> map,
        CancellationToken ct)
    {
        var values = await QueryAsync(sql, bind, map, ct);
        return values.SingleOrDefault();
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Action<NpgsqlCommand>? bind,
        Func<NpgsqlDataReader, T> map,
        CancellationToken ct)
    {
        var (connection, owns) = await AcquireAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(sql, connection, CurrentTransaction);
            bind?.Invoke(command);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var results = new List<T>();
            while (await reader.ReadAsync(ct)) results.Add(map(reader));
            return results;
        }
        finally { if (owns) await connection.DisposeAsync(); }
    }

    private NpgsqlTransaction? CurrentTransaction =>
        _state.Value?.Current?.Transaction ?? _hostTransactions.Current?.Transaction;

    private async Task<(NpgsqlConnection Connection, bool Owns)> AcquireAsync(CancellationToken ct) =>
        _state.Value?.Current is { } state
            ? (state.Connection, false)
            : _hostTransactions.Current is { IsCompleted: false } host
                ? (host.Connection, false)
                : (await _dataSource.OpenConnectionAsync(ct), true);

    private sealed record TransactionState(NpgsqlConnection Connection, NpgsqlTransaction Transaction);

    private sealed class TransactionHolder
    {
        public TransactionState? Current { get; set; }
    }

    private sealed class CatalogTransaction(TransactionState state, Action completed) : ICatalogTransaction
    {
        private bool _completed;
        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (_completed) return;
            try { await state.Transaction.CommitAsync(ct); }
            finally { await FinishAsync(); }
        }
        public async Task RollbackAsync(CancellationToken ct = default)
        {
            if (_completed) return;
            try { await state.Transaction.RollbackAsync(ct); }
            finally { await FinishAsync(); }
        }
        private async Task FinishAsync()
        {
            if (_completed) return;
            _completed = true;
            completed();
            await state.Transaction.DisposeAsync();
            await state.Connection.DisposeAsync();
        }
    }
}
