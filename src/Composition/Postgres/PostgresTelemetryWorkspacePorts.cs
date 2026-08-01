using IUMP.Api.Infrastructure;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Telemetry.Contracts;
using Npgsql;

namespace IUMP.Composition.Postgres;

/// PostgreSQL adapter for the provider-neutral telemetry workspace contract.
/// Every query applies the caller scope in SQL before composing or paging data.
public sealed class PostgresTelemetryWorkspacePorts(NpgsqlDataSource dataSource)
    : ITelemetryWorkspaceQueryPort
{
    public async Task<TelemetryWorkspaceOptions> GetOptionsAsync(
        ServerPrincipal principal,
        TelemetryOptionsQuery query,
        CancellationToken ct = default)
    {
        if (query.Validate() is { } validationError)
            throw new ArgumentOutOfRangeException(nameof(query), validationError);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return query.Level switch
        {
            TelemetryOptionLevel.Sites => await ReadSitesAsync(connection, principal, query, ct),
            TelemetryOptionLevel.Areas => await ReadAreasAsync(connection, principal, query, ct),
            TelemetryOptionLevel.Assets => await ReadAssetsAsync(connection, principal, query, ct),
            TelemetryOptionLevel.Points => await ReadPointsAsync(connection, principal, query, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(query.Level))
        };
    }

    private static async Task<TelemetryWorkspaceOptions> ReadSitesAsync(
        NpgsqlConnection connection, ServerPrincipal principal, TelemetryOptionsQuery query, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT s.id,s.code,s.name
            FROM organization.sites s
            WHERE s.status='Active'
              AND (@is_admin OR s.id=ANY(@site_ids) OR EXISTS (
                  SELECT 1 FROM organization.areas scoped_area
                  WHERE scoped_area.site_id=s.id AND scoped_area.id=ANY(@area_ids)))
            ORDER BY s.code,s.id
            """, connection);
        AddScope(command, principal);
        var items = new List<TelemetrySiteOption>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        return new(items, [], [], [], ScopedCount: items.Count, Page: query.Page, PageSize: query.PageSize);
    }

    private static async Task<TelemetryWorkspaceOptions> ReadAreasAsync(
        NpgsqlConnection connection, ServerPrincipal principal, TelemetryOptionsQuery query, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT ar.id,ar.site_id,ar.code,ar.name
            FROM organization.areas ar
            JOIN organization.sites s ON s.id=ar.site_id
            WHERE ar.site_id=@site_id AND s.status='Active' AND ar.status='Active'
              AND (@is_admin OR s.id=ANY(@site_ids) OR ar.id=ANY(@area_ids))
            ORDER BY ar.code,ar.id
            """, connection);
        AddScope(command, principal);
        command.Parameters.AddWithValue("site_id", query.SiteId!.Value);
        var items = new List<TelemetryAreaOption>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3)));
        return new([], items, [], [], ScopedCount: items.Count, Page: query.Page, PageSize: query.PageSize);
    }

    private static async Task<TelemetryWorkspaceOptions> ReadAssetsAsync(
        NpgsqlConnection connection, ServerPrincipal principal, TelemetryOptionsQuery query, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT a.id,a.site_id,a.area_id,a.code,a.name
            FROM organization.assets a
            JOIN organization.areas ar ON ar.id=a.area_id AND ar.site_id=a.site_id
            JOIN organization.sites s ON s.id=a.site_id
            WHERE a.site_id=@site_id AND a.area_id=@area_id
              AND s.status='Active' AND ar.status='Active' AND a.status='Active'
              AND (@is_admin OR s.id=ANY(@site_ids) OR ar.id=ANY(@area_ids))
            ORDER BY a.code,a.id
            """, connection);
        AddScope(command, principal);
        command.Parameters.AddWithValue("site_id", query.SiteId!.Value);
        command.Parameters.AddWithValue("area_id", query.AreaId!.Value);
        var items = new List<TelemetryAssetOption>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4)));
        return new([], [], items, [], ScopedCount: items.Count, Page: query.Page, PageSize: query.PageSize);
    }

    private static async Task<TelemetryWorkspaceOptions> ReadPointsAsync(
        NpgsqlConnection connection, ServerPrincipal principal, TelemetryOptionsQuery query, CancellationToken ct)
    {
        _ = query.TryGetOffset(out var offset);
        await using var command = new NpgsqlCommand("""
            SELECT count(*)
            FROM organization.measurement_points p
            JOIN organization.assets a ON a.id=p.asset_id AND a.area_id=p.area_id AND a.site_id=p.site_id
            JOIN organization.areas ar ON ar.id=p.area_id AND ar.site_id=p.site_id
            JOIN organization.sites s ON s.id=p.site_id
            WHERE p.site_id=@site_id AND p.area_id=@area_id AND p.asset_id=@asset_id
              AND s.status='Active' AND ar.status='Active' AND a.status='Active' AND p.status='Active'
              AND (@is_admin OR s.id=ANY(@site_ids) OR ar.id=ANY(@area_ids))
              AND (@search='' OR p.code ILIKE '%' || @search || '%' OR COALESCE(p.description,p.code) ILIKE '%' || @search || '%');

            SELECT p.id,p.site_id,p.area_id,p.asset_id,p.code,COALESCE(p.description,p.code),
                   metric.code,u.symbol
            FROM organization.measurement_points p
            JOIN organization.assets a ON a.id=p.asset_id AND a.area_id=p.area_id AND a.site_id=p.site_id
            JOIN organization.areas ar ON ar.id=p.area_id AND ar.site_id=p.site_id
            JOIN organization.sites s ON s.id=p.site_id
            LEFT JOIN catalog.metrics metric ON metric.id::text=p.metric_id
            LEFT JOIN catalog.units u ON u.id::text=p.unit_id
            WHERE p.site_id=@site_id AND p.area_id=@area_id AND p.asset_id=@asset_id
              AND s.status='Active' AND ar.status='Active' AND a.status='Active' AND p.status='Active'
              AND (@is_admin OR s.id=ANY(@site_ids) OR ar.id=ANY(@area_ids))
              AND (@search='' OR p.code ILIKE '%' || @search || '%' OR COALESCE(p.description,p.code) ILIKE '%' || @search || '%')
            ORDER BY p.code,p.id
            OFFSET @offset LIMIT @limit
            """, connection);
        AddScope(command, principal);
        command.Parameters.AddWithValue("site_id", query.SiteId!.Value);
        command.Parameters.AddWithValue("area_id", query.AreaId!.Value);
        command.Parameters.AddWithValue("asset_id", query.AssetId!.Value);
        command.Parameters.AddWithValue("search", query.Search?.Trim() ?? string.Empty);
        command.Parameters.Add("offset", NpgsqlTypes.NpgsqlDbType.Bigint).Value = offset;
        command.Parameters.AddWithValue("limit", query.PageSize);
        var items = new List<TelemetryPointOption>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        _ = await reader.ReadAsync(ct);
        var count = reader.GetInt64(0);
        _ = await reader.NextResultAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3),
                reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? reader.GetString(4) : reader.GetString(6),
                reader.IsDBNull(7) ? "" : reader.GetString(7)));
        }
        return new([], [], [], items, ScopedCount: count, Page: query.Page, PageSize: query.PageSize);
    }

    public async Task<TelemetryWorkspaceCurrent> GetCurrentAsync(
        TelemetryHierarchySelection selection,
        ServerPrincipal principal,
        CancellationToken ct = default)
    {
        var selectionError = TelemetrySelectionRules.Validate(selection);
        if (selectionError is not null)
            throw new TelemetryHierarchyConflictException(selectionError);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var point = await ReadPointAsync(connection, selection, principal, ct);
        if (point is null)
            throw new RuntimeScopeDeniedException();
        if (point.Value.SiteId != selection.SiteId ||
            (selection.AreaId is { } areaId && point.Value.AreaId != areaId) ||
            (selection.AssetId is { } assetId && point.Value.AssetId != assetId))
            throw new TelemetryHierarchyConflictException();

        var pointOption = point.Value.Option;
        var relationships = await ReadRelationshipsAsync(connection, selection.PointId, ct);
        var queriedAt = DateTime.UtcNow;
        if (relationships.Count == 0)
            return new TelemetryWorkspaceCurrent(
                selection, pointOption, TelemetryDataState.NotConfigured, false, null, null,
                "NOT_CONFIGURED", null, null, null, null, null, queriedAt, "NOT_CONFIGURED");
        if (relationships.Count > 1)
            return new TelemetryWorkspaceCurrent(
                selection, pointOption, TelemetryDataState.Ambiguous, false, null, null,
                "AMBIGUOUS_ACTIVE_MAPPING", null, null, null, null, null, queriedAt,
                "AMBIGUOUS_ACTIVE_MAPPING");

        var relationship = relationships[0];
        var latest = await ReadLatestAsync(connection, selection.PointId, relationship.Source.SourceId, relationship.MappingId, ct);
        var health = await ReadHealthAsync(connection, selection.PointId, relationship.Source.SourceId, ct);
        var run = await ReadRunAsync(connection, selection.PointId, relationship.Source.SourceId, relationship.MappingId, ct);
        if (latest is null)
            return new TelemetryWorkspaceCurrent(
                selection, pointOption, TelemetryDataState.NoData, false, null, null,
                "NO_DATA", null, null, relationship.Source,
                health, run, queriedAt, "NO_DATA");

        var accepted = latest.Value;
        return new TelemetryWorkspaceCurrent(
            selection, pointOption, TelemetryDataState.Data, true, accepted.Value, accepted.Quality,
            accepted.ReasonCode, accepted.SourceTimestampUtc, accepted.ReceivedAtUtc,
            relationship.Source, health, run, queriedAt);
    }

    private static void AddScope(NpgsqlCommand command, ServerPrincipal principal)
    {
        command.Parameters.AddWithValue("is_admin", principal.IsAdministrator);
        command.Parameters.AddWithValue("site_ids", principal.SiteIds
            .Where(value => Guid.TryParse(value, out _)).Select(Guid.Parse).ToArray());
        command.Parameters.AddWithValue("area_ids", principal.AreaIds
            .Where(value => Guid.TryParse(value, out _)).Select(Guid.Parse).ToArray());
    }

    private static async Task<(TelemetryPointOption Option, Guid SiteId, Guid AreaId, Guid AssetId)?> ReadPointAsync(
        NpgsqlConnection connection,
        TelemetryHierarchySelection selection,
        ServerPrincipal principal,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT s.id,s.code,s.name,ar.id,ar.code,ar.name,a.id,a.code,a.name,
                   p.id,p.code,COALESCE(p.description,p.code),metric.code,u.symbol
            FROM organization.measurement_points p
            JOIN organization.sites s ON s.id=p.site_id
            JOIN organization.areas ar ON ar.id=p.area_id
            JOIN organization.assets a ON a.id=p.asset_id
            LEFT JOIN catalog.metrics metric ON metric.id::text=p.metric_id
            LEFT JOIN catalog.units u ON u.id::text=p.unit_id
            WHERE p.id=@point_id
              AND (@is_admin OR s.id = ANY(@site_ids) OR ar.id = ANY(@area_ids))
              AND s.status='Active' AND ar.status='Active' AND a.status='Active' AND p.status='Active'
            """, connection);
        command.Parameters.AddWithValue("point_id", selection.PointId);
        AddScope(command, principal);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var siteId = reader.GetGuid(0);
        var areaId = reader.GetGuid(3);
        var assetId = reader.GetGuid(6);
        return (new TelemetryPointOption(
            reader.GetGuid(9), siteId, areaId, assetId, reader.GetString(10), reader.GetString(11),
            reader.IsDBNull(12) ? reader.GetString(10) : reader.GetString(12),
            reader.IsDBNull(13) ? "" : reader.GetString(13)), siteId, areaId, assetId);
    }

    private static async Task<List<(Guid MappingId, TelemetrySourceSummary Source)>> ReadRelationshipsAsync(
        NpgsqlConnection connection, Guid pointId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT m.mapping_id,ds.id,ds.code,ds.name
            FROM catalog.source_point_mapping m
            JOIN catalog.data_sources ds ON ds.id=m.data_source_id
            WHERE m.point_id=@point_id AND m.status='Active'
              AND m.effective_from<=now()
              AND (m.effective_to IS NULL OR m.effective_to>now())
              AND ds.status='Active'
            ORDER BY m.mapping_id
            """, connection);
        command.Parameters.AddWithValue("point_id", pointId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<(Guid, TelemetrySourceSummary)>();
        while (await reader.ReadAsync(ct))
            result.Add((reader.GetGuid(0), new(reader.GetGuid(1), reader.GetString(2), reader.GetString(3))));
        return result;
    }

    private static async Task<(double Value, string Quality, string? ReasonCode,
        DateTime SourceTimestampUtc, DateTime ReceivedAtUtc)?> ReadLatestAsync(
        NpgsqlConnection connection, Guid pointId, Guid sourceId, Guid mappingId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT numeric_value,quality_code,reason_code,source_timestamp_utc,received_at_utc
            FROM telemetry.point_latest
            WHERE point_id=@point_id AND source_id=@source_id AND mapping_id=@mapping_id
            LIMIT 1
            """, connection);
        command.Parameters.AddWithValue("point_id", pointId);
        command.Parameters.AddWithValue("source_id", sourceId);
        command.Parameters.AddWithValue("mapping_id", mappingId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return (reader.GetDouble(0), reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetDateTime(3).ToUniversalTime(),
            reader.GetDateTime(4).ToUniversalTime());
    }

    private static async Task<TelemetryHealthSummary?> ReadHealthAsync(
        NpgsqlConnection connection, Guid pointId, Guid sourceId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT health_status,last_accepted_received_at_utc,run_status,
                   generated_count,accepted_count,rejected_count,evaluated_at_utc,
                   expected_interval_seconds,no_data_after_seconds
            FROM telemetry.point_source_status
            WHERE point_id=@point_id AND source_id=@source_id
            LIMIT 1
            """, connection);
        command.Parameters.AddWithValue("point_id", pointId);
        command.Parameters.AddWithValue("source_id", sourceId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new TelemetryHealthSummary(pointId, sourceId,
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetDateTime(1).ToUniversalTime(),
            reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetInt64(3),
            reader.GetInt64(4), reader.GetInt64(5), reader.GetDateTime(6).ToUniversalTime(),
            reader.GetInt32(7), reader.GetInt32(8));
    }

    private static async Task<TelemetryRunSummary?> ReadRunAsync(
        NpgsqlConnection connection, Guid pointId, Guid sourceId, Guid mappingId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT r.run_id,r.status,r.generated_count,r.accepted_count,r.rejected_count,
                   (SELECT max(raw.received_at_utc) FROM telemetry.measurement_raw raw
                     WHERE raw.point_id=@point_id AND raw.source_id=@source_id
                       AND raw.mapping_id=@mapping_id AND raw.simulator_run_id=r.run_id)
            FROM acquisition.simulator_run r
            JOIN acquisition.simulator_run_point_state ps
              ON ps.run_id=r.run_id AND ps.point_id=@point_id AND ps.mapping_id=@mapping_id
            WHERE r.source_id=@source_id AND r.status IN ('Running','Paused')
            ORDER BY r.created_at_utc DESC
            LIMIT 1
            """, connection);
        command.Parameters.AddWithValue("point_id", pointId);
        command.Parameters.AddWithValue("source_id", sourceId);
        command.Parameters.AddWithValue("mapping_id", mappingId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new TelemetryRunSummary(reader.GetGuid(0),
            reader.GetString(1), reader.GetInt64(2),
            reader.GetInt64(3), reader.GetInt64(4),
            reader.IsDBNull(5) ? null : reader.GetDateTime(5).ToUniversalTime());
    }
}
