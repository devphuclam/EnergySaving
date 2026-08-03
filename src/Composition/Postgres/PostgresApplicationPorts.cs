using System.Text.Json;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Acquisition.Application;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Audit.Application;
using IUMP.Modules.Audit.Contracts;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.IAM.Contracts;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Modules.Telemetry.Contracts;
using IUMP.Infrastructure.Postgres;

namespace IUMP.Api.Infrastructure;

public sealed class PostgresConfigurationQueryPort(
    IOrganizationQueryRepository organization,
    IOrganizationActivationTargetQuery organizationScopes,
    CatalogRuntimeGateway catalog,
    ICatalogSourceScopeQuery sourceScopes,
    IAcquisitionConfigurationRepository configurations) : IConfigurationQueryPort
{
    public async Task<IReadOnlyList<object>> ListAsync(
        string resource, ServerPrincipal principal, CancellationToken ct = default)
    {
        var scope = principal.IsAdministrator
            ? OrganizationQueryScope.Global()
            : new OrganizationQueryScope(false,
                principal.SiteIds.Where(value => Guid.TryParse(value, out _)).Select(Guid.Parse).ToArray(),
                principal.AreaIds.Where(value => Guid.TryParse(value, out _)).Select(Guid.Parse).ToArray());
        var page = new ScopeFilter(1, 200);
        if (resource == "sites")
            return (await organization.GetSitesAsync(scope, page, ct)).Items.Cast<object>().ToArray();
        if (resource is "areas" or "assets" or "points")
        {
            var sites = (await organization.GetSitesAsync(scope, page, ct)).Items;
            var areas = new List<AreaSnapshot>();
            foreach (var site in sites)
                areas.AddRange((await organization.GetAreasForSiteAsync(
                    site.Id, scope, page, ct)).Items);
            if (resource == "areas") return areas.Cast<object>().ToArray();
            var assets = new List<AssetSnapshot>();
            foreach (var area in areas)
                assets.AddRange((await organization.GetAssetsForAreaAsync(
                    area.Id, scope, page, ct)).Items);
            if (resource == "assets") return assets.Cast<object>().ToArray();
            var points = new List<PointSnapshot>();
            foreach (var asset in assets)
                points.AddRange((await organization.GetPointsForAssetAsync(
                    asset.Id, scope, page, ct)).Items);
            return points.Cast<object>().ToArray();
        }
        if (resource.StartsWith("areas:", StringComparison.Ordinal) &&
            Guid.TryParse(resource[6..], out var siteId))
            return (await organization.GetAreasForSiteAsync(siteId, scope, page, ct)).Items.Cast<object>().ToArray();
        if (resource.StartsWith("assets:", StringComparison.Ordinal) &&
            Guid.TryParse(resource[7..], out var areaId))
            return (await organization.GetAssetsForAreaAsync(areaId, scope, page, ct)).Items.Cast<object>().ToArray();
        if (resource.StartsWith("points:", StringComparison.Ordinal) &&
            Guid.TryParse(resource[7..], out var assetId))
            return (await organization.GetPointsForAssetAsync(assetId, scope, page, ct)).Items.Cast<object>().ToArray();
        if (resource == "metrics")
            return await catalog.GetMetricsAsync(ct);
        if (resource == "units")
            return await catalog.GetUnitsAsync(ct);
        if (resource == "data-sources")
        {
            var values = new List<object>();
            foreach (var source in await catalog.GetDataSourceSnapshotsAsync(ct))
                if (await CanSeeSourceAsync(source.Id, principal, ct))
                    values.Add(source);
            return values;
        }
        if (resource == "source-point-mappings")
        {
            var values = new List<object>();
            foreach (var mapping in await catalog.GetMappingSnapshotsAsync(ct))
                if (await CanSeePointAsync(mapping.PointId, principal, ct))
                    values.Add(mapping);
            return values;
        }
        if (resource == "simulator-configurations")
        {
            var values = new List<object>();
            foreach (var configuration in await configurations.ListHeadsAsync(ct))
                if (await CanSeeSourceAsync(configuration.SourceId, principal, ct))
                    values.Add(configuration);
            return values;
        }
        if (resource.StartsWith("compatible-units:", StringComparison.Ordinal) &&
            Guid.TryParse(resource[17..], out var metricId))
            return await catalog.GetCompatibilitiesAsync(metricId, ct);
        if (resource.StartsWith("data-source:", StringComparison.Ordinal) &&
            Guid.TryParse(resource[12..], out var sourceId))
            return await CanSeeSourceAsync(sourceId, principal, ct)
                ? One(await catalog.GetDataSourceAsync(sourceId, ct))
                : Array.Empty<object>();
        if (resource.StartsWith("source-point-mapping:", StringComparison.Ordinal) &&
            Guid.TryParse(resource[21..], out var mappingId))
        {
            var mapping = (await catalog.GetMappingSnapshotsAsync(ct))
                .SingleOrDefault(value => value.Id == mappingId);
            return mapping is not null &&
                await CanSeePointAsync(mapping.PointId, principal, ct)
                    ? One(mapping)
                    : Array.Empty<object>();
        }
        if (resource.StartsWith("simulator-configuration:", StringComparison.Ordinal) &&
            Guid.TryParse(resource[24..], out var configurationId))
        {
            var configuration = await configurations.GetHeadAsync(configurationId, ct);
            return configuration is not null &&
                await CanSeeSourceAsync(configuration.SourceId, principal, ct)
                    ? One(configuration)
                    : Array.Empty<object>();
        }
        return Array.Empty<object>();
    }

    private static IReadOnlyList<object> One(object? value) =>
        value is null ? Array.Empty<object>() : new[] { value };

    private async Task<bool> CanSeePointAsync(
        Guid pointId,
        ServerPrincipal principal,
        CancellationToken ct)
    {
        if (principal.IsAdministrator) return true;
        var scope = await organizationScopes.GetPointScopeAsync(new PointId(pointId), ct);
        return scope is not null &&
            principal.HasScope(scope.SiteId.ToString("D"), scope.AreaId?.ToString("D"));
    }

    private async Task<bool> CanSeeSourceAsync(
        Guid sourceId,
        ServerPrincipal principal,
        CancellationToken ct)
    {
        if (principal.IsAdministrator) return true;
        var scope = await sourceScopes.GetSourceScopeAsync(sourceId, ct);
        return scope is not null && scope.Exists &&
            scope.MappedScopes.Any(value =>
                principal.HasScope(value.SiteId, value.AreaId));
    }
}

