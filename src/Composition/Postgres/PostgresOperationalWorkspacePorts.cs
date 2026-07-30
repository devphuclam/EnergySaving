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
        var sites = (await organization.GetSitesAsync(scope, new ScopeFilter(1, 100), ct)).Items;
        var summaries = sites.Select(site => new WorkspaceSiteSummary(
            site.Id, site.Code, site.Name, site.Status.ToString(), site.Version)).ToArray();
        var hasScope = principal.IsAdministrator ||
            principal.SiteIds.Count > 0 || principal.AreaIds.Count > 0;
        if (sites.Count == 0)
            return OperationalWorkspaceStatusBuilder.Build(
                principal.IsAdministrator, hasScope, false, 0, 0, true, summaries);

        var selectedSite = sites[0];
        var completed = 0;
        var eligibleEngineers = await engineers.ListEligibleEngineersAsync(ct);
        if (!principal.IsAdministrator ||
            eligibleEngineers.Any(value => value.AssignedSiteIds.Contains(selectedSite.Id)))
            completed = 1;

        var areas = (await organization.GetAreasForSiteAsync(
            selectedSite.Id, scope, new ScopeFilter(1, 100), ct)).Items;
        if (completed == 1 && areas.Count > 0) completed = 2;
        var area = areas.FirstOrDefault();
        var assets = area is null
            ? Array.Empty<AssetSnapshot>()
            : (await organization.GetAssetsForAreaAsync(
                area.Id, scope, new ScopeFilter(1, 100), ct)).Items.ToArray();
        if (completed == 2 && assets.Length > 0) completed = 3;
        var asset = assets.FirstOrDefault();
        var points = asset is null
            ? Array.Empty<PointSnapshot>()
            : (await organization.GetPointsForAssetAsync(
                asset.Id, scope, new ScopeFilter(1, 100), ct)).Items.ToArray();
        if (completed == 3 && points.Length > 0) completed = 4;
        var point = points.FirstOrDefault();

        var mappings = point is null
            ? Array.Empty<CatalogRuntimeMapping>()
            : (await catalog.GetMappingSnapshotsAsync(ct))
                .Where(value => value.PointId == point.Id).ToArray();
        var sources = await catalog.GetDataSourceSnapshotsAsync(ct);
        var source = mappings.Select(mapping =>
            sources.FirstOrDefault(value => value.Id == mapping.DataSourceId))
            .FirstOrDefault(value =>
                value is not null && value.SiteId == selectedSite.Id)
            ?? sources.FirstOrDefault(value =>
                value.SiteId == selectedSite.Id &&
                value.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase));
        if (completed == 4 && source is not null) completed = 5;
        var mapping = mappings.FirstOrDefault();
        if (completed == 5 && mapping is not null) completed = 6;
        var configuration = source is null ? null :
            await configurations.GetBySourceIdAsync(source.Id, ct);
        if (completed == 6 && configuration is not null) completed = 7;

        var operational = selectedSite.Status == SiteStatus.Active &&
            area?.Status == AreaStatus.Active &&
            asset?.Status == AssetStatus.Active &&
            point?.Status == PointStatus.Active &&
            source?.Status == "Active" &&
            mapping?.Status == "Active" &&
            configuration is not null;
        if (completed == 7 && operational) completed = 8;

        var status = OperationalWorkspaceStatusBuilder.Build(
            principal.IsAdministrator, hasScope, true, completed,
            operational ? 1 : 0, true, summaries);
        var validationFailures = new List<WorkspaceValidationFailure>();
        if (source?.Status == "Decommissioned")
            validationFailures.Add(new WorkspaceValidationFailure(
                WorkspaceStep.DataSource, "sourceId",
                "SOURCE_TERMINAL", "setup.source.terminal"));
        if (mapping?.Status is "Inactive" or "Superseded")
            validationFailures.Add(new WorkspaceValidationFailure(
                WorkspaceStep.Mapping, "mappingId",
                "MAPPING_INELIGIBLE", "setup.mapping.ineligible"));
        if (point?.Status == PointStatus.Decommissioned)
            validationFailures.Add(new WorkspaceValidationFailure(
                WorkspaceStep.MeasurementPoint, "pointId",
                "POINT_TERMINAL", "setup.point.terminal"));
        if (area?.Status == AreaStatus.Inactive)
            validationFailures.Add(new WorkspaceValidationFailure(
                WorkspaceStep.Area, "areaId",
                "AREA_INELIGIBLE", "setup.area.ineligible"));
        if (asset?.Status is AssetStatus.Inactive or AssetStatus.Decommissioned)
            validationFailures.Add(new WorkspaceValidationFailure(
                WorkspaceStep.Asset, "assetId",
                "ASSET_INELIGIBLE", "setup.asset.ineligible"));
        if (point?.Status == PointStatus.Inactive)
            validationFailures.Add(new WorkspaceValidationFailure(
                WorkspaceStep.MeasurementPoint, "pointId",
                "POINT_INELIGIBLE", "setup.point.ineligible"));
        return status with
        {
            Chain = new WorkspaceChainSelection(
                selectedSite.Id, selectedSite.Version,
                area?.Id, area?.Version,
                asset?.Id, asset?.Version,
                point?.Id, point?.Version,
                source?.Id, source?.Version,
                mapping?.Id, mapping?.Version,
                configuration?.ConfigurationId, configuration?.Version),
            ActivationSteps = new[]
            {
                (Name: "site", Pending: selectedSite.Status != SiteStatus.Active),
                (Name: "area", Pending: area?.Status != AreaStatus.Active),
                (Name: "asset", Pending: asset?.Status != AssetStatus.Active),
                (Name: "data-source", Pending: source?.Status != "Active"),
                (Name: "mapping", Pending: mapping?.Status != "Active"),
                (Name: "measurement-point", Pending: point?.Status != PointStatus.Active)
            }.Where(value => value.Pending).Select(value => value.Name).ToArray(),
            CurrentUserId = principal.UserId,
            ValidationFailures = validationFailures
        };
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
                siteId.ToString("D"), areaId.ToString("D")) ||
            (!principal.IsAdministrator &&
             !principal.SiteIds.Contains(siteId.ToString("D"))))
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
