using System.Text.Json;
using IUMP.Api.Infrastructure;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.IAM.Contracts;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;

namespace IUMP.Composition.Postgres;

public sealed class PostgresWorkspaceSiteExistence(
    IOrganizationQueryRepository organization) : IWorkspaceSiteExistence
{
    public Task<bool> ExistsAsync(Guid siteId, CancellationToken ct = default) =>
        organization.SiteExistsAsync(siteId, ct);
}

public sealed class PostgresOperationalWorkspacePorts(
    IOrganizationQueryRepository organization,
    CatalogRuntimeGateway catalog,
    IAcquisitionConfigurationRepository configurations,
    IEngineerScopeAssignmentService engineers,
    ITransactionalOutboxWriter outbox) :
    IOperationalWorkspaceQueryPort,
    IOperationalWorkspaceCommandPort
{
    public async Task<OperationalWorkspaceStatus> GetStatusAsync(
        ServerPrincipal principal, CancellationToken ct = default)
    {
        var scope = principal.IsAdministrator
            ? OrganizationQueryScope.Global()
            : new OrganizationQueryScope(false,
                Parse(principal.SiteIds), Parse(principal.AreaIds));
        var hasScope = principal.IsAdministrator ||
            principal.SiteIds.Count > 0 || principal.AreaIds.Count > 0;
        var sites = await All(
            filter => organization.GetSitesAsync(scope, filter, ct));
        var authorizedSiteIds = sites.Select(value => value.Id).ToHashSet();
        var areas = new List<AreaSnapshot>();
        var assets = new List<AssetSnapshot>();
        var points = new List<PointSnapshot>();
        foreach (var site in sites)
        {
            var siteAreas = await All(
                filter => organization.GetAreasForSiteAsync(
                    site.Id, scope, filter, ct));
            areas.AddRange(siteAreas);
            foreach (var area in siteAreas)
            {
                var areaAssets = await All(
                    filter => organization.GetAssetsForAreaAsync(
                        area.Id, scope, filter, ct));
                assets.AddRange(areaAssets);
                foreach (var asset in areaAssets)
                    points.AddRange(await All(
                        filter => organization.GetPointsForAssetAsync(
                            asset.Id, scope, filter, ct)));
            }
        }

        var assignedSiteIds = principal.IsAdministrator
            ? (await engineers.ListEligibleEngineersAsync(ct))
                .SelectMany(value => value.AssignedSiteIds)
                .ToHashSet()
            : [];
        var pointIds = points.Select(value => value.Id).ToHashSet();
        var directSiteIds = Parse(principal.SiteIds).ToHashSet();
        var allMappings = await catalog.GetMappingSnapshotsAsync(ct);
        var sources = (await catalog.GetDataSourceSnapshotsAsync(ct))
            .Where(value =>
                value.SiteId.HasValue &&
                authorizedSiteIds.Contains(value.SiteId.Value) &&
                (principal.IsAdministrator ||
                 directSiteIds.Contains(value.SiteId.Value) ||
                 allMappings.Any(mapping =>
                     mapping.DataSourceId == value.Id &&
                     pointIds.Contains(mapping.PointId))))
            .ToArray();
        var sourceIds = sources.Select(value => value.Id).ToHashSet();
        var mappings = allMappings
            .Where(value =>
                sourceIds.Contains(value.DataSourceId) &&
                pointIds.Contains(value.PointId))
            .ToArray();
        var configurationValues = new List<WorkspacePersistedConfiguration>();
        foreach (var source in sources)
        {
            var value = await configurations.GetBySourceIdAsync(source.Id, ct);
            if (value is not null)
                configurationValues.Add(new WorkspacePersistedConfiguration(
                    value.ConfigurationId, value.SourceId, value.Version));
        }

        var snapshot = new WorkspacePersistedSnapshot(
            sites.Select(value => new WorkspacePersistedSite(
                value.Id, value.Code, value.Name, value.Status.ToString(),
                value.Version, assignedSiteIds.Contains(value.Id), true)).ToArray(),
            areas.Select(value => new WorkspacePersistedArea(
                value.Id, value.SiteId, value.Status.ToString(), value.Version)).ToArray(),
            assets.Select(value => new WorkspacePersistedAsset(
                value.Id, value.SiteId, value.AreaId, value.Status.ToString(),
                value.Version)).ToArray(),
            points.Select(value => new WorkspacePersistedPoint(
                value.Id, value.SiteId, value.AreaId, value.AssetId,
                value.Status.ToString(), value.Version)).ToArray(),
            sources.Select(value => new WorkspacePersistedSource(
                value.Id, value.SiteId!.Value, value.Status, value.Version)).ToArray(),
            mappings.Select(value => new WorkspacePersistedMapping(
                value.Id, value.DataSourceId, value.PointId, value.Status,
                value.Version)).ToArray(),
            configurationValues);
        return OperationalWorkspaceStatusBuilder.BuildFromSnapshot(
            principal.IsAdministrator, hasScope, true, snapshot) with
        {
            CurrentUserId = principal.UserId
        };

        static async Task<IReadOnlyList<T>> All<T>(
            Func<ScopeFilter, Task<PagedResult<T>>> query)
        {
            const int pageSize = 200;
            var values = new List<T>();
            var page = 1;
            while (true)
            {
                var result = await query(new ScopeFilter(page, pageSize));
                values.AddRange(result.Items);
                if (values.Count >= result.TotalCount || result.Items.Count == 0)
                    return values;
                page++;
            }
        }
    }

    public async Task<WorkspaceChainValidation> ValidateChainAsync(
        WorkspaceChainSelection requested,
        ServerPrincipal principal,
        CancellationToken ct = default)
    {
        if (requested is not
            {
                SiteId: { } siteId,
                AreaId: { } areaId,
                AssetId: { } assetId,
                PointId: { } pointId,
                SourceId: { } sourceId,
                MappingId: { } mappingId,
                ConfigurationId: { } configurationId
            } ||
            !principal.HasScope(
                siteId.ToString("D"), areaId.ToString("D")))
            return new WorkspaceChainValidation(
                false,
                [Failure(WorkspaceStep.SiteAndEngineer, null, "NOT_FOUND")],
                new Dictionary<string, long>(), [], false);

        var site = await organization.GetSiteSnapshotAsync(siteId, ct);
        var area = await organization.GetAreaSnapshotAsync(areaId, ct);
        var asset = await organization.GetAssetSnapshotAsync(assetId, ct);
        var point = await organization.GetPointSnapshotAsync(pointId, ct);
        var source = await catalog.GetDataSourceSnapshotAsync(sourceId, ct);
        var mapping = await catalog.GetMappingSnapshotAsync(mappingId, ct);
        var configuration = await configurations.GetBySourceIdAsync(sourceId, ct);
        var failures = new List<WorkspaceValidationFailure>();
        if (site is null || area is null || asset is null || point is null ||
            source is null || mapping is null || configuration is null)
            failures.Add(Failure(
                WorkspaceStep.ValidateAndActivate, null, "NOT_FOUND"));
        else
        {
            if (area.SiteId != siteId)
                failures.Add(Failure(
                    WorkspaceStep.Area, "areaId", "AREA_SITE_MISMATCH"));
            if (asset.SiteId != siteId || asset.AreaId != areaId)
                failures.Add(Failure(
                    WorkspaceStep.Asset, "assetId", "ASSET_ANCESTRY_MISMATCH"));
            if (point.SiteId != siteId || point.AreaId != areaId ||
                point.AssetId != assetId)
                failures.Add(Failure(
                    WorkspaceStep.MeasurementPoint, "pointId",
                    "POINT_ANCESTRY_MISMATCH"));
            if (source.SiteId != siteId)
                failures.Add(Failure(
                    WorkspaceStep.DataSource, "sourceId",
                    "SOURCE_SITE_MISMATCH"));
            if (mapping.DataSourceId != sourceId ||
                mapping.PointId != pointId)
                failures.Add(Failure(
                    WorkspaceStep.Mapping, "mappingId",
                    "MAPPING_RELATIONSHIP_MISMATCH"));
            if (configuration.ConfigurationId != configurationId)
                failures.Add(Failure(
                    WorkspaceStep.SimulatorConfiguration, "configurationId",
                    "CONFIGURATION_SOURCE_MISMATCH"));
            if (site.Status != SiteStatus.Active)
                failures.Add(Failure(
                    WorkspaceStep.SiteAndEngineer, "siteId",
                    "SITE_NOT_ACTIVE"));
            if (source.Status == "Decommissioned")
                failures.Add(Failure(
                    WorkspaceStep.DataSource, "sourceId", "SOURCE_TERMINAL"));
            if (mapping.Status is "Inactive" or "Superseded")
                failures.Add(Failure(
                    WorkspaceStep.Mapping, "mappingId", "MAPPING_INELIGIBLE"));
            if (point.Status == PointStatus.Decommissioned)
                failures.Add(Failure(
                    WorkspaceStep.MeasurementPoint, "pointId",
                    "POINT_TERMINAL"));
            if (area.Status == AreaStatus.Inactive)
                failures.Add(Failure(
                    WorkspaceStep.Area, "areaId", "AREA_INELIGIBLE"));
            if (asset.Status is
                AssetStatus.Inactive or AssetStatus.Decommissioned)
                failures.Add(Failure(
                    WorkspaceStep.Asset, "assetId", "ASSET_INELIGIBLE"));
            if (point.Status == PointStatus.Inactive)
                failures.Add(Failure(
                    WorkspaceStep.MeasurementPoint, "pointId",
                    "POINT_INELIGIBLE"));
        }
        if (failures.Count > 0)
            return new WorkspaceChainValidation(
                false, failures, new Dictionary<string, long>(), [], false);

        var versions = new Dictionary<string, long>();
        Add("site", site!.Version);
        Add("area", area!.Version);
        Add("asset", asset!.Version);
        Add("point", point!.Version);
        Add("source", source!.Version);
        Add("mapping", mapping!.Version);
        Add("configuration", configuration!.Version);
        var activationSteps = new[]
        {
            (Name: "area", Pending: area.Status != AreaStatus.Active),
            (Name: "asset", Pending: asset.Status != AssetStatus.Active),
            (Name: "data-source", Pending: source.Status != "Active"),
            (Name: "mapping", Pending: mapping.Status != "Active"),
            (Name: "measurement-point", Pending: point.Status != PointStatus.Active)
        }.Where(value => value.Pending).Select(value => value.Name).ToArray();
        return new WorkspaceChainValidation(
            true, [], versions, activationSteps, false);

        void Add(string key, long? value)
        {
            if (value.HasValue) versions[key] = value.Value;
        }

        static WorkspaceValidationFailure Failure(
            WorkspaceStep step,
            string? field,
            string code) =>
            new(step, field, code, $"setup.{step}.{code}".ToLowerInvariant());
    }

    public async Task<IReadOnlyList<WorkspaceEngineerCandidate>> ListEngineersAsync(
        ServerPrincipal principal, CancellationToken ct = default)
    {
        if (!principal.IsAdministrator)
            return Array.Empty<WorkspaceEngineerCandidate>();
        return (await engineers.ListEligibleEngineersAsync(ct))
            .Select(value => new WorkspaceEngineerCandidate(
                value.UserId, value.Username, value.Status, value.AssignedSiteIds))
            .ToArray();
    }

    public async Task<CommandExecutionResult> AssignEngineerAsync(
        Guid siteId,
        Guid engineerUserId,
        ServerPrincipal principal,
        IHostTransaction transaction,
        CancellationToken ct = default)
    {
        var result = await engineers.AssignSiteAsync(
            siteId, engineerUserId, principal.UserId, ct);
        if (!result.IsSuccess)
            return new CommandExecutionResult(
                result.Code == "FORBIDDEN" ? 403 :
                result.Code == "NOT_FOUND" ? 404 : 422,
                JsonSerializer.Serialize(new { errorCode = result.Code }), null);

        if (result.Code == "ASSIGNED")
        {
            await outbox.EnqueueAsync(new OwnerEventEnvelope(
                Guid.NewGuid(), "IAM.EngineerSiteScopeAssigned.v1", 1, "IAM",
                "UserScope", engineerUserId.ToString("D"), 1,
                principal.UserId.ToString("D"), principal.Username,
                new Dictionary<string, object?>(),
                new Dictionary<string, object?> { ["siteId"] = siteId.ToString("D") },
                "Assign", "Engineer assigned to Site scope", DateTime.UtcNow,
                Guid.NewGuid().ToString("D"), null, siteId.ToString("D"), null),
                transaction, ct);
        }
        return CommandExecutionResult.Ok(
            result.Code == "ASSIGNED" ? 201 : 200,
            JsonSerializer.Serialize(new
            {
                siteId,
                engineerUserId,
                status = result.Code == "ASSIGNED" ? "Assigned" : "AlreadyAssigned"
            }),
            engineerUserId.ToString("D"));
    }

    private static Guid[] Parse(IEnumerable<string> values) =>
        values.Where(value => Guid.TryParse(value, out _)).Select(Guid.Parse).ToArray();
}