public sealed class PostgresConfigurationCommandPort(
    IOrganizationActivationTargetQuery organization,
    OrganizationRuntimeGateway organizationCommands,
    CatalogRuntimeGateway catalog,
    ICatalogSourceScopeQuery sourceScopes,
    IAcquisitionConfigurationRepository configurations,
    IActivationIdentityParticipant activationIdentity,
    IActivationOrganizationParticipant activationOrganization,
    IActivationCatalogParticipant activationCatalog,
    IOrganizationAuthorization organizationAuthorization,
    IEngineerScopeAssignmentService engineerScopes,
    ITransactionalOutboxWriter outbox,
    IHostTransactionBackend transactionBackend) : IConfigurationCommandPort
{
    public async Task<CommandExecutionResult> CreateSiteAsync(
        ConfigurationCommandRequest request, ServerPrincipal principal,
        IHostTransaction transaction, CancellationToken ct = default)
    {
        var result = await organizationCommands.CreateSiteAsync(
            request.Name, principal.UserId.ToString("D"), ct);
        if (!result.IsSuccess) return OrganizationFailure(result);
        await StageEventAsync("Organization.SiteCreated.v1", "Site", result.Id!.Value,
            result.Version!.Value, "Create", principal, transaction, ct,
            result.SiteId);
        return Created("Site", result.Id.Value, result.Version.Value);
    }

    public async Task<CommandExecutionResult> UpdateSiteAsync(
        ConfigurationCommandRequest request, ServerPrincipal principal,
        IHostTransaction transaction, CancellationToken ct = default)
    {
        if (request.TargetId is not { } id || request.ExpectedVersion is not { } expected)
            return Failure(400, "INVALID_REQUEST");
        var result = await organizationCommands.UpdateSiteAsync(
            id, request.Name, expected, principal.UserId.ToString("D"), ct);
        if (!result.IsSuccess) return OrganizationFailure(result);
        await StageEventAsync("Organization.SiteUpdated.v1", "Site", id,
            result.Version!.Value, "Update", principal, transaction, ct, id);
        return Updated("Site", id, result.Version.Value);
    }

    public async Task<CommandExecutionResult> ExecuteAsync(
        string operationCode, ConfigurationCommandRequest request,
        ServerPrincipal principal, IHostTransaction transaction,
        CancellationToken ct = default)
    {
        if (operationCode == "Organization.CreateArea.v1" && request.TargetId is { } siteId)
        {
            var result = await organizationCommands.CreateAreaAsync(
                siteId, request.Name, principal.UserId.ToString("D"), ct);
            if (!result.IsSuccess) return OrganizationFailure(result);
            if (!principal.IsAdministrator && result.AreaId is { } createdAreaId)
            {
                var scope = await engineerScopes.EnsureAreaScopeAsync(
                    siteId, createdAreaId, principal.UserId, ct);
                if (!scope.IsSuccess)
                    return Failure(
                        scope.Code == "FORBIDDEN" ? 403 : 422, scope.Code);
            }
            await StageEventAsync("Organization.AreaCreated.v1", "Area", result.Id!.Value,
                result.Version!.Value, "Create", principal, transaction, ct,
                result.SiteId, result.AreaId);
            return Created("Area", result.Id.Value, result.Version.Value);
        }
        if (operationCode == "Organization.CreateAsset.v1" && request.TargetId is { } areaId)
        {
            var result = await organizationCommands.CreateAssetAsync(
                areaId, request.Name, principal.UserId.ToString("D"), ct);
            if (!result.IsSuccess) return OrganizationFailure(result);
            await StageEventAsync("Organization.AssetCreated.v1", "Asset", result.Id!.Value,
                result.Version!.Value, "Create", principal, transaction, ct,
                result.SiteId, result.AreaId);
            return Created("Asset", result.Id.Value, result.Version.Value);
        }
        if (operationCode == "Organization.CreatePoint.v1")
        {
            var assetId = request.TargetId ?? GuidField(request, "assetId");
            if (assetId is null) return Failure(400, "ASSET_ID_REQUIRED");
            var metricId = GuidField(request, "metricId");
            var unitId = GuidField(request, "unitId");
            var ownerId = GuidField(request, "dataOwnerUserId");
            if (metricId is null || unitId is null || ownerId is null)
                return Failure(400, "POINT_CONFIGURATION_REQUIRED");
            var expectedInterval = IntField(request, "expectedIntervalSeconds", 1);
            var noDataAfter = IntField(request, "noDataAfterSeconds",
                Math.Max(3, expectedInterval * 3));
            var result = await organizationCommands.CreatePointAsync(
                assetId.Value, request.Name, metricId.Value, unitId.Value,
                ownerId.Value, expectedInterval, noDataAfter,
                principal.UserId.ToString("D"), ct);
            if (!result.IsSuccess) return OrganizationFailure(result);
            await StageEventAsync("Organization.PointCreated.v1", "MeasurementPoint",
                result.Id!.Value, result.Version!.Value, "Create",
                principal, transaction, ct, result.SiteId, result.AreaId);
            return Created("Point", result.Id.Value, result.Version.Value);
        }
        if (operationCode == "Catalog.CreateMetric.v1")
        {
            if (await DeniedAsync(principal, OrganizationResource.RootSite, null, ct) is { } denied)
                return denied;
            var metric = await catalog.CreateMetricAsync(
                Code(request.Name, "METRIC"), Text(request.Name, "Metric"), ct);
            return Created(metric.EntityType, metric.Id, metric.Version);
        }
        if (operationCode == "Catalog.CreateUnit.v1")
        {
            if (await DeniedAsync(principal, OrganizationResource.RootSite, null, ct) is { } denied)
                return denied;
            var unit = await catalog.CreateUnitAsync(
                Code(request.Name, "UNIT"), Text(request.Name, "u"), ct);
            return Created(unit.EntityType, unit.Id, unit.Version);
        }
        if (operationCode == "Acquisition.CreateSource.v1")
        {
            var sourceSiteId = GuidField(request, "siteId");
            if (sourceSiteId is null)
                return Failure(400, "SITE_ID_REQUIRED");
            if (await DeniedAsync(
                principal, OrganizationResource.SiteChild, sourceSiteId, ct) is { } denied)
                return denied;
            var source = await catalog.CreateSourceAsync(
                Code(request.Name, "SOURCE"), Text(request.Name, "Source"),
                ct, sourceSiteId);
            return Created(source.EntityType, source.Id, source.Version);
        }
        if (operationCode == "Catalog.SetMetricCompatibleUnits.v1")
        {
            var metricId = request.TargetId ?? GuidField(request, "metricId");
            var unitId = GuidField(request, "unitId");
            if (metricId is null || unitId is null)
                return Failure(400, "COMPATIBILITY_IDS_REQUIRED");
            if (await DeniedAsync(principal, OrganizationResource.RootSite, null, ct) is { } denied)
                return denied;
            var compatibility = await catalog.CreateCompatibilityAsync(
                metricId.Value, unitId.Value,
                BoolField(request, "isCanonical", true), ct);
            await StageEventAsync("Catalog.MetricUnitCompatibilityChanged.v1",
                "MetricUnitCompatibility", metricId.Value, compatibility.Version,
                "Create", principal, transaction, ct);
            return Created(compatibility.EntityType, compatibility.Id, compatibility.Version);
        }
        if (operationCode == "Acquisition.CreateMapping.v1")
        {
            var sourceId = GuidField(request, "sourceId");
            var mappedPointId = GuidField(request, "pointId");
            if (sourceId is null || mappedPointId is null)
                return Failure(400, "MAPPING_IDS_REQUIRED");
            var pointScope = await organization.GetPointScopeAsync(new PointId(mappedPointId.Value), ct);
            if (pointScope is null) return Failure(404, "NOT_FOUND");
            var source = await catalog.GetDataSourceSnapshotAsync(
                sourceId.Value, ct);
            if (source is null || source.SiteId != pointScope.SiteId ||
                (!principal.IsAdministrator &&
                 !principal.SiteIds.Contains(
                     pointScope.SiteId.ToString("D"))))
                return Failure(404, "NOT_FOUND");
            if (await DeniedAsync(
                principal, OrganizationResource.SiteChild, pointScope.SiteId,
                ct, pointScope.AreaId) is { } denied)
                return denied;
            var mapping = await catalog.CreateMappingAsync(
                sourceId.Value, mappedPointId.Value,
                TimestampField(request, "effectiveFromUtc", DateTime.UtcNow.AddSeconds(-1)),
                TimestampFieldOrNull(request, "effectiveToUtc"), ct);
            await StageEventAsync("Catalog.SourcePointMappingChanged.v1",
                "SourcePointMapping", mapping.Id, mapping.Version,
                "Create", principal, transaction, ct);
            return Created(mapping.EntityType, mapping.Id, mapping.Version);
        }
        if (operationCode == "Acquisition.CreateSimulatorConfiguration.v1")
        {
            var sourceId = GuidField(request, "sourceId");
            if (sourceId is null) return Failure(400, "SOURCE_ID_REQUIRED");
            if (await DeniedForSourceAsync(principal, sourceId.Value, allowUnmapped: true, ct) is { } denied)
                return denied;
            var createdConfigurationId = Guid.NewGuid();
            var scenario = Enum.TryParse<SimulatorScenario>(
                StringField(request, "scenarioType") ?? "Constant", true, out var parsed)
                ? parsed : SimulatorScenario.Constant;
            var minimum = DoubleField(request, "minimumValue", 42);
            var maximum = DoubleField(request, "maximumValue",
                scenario == SimulatorScenario.Constant ? minimum : minimum + 10);
            var version = new SimulatorConfigurationVersion(
                createdConfigurationId, 1, IntField(request, "intervalSeconds", 1),
                minimum, maximum, ULongField(request, "deterministicSeed", 42),
                scenario, SimulatorConfigurationConstants.AlgorithmId,
                SimulatorConfigurationConstants.AlgorithmVersion,
                principal.UserId.ToString("D"), principal.Username, DateTime.UtcNow,
                $"configuration-{createdConfigurationId:D}", null);
            await configurations.CreateAsync(
                new SimulatorConfigurationHead(createdConfigurationId, sourceId.Value, 1, 1),
                version, ct);
            await StageEventAsync("Acquisition.SimulatorConfigurationChanged.v1",
                "SimulatorConfiguration", createdConfigurationId, 1,
                "Create", principal, transaction, ct);
            return Created("SimulatorConfiguration", createdConfigurationId, 1);
        }
        if (operationCode is "Catalog.UpdateMetric.v1" or "Catalog.UpdateUnit.v1" &&
            request.TargetId is { } catalogTarget &&
            request.ExpectedVersion is { } catalogVersion)
        {
            if (await DeniedAsync(principal, OrganizationResource.RootSite, null, ct) is { } denied)
                return denied;
            try
            {
                var mutation = operationCode == "Catalog.UpdateMetric.v1"
                    ? await catalog.UpdateMetricAsync(
                        catalogTarget, catalogVersion, Text(request.Name, "Metric"), ct)
                    : await catalog.UpdateUnitAsync(
                        catalogTarget, catalogVersion, Text(request.Name, "u"), ct);
                if (mutation is null) return Failure(404, "NOT_FOUND");
                await StageEventAsync(
                    operationCode == "Catalog.UpdateMetric.v1"
                        ? "Catalog.MetricChanged.v1"
                        : "Catalog.UnitChanged.v1",
                    mutation.EntityType, mutation.Id, mutation.Version,
                    "Update", principal, transaction, ct);
                return Updated(mutation.EntityType, mutation.Id, mutation.Version);
            }
            catch (ArgumentException)
            {
                return Failure(422, "VALIDATION_FAILED");
            }
            catch (InvalidOperationException exception)
            {
                return Failure(409, exception.Message);
            }
        }
        if (operationCode == "Acquisition.UpdateSource.v1" &&
            request.TargetId is { } updateSourceId &&
            request.ExpectedVersion is { } updateSourceVersion)
        {
            if (await DeniedForSourceAsync(
                principal, updateSourceId, allowUnmapped: true, ct) is { } denied)
                return denied;
            if (IsDelete(request))
            {
                var deletion = await catalog.DeleteSourceAsync(
                    updateSourceId, updateSourceVersion, ct);
                if (!deletion.IsAllowed)
                    return Failure(
                        deletion.Code.Equals("NOT_FOUND", StringComparison.OrdinalIgnoreCase)
                            ? 404 : 409,
                        deletion.Code.ToUpperInvariant());
                await StageEventAsync("Catalog.DataSourceDeleted.v1", "DataSource",
                    updateSourceId, updateSourceVersion, "Delete",
                    principal, transaction, ct);
                return Deleted("DataSource", updateSourceId);
            }
            try
            {
                var source = await catalog.UpdateSourceAsync(
                    updateSourceId, updateSourceVersion,
                    Text(request.Name, "Source"), ct);
                if (source is null) return Failure(404, "NOT_FOUND");
                await StageEventAsync("Catalog.DataSourceChanged.v1", "DataSource",
                    updateSourceId, source.Version, "Update",
                    principal, transaction, ct);
                return Updated("DataSource", updateSourceId, source.Version);
            }
            catch (ArgumentException)
            {
                return Failure(422, "VALIDATION_FAILED");
            }
            catch (InvalidOperationException exception)
            {
                return Failure(409, exception.Message);
            }
        }
        if (operationCode == "Acquisition.UpdateMapping.v1" &&
            request.TargetId is { } updateMappingId &&
            request.ExpectedVersion is { } updateMappingVersion)
        {
            var mappingScope = await catalog.GetMappingSnapshotAsync(updateMappingId, ct);
            if (mappingScope is null) return Failure(404, "NOT_FOUND");
            var pointScope = await organization.GetPointScopeAsync(
                new PointId(mappingScope.PointId), ct);
            if (pointScope is null) return Failure(404, "NOT_FOUND");
            if (await DeniedAsync(
                principal, OrganizationResource.SiteChild, pointScope.SiteId,
                ct, pointScope.AreaId) is { } denied)
                return denied;
            if (IsDelete(request))
            {
                var deletion = await catalog.DeleteMappingAsync(
                    updateMappingId, updateMappingVersion, ct);
                if (!deletion.IsAllowed)
                    return Failure(
                        deletion.Code.Equals("NOT_FOUND", StringComparison.OrdinalIgnoreCase)
                            ? 404 : 409,
                        deletion.Code.ToUpperInvariant());
                await StageEventAsync("Catalog.SourcePointMappingDeleted.v1",
                    "SourcePointMapping", updateMappingId, updateMappingVersion,
                    "Delete", principal, transaction, ct,
                    pointScope.SiteId, pointScope.AreaId);
                return Deleted("SourcePointMapping", updateMappingId);
            }
            try
            {
                var mapping = await catalog.UpdateMappingAsync(
                    updateMappingId, updateMappingVersion,
                    TimestampField(request, "effectiveFromUtc", mappingScope.EffectiveFrom),
                    HasField(request, "effectiveToUtc")
                        ? TimestampFieldOrNull(request, "effectiveToUtc")
                        : mappingScope.EffectiveTo,
                    ct);
                if (mapping is null) return Failure(404, "NOT_FOUND");
                await StageEventAsync("Catalog.SourcePointMappingChanged.v1",
                    "SourcePointMapping", updateMappingId, mapping.Version,
                    "Update", principal, transaction, ct,
                    pointScope.SiteId, pointScope.AreaId);
                return Updated("SourcePointMapping", updateMappingId, mapping.Version);
            }
            catch (ArgumentException)
            {
                return Failure(422, "VALIDATION_FAILED");
            }
            catch (InvalidOperationException exception)
            {
                return Failure(409, exception.Message);
            }
        }
        if (operationCode == "Acquisition.ValidateSimulatorConfiguration.v1")
        {
            var sourceId = GuidField(request, "sourceId");
            if (sourceId is not null &&
                await DeniedForSourceAsync(
                    principal, sourceId.Value, allowUnmapped: true, ct) is { } denied)
                return denied;
            return TryBuildConfigurationVersion(
                Guid.NewGuid(), 1, request, principal, out _, out var validationError)
                ? CommandExecutionResult.Ok(200,
                    JsonSerializer.Serialize(new
                    {
                        valid = true,
                        algorithmId = SimulatorConfigurationConstants.AlgorithmId,
                        algorithmVersion = SimulatorConfigurationConstants.AlgorithmVersion
                    }), null)
                : Failure(422, validationError ?? "VALIDATION_FAILED");
        }
        if (operationCode == "Acquisition.UpdateSimulatorConfiguration.v1" &&
            request.TargetId is { } configurationId &&
            request.ExpectedVersion is { } expectedConfigurationVersion)
        {
            var head = await configurations.GetHeadAsync(configurationId, ct);
            if (head is null) return Failure(404, "NOT_FOUND");
            if (await DeniedForSourceAsync(
                principal, head.SourceId, allowUnmapped: true, ct) is { } denied)
                return denied;
            if (head.Version != expectedConfigurationVersion)
                return Failure(409, "VERSION_CONFLICT");
            if (IsDelete(request))
                return Failure(409, "CONFIGURATION_RETENTION_REQUIRED");
            var current = (await configurations.ListVersionsAsync(configurationId, ct))
                .OrderByDescending(value => value.ConfigurationVersion)
                .FirstOrDefault();
            if (current is null) return Failure(404, "NOT_FOUND");
            if (!TryBuildConfigurationVersion(
                configurationId, checked(current.ConfigurationVersion + 1),
                request, principal, out var next, out var validationError))
                return Failure(422, validationError ?? "VALIDATION_FAILED");
            if (Equivalent(current, next))
                return Failure(409, "NO_OP");
            try
            {
                // A behavior-changing edit creates a fresh Draft. Any review or
                // validation receipt for the superseded version is no longer valid.
                await configurations.InvalidateReceiptAsync(
                    configurationId, current.ConfigurationVersion, ct);
                await configurations.AppendDraftVersionAsync(
                    configurationId, head.Version, next, ct);
            }
            catch (InvalidOperationException)
            {
                return Failure(409, "VERSION_CONFLICT");
            }
            await StageEventAsync(
                "Acquisition.SimulatorConfigurationChanged.v1",
                "SimulatorConfiguration", configurationId, head.Version + 1,
                "Update", principal, transaction, ct);
            return Updated(
                "SimulatorConfiguration", configurationId, head.Version + 1);
        }
        if (operationCode == "Organization.UpdateArea.v1" &&
            request.TargetId is { } updateAreaId &&
            request.ExpectedVersion is { } updateAreaVersion)
        {
            var result = await organizationCommands.UpdateAreaAsync(
                updateAreaId, request.Name, updateAreaVersion,
                principal.UserId.ToString("D"), ct);
            if (!result.IsSuccess) return OrganizationFailure(result);
            await StageEventAsync("Organization.AreaUpdated.v1", "Area",
                result.Id!.Value, result.Version!.Value, "Update",
                principal, transaction, ct, result.SiteId, result.AreaId);
            return Updated("Area", result.Id.Value, result.Version.Value);
        }
        if (operationCode == "Organization.UpdateAsset.v1" &&
            request.TargetId is { } updateAssetId &&
            request.ExpectedVersion is { } updateAssetVersion)
        {
            var result = await organizationCommands.UpdateAssetAsync(
                updateAssetId, request.Name, updateAssetVersion,
                principal.UserId.ToString("D"), ct);
            if (!result.IsSuccess) return OrganizationFailure(result);
            await StageEventAsync("Organization.AssetUpdated.v1", "Asset",
                result.Id!.Value, result.Version!.Value, "Update",
                principal, transaction, ct, result.SiteId, result.AreaId);
            return Updated("Asset", result.Id.Value, result.Version.Value);
        }
        if (operationCode == "Organization.UpdatePoint.v1" &&
            request.TargetId is { } updatePointId &&
            request.ExpectedVersion is { } updatePointVersion)
        {
            var metricId = GuidField(request, "metricId");
            var unitId = GuidField(request, "unitId");
            var ownerId = GuidField(request, "dataOwnerUserId");
            if (metricId is null || unitId is null || ownerId is null)
                return Failure(400, "POINT_CONFIGURATION_REQUIRED");
            var expectedInterval = IntField(request, "expectedIntervalSeconds", 1);
            var noDataAfter = IntField(
                request, "noDataAfterSeconds", Math.Max(3, expectedInterval * 3));
            var result = await organizationCommands.UpdatePointAsync(
                updatePointId, StringField(request, "description"),
                metricId.Value, unitId.Value, ownerId.Value,
                expectedInterval, noDataAfter, updatePointVersion,
                principal.UserId.ToString("D"), ct);
            if (!result.IsSuccess) return OrganizationFailure(result);
            await StageEventAsync("Organization.PointConfigurationChanged.v1",
                "MeasurementPoint", result.Id!.Value, result.Version!.Value,
                "Update", principal, transaction, ct, result.SiteId, result.AreaId);
            return Updated("Point", result.Id.Value, result.Version.Value);
        }
        if (operationCode == "Organization.DeactivatePoint.v1" &&
            request.TargetId is { } deactivatePointId &&
            request.ExpectedVersion is { } deactivatePointVersion)
        {
            var result = await organizationCommands.InactivatePointAsync(
                deactivatePointId, deactivatePointVersion,
                principal.UserId.ToString("D"), ct);
            if (!result.IsSuccess) return OrganizationFailure(result);
            await StageEventAsync("Organization.PointStatusChanged.v1",
                "MeasurementPoint", result.Id!.Value, result.Version!.Value,
                "Inactivate", principal, transaction, ct,
                result.SiteId, result.AreaId);
            return Updated("Point", result.Id.Value, result.Version.Value);
        }
        if (operationCode == "Organization.ActivatePoint.v1" &&
            request.TargetId is { } pointId &&
            request.ExpectedVersion is { } pointVersion)
        {
            await using var coordinator = new HostTransactionCoordinator(transactionBackend);
            var result = await ActivateMeasurementPoint.ExecuteAsync(
                new PointId(pointId), pointVersion,
                new OrganizationCommandContext(
                    principal.UserId.ToString("D"),
                    $"point-activation-{pointId:D}", null),
                organization, activationIdentity, activationOrganization,
                activationCatalog, organizationAuthorization, outbox,
                coordinator, ct);
            return result.IsSuccess
                ? Updated("Point", pointId, result.NewVersion ?? pointVersion)
                : Failure(result.ErrorCode == "NOT_FOUND" ? 404 :
                    result.ErrorCode == "FORBIDDEN" ? 403 : 409,
                    result.ErrorCode ?? "POINT_ACTIVATION_FAILED");
        }
        if (operationCode is "Organization.ActivateSite.v1" or
            "Organization.DeactivateSite.v1" &&
            request.TargetId is { } siteTarget &&
            request.ExpectedVersion is { } siteVersion)
        {
            var result = await organizationCommands.TransitionSiteAsync(
                siteTarget, siteVersion,
                operationCode.Contains("Activate", StringComparison.Ordinal)
                    ? "activate" : "inactivate",
                principal.UserId.ToString("D"), ct);
            if (!result.IsSuccess) return OrganizationFailure(result);
            await StageEventAsync("Organization.SiteStatusChanged.v1", "Site",
                siteTarget, result.Version!.Value, "StatusChange", principal, transaction, ct,
                siteTarget);
            return Updated("Site", siteTarget, result.Version.Value);
        }
        if (operationCode is "Organization.ActivateArea.v1" or
            "Organization.DeactivateArea.v1" &&
            request.TargetId is { } areaTarget &&
            request.ExpectedVersion is { } areaVersion)
        {
            var result = await organizationCommands.TransitionAreaAsync(
                areaTarget, areaVersion,
                operationCode.Contains("Activate", StringComparison.Ordinal)
                    ? "activate" : "inactivate",
                principal.UserId.ToString("D"), ct);
            if (!result.IsSuccess) return OrganizationFailure(result);
            await StageEventAsync("Organization.AreaStatusChanged.v1", "Area",
                areaTarget, result.Version!.Value, "StatusChange", principal, transaction, ct,
                result.SiteId, result.AreaId);
            return Updated("Area", areaTarget, result.Version.Value);
        }
        if (operationCode is "Organization.ActivateAsset.v1" or
            "Organization.DeactivateAsset.v1" &&
            request.TargetId is { } assetTarget &&
            request.ExpectedVersion is { } assetVersion)
        {
            var result = await organizationCommands.TransitionAssetAsync(
                assetTarget, assetVersion,
                operationCode.Contains("Activate", StringComparison.Ordinal)
                    ? "activate" : "inactivate",
                principal.UserId.ToString("D"), ct);
            if (!result.IsSuccess) return OrganizationFailure(result);
            await StageEventAsync("Organization.AssetStatusChanged.v1", "Asset",
                assetTarget, result.Version!.Value, "StatusChange", principal, transaction, ct,
                result.SiteId, result.AreaId);
            return Updated("Asset", assetTarget, result.Version.Value);
        }
        if (operationCode is "Acquisition.ActivateSource.v1" or
            "Acquisition.SuspendSource.v1" or
            "Acquisition.DecommissionSource.v1" &&
            request.TargetId is { } sourceTarget &&
            request.ExpectedVersion is { } sourceVersion)
        {
            if (await DeniedForSourceAsync(principal, sourceTarget, allowUnmapped: true, ct) is { } denied)
                return denied;
            CatalogRuntimeMutation? source;
            try
            {
                source = await catalog.TransitionSourceAsync(
                    sourceTarget, sourceVersion,
                    operationCode switch
                    {
                        "Acquisition.ActivateSource.v1" => "activate",
                        "Acquisition.SuspendSource.v1" => "suspend",
                        _ => "decommission"
                    }, ct);
            }
            catch (InvalidOperationException exception)
            {
                return Failure(409, exception.Message);
            }
            if (source is null) return Failure(404, "NOT_FOUND");
            await StageEventAsync("Catalog.DataSourceStatusChanged.v1", "DataSource",
                sourceTarget, source.Version, "StatusChange", principal, transaction, ct);
            return Updated("DataSource", sourceTarget, source.Version);
        }
        if (operationCode is "Acquisition.ActivateMapping.v1" or
            "Acquisition.InactivateMapping.v1" or
            "Acquisition.SupersedeMapping.v1" &&
            request.TargetId is { } mappingTarget &&
            request.ExpectedVersion is { } mappingVersion)
        {
            var mappingScope = await catalog.GetMappingSnapshotAsync(mappingTarget, ct);
            if (mappingScope is null)
                return Failure(404, "NOT_FOUND");
            var pointScope = await organization.GetPointScopeAsync(
                new PointId(mappingScope.PointId), ct);
            if (pointScope is null) return Failure(404, "NOT_FOUND");
            if (await DeniedAsync(
                principal, OrganizationResource.SiteChild, pointScope.SiteId,
                ct, pointScope.AreaId) is { } denied)
                return denied;
            CatalogRuntimeMutation? mapping;
            await using var mutationSavepoint =
                await transactionBackend.BeginAsync(ct);
            if (mutationSavepoint.TransactionId != transaction.TransactionId)
            {
                await transactionBackend.RollbackAsync(
                    mutationSavepoint, CancellationToken.None);
                throw new InvalidOperationException(
                    "MAPPING_SAVEPOINT_OUTSIDE_OWNER_TRANSACTION");
            }
            try
            {
                mapping = await catalog.TransitionMappingAsync(
                    mappingTarget, mappingVersion,
                    operationCode switch
                    {
                        "Acquisition.ActivateMapping.v1" => "activate",
                        "Acquisition.InactivateMapping.v1" => "inactivate",
                        _ => "supersede"
                    }, ct);
                await transactionBackend.CommitAsync(mutationSavepoint, ct);
            }
            catch (InvalidOperationException exception)
            {
                await transactionBackend.RollbackAsync(
                    mutationSavepoint, CancellationToken.None);
                return Failure(409, exception.Message);
            }
            if (mapping is null) return Failure(404, "NOT_FOUND");
            await StageEventAsync("Catalog.SourcePointMappingChanged.v1",
                "SourcePointMapping", mappingTarget, mapping.Version,
                "StatusChange", principal, transaction, ct);
            return Updated("SourcePointMapping", mappingTarget, mapping.Version);
        }
        return Failure(409, "RUNTIME_OPERATION_REQUIRES_FULL_PAYLOAD");
    }

    private async Task<CommandExecutionResult?> DeniedAsync(
        ServerPrincipal principal,
        OrganizationResource resource,
        Guid? siteId,
        CancellationToken ct,
        Guid? areaId = null)
    {
        var decision = await organizationAuthorization.AuthorizeTargetAsync(
            principal.UserId.ToString("D"), resource,
            siteId?.ToString("D"), areaId?.ToString("D"), ct);
        if (decision.IsAllowed) return null;
        return decision.Code.Equals("NotFound", StringComparison.OrdinalIgnoreCase)
            ? Failure(404, "NOT_FOUND")
            : Failure(403, "FORBIDDEN");
    }

    private async Task<CommandExecutionResult?> DeniedForSourceAsync(
        ServerPrincipal principal,
        Guid sourceId,
        bool allowUnmapped,
        CancellationToken ct)
    {
        var snapshot = await sourceScopes.GetSourceScopeAsync(sourceId, ct);
        if (snapshot is null || !snapshot.Exists) return Failure(404, "NOT_FOUND");
        var mappedScopes = snapshot.MappedScopes
            .DistinctBy(
                value => $"{value.SiteId}:{value.AreaId}",
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (mappedScopes.Length == 0)
            return allowUnmapped
                ? await DeniedAsync(principal, OrganizationResource.SiteChild, null, ct)
                : Failure(404, "NOT_FOUND");
        foreach (var mappedScope in mappedScopes)
        {
            if (!Guid.TryParse(mappedScope.SiteId, out var siteId))
                return Failure(404, "NOT_FOUND");
            Guid? areaId = Guid.TryParse(mappedScope.AreaId, out var parsedArea)
                ? parsedArea
                : null;
            if (await DeniedAsync(
                principal, OrganizationResource.SiteChild, siteId,
                ct, areaId) is { } denied)
                return denied;
        }
        return null;
    }

    private async ValueTask StageEventAsync(
        string eventType,
        string aggregateType,
        Guid aggregateId,
        long aggregateVersion,
        string action,
        ServerPrincipal principal,
        IHostTransaction transaction,
        CancellationToken ct,
        Guid? siteId = null,
        Guid? areaId = null)
    {
        var occurred = DateTime.UtcNow;
        await outbox.EnqueueAsync(new OwnerEventEnvelope(
            Guid.NewGuid(), eventType, 1, "IUMP.Runtime", aggregateType,
            aggregateId.ToString("D"), aggregateVersion,
            principal.UserId.ToString("D"), principal.Username,
            new Dictionary<string, object?>(),
            new Dictionary<string, object?>
            {
                ["id"] = aggregateId.ToString("D"),
                ["version"] = aggregateVersion
            },
            action, $"{aggregateType} {action.ToLowerInvariant()} accepted.",
            occurred, $"runtime-{aggregateId:D}-{aggregateVersion}", null,
            siteId?.ToString("D"), areaId?.ToString("D")), transaction, ct);
    }

    private static CommandFingerprintField? Field(
        ConfigurationCommandRequest request,
        string name) =>
        request.Fields.LastOrDefault(field =>
            field.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static bool HasField(
        ConfigurationCommandRequest request,
        string name) => Field(request, name) is not null;

    private static bool IsDelete(ConfigurationCommandRequest request) =>
        string.Equals(StringField(request, "httpMethod"), "DELETE",
            StringComparison.OrdinalIgnoreCase);

    private static string? StringField(
        ConfigurationCommandRequest request,
        string name) =>
        Field(request, name)?.Value?.ToString();

    private static Guid? GuidField(
        ConfigurationCommandRequest request,
        string name) =>
        Field(request, name)?.Value switch
        {
            Guid value => value,
            string value when Guid.TryParse(value, out var parsed) => parsed,
            _ => null
        };

    private static int IntField(
        ConfigurationCommandRequest request,
        string name,
        int fallback) =>
        Field(request, name)?.Value is { } value
            ? Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture)
            : fallback;

    private static ulong ULongField(
        ConfigurationCommandRequest request,
        string name,
        ulong fallback) =>
        Field(request, name)?.Value is { } value
            ? Convert.ToUInt64(value, System.Globalization.CultureInfo.InvariantCulture)
            : fallback;

    private static double DoubleField(
        ConfigurationCommandRequest request,
        string name,
        double fallback) =>
        Field(request, name)?.Value is { } value
            ? Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture)
            : fallback;

    private static bool BoolField(
        ConfigurationCommandRequest request,
        string name,
        bool fallback) =>
        Field(request, name)?.Value is { } value
            ? Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture)
            : fallback;

    private static DateTime TimestampField(
        ConfigurationCommandRequest request,
        string name,
        DateTime fallback) =>
        Field(request, name)?.Value is DateTime value
            ? value.ToUniversalTime() : fallback.ToUniversalTime();

    private static DateTime? TimestampFieldOrNull(
        ConfigurationCommandRequest request,
        string name) =>
        Field(request, name)?.Value is DateTime value
            ? value.ToUniversalTime() : null;

    private static CommandExecutionResult Created(string type, Guid id, long version) =>
        CommandExecutionResult.Ok(201,
            JsonSerializer.Serialize(new { id, type, version }),
            id.ToString("D"), $"/api/v1/{type.ToLowerInvariant()}s/{id:D}", $"\"{version}\"");

    private static CommandExecutionResult Updated(string type, Guid id, long version) =>
        CommandExecutionResult.Ok(200,
            JsonSerializer.Serialize(new { id, type, version }),
            id.ToString("D"), null, $"\"{version}\"");

    private static CommandExecutionResult Deleted(string type, Guid id) =>
        CommandExecutionResult.Ok(200,
            JsonSerializer.Serialize(new { id, type, deleted = true }),
            id.ToString("D"));

    private static bool TryBuildConfigurationVersion(
        Guid configurationId,
        long configurationVersion,
        ConfigurationCommandRequest request,
        ServerPrincipal principal,
        out SimulatorConfigurationVersion version,
        out string? errorCode)
    {
        version = null!;
        errorCode = null;
        try
        {
            var scenario = Enum.TryParse<SimulatorScenario>(
                StringField(request, "scenarioType") ?? "Constant",
                true, out var parsed)
                ? parsed
                : throw new ArgumentException("Scenario is invalid.");
            var minimum = DoubleField(request, "minimumValue", 42);
            var maximum = DoubleField(
                request, "maximumValue",
                scenario == SimulatorScenario.Constant ? minimum : minimum + 10);
            version = new SimulatorConfigurationVersion(
                configurationId, configurationVersion,
                IntField(request, "intervalSeconds", 1),
                minimum, maximum,
                ULongField(request, "deterministicSeed", 42),
                scenario,
                SimulatorConfigurationConstants.AlgorithmId,
                SimulatorConfigurationConstants.AlgorithmVersion,
                principal.UserId.ToString("D"), principal.Username,
                DateTime.UtcNow,
                $"configuration-{configurationId:D}-{configurationVersion}", null);
            return true;
        }
        catch (ArgumentException)
        {
            errorCode = "VALIDATION_FAILED";
            return false;
        }
        catch (OverflowException)
        {
            errorCode = "VALIDATION_FAILED";
            return false;
        }
    }

    private static bool Equivalent(
        SimulatorConfigurationVersion left,
        SimulatorConfigurationVersion right) =>
        left.IntervalSeconds == right.IntervalSeconds &&
        left.MinimumValue == right.MinimumValue &&
        left.MaximumValue == right.MaximumValue &&
        left.DeterministicSeed == right.DeterministicSeed &&
        left.ScenarioType == right.ScenarioType &&
        left.AlgorithmId == right.AlgorithmId &&
        left.AlgorithmVersion == right.AlgorithmVersion;

    private static CommandExecutionResult Failure(int status, string code) =>
        new(status, JsonSerializer.Serialize(new { errorCode = code }), null);

    private static CommandExecutionResult OrganizationFailure(
        OrganizationRuntimeMutation result) =>
        Failure(result.StatusCode, result.ErrorCode ?? "ORGANIZATION_COMMAND_FAILED");

    private static string Text(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string Code(string value, string fallback)
    {
        var source = Text(value, fallback).ToUpperInvariant();
        var chars = source.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        var result = new string(chars).Trim('_');
        return result.Length == 0 ? fallback : result[..Math.Min(result.Length, 48)];
    }
}

