using IUMP.BuildingBlocks.Persistence;
using IUMP.Infrastructure.Postgres;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using Npgsql;

namespace IUMP.Modules.Organization.Infrastructure;

public sealed class PostgresOrganizationRepositories :
    IOrganizationCommandRepository,
    IOrganizationQueryRepository,
    IActivationOrganizationParticipant
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresTransactionContext _hostTransactions;
    private readonly AsyncLocal<TransactionHolder?> _state = new();

    public PostgresOrganizationRepositories(
        NpgsqlDataSource dataSource,
        PostgresTransactionContext hostTransactions)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _hostTransactions = hostTransactions ?? throw new ArgumentNullException(nameof(hostTransactions));
    }

    public Task<Site?> GetSiteAsync(SiteId id, CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT id, code, name, description, timezone, status, version
            FROM organization.sites WHERE id=@id
            """, command => command.Parameters.AddWithValue("id", id.Value), MapSite, ct);

    public Task<Site?> FindSiteByCodeAsync(string code, CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT id, code, name, description, timezone, status, version
            FROM organization.sites WHERE upper(code)=upper(@code)
            """, command => command.Parameters.AddWithValue("code", code), MapSite, ct);

    public Task AddSiteAsync(Site site, CancellationToken ct = default) =>
        InsertAsync("""
            INSERT INTO organization.sites
                (id, code, name, description, timezone, status, version)
            VALUES (@id,@code,@name,@description,@timezone,@status,@version)
            """, command => BindSite(command, site), ct);

    public Task UpdateSiteAsync(Site site, CancellationToken ct = default) =>
        OptimisticAsync("""
            UPDATE organization.sites
            SET name=@name, description=@description, timezone=@timezone,
                status=@status, version=@version, updated_at=now()
            WHERE id=@id AND version=@expected_version
            """, command =>
        {
            BindSite(command, site);
            command.Parameters.AddWithValue("expected_version", site.Version - 1);
        }, ct);

    public async Task<IReadOnlyList<Site>> GetAllSitesAsync(CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT id, code, name, description, timezone, status, version
            FROM organization.sites ORDER BY code,id
            """, null, MapSite, ct);

    public Task<Area?> GetAreaAsync(AreaId id, CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT id,site_id,code,name,description,status,version
            FROM organization.areas WHERE id=@id
            """, command => command.Parameters.AddWithValue("id", id.Value), MapArea, ct);

    public async Task<OrganizationTargetScope?> GetAreaScopeAsync(AreaId id, CancellationToken ct = default) =>
        await QuerySingleAsync("""
            SELECT site_id,id FROM organization.areas WHERE id=@id
            """, command => command.Parameters.AddWithValue("id", id.Value),
            reader => new OrganizationTargetScope(reader.GetGuid(0), reader.GetGuid(1)), ct);

    public Task<Area?> FindAreaByCodeAsync(SiteId siteId, string code, CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT id,site_id,code,name,description,status,version
            FROM organization.areas
            WHERE site_id=@site_id AND upper(code)=upper(@code)
            """, command =>
        {
            command.Parameters.AddWithValue("site_id", siteId.Value);
            command.Parameters.AddWithValue("code", code);
        }, MapArea, ct);

    public Task AddAreaAsync(Area area, CancellationToken ct = default) =>
        InsertAsync("""
            INSERT INTO organization.areas
                (id,site_id,code,name,description,status,version)
            VALUES (@id,@site_id,@code,@name,@description,@status,@version)
            """, command => BindArea(command, area), ct);

    public Task UpdateAreaAsync(Area area, CancellationToken ct = default) =>
        OptimisticAsync("""
            UPDATE organization.areas
            SET name=@name,description=@description,status=@status,
                version=@version,updated_at=now()
            WHERE id=@id AND version=@expected_version
            """, command =>
        {
            BindArea(command, area);
            command.Parameters.AddWithValue("expected_version", area.Version - 1);
        }, ct);

    public async Task<IReadOnlyList<Area>> GetAreasForSiteAsync(
        SiteId siteId,
        CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT id,site_id,code,name,description,status,version
            FROM organization.areas WHERE site_id=@site_id ORDER BY code,id
            """, command => command.Parameters.AddWithValue("site_id", siteId.Value), MapArea, ct);

    public Task<Asset?> GetAssetAsync(AssetId id, CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT id,site_id,area_id,code,name,description,status,version
            FROM organization.assets WHERE id=@id
            """, command => command.Parameters.AddWithValue("id", id.Value), MapAsset, ct);

    public async Task<OrganizationTargetScope?> GetAssetScopeAsync(
        AssetId id,
        CancellationToken ct = default) =>
        await QuerySingleAsync("""
            SELECT site_id,area_id,id FROM organization.assets WHERE id=@id
            """, command => command.Parameters.AddWithValue("id", id.Value),
            reader => new OrganizationTargetScope(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2)), ct);

    public Task<Asset?> FindAssetByCodeAsync(
        AreaId areaId,
        string code,
        CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT id,site_id,area_id,code,name,description,status,version
            FROM organization.assets
            WHERE area_id=@area_id AND upper(code)=upper(@code)
            """, command =>
        {
            command.Parameters.AddWithValue("area_id", areaId.Value);
            command.Parameters.AddWithValue("code", code);
        }, MapAsset, ct);

    public Task AddAssetAsync(Asset asset, CancellationToken ct = default) =>
        InsertAsync("""
            INSERT INTO organization.assets
                (id,site_id,area_id,code,name,description,status,version)
            VALUES (@id,@site_id,@area_id,@code,@name,@description,@status,@version)
            """, command => BindAsset(command, asset), ct);

    public Task UpdateAssetAsync(Asset asset, CancellationToken ct = default) =>
        OptimisticAsync("""
            UPDATE organization.assets
            SET name=@name,description=@description,status=@status,
                version=@version,updated_at=now()
            WHERE id=@id AND version=@expected_version
            """, command =>
        {
            BindAsset(command, asset);
            command.Parameters.AddWithValue("expected_version", asset.Version - 1);
        }, ct);

    public async Task<IReadOnlyList<Asset>> GetAssetsForAreaAsync(
        AreaId areaId,
        CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT id,site_id,area_id,code,name,description,status,version
            FROM organization.assets WHERE area_id=@area_id ORDER BY code,id
            """, command => command.Parameters.AddWithValue("area_id", areaId.Value), MapAsset, ct);

    public async Task<IReadOnlyList<Asset>> GetAssetsForSiteAsync(
        SiteId siteId,
        CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT id,site_id,area_id,code,name,description,status,version
            FROM organization.assets WHERE site_id=@site_id ORDER BY code,id
            """, command => command.Parameters.AddWithValue("site_id", siteId.Value), MapAsset, ct);

    public Task<MeasurementPoint?> GetPointAsync(PointId id, CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT id,site_id,area_id,asset_id,code,description,metric_id,unit_id,
                   data_owner_user_id,expected_interval_seconds,no_data_after_seconds,status,version
            FROM organization.measurement_points WHERE id=@id
            """, command => command.Parameters.AddWithValue("id", id.Value), MapPoint, ct);

    public async Task<OrganizationTargetScope?> GetPointScopeAsync(
        PointId id,
        CancellationToken ct = default) =>
        await QuerySingleAsync("""
            SELECT site_id,area_id,asset_id
            FROM organization.measurement_points WHERE id=@id
            """, command => command.Parameters.AddWithValue("id", id.Value),
            reader => new OrganizationTargetScope(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2)), ct);

    public Task<MeasurementPoint?> FindPointByCodeAsync(
        SiteId siteId,
        string code,
        CancellationToken ct = default) =>
        QuerySingleAsync("""
            SELECT id,site_id,area_id,asset_id,code,description,metric_id,unit_id,
                   data_owner_user_id,expected_interval_seconds,no_data_after_seconds,status,version
            FROM organization.measurement_points
            WHERE site_id=@site_id AND upper(code)=upper(@code)
            """, command =>
        {
            command.Parameters.AddWithValue("site_id", siteId.Value);
            command.Parameters.AddWithValue("code", code);
        }, MapPoint, ct);

    public Task AddPointAsync(MeasurementPoint point, CancellationToken ct = default) =>
        InsertAsync("""
            INSERT INTO organization.measurement_points
                (id,site_id,area_id,asset_id,code,description,metric_id,unit_id,
                 data_owner_user_id,expected_interval_seconds,no_data_after_seconds,status,version)
            VALUES
                (@id,@site_id,@area_id,@asset_id,@code,@description,@metric_id,@unit_id,
                 @data_owner_user_id,@expected_interval_seconds,@no_data_after_seconds,@status,@version)
            """, command => BindPoint(command, point), ct);

    public Task UpdatePointAsync(MeasurementPoint point, CancellationToken ct = default) =>
        OptimisticAsync("""
            UPDATE organization.measurement_points
            SET description=@description,metric_id=@metric_id,unit_id=@unit_id,
                data_owner_user_id=@data_owner_user_id,
                expected_interval_seconds=@expected_interval_seconds,
                no_data_after_seconds=@no_data_after_seconds,
                status=@status,version=@version,updated_at=now()
            WHERE id=@id AND version=@expected_version
            """, command =>
        {
            BindPoint(command, point);
            command.Parameters.AddWithValue("expected_version", point.Version - 1);
        }, ct);

    public async Task<IReadOnlyList<MeasurementPoint>> GetPointsForAssetAsync(
        AssetId assetId,
        CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT id,site_id,area_id,asset_id,code,description,metric_id,unit_id,
                   data_owner_user_id,expected_interval_seconds,no_data_after_seconds,status,version
            FROM organization.measurement_points WHERE asset_id=@asset_id ORDER BY code,id
            """, command => command.Parameters.AddWithValue("asset_id", assetId.Value), MapPoint, ct);

    public async Task<IReadOnlyList<MeasurementPoint>> GetPointsForSiteAsync(
        SiteId siteId,
        CancellationToken ct = default) =>
        await QueryAsync("""
            SELECT id,site_id,area_id,asset_id,code,description,metric_id,unit_id,
                   data_owner_user_id,expected_interval_seconds,no_data_after_seconds,status,version
            FROM organization.measurement_points WHERE site_id=@site_id ORDER BY code,id
            """, command => command.Parameters.AddWithValue("site_id", siteId.Value), MapPoint, ct);

    public Task AddLifecycleEntryAsync(PointLifecycleEntry entry, CancellationToken ct = default)
    {
        if (!Guid.TryParse(entry.HistoryId, out var historyId) ||
            !Guid.TryParse(entry.PointId, out var pointId))
            throw new InvalidOperationException("ORGANIZATION_INVALID_HISTORY_ID");
        return InsertAsync("""
            INSERT INTO organization.point_lifecycle_history
                (id,point_id,point_version,old_status,new_status,actor_id,actor_username,
                 reason,occurred_at,correlation_id,causation_id)
            VALUES
                (@id,@point_id,@point_version,@old_status,@new_status,@actor_id,@actor_username,
                 @reason,@occurred_at,@correlation_id,@causation_id)
            """, command =>
        {
            command.Parameters.AddWithValue("id", historyId);
            command.Parameters.AddWithValue("point_id", pointId);
            command.Parameters.AddWithValue("point_version", entry.PointVersion);
            command.Parameters.AddWithValue("old_status", entry.OldStatus.ToString());
            command.Parameters.AddWithValue("new_status", entry.NewStatus.ToString());
            command.Parameters.AddWithValue("actor_id", entry.ActorId);
            command.Parameters.AddWithValue("actor_username", (object?)entry.ActorUsername ?? DBNull.Value);
            command.Parameters.AddWithValue("reason", (object?)entry.Reason ?? DBNull.Value);
            command.Parameters.AddWithValue("occurred_at", entry.OccurredAt.ToUniversalTime());
            command.Parameters.AddWithValue("correlation_id", (object?)entry.CorrelationId ?? DBNull.Value);
            command.Parameters.AddWithValue("causation_id", (object?)entry.CausationId ?? DBNull.Value);
        }, ct);
    }

    public async Task<IReadOnlyList<PointLifecycleEntry>> GetLifecycleForPointAsync(
        string pointId,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(pointId, out var id)) return Array.Empty<PointLifecycleEntry>();
        return await QueryAsync("""
            SELECT id,point_id,point_version,old_status,new_status,actor_id,actor_username,
                   reason,occurred_at,correlation_id,causation_id
            FROM organization.point_lifecycle_history
            WHERE point_id=@point_id ORDER BY point_version,id
            """, command => command.Parameters.AddWithValue("point_id", id),
            reader => new PointLifecycleEntry(
                reader.GetGuid(0).ToString("D"),
                reader.GetGuid(1).ToString("D"),
                reader.GetInt64(2),
                Enum.Parse<PointStatus>(reader.GetString(3), false),
                Enum.Parse<PointStatus>(reader.GetString(4), false),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetDateTime(8).ToUniversalTime(),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10)), ct);
    }

    public async Task<bool> IsPointCodeReservedAsync(
        SiteId siteId,
        string code,
        CancellationToken ct = default) =>
        await ScalarAsync<bool>("""
            SELECT EXISTS(
              SELECT 1 FROM organization.measurement_points
              WHERE site_id=@site_id AND upper(code)=upper(@code))
            """, command =>
        {
            command.Parameters.AddWithValue("site_id", siteId.Value);
            command.Parameters.AddWithValue("code", code);
        }, ct);

    public Task<IOrganizationTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var holder = _state.Value ??= new TransactionHolder();
        if (holder.Current is not null)
            throw new InvalidOperationException("ORGANIZATION_TRANSACTION_ALREADY_ACTIVE");
        return BeginTransactionCoreAsync(holder, ct);
    }

    private async Task<IOrganizationTransaction> BeginTransactionCoreAsync(
        TransactionHolder holder,
        CancellationToken ct)
    {
        var connection = await _dataSource.OpenConnectionAsync(ct);
        var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead,
            ct);
        var state = new TransactionState(connection, transaction);
        holder.Current = state;
        return new OrganizationTransaction(state, () => holder.Current = null);
    }

    public async Task<SiteSnapshot?> GetSiteSnapshotAsync(Guid id, CancellationToken ct = default) =>
        await QuerySingleAsync("""
            SELECT s.id,s.code,s.name,s.description,s.timezone,s.status,s.version,
                   count(a.id)::int
            FROM organization.sites s
            LEFT JOIN organization.areas a ON a.site_id=s.id
            WHERE s.id=@id
            GROUP BY s.id
            """, command => command.Parameters.AddWithValue("id", id), MapSiteSnapshot, ct);

    async Task<SiteSnapshot?> IOrganizationQueryRepository.FindSiteByCodeAsync(
        string code,
        CancellationToken ct) =>
        await QuerySingleAsync("""
            SELECT s.id,s.code,s.name,s.description,s.timezone,s.status,s.version,
                   count(a.id)::int
            FROM organization.sites s
            LEFT JOIN organization.areas a ON a.site_id=s.id
            WHERE upper(s.code)=upper(@code)
            GROUP BY s.id
            """, command => command.Parameters.AddWithValue("code", code), MapSiteSnapshot, ct);

    public async Task<PagedResult<SiteSnapshot>> GetSitesAsync(
        OrganizationQueryScope scope,
        ScopeFilter filter,
        CancellationToken ct = default)
    {
        var rows = await QueryAsync("""
            SELECT s.id,s.code,s.name,s.description,s.timezone,s.status,s.version,
                   count(DISTINCT a.id)::int,
                   count(*) OVER()::int
            FROM organization.sites s
            LEFT JOIN organization.areas a ON a.site_id=s.id
            WHERE @is_global OR s.id=ANY(@site_ids)
               OR EXISTS(SELECT 1 FROM organization.areas scoped
                         WHERE scoped.site_id=s.id AND scoped.id=ANY(@area_ids))
            GROUP BY s.id
            ORDER BY s.code,s.id
            OFFSET @offset LIMIT @limit
            """, command => BindPageScope(command, scope, filter),
            reader => (Snapshot: MapSiteSnapshot(reader), Total: reader.GetInt32(8)), ct);
        return Page(rows.Select(row => row.Snapshot).ToArray(), rows.FirstOrDefault().Total, filter);
    }

    public async Task<AreaSnapshot?> GetAreaSnapshotAsync(Guid id, CancellationToken ct = default) =>
        await QuerySingleAsync("""
            SELECT a.id,a.site_id,a.code,a.name,a.description,a.status,a.version,count(x.id)::int
            FROM organization.areas a
            LEFT JOIN organization.assets x ON x.area_id=a.id
            WHERE a.id=@id GROUP BY a.id
            """, command => command.Parameters.AddWithValue("id", id), MapAreaSnapshot, ct);

    public async Task<PagedResult<AreaSnapshot>> GetAreasForSiteAsync(
        Guid siteId,
        OrganizationQueryScope scope,
        ScopeFilter filter,
        CancellationToken ct = default)
    {
        var rows = await QueryAsync("""
            SELECT a.id,a.site_id,a.code,a.name,a.description,a.status,a.version,
                   count(DISTINCT x.id)::int,count(*) OVER()::int
            FROM organization.areas a
            LEFT JOIN organization.assets x ON x.area_id=a.id
            WHERE a.site_id=@parent_id
              AND (@is_global OR a.site_id=ANY(@site_ids) OR a.id=ANY(@area_ids))
            GROUP BY a.id
            ORDER BY a.code,a.id OFFSET @offset LIMIT @limit
            """, command =>
        {
            BindPageScope(command, scope, filter);
            command.Parameters.AddWithValue("parent_id", siteId);
        }, reader => (Snapshot: MapAreaSnapshot(reader), Total: reader.GetInt32(8)), ct);
        return Page(rows.Select(row => row.Snapshot).ToArray(), rows.FirstOrDefault().Total, filter);
    }

    public async Task<AssetSnapshot?> GetAssetSnapshotAsync(Guid id, CancellationToken ct = default) =>
        await QuerySingleAsync("""
            SELECT a.id,a.site_id,a.area_id,a.code,a.name,a.description,a.status,a.version,
                   count(p.id)::int
            FROM organization.assets a
            LEFT JOIN organization.measurement_points p ON p.asset_id=a.id
            WHERE a.id=@id GROUP BY a.id
            """, command => command.Parameters.AddWithValue("id", id), MapAssetSnapshot, ct);

    public async Task<PagedResult<AssetSnapshot>> GetAssetsForAreaAsync(
        Guid areaId,
        OrganizationQueryScope scope,
        ScopeFilter filter,
        CancellationToken ct = default)
    {
        var rows = await QueryAsync("""
            SELECT a.id,a.site_id,a.area_id,a.code,a.name,a.description,a.status,a.version,
                   count(DISTINCT p.id)::int,count(*) OVER()::int
            FROM organization.assets a
            LEFT JOIN organization.measurement_points p ON p.asset_id=a.id
            WHERE a.area_id=@parent_id
              AND (@is_global OR a.site_id=ANY(@site_ids) OR a.area_id=ANY(@area_ids))
            GROUP BY a.id
            ORDER BY a.code,a.id OFFSET @offset LIMIT @limit
            """, command =>
        {
            BindPageScope(command, scope, filter);
            command.Parameters.AddWithValue("parent_id", areaId);
        }, reader => (Snapshot: MapAssetSnapshot(reader), Total: reader.GetInt32(9)), ct);
        return Page(rows.Select(row => row.Snapshot).ToArray(), rows.FirstOrDefault().Total, filter);
    }

    public async Task<PointSnapshot?> GetPointSnapshotAsync(Guid id, CancellationToken ct = default) =>
        await QuerySingleAsync("""
            SELECT id,site_id,area_id,asset_id,code,description,metric_id,unit_id,
                   data_owner_user_id,expected_interval_seconds,no_data_after_seconds,status,version
            FROM organization.measurement_points WHERE id=@id
            """, command => command.Parameters.AddWithValue("id", id), MapPointSnapshot, ct);

    public Task<PagedResult<PointSnapshot>> GetPointsForAssetAsync(
        Guid assetId,
        OrganizationQueryScope scope,
        ScopeFilter filter,
        CancellationToken ct = default) =>
        GetPointPageAsync("p.asset_id=@parent_id", assetId, scope, filter, ct);

    public Task<PagedResult<PointSnapshot>> GetPointsForSiteAsync(
        Guid siteId,
        OrganizationQueryScope scope,
        ScopeFilter filter,
        CancellationToken ct = default) =>
        GetPointPageAsync("p.site_id=@parent_id", siteId, scope, filter, ct);

    public async Task<bool> SiteExistsAsync(Guid id, CancellationToken ct = default) =>
        await ScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM organization.sites WHERE id=@id)",
            command => command.Parameters.AddWithValue("id", id), ct);

    public async Task<long> GetSiteVersionAsync(Guid id, CancellationToken ct = default) =>
        await ScalarAsync<long?>("SELECT version FROM organization.sites WHERE id=@id",
            command => command.Parameters.AddWithValue("id", id), ct) ?? 0;

    public async Task<AreaAncestrySnapshot?> GetAreaAncestryAsync(
        Guid areaId,
        CancellationToken ct = default) =>
        await QuerySingleAsync("""
            SELECT id,site_id FROM organization.areas WHERE id=@id
            """, command => command.Parameters.AddWithValue("id", areaId),
            reader => new AreaAncestrySnapshot(reader.GetGuid(0), reader.GetGuid(1)), ct);

    public async ValueTask AcquireLockAsync(
        IHostTransaction transaction,
        LockRequest request,
        CancellationToken ct = default)
    {
        var postgres = RequireTransaction(transaction);
        var (table, key) = request.Target switch
        {
            LockTarget.OrganizationSite => ("organization.sites", "id"),
            LockTarget.OrganizationArea => ("organization.areas", "id"),
            LockTarget.OrganizationAsset => ("organization.assets", "id"),
            LockTarget.OrganizationPoint => ("organization.measurement_points", "id"),
            _ => throw new InvalidOperationException("ORGANIZATION_LOCK_TARGET_INVALID")
        };
        if (!Guid.TryParse(request.Id, out var id))
            throw new InvalidOperationException("ORGANIZATION_LOCK_ID_INVALID");
        await using var command = new NpgsqlCommand(
            $"SELECT {key} FROM {table} WHERE {key}=@id FOR UPDATE",
            postgres.Connection,
            postgres.Transaction);
        command.Parameters.AddWithValue("id", id);
        _ = await command.ExecuteScalarAsync(ct);
    }

    public async Task<ActivationOrganizationSnapshot?> ReadLockedSnapshotAsync(
        IHostTransaction transaction,
        PointId pointId,
        CancellationToken ct = default)
    {
        var postgres = RequireTransaction(transaction);
        const string sql = """
            SELECT
              p.id,p.site_id,p.area_id,p.asset_id,p.code,p.description,p.metric_id,p.unit_id,
              p.data_owner_user_id,p.expected_interval_seconds,p.no_data_after_seconds,p.status,p.version,
              s.id,s.code,s.name,s.description,s.timezone,s.status,s.version,
              ar.id,ar.site_id,ar.code,ar.name,ar.description,ar.status,ar.version,
              a.id,a.site_id,a.area_id,a.code,a.name,a.description,a.status,a.version
            FROM organization.measurement_points p
            JOIN organization.sites s ON s.id=p.site_id
            JOIN organization.areas ar ON ar.id=p.area_id
            JOIN organization.assets a ON a.id=p.asset_id
            WHERE p.id=@id
            FOR UPDATE OF s,ar,a,p
            """;
        await using var command = new NpgsqlCommand(sql, postgres.Connection, postgres.Transaction);
        command.Parameters.AddWithValue("id", pointId.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new ActivationOrganizationSnapshot(
            MapPoint(reader),
            new Site(new SiteId(reader.GetGuid(13)), reader.GetString(14), reader.GetString(15),
                reader.IsDBNull(16) ? null : reader.GetString(16), reader.GetString(17),
                Enum.Parse<SiteStatus>(reader.GetString(18), false), reader.GetInt64(19)),
            new Area(new AreaId(reader.GetGuid(20)), new SiteId(reader.GetGuid(21)),
                reader.GetString(22), reader.GetString(23),
                reader.IsDBNull(24) ? null : reader.GetString(24),
                Enum.Parse<AreaStatus>(reader.GetString(25), false), reader.GetInt64(26)),
            new Asset(new AssetId(reader.GetGuid(27)), new SiteId(reader.GetGuid(28)),
                new AreaId(reader.GetGuid(29)), reader.GetString(30), reader.GetString(31),
                reader.IsDBNull(32) ? null : reader.GetString(32),
                Enum.Parse<AssetStatus>(reader.GetString(33), false), reader.GetInt64(34)));
    }

    public async Task<MeasurementPoint> StageActivationAsync(
        IHostTransaction transaction,
        ActivationOrganizationSnapshot snapshot,
        string actorUserId,
        string? actorUsername,
        string? correlationId,
        string? causationId,
        CancellationToken ct = default)
    {
        if (!snapshot.Point.TryActivate())
            throw new InvalidOperationException("POINT_NOT_ACTIVATABLE");
        var postgres = RequireTransaction(transaction);
        await using var update = new NpgsqlCommand("""
            UPDATE organization.measurement_points
            SET status='Active',version=@version,updated_at=now()
            WHERE id=@id AND version=@expected_version AND status='Draft'
            """, postgres.Connection, postgres.Transaction);
        update.Parameters.AddWithValue("id", snapshot.Point.Id.Value);
        update.Parameters.AddWithValue("version", snapshot.Point.Version);
        update.Parameters.AddWithValue("expected_version", snapshot.Point.Version - 1);
        if (await update.ExecuteNonQueryAsync(ct) != 1)
            throw new InvalidOperationException("ORGANIZATION_VERSION_CONFLICT");

        await using var history = new NpgsqlCommand("""
            INSERT INTO organization.point_lifecycle_history
                (id,point_id,point_version,old_status,new_status,actor_id,actor_username,
                 reason,occurred_at,correlation_id,causation_id)
            VALUES
                (@history_id,@point_id,@version,'Draft','Active',@actor_id,@actor_username,
                 'activate',now(),@correlation_id,@causation_id)
            """, postgres.Connection, postgres.Transaction);
        history.Parameters.AddWithValue("history_id", Guid.NewGuid());
        history.Parameters.AddWithValue("point_id", snapshot.Point.Id.Value);
        history.Parameters.AddWithValue("version", snapshot.Point.Version);
        history.Parameters.AddWithValue("actor_id", actorUserId);
        history.Parameters.AddWithValue("actor_username", (object?)actorUsername ?? DBNull.Value);
        history.Parameters.AddWithValue("correlation_id", (object?)correlationId ?? DBNull.Value);
        history.Parameters.AddWithValue("causation_id", (object?)causationId ?? DBNull.Value);
        await history.ExecuteNonQueryAsync(ct);
        return snapshot.Point;
    }

    private async Task<PagedResult<PointSnapshot>> GetPointPageAsync(
        string parentPredicate,
        Guid parentId,
        OrganizationQueryScope scope,
        ScopeFilter filter,
        CancellationToken ct)
    {
        var sql = $"""
            SELECT p.id,p.site_id,p.area_id,p.asset_id,p.code,p.description,p.metric_id,p.unit_id,
                   p.data_owner_user_id,p.expected_interval_seconds,p.no_data_after_seconds,p.status,p.version,
                   count(*) OVER()::int
            FROM organization.measurement_points p
            WHERE {parentPredicate}
              AND (@is_global OR p.site_id=ANY(@site_ids) OR p.area_id=ANY(@area_ids))
            ORDER BY p.code,p.id OFFSET @offset LIMIT @limit
            """;
        var rows = await QueryAsync(sql, command =>
        {
            BindPageScope(command, scope, filter);
            command.Parameters.AddWithValue("parent_id", parentId);
        }, reader => (Snapshot: MapPointSnapshot(reader), Total: reader.GetInt32(13)), ct);
        return Page(rows.Select(row => row.Snapshot).ToArray(), rows.FirstOrDefault().Total, filter);
    }

    private static PagedResult<T> Page<T>(IReadOnlyList<T> items, int total, ScopeFilter filter) =>
        new(items, total, Math.Max(1, filter.Page), Math.Clamp(filter.PageSize, 1, 200));

    private static void BindPageScope(
        NpgsqlCommand command,
        OrganizationQueryScope scope,
        ScopeFilter filter)
    {
        var page = Math.Max(1, filter.Page);
        var size = Math.Clamp(filter.PageSize, 1, 200);
        command.Parameters.AddWithValue("is_global", scope.IsGlobal);
        command.Parameters.AddWithValue("site_ids", scope.SiteIds.ToArray());
        command.Parameters.AddWithValue("area_ids", scope.AreaIds.ToArray());
        command.Parameters.AddWithValue("offset", (page - 1) * size);
        command.Parameters.AddWithValue("limit", size);
    }

    private static Site MapSite(NpgsqlDataReader reader) => new(
        new SiteId(reader.GetGuid(0)), reader.GetString(1), reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4),
        Enum.Parse<SiteStatus>(reader.GetString(5), false), reader.GetInt64(6));

    private static Area MapArea(NpgsqlDataReader reader) => new(
        new AreaId(reader.GetGuid(0)), new SiteId(reader.GetGuid(1)),
        reader.GetString(2), reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        Enum.Parse<AreaStatus>(reader.GetString(5), false), reader.GetInt64(6));

    private static Asset MapAsset(NpgsqlDataReader reader) => new(
        new AssetId(reader.GetGuid(0)), new SiteId(reader.GetGuid(1)),
        new AreaId(reader.GetGuid(2)), reader.GetString(3), reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        Enum.Parse<AssetStatus>(reader.GetString(6), false), reader.GetInt64(7));

    private static MeasurementPoint MapPoint(NpgsqlDataReader reader) => new(
        new PointId(reader.GetGuid(0)), new SiteId(reader.GetGuid(1)),
        new AreaId(reader.GetGuid(2)), new AssetId(reader.GetGuid(3)),
        reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.GetString(6), reader.GetString(7), reader.GetString(8),
        reader.GetInt32(9), reader.GetInt32(10),
        Enum.Parse<PointStatus>(reader.GetString(11), false), reader.GetInt64(12));

    private static SiteSnapshot MapSiteSnapshot(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),reader.GetString(1),reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),reader.GetString(4),
        Enum.Parse<SiteStatus>(reader.GetString(5),false),reader.GetInt64(6),reader.GetInt32(7));

    private static AreaSnapshot MapAreaSnapshot(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),reader.GetGuid(1),reader.GetString(2),reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        Enum.Parse<AreaStatus>(reader.GetString(5),false),reader.GetInt64(6),reader.GetInt32(7));

    private static AssetSnapshot MapAssetSnapshot(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),reader.GetGuid(1),reader.GetGuid(2),reader.GetString(3),reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        Enum.Parse<AssetStatus>(reader.GetString(6),false),reader.GetInt64(7),reader.GetInt32(8));

    private static PointSnapshot MapPointSnapshot(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),reader.GetGuid(1),reader.GetGuid(2),reader.GetGuid(3),
        reader.GetString(4),reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.GetString(6),reader.GetString(7),reader.GetString(8),
        reader.GetInt32(9),reader.GetInt32(10),
        Enum.Parse<PointStatus>(reader.GetString(11),false),reader.GetInt64(12));

    private static void BindSite(NpgsqlCommand command, Site value)
    {
        command.Parameters.AddWithValue("id",value.Id.Value);
        command.Parameters.AddWithValue("code",value.Code);
        command.Parameters.AddWithValue("name",value.Name);
        command.Parameters.AddWithValue("description",(object?)value.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("timezone",value.Timezone);
        command.Parameters.AddWithValue("status",value.Status.ToString());
        command.Parameters.AddWithValue("version",value.Version);
    }

    private static void BindArea(NpgsqlCommand command, Area value)
    {
        command.Parameters.AddWithValue("id",value.Id.Value);
        command.Parameters.AddWithValue("site_id",value.SiteId.Value);
        command.Parameters.AddWithValue("code",value.Code);
        command.Parameters.AddWithValue("name",value.Name);
        command.Parameters.AddWithValue("description",(object?)value.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("status",value.Status.ToString());
        command.Parameters.AddWithValue("version",value.Version);
    }

    private static void BindAsset(NpgsqlCommand command, Asset value)
    {
        command.Parameters.AddWithValue("id",value.Id.Value);
        command.Parameters.AddWithValue("site_id",value.SiteId.Value);
        command.Parameters.AddWithValue("area_id",value.AreaId.Value);
        command.Parameters.AddWithValue("code",value.Code);
        command.Parameters.AddWithValue("name",value.Name);
        command.Parameters.AddWithValue("description",(object?)value.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("status",value.Status.ToString());
        command.Parameters.AddWithValue("version",value.Version);
    }

    private static void BindPoint(NpgsqlCommand command, MeasurementPoint value)
    {
        command.Parameters.AddWithValue("id",value.Id.Value);
        command.Parameters.AddWithValue("site_id",value.SiteId.Value);
        command.Parameters.AddWithValue("area_id",value.AreaId.Value);
        command.Parameters.AddWithValue("asset_id",value.AssetId.Value);
        command.Parameters.AddWithValue("code",value.Code);
        command.Parameters.AddWithValue("description",(object?)value.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("metric_id",value.MetricId);
        command.Parameters.AddWithValue("unit_id",value.UnitId);
        command.Parameters.AddWithValue("data_owner_user_id",value.DataOwnerUserId);
        command.Parameters.AddWithValue("expected_interval_seconds",value.ExpectedIntervalSeconds);
        command.Parameters.AddWithValue("no_data_after_seconds",value.NoDataAfterSeconds);
        command.Parameters.AddWithValue("status",value.Status.ToString());
        command.Parameters.AddWithValue("version",value.Version);
    }

    private async Task InsertAsync(string sql,Action<NpgsqlCommand> bind,CancellationToken ct)
    {
        try { _=await ExecuteCoreAsync(sql,bind,ct); }
        catch(PostgresException exception) when (
            exception.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation)
        { throw new InvalidOperationException("ORGANIZATION_CONFLICT",exception); }
    }

    private async Task OptimisticAsync(string sql,Action<NpgsqlCommand> bind,CancellationToken ct)
    {
        try
        {
            if(await ExecuteCoreAsync(sql,bind,ct)!=1)
                throw new InvalidOperationException("ORGANIZATION_VERSION_CONFLICT");
        }
        catch(PostgresException exception) when (
            exception.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation)
        { throw new InvalidOperationException("ORGANIZATION_CONFLICT",exception); }
    }

    private async Task<int> ExecuteCoreAsync(string sql,Action<NpgsqlCommand> bind,CancellationToken ct)
    {
        var(connection,owns)=await AcquireAsync(ct);
        try
        {
            await using var command=new NpgsqlCommand(sql,connection,CurrentTransaction);
            bind(command);
            return await command.ExecuteNonQueryAsync(ct);
        }
        finally { if(owns) await connection.DisposeAsync(); }
    }

    private async Task<T> ScalarAsync<T>(string sql,Action<NpgsqlCommand> bind,CancellationToken ct)
    {
        var(connection,owns)=await AcquireAsync(ct);
        try
        {
            await using var command=new NpgsqlCommand(sql,connection,CurrentTransaction);
            bind(command);
            var result=await command.ExecuteScalarAsync(ct);
            return result is null or DBNull ? default! : (T)result;
        }
        finally { if(owns) await connection.DisposeAsync(); }
    }

    private async Task<T?> QuerySingleAsync<T>(
        string sql,Action<NpgsqlCommand> bind,Func<NpgsqlDataReader,T> map,CancellationToken ct)
    {
        var values=await QueryAsync(sql,bind,map,ct);
        return values.SingleOrDefault();
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,Action<NpgsqlCommand>? bind,Func<NpgsqlDataReader,T> map,CancellationToken ct)
    {
        var(connection,owns)=await AcquireAsync(ct);
        try
        {
            await using var command=new NpgsqlCommand(sql,connection,CurrentTransaction);
            bind?.Invoke(command);
            await using var reader=await command.ExecuteReaderAsync(ct);
            var results=new List<T>();
            while(await reader.ReadAsync(ct)) results.Add(map(reader));
            return results;
        }
        finally { if(owns) await connection.DisposeAsync(); }
    }

    private NpgsqlTransaction? CurrentTransaction =>
        _state.Value?.Current?.Transaction ?? _hostTransactions.Current?.Transaction;

    private async Task<(NpgsqlConnection Connection,bool Owns)> AcquireAsync(CancellationToken ct) =>
        _state.Value?.Current is { } state
            ? (state.Connection,false)
            : _hostTransactions.Current is { IsCompleted: false } host
                ? (host.Connection,false)
                : (await _dataSource.OpenConnectionAsync(ct),true);

    private static PostgresHostTransaction RequireTransaction(IHostTransaction transaction) =>
        PostgresTransactionResolver.Require(transaction);

    private sealed record TransactionState(NpgsqlConnection Connection,NpgsqlTransaction Transaction);

    private sealed class TransactionHolder
    {
        public TransactionState? Current { get; set; }
    }

    private sealed class OrganizationTransaction(TransactionState state,Action completed):IOrganizationTransaction
    {
        private bool _completed;
        public async Task CommitAsync(CancellationToken ct=default)
        {
            if(_completed)return;
            try{await state.Transaction.CommitAsync(ct);}finally{await FinishAsync();}
        }
        public async Task RollbackAsync(CancellationToken ct=default)
        {
            if(_completed)return;
            try{await state.Transaction.RollbackAsync(ct);}finally{await FinishAsync();}
        }
        private async Task FinishAsync()
        {
            if(_completed)return;
            _completed=true;
            completed();
            await state.Transaction.DisposeAsync();
            await state.Connection.DisposeAsync();
        }
    }
}
