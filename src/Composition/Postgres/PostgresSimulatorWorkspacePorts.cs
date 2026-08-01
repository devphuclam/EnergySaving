using System.Text.Json;
using IUMP.Api.Infrastructure;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;
using Npgsql;

namespace IUMP.Composition.Postgres;

/// <summary>
/// PostgreSQL adapter for the explicit Simulator workspace. Scope predicates are applied
/// inside the selector and history queries before ordering and paging.
/// </summary>
public sealed class PostgresSimulatorWorkspaceQueryPort(
    NpgsqlDataSource dataSource) : ISimulatorWorkspaceQueryPort
{
    public async Task<SimulatorWorkspaceSnapshot> GetAsync(
        SimulatorSelection? selection,
        int page,
        int pageSize,
        ServerPrincipal principal,
        CancellationToken ct = default)
    {
        var boundedPage = Math.Max(1, page);
        var boundedPageSize = Math.Clamp(pageSize, 1, 100);
        var siteIds = ParseIds(principal.SiteIds);
        var areaIds = ParseIds(principal.AreaIds);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var options = await ReadOptionsAsync(connection, principal.IsAdministrator,
            siteIds, areaIds, ct);
        var selectedOption = SimulatorWorkspaceSelectionRules.Resolve(options, selection);
        if (selection is not null && selectedOption is null)
        {
            var visibleSelection = await IsSelectionVisibleAsync(connection, selection,
                principal.IsAdministrator, siteIds, areaIds, ct);
            return EmptySnapshot(options, selection, boundedPage, boundedPageSize,
                "validation", visibleSelection
                    ? "SIMULATOR_SELECTION_INELIGIBLE"
                    : "SIMULATOR_SELECTION_NOT_FOUND");
        }

        var history = selectedOption is null
            ? new SimulatorRunHistoryPage(Array.Empty<SimulatorRunHistoryItem>(), 0,
                boundedPage, boundedPageSize)
            : await ReadHistoryAsync(connection, selectedOption, principal.IsAdministrator,
                siteIds.Select(value => value.ToString("D")).ToArray(),
                areaIds.Select(value => value.ToString("D")).ToArray(),
                boundedPage, boundedPageSize, ct);
        var current = history.Items.FirstOrDefault(item =>
            item.Status is "Running" or "Paused");
        var state = selectedOption is not null
            ? "ready"
            : options.Count == 0 ? "empty" : "no-selection";
        return new SimulatorWorkspaceSnapshot(options, selection, current, history, state);
    }

    private static SimulatorWorkspaceSnapshot EmptySnapshot(
        IReadOnlyList<SimulatorSelectionOption> options,
        SimulatorSelection selection,
        int page,
        int pageSize,
        string state,
        string errorCode) => new(
            options, selection, null,
            new SimulatorRunHistoryPage(Array.Empty<SimulatorRunHistoryItem>(), 0, page, pageSize),
            state, errorCode);

    private static async Task<IReadOnlyList<SimulatorSelectionOption>> ReadOptionsAsync(
        NpgsqlConnection connection,
        bool isGlobal,
        Guid[] siteIds,
        Guid[] areaIds,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT DISTINCT
                   s.id,s.code,s.name,
                   ar.id,ar.code,ar.name,
                   a.id,a.code,a.name,
                   ds.id,ds.code,ds.name,ds.version,
                   c.configuration_id,v.configuration_version,v.interval_seconds
            FROM catalog.data_sources ds
            JOIN acquisition.simulator_configuration c ON c.source_id=ds.id
            JOIN acquisition.simulator_configuration_version v
              ON v.configuration_id=c.configuration_id
             AND v.configuration_version=c.current_configuration_version
            JOIN catalog.source_point_mapping m
              ON m.data_source_id=ds.id
             AND m.status='Active'
             AND m.effective_from<=now()
             AND (m.effective_to IS NULL OR m.effective_to>now())
            JOIN organization.measurement_points p ON p.id::text=m.point_id
            JOIN organization.sites s ON s.id=p.site_id
            JOIN organization.areas ar ON ar.id=p.area_id
            JOIN organization.assets a ON a.id=p.asset_id
            WHERE ds.source_type='Simulator'
              AND ds.status='Active'
              AND p.status='Active'
              AND s.status='Active'
              AND ar.status='Active'
              AND a.status='Active'
              AND (@is_global OR s.id=ANY(@site_ids) OR ar.id=ANY(@area_ids))
            ORDER BY s.code,ar.code,a.code,ds.code,c.configuration_id
            """, connection);
        command.Parameters.AddWithValue("is_global", isGlobal);
        command.Parameters.AddWithValue("site_ids", siteIds);
        command.Parameters.AddWithValue("area_ids", areaIds);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var options = new List<SimulatorSelectionOption>();
        while (await reader.ReadAsync(ct))
        {
            options.Add(new SimulatorSelectionOption(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                reader.GetGuid(3), reader.GetString(4), reader.GetString(5),
                reader.GetGuid(6), reader.GetString(7), reader.GetString(8),
                reader.GetGuid(9), reader.GetString(10), reader.GetString(11),
                reader.GetInt64(12), reader.GetGuid(13), reader.GetInt64(14),
                reader.GetInt32(15), true, null));
        }
        return options
            .GroupBy(option => new
            {
                option.SiteId, option.AreaId, option.AssetId, option.SourceId,
                option.ConfigurationId, option.ConfigurationVersion
            })
            .Select(group => group.First())
            .ToArray();
    }

    private static async Task<SimulatorRunHistoryPage> ReadHistoryAsync(
        NpgsqlConnection connection,
        SimulatorSelectionOption selected,
        bool isGlobal,
        string[] siteIds,
        string[] areaIds,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT r.run_id,r.source_id,r.configuration_id,r.configuration_version,
                   r.status,r.version,r.generated_count,r.accepted_count,r.rejected_count,
                   (SELECT MAX(raw.received_at_utc)
                      FROM telemetry.measurement_raw raw
                     WHERE raw.simulator_run_id=r.run_id),
                   COALESCE(cv.interval_seconds,@interval_seconds),r.created_at_utc,
                   count(*) OVER()::int
            FROM acquisition.simulator_run r
            LEFT JOIN acquisition.simulator_configuration_version cv
              ON cv.configuration_id=r.configuration_id
             AND cv.configuration_version=r.configuration_version
            WHERE r.source_id=@source_id
              AND r.configuration_id=@configuration_id
              AND r.configuration_version=@configuration_version
              AND EXISTS (
                  SELECT 1
                  FROM acquisition.simulator_run_point_state ps
                  WHERE ps.run_id=r.run_id
                    AND (@is_global OR ps.site_id=ANY(@site_ids)
                         OR ps.area_id=ANY(@area_ids))
              )
            ORDER BY r.created_at_utc DESC,r.run_id DESC
            OFFSET @offset LIMIT @limit
            """, connection);
        command.Parameters.AddWithValue("source_id", selected.SourceId);
        command.Parameters.AddWithValue("configuration_id", selected.ConfigurationId);
        command.Parameters.AddWithValue("configuration_version", selected.ConfigurationVersion);
        command.Parameters.AddWithValue("interval_seconds", selected.IntervalSeconds);
        command.Parameters.AddWithValue("is_global", isGlobal);
        command.Parameters.AddWithValue("site_ids", siteIds);
        command.Parameters.AddWithValue("area_ids", areaIds);
        command.Parameters.AddWithValue("offset", (page - 1) * pageSize);
        command.Parameters.AddWithValue("limit", pageSize);

        await using var reader = await command.ExecuteReaderAsync(ct);
        var items = new List<SimulatorRunHistoryItem>();
        var total = 0;
        while (await reader.ReadAsync(ct))
        {
            total = reader.GetInt32(12);
            items.Add(new SimulatorRunHistoryItem(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetInt64(3),
                reader.GetString(4), reader.GetInt64(5), reader.GetInt64(6), reader.GetInt64(7),
                reader.GetInt64(8), reader.IsDBNull(9) ? null : reader.GetDateTime(9).ToUniversalTime(),
                reader.GetInt32(10), reader.GetDateTime(11).ToUniversalTime()));
        }
        return new SimulatorRunHistoryPage(items, total, page, pageSize);
    }

    private static async Task<bool> IsSelectionVisibleAsync(
        NpgsqlConnection connection,
        SimulatorSelection selection,
        bool isGlobal,
        Guid[] siteIds,
        Guid[] areaIds,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS(
                SELECT 1
                FROM catalog.data_sources ds
                JOIN acquisition.simulator_configuration c ON c.source_id=ds.id
                JOIN organization.sites s ON s.id=@site_id
                WHERE ds.id=@source_id
                  AND c.configuration_id=@configuration_id
                  AND c.current_configuration_version=@configuration_version
                  AND s.status='Active'
                  AND (@is_global OR EXISTS(
                       SELECT 1
                       FROM catalog.source_point_mapping m
                       JOIN organization.measurement_points p ON p.id::text=m.point_id
                       JOIN organization.areas ar ON ar.id=p.area_id
                       JOIN organization.assets a ON a.id=p.asset_id
                       WHERE m.data_source_id=ds.id
                         AND p.site_id=@site_id
                         AND (@has_area=false OR p.area_id=@area_id)
                         AND (@has_asset=false OR p.asset_id=@asset_id)
                         AND (p.site_id=ANY(@site_ids) OR p.area_id=ANY(@area_ids))))
            )
            """, connection);
        command.Parameters.AddWithValue("source_id", selection.SourceId);
        command.Parameters.AddWithValue("configuration_id", selection.ConfigurationId);
        command.Parameters.AddWithValue("configuration_version", selection.ConfigurationVersion);
        command.Parameters.AddWithValue("site_id", selection.SiteId);
        command.Parameters.AddWithValue("area_id", selection.AreaId ?? Guid.Empty);
        command.Parameters.AddWithValue("asset_id", selection.AssetId ?? Guid.Empty);
        command.Parameters.AddWithValue("has_area", selection.AreaId.HasValue);
        command.Parameters.AddWithValue("has_asset", selection.AssetId.HasValue);
        command.Parameters.AddWithValue("is_global", isGlobal);
        command.Parameters.AddWithValue("site_ids", siteIds);
        command.Parameters.AddWithValue("area_ids", areaIds);
        return (bool)(await command.ExecuteScalarAsync(ct) ?? false);
    }

    private static Guid[] ParseIds(IEnumerable<string> values) => values
        .Where(value => Guid.TryParse(value, out _))
        .Select(Guid.Parse)
        .Distinct()
        .ToArray();
}

/// Bridges the selected-context contract to the existing transactional run command port.
public sealed class PostgresSimulatorWorkspaceCommandPort(
    ISimulatorWorkspaceQueryPort workspace,
    ISimulatorCommandPort commands) : ISimulatorWorkspaceCommandPort
{
    public async Task<CommandExecutionResult> ExecuteAsync(
        string operationCode,
        SimulatorSelection selection,
        Guid? runId,
        long? expectedVersion,
        ServerPrincipal principal,
        IHostTransaction transaction,
        CancellationToken ct = default)
    {
        if (!SimulatorWorkspaceSelectionRules.IsExplicit(selection))
            return Failure(400, "SIMULATOR_SELECTION_REQUIRED");

        var snapshot = await workspace.GetAsync(selection, 1, 1, principal, ct);
        if (snapshot.ErrorCode is not null ||
            SimulatorWorkspaceSelectionRules.Resolve(snapshot.Options, selection) is null)
            return Failure(422, snapshot.ErrorCode ?? "SIMULATOR_SELECTION_INELIGIBLE");

        if (operationCode != CommandOperationCodes.StartSimulator)
        {
            if (runId is null) return Failure(400, "SIMULATOR_RUN_REQUIRED");
            if (snapshot.CurrentRun?.RunId != runId)
                return Failure(404, "SIMULATOR_RUN_NOT_FOUND");
        }

        return await commands.ExecuteAsync(operationCode,
            operationCode == CommandOperationCodes.StartSimulator
                ? selection.SourceId : runId!.Value,
            expectedVersion, principal, transaction, ct);
    }

    private static CommandExecutionResult Failure(int status, string code) =>
        new(status, JsonSerializer.Serialize(new { errorCode = code }), null);
}