public sealed class PostgresSimulatorQueryPort(
    IAcquisitionRunRepository runs) : ISimulatorQueryPort
{
    public async Task<object> GetRunAsync(
        Guid runId, ServerPrincipal principal, CancellationToken ct = default)
    {
        var run = await runs.GetAsync(runId, ct)
            ?? await runs.GetCurrentBySourceAsync(runId, ct);
        if (run is null) return new { status = "NotFound", runId };
        var points = await runs.ListPointStatesAsync(run.RunId, ct);
        foreach (var point in points)
        {
            if (!principal.HasScope(point.SiteId, point.AreaId))
                return new { status = "NotFound", runId };
        }
        return run;
    }
}

public sealed class PostgresSimulatorCommandPort(
    SimulatorRunCommandService commands) : ISimulatorCommandPort, ISimulatorSelectedStartCommandPort
{
    public async Task<CommandExecutionResult> ExecuteSelectedStartAsync(
        SimulatorSelection selection,
        ServerPrincipal principal,
        IHostTransaction transaction,
        CancellationToken ct = default)
    {
        // ExecuteTransactionalAsync establishes this host transaction in the scoped
        // PostgresTransactionContext. The Acquisition unit of work deliberately borrows that
        // ambient transaction, so owner Run, outbox, and idempotency completion commit together.
        _ = PostgresTransactionResolver.Require(transaction);
        var started = await commands.StartAsync(new StartSimulatorCommand(
            selection.SourceId, principal.UserId.ToString("D"),
            $"simulator-selected-start-{selection.SourceId:D}-{selection.ConfigurationId:D}", null,
            new SimulatorStartSelection(selection.SiteId, selection.AreaId, selection.AssetId,
                selection.SourceId, selection.ConfigurationId, selection.ConfigurationVersion)), ct);
        return started.IsSuccess
            ? CommandExecutionResult.Ok(202,
                JsonSerializer.Serialize(new
                {
                    runId = started.RunId,
                    status = "Running",
                    version = started.Version,
                    sourceId = selection.SourceId,
                    configurationId = selection.ConfigurationId,
                    configurationVersion = selection.ConfigurationVersion
                }),
                started.RunId?.ToString("D"),
                $"/api/v1/simulators/{started.RunId:D}",
                $"\"{started.Version}\"")
            : new(started.Code is "FORBIDDEN" or "NOT_VISIBLE" ? 403 :
                started.Code == "NOT_FOUND" ? 404 :
                started.Code is "PROVIDER_VERSION_DRIFT" or "DOMAIN_CONFLICT" or "SIMULATOR_RUN_CONFLICT" ? 409 : 422,
                JsonSerializer.Serialize(new { errorCode = started.Code }), null);
    }

    public async Task<CommandExecutionResult> ExecuteAsync(
        string operationCode, Guid targetId, long? expectedVersion, ServerPrincipal principal,
        IHostTransaction transaction, CancellationToken ct = default)
    {
        if (operationCode == "Simulator.Start.v1")
        {
            var started = await commands.StartAsync(new StartSimulatorCommand(
                targetId, principal.UserId.ToString("D"),
                $"simulator-start-{targetId:D}", null), ct);
            return started.IsSuccess
                ? CommandExecutionResult.Ok(202,
                    JsonSerializer.Serialize(new
                    {
                        runId = started.RunId,
                        status = "Running",
                        version = started.Version
                    }),
                    started.RunId?.ToString("D"),
                    $"/api/v1/simulators/{started.RunId:D}",
                    $"\"{started.Version}\"")
                : new(started.Code == "NOT_FOUND" ? 404 : 409,
                    JsonSerializer.Serialize(new { errorCode = started.Code }),
                    null);
        }
        var target = operationCode switch
        {
            "Simulator.Pause.v1" => SimulatorRunStatus.Paused,
            "Simulator.Resume.v1" => SimulatorRunStatus.Running,
            "Simulator.Stop.v1" => SimulatorRunStatus.Stopped,
            _ => (SimulatorRunStatus?)null
        };
        if (target is null)
            return new(400, "{\"errorCode\":\"UNKNOWN_OPERATION\"}", null);
        if (expectedVersion is null)
            return new(400, "{\"errorCode\":\"EXPECTED_VERSION_REQUIRED\"}", null);
        var changed = await commands.ChangeStatusAsync(new ChangeSimulatorRunStatusCommand(
            targetId, expectedVersion.Value, target.Value,
            principal.UserId.ToString("D"),
            $"simulator-control-{targetId:D}-{expectedVersion.Value}", null), ct);
        return changed.IsSuccess
            ? CommandExecutionResult.Ok(200,
                JsonSerializer.Serialize(new
                {
                    runId = changed.RunId,
                    status = target.Value,
                    version = changed.Version
                }),
                changed.RunId?.ToString("D"), null, $"\"{changed.Version}\"")
            : new(changed.Code == "NOT_FOUND" ? 404 :
                changed.Code is "FORBIDDEN" or "NOT_VISIBLE" ? 403 : 409,
                JsonSerializer.Serialize(new { errorCode = changed.Code }), null);
    }
}

public sealed class PostgresTelemetryQueryPort(
    IPointLatestProjectionRepository latest,
    ISourceHealthRepository health,
    IOrganizationQueryRepository organization,
    IAcquisitionRunRepository runs) : ITelemetryQueryPort
{
    public async Task<LatestQueryResult> GetLatestAsync(
        Guid pointId, ServerPrincipal principal, CancellationToken ct = default)
    {
        await RequirePointScopeAsync(pointId, principal, ct);
        var value = await latest.GetCurrentAsync(pointId, ct);
        if (value is null)
            return new LatestQueryResult(
                pointId, null, null, "No Data", true, "NO_DATA");
        var run = await runs.GetAsync(value.SimulatorRunId, ct);
        return new LatestQueryResult(
            pointId, value.NumericValue, value.UnitCode,
            value.QualityCode.ToString(), false, value.ReasonCode,
            value.SourceTimestampUtc, value.ReceivedAtUtc,
            run?.Status.ToString(), run?.GeneratedCount ?? 0,
            run?.AcceptedCount ?? 0, run?.RejectedCount ?? 0);
    }

    public async Task<object> GetSourceHealthAsync(
        Guid pointId, ServerPrincipal principal, CancellationToken ct = default)
    {
        await RequirePointScopeAsync(pointId, principal, ct);
        return await health.GetSourceHealthAsync(pointId, ct)
            ?? (object)new { pointId, status = "NoData" };
    }

    public async Task<IReadOnlyList<LatestQueryResult>> GetCurrentAsync(
        Guid siteId, ServerPrincipal principal, CancellationToken ct = default)
    {
        if (!principal.IsAdministrator &&
            !principal.HasScope(siteId.ToString("D"), null))
        {
            var hasAreaInSite = false;
            foreach (var areaScope in principal.AreaIds)
            {
                if (!Guid.TryParse(areaScope, out var areaId)) continue;
                var ancestry = await organization.GetAreaAncestryAsync(areaId, ct);
                if (ancestry?.SiteId != siteId) continue;
                hasAreaInSite = true;
                break;
            }
            if (!hasAreaInSite)
                throw new RuntimeScopeDeniedException();
        }
        var scope = principal.IsAdministrator
            ? OrganizationQueryScope.Global()
            : new OrganizationQueryScope(
                false,
                principal.SiteIds.Where(value => Guid.TryParse(value, out _))
                    .Select(Guid.Parse).ToArray(),
                principal.AreaIds.Where(value => Guid.TryParse(value, out _))
                    .Select(Guid.Parse).ToArray());
        var points = (await organization.GetPointsForSiteAsync(
            siteId, scope, new ScopeFilter(1, 500), ct)).Items;
        var results = new List<LatestQueryResult>();
        foreach (var point in points)
            results.Add(await GetLatestAsync(point.Id, principal, ct));
        return results;
    }

    private async Task RequirePointScopeAsync(
        Guid pointId,
        ServerPrincipal principal,
        CancellationToken ct)
    {
        var scope = await organization.GetPointSnapshotAsync(pointId, ct);
        if (scope is null ||
            !principal.HasScope(
                scope.SiteId.ToString("D"),
                scope.AreaId.ToString("D")))
            throw new RuntimeScopeDeniedException();
    }
}

public sealed class PostgresAuditQueryPort(
    AuditQueryService queries) : IAuditQueryPort
{
    public async Task<AuditQueryPage> QueryAsync(
        IReadOnlyDictionary<string, string?> filters, ServerPrincipal principal,
        string? cursor, int pageSize, CancellationToken ct = default)
    {
        filters.TryGetValue("objectType", out var objectType);
        filters.TryGetValue("action", out var action);
        filters.TryGetValue("actorId", out var actorId);
        filters.TryGetValue("correlationId", out var correlationId);
        filters.TryGetValue("fromUtc", out var fromRaw);
        filters.TryGetValue("toUtc", out var toRaw);
        filters.TryGetValue("entityId", out var entityId);
        filters.TryGetValue("siteId", out var siteId);
        filters.TryGetValue("areaId", out var areaId);
        var hasFrom = !string.IsNullOrWhiteSpace(fromRaw);
        var hasTo = !string.IsNullOrWhiteSpace(toRaw);
        var validFrom = DateTime.TryParse(fromRaw, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsedFrom);
        var validTo = DateTime.TryParse(toRaw, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsedTo);
        if ((hasFrom && !validFrom) || (hasTo && !validTo) ||
            (hasFrom && hasTo && parsedFrom > parsedTo))
            return new AuditQueryPage([], "VALIDATION");
        DateTime? fromUtc = hasFrom ? parsedFrom : null;
        DateTime? toUtc = hasTo ? parsedTo : null;
        var request = new AuditQueryRequest(
            objectType, action, actorId, correlationId, fromUtc, 1, pageSize)
        {
            KeysetCursor = cursor,
            ToUtc = toUtc,
            EntityId = entityId,
            SiteId = siteId,
            AreaId = areaId
        };
        var caller = new AuditCaller(
            principal.IsAdministrator, principal.HasCapability("AUDIT_READ"),
            principal.SiteIds, principal.AreaIds, true, principal.IsAdministrator);
        var result = await queries.QueryAsync(request, caller, ct);
        return new AuditQueryPage(result.Items.Cast<object>().ToArray(),
            result.ErrorCode, result.NextCursor, result.TotalCount);
    }
}
