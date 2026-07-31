using System.Text.Json;
using IUMP.Api.Infrastructure;
using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Acquisition.Application;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Integration.Contracts;
using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;

namespace IUMP.Composition.Postgres;

/// <summary>
/// PostgreSQL composition adapter for configuration management. Queries resolve the
/// authorized scope from the server principal before search/status filtering and paging.
/// Duplication always produces a new Draft through the owner modules' duplication
/// services; owner events are staged into the provided host transaction outbox.
/// </summary>
public sealed class PostgresConfigurationManagementPorts(
    IOrganizationQueryRepository organization,
    IOrganizationCommandRepository organizationCommands,
    IOrganizationAuthorization organizationAuthorization,
    ICatalogCommandRepository catalogCommands,
    IConfigurationCallerSnapshotProvider configurationCallers,
    ICatalogSourceScopeQuery sourceScopes,
    IAcquisitionConfigurationRepository configurations,
    CatalogRuntimeGateway catalog,
    ITransactionalOutboxWriter outbox) :
    IConfigurationManagementQueryPort,
    IConfigurationManagementCommandPort
{
    public async Task<ConfigurationManagementPage<object>> QueryAsync(
        string resource,
        ManagementQueryFilter filter,
        ServerPrincipal principal,
        CancellationToken ct = default)
    {
        if (!ConfigurationManagementResources.IsKnown(resource))
            return new ConfigurationManagementPage<object>(
                Array.Empty<object>(), 0, filter.Page, filter.PageSize);

        var scope = principal.IsAdministrator
            ? OrganizationQueryScope.Global()
            : new OrganizationQueryScope(false,
                Parse(principal.SiteIds), Parse(principal.AreaIds));
        var authorizedSites = await All(
            value => organization.GetSitesAsync(scope, value, ct));

        object[] items;
        var total = 0;
        switch (resource)
        {
            case ConfigurationManagementResources.Sites:
                var sites = ApplySiteFilter(authorizedSites, filter);
                (items, total) = Page(sites.Select(ToManagementItem), filter);
                break;
            case ConfigurationManagementResources.Areas:
            {
                var siteIds = NarrowSites(authorizedSites, filter.SiteId)
                    .Select(value => value.Id).ToArray();
                var areas = new List<AreaSnapshot>();
                foreach (var siteId in siteIds)
                    areas.AddRange(await All(value =>
                        organization.GetAreasForSiteAsync(siteId, scope, value, ct)));
                var filtered = areas.Where(value =>
                    Matches(value.Code, value.Name, value.Description, filter.Search) &&
                    Matches(value.Status.ToString(), filter.Status) &&
                    (filter.AreaId is null ||
                     value.Id == ParseGuid(filter.AreaId)));
                (items, total) = Page(filtered.Select(ToManagementItem), filter);
                break;
            }
            case ConfigurationManagementResources.Assets:
            {
                var siteIds = NarrowSites(authorizedSites, filter.SiteId)
                    .Select(value => value.Id).ToArray();
                var areas = new List<AreaSnapshot>();
                foreach (var siteId in siteIds)
                    areas.AddRange(await All(value =>
                        organization.GetAreasForSiteAsync(siteId, scope, value, ct)));
                var areaIds = areas.Select(value => value.Id).ToArray();
                var assets = new List<AssetSnapshot>();
                foreach (var areaId in areaIds)
                    assets.AddRange(await All(value =>
                        organization.GetAssetsForAreaAsync(areaId, scope, value, ct)));
                var filtered = assets.Where(value =>
                    Matches(value.Code, value.Name, value.Description, filter.Search) &&
                    Matches(value.Status.ToString(), filter.Status) &&
                    (filter.AreaId is null ||
                     value.AreaId == ParseGuid(filter.AreaId)));
                (items, total) = Page(filtered.Select(ToManagementItem), filter);
                break;
            }
            case ConfigurationManagementResources.Points:
            {
                var siteIds = NarrowSites(authorizedSites, filter.SiteId)
                    .Select(value => value.Id).ToArray();
                var points = new List<PointSnapshot>();
                foreach (var siteId in siteIds)
                    points.AddRange(await All(value =>
                        organization.GetPointsForSiteAsync(siteId, scope, value, ct)));
                var filtered = points.Where(value =>
                    Matches(value.Code, value.Description, null, filter.Search) &&
                    Matches(value.Status.ToString(), filter.Status) &&
                    (filter.AreaId is null ||
                     value.AreaId == ParseGuid(filter.AreaId)));
                (items, total) = Page(filtered.Select(ToManagementItem), filter);
                break;
            }
            case ConfigurationManagementResources.DataSources:
            {
                var pointIds = await AuthorizedPointIdsAsync(
                    authorizedSites, scope, ct);
                var visible = await VisibleSourceIdsAsync(
                    authorizedSites, pointIds, ct);
                var sources = (await catalog.GetDataSourceSnapshotsAsync(ct))
                    .Where(value => visible.Contains(value.Id))
                    .Where(value =>
                        Matches(value.Code, value.Name, null, filter.Search) &&
                        Matches(value.Status, filter.Status) &&
                        (filter.SiteId is null ||
                         (value.SiteId?.ToString("D") ?? string.Empty)
                             .Equals(filter.SiteId, StringComparison.OrdinalIgnoreCase)));
                (items, total) = Page(sources.Select(ToManagementItem), filter);
                break;
            }
            case ConfigurationManagementResources.SourcePointMappings:
            {
                var pointIds = await AuthorizedPointIdsAsync(
                    authorizedSites, scope, ct);
                var visible = await VisibleSourceIdsAsync(
                    authorizedSites, pointIds, ct);
                var candidateMappings = (await catalog.GetMappingSnapshotsAsync(ct))
                    .Where(value => visible.Contains(value.DataSourceId) &&
                                   pointIds.Contains(value.PointId));
                var mappingSiteIds = new Dictionary<Guid, Guid>();
                foreach (var value in candidateMappings)
                {
                    if (mappingSiteIds.ContainsKey(value.PointId)) continue;
                    var point = await organization.GetPointSnapshotAsync(
                        value.PointId, ct);
                    if (point is not null)
                        mappingSiteIds[value.PointId] = point.SiteId;
                }
                var mappings = candidateMappings.Where(value =>
                    Matches(value.PointId.ToString("D"), null, null,
                        filter.Search) &&
                    Matches(value.Status, filter.Status) &&
                    (filter.SiteId is null ||
                     (mappingSiteIds.TryGetValue(value.PointId, out var siteId) &&
                      siteId.ToString("D").Equals(filter.SiteId,
                          StringComparison.OrdinalIgnoreCase))));
                (items, total) = Page(mappings.Select(ToManagementItem), filter);
                break;
            }
            case ConfigurationManagementResources.SimulatorConfigurations:
            {
                var pointIds = await AuthorizedPointIdsAsync(
                    authorizedSites, scope, ct);
                var visible = await VisibleSourceIdsAsync(
                    authorizedSites, pointIds, ct);
                var heads = (await configurations.ListHeadsAsync(ct))
                    .Where(value => visible.Contains(value.SourceId));
                if (filter.SiteId is not null)
                {
                    var narrowedSiteIds = NarrowSites(authorizedSites, filter.SiteId)
                        .Select(value => value.Id).ToHashSet();
                    var scopedSources = (await catalog.GetDataSourceSnapshotsAsync(ct))
                        .Where(value => value.SiteId.HasValue &&
                                       narrowedSiteIds.Contains(value.SiteId.Value))
                        .Select(value => value.Id).ToHashSet();
                    heads = heads.Where(value =>
                        scopedSources.Contains(value.SourceId));
                }
                var configItems = new List<object>();
                var configTotal = 0;
                var pageStart = (Math.Max(1, filter.Page) - 1) * Math.Clamp(filter.PageSize, 1, 200);
                var pageSize = Math.Clamp(filter.PageSize, 1, 200);
                foreach (var head in heads)
                {
                    if (configTotal >= pageStart && configItems.Count < pageSize)
                        configItems.Add(await ToConfigurationItemAsync(head, ct));
                    configTotal++;
                }
                return new ConfigurationManagementPage<object>(
                    configItems, configTotal, filter.Page, filter.PageSize);
            }
            default:
                items = Array.Empty<object>();
                break;
        }
        return new ConfigurationManagementPage<object>(
            items, total, filter.Page, filter.PageSize);
    }

    public async Task<object?> GetDetailAsync(
        string resource,
        Guid id,
        ServerPrincipal principal,
        CancellationToken ct = default)
    {
        if (!ConfigurationManagementResources.IsKnown(resource))
            return null;
        var scope = principal.IsAdministrator
            ? OrganizationQueryScope.Global()
            : new OrganizationQueryScope(false,
                Parse(principal.SiteIds), Parse(principal.AreaIds));
        var authorizedSites = await All(
            value => organization.GetSitesAsync(scope, value, ct));
        var authorizedIds = authorizedSites.Select(value => value.Id).ToHashSet();

        switch (resource)
        {
            case ConfigurationManagementResources.Sites:
            {
                var value = await organization.GetSiteSnapshotAsync(id, ct);
                return value is not null &&
                       (principal.IsAdministrator || authorizedIds.Contains(value.Id))
                    ? ToManagementItem(value)
                    : null;
            }
            case ConfigurationManagementResources.Areas:
            {
                var value = await organization.GetAreaSnapshotAsync(id, ct);
                return value is not null &&
                       (principal.IsAdministrator ||
                        authorizedIds.Contains(value.SiteId))
                    ? ToManagementItem(value)
                    : null;
            }
            case ConfigurationManagementResources.Assets:
            {
                var value = await organization.GetAssetSnapshotAsync(id, ct);
                return value is not null &&
                       (principal.IsAdministrator ||
                        authorizedIds.Contains(value.SiteId))
                    ? ToManagementItem(value)
                    : null;
            }
            case ConfigurationManagementResources.Points:
            {
                var value = await organization.GetPointSnapshotAsync(id, ct);
                return value is not null &&
                       (principal.IsAdministrator ||
                        authorizedIds.Contains(value.SiteId))
                    ? ToManagementItem(value)
                    : null;
            }
            case ConfigurationManagementResources.DataSources:
            {
                var value = await catalog.GetDataSourceSnapshotAsync(id, ct);
                var pointIds = await AuthorizedPointIdsAsync(
                    authorizedSites, scope, ct);
                var visible = await VisibleSourceIdsAsync(
                    authorizedSites, pointIds, ct);
                return value is not null && visible.Contains(value.Id)
                    ? ToManagementItem(value)
                    : null;
            }
            case ConfigurationManagementResources.SourcePointMappings:
            {
                var value = await catalog.GetMappingSnapshotAsync(id, ct);
                if (value is null) return null;
                var pointIds = await AuthorizedPointIdsAsync(
                    authorizedSites, scope, ct);
                var visible = await VisibleSourceIdsAsync(
                    authorizedSites, pointIds, ct);
                return visible.Contains(value.DataSourceId) &&
                       pointIds.Contains(value.PointId)
                    ? ToManagementItem(value)
                    : null;
            }
            case ConfigurationManagementResources.SimulatorConfigurations:
            {
                var value = await configurations.GetHeadAsync(id, ct);
                if (value is null) return null;
                var pointIds = await AuthorizedPointIdsAsync(
                    authorizedSites, scope, ct);
                var visible = await VisibleSourceIdsAsync(
                    authorizedSites, pointIds, ct);
                return visible.Contains(value.SourceId)
                    ? await ToConfigurationItemAsync(value, ct)
                    : null;
            }
            default:
                return null;
        }
    }

    public async Task<CommandExecutionResult> DuplicateAsync(
        string resource,
        Guid targetId,
        ServerPrincipal principal,
        IHostTransaction transaction,
        CancellationToken ct = default)
    {
        if (!ConfigurationManagementResources.IsKnown(resource))
            return Failure(404, "NOT_FOUND");
        var actor = principal.UserId.ToString("D");
        switch (resource)
        {
            case ConfigurationManagementResources.Sites:
            {
                var service = new OrganizationDuplicationService(
                    organizationCommands, organizationAuthorization);
                var outcome = await service.DuplicateSiteAsync(
                    new SiteId(targetId), actor, ct);
                if (!outcome.IsSuccess) return DuplicationFailure(outcome.Code);
                await StageAsync(transaction, ToEnvelopes(service.Events), ct);
                return DuplicationCreated(outcome.NewId, outcome.ProposedCode,
                    outcome.ProposedName, outcome.Status, outcome.Version,
                    outcome.ReviewRelationships);
            }
            case ConfigurationManagementResources.Areas:
            {
                var service = new OrganizationDuplicationService(
                    organizationCommands, organizationAuthorization);
                var outcome = await service.DuplicateAreaAsync(
                    new AreaId(targetId), actor, ct);
                if (!outcome.IsSuccess) return DuplicationFailure(outcome.Code);
                await StageAsync(transaction, ToEnvelopes(service.Events), ct);
                return DuplicationCreated(outcome.NewId, outcome.ProposedCode,
                    outcome.ProposedName, outcome.Status, outcome.Version,
                    outcome.ReviewRelationships);
            }
            case ConfigurationManagementResources.Assets:
            {
                var service = new OrganizationDuplicationService(
                    organizationCommands, organizationAuthorization);
                var outcome = await service.DuplicateAssetAsync(
                    new AssetId(targetId), actor, ct);
                if (!outcome.IsSuccess) return DuplicationFailure(outcome.Code);
                await StageAsync(transaction, ToEnvelopes(service.Events), ct);
                return DuplicationCreated(outcome.NewId, outcome.ProposedCode,
                    outcome.ProposedName, outcome.Status, outcome.Version,
                    outcome.ReviewRelationships);
            }
            case ConfigurationManagementResources.Points:
            {
                var service = new OrganizationDuplicationService(
                    organizationCommands, organizationAuthorization);
                var outcome = await service.DuplicatePointAsync(
                    new PointId(targetId), actor, ct);
                if (!outcome.IsSuccess) return DuplicationFailure(outcome.Code);
                await StageAsync(transaction, ToEnvelopes(service.Events), ct);
                return DuplicationCreated(outcome.NewId, outcome.ProposedCode,
                    outcome.ProposedName, outcome.Status, outcome.Version,
                    outcome.ReviewRelationships);
            }
            case ConfigurationManagementResources.DataSources:
            {
                var gateway = CatalogDuplicationGateway();
                var outcome = await gateway.DuplicateSourceAsync(targetId, actor, ct);
                if (!outcome.IsSuccess) return DuplicationFailure(outcome.Code);
                await StageAsync(transaction, ToEnvelopes(gateway.Events), ct);
                return DuplicationCreated(outcome.NewId, outcome.ProposedCode,
                    outcome.ProposedName, outcome.Status, outcome.Version,
                    outcome.ReviewRelationships);
            }
            case ConfigurationManagementResources.SourcePointMappings:
            {
                var gateway = CatalogDuplicationGateway();
                var outcome = await gateway.DuplicateMappingAsync(targetId, actor, ct);
                if (!outcome.IsSuccess) return DuplicationFailure(outcome.Code);
                await StageAsync(transaction, ToEnvelopes(gateway.Events), ct);
                return DuplicationCreated(outcome.NewId, outcome.ProposedCode,
                    outcome.ProposedName, outcome.Status, outcome.Version,
                    outcome.ReviewRelationships);
            }
            case ConfigurationManagementResources.SimulatorConfigurations:
            {
                var head = await configurations.GetHeadAsync(targetId, ct);
                if (head is null) return Failure(404, "NOT_FOUND");
                var service = new SimulatorConfigurationService(
                    configurations, configurationCallers, sourceScopes);
                var outcome = await service.DuplicateAsync(
                    new SimulatorConfigurationDuplicateCommand(
                        targetId, head.SourceId, actor,
                        $"duplicate-{targetId:D}", null), ct);
                if (!outcome.IsSuccess)
                    return Failure(
                        outcome.Code == "NOT_FOUND" ? 404 :
                        outcome.Code == "FORBIDDEN" ? 403 :
                        outcome.Code == "VERSION_CONFLICT" ? 409 : 422,
                        outcome.Error ?? outcome.Code);
                await StageAsync(transaction, ToEnvelopes(service.Events), ct);
                return CommandExecutionResult.Ok(201,
                    JsonSerializer.Serialize(new
                    {
                        id = outcome.NewConfigurationId,
                        sourceId = head.SourceId,
                        status = "Draft",
                        version = 1
                    }),
                    outcome.NewConfigurationId?.ToString("D"));
            }
            default:
                return Failure(404, "NOT_FOUND");
        }
    }

    public async Task<CommandExecutionResult> ActivateSimulatorConfigurationVersionAsync(
        Guid configurationId,
        long expectedHeadVersion,
        long draftConfigurationVersion,
        ServerPrincipal principal,
        IHostTransaction transaction,
        CancellationToken ct = default)
    {
        var service = new SimulatorConfigurationService(
            configurations, configurationCallers, sourceScopes);
        var result = await service.ActivateVersionAsync(
            new SimulatorConfigurationActivateVersionCommand(
                configurationId, expectedHeadVersion, draftConfigurationVersion,
                principal.UserId.ToString("D"),
                $"activate-{configurationId:D}-{draftConfigurationVersion}", null),
            ct);
        if (!result.IsSuccess)
            return Failure(
                result.Code == "NOT_FOUND" ? 404 :
                result.Code == "FORBIDDEN" ? 403 :
                result.Code == "VERSION_CONFLICT" ? 409 : 422,
                result.Error ?? result.Code);
        await StageAsync(transaction, ToEnvelopes(service.Events), ct);
        return CommandExecutionResult.Ok(200,
            JsonSerializer.Serialize(new
            {
                configurationId,
                currentConfigurationVersion = draftConfigurationVersion,
                version = expectedHeadVersion + 1
            }),
            configurationId.ToString("D"),
            $"\"{expectedHeadVersion + 1}\"");
    }

    private CatalogConfigurationDuplicationGateway CatalogDuplicationGateway() =>
        new(catalogCommands, new CatalogCallerBridge(configurationCallers));

    private async Task<HashSet<Guid>> AuthorizedPointIdsAsync(
        IReadOnlyList<SiteSnapshot> authorizedSites,
        OrganizationQueryScope scope,
        CancellationToken ct)
    {
        var points = new List<PointSnapshot>();
        foreach (var site in authorizedSites)
            points.AddRange(await All(value =>
                organization.GetPointsForSiteAsync(site.Id, scope, value, ct)));
        return points.Select(value => value.Id).ToHashSet();
    }

    private async Task<HashSet<Guid>> VisibleSourceIdsAsync(
        IReadOnlyList<SiteSnapshot> authorizedSites,
        IReadOnlySet<Guid> pointIds,
        CancellationToken ct)
    {
        var authorizedSiteIds = authorizedSites.Select(value => value.Id).ToHashSet();
        var sources = await catalog.GetDataSourceSnapshotsAsync(ct);
        var allMappings = await catalog.GetMappingSnapshotsAsync(ct);
        return sources
            .Where(value =>
                (value.SiteId.HasValue &&
                 authorizedSiteIds.Contains(value.SiteId.Value)) ||
                allMappings.Any(mapping =>
                    mapping.DataSourceId == value.Id &&
                    pointIds.Contains(mapping.PointId)))
            .Select(value => value.Id)
            .ToHashSet();
    }

    private static IReadOnlyList<SiteSnapshot> ApplySiteFilter(
        IReadOnlyList<SiteSnapshot> authorizedSites,
        ManagementQueryFilter filter) =>
        NarrowSites(authorizedSites, filter.SiteId)
            .Where(value =>
                Matches(value.Code, value.Name, value.Description, filter.Search) &&
                Matches(value.Status.ToString(), filter.Status))
            .ToArray();

    private static IReadOnlyList<SiteSnapshot> NarrowSites(
        IReadOnlyList<SiteSnapshot> authorizedSites,
        string? siteId)
    {
        if (siteId is null) return authorizedSites;
        var parsed = ParseGuid(siteId);
        if (parsed is null) return Array.Empty<SiteSnapshot>();
        return authorizedSites
            .Where(value => value.Id == parsed.Value)
            .ToArray();
    }

    private static (object[] Items, int Total) Page<T>(
        IEnumerable<T> values,
        ManagementQueryFilter filter)
    {
        var items = values.ToArray();
        var total = items.Length;
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);
        var slice = items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Cast<object>()
            .ToArray();
        return (slice, total);
    }

    private static bool Matches(string? left, string? middle, string? right, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        return (left?.Contains(search, StringComparison.OrdinalIgnoreCase) == true) ||
               (middle?.Contains(search, StringComparison.OrdinalIgnoreCase) == true) ||
               (right?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool Matches(string? value, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected)) return true;
        return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static Guid? ParseGuid(string value) =>
        Guid.TryParse(value, out var parsed) ? parsed : null;

    private static Guid[] Parse(IEnumerable<string> values) =>
        values.Where(value => Guid.TryParse(value, out _))
            .Select(Guid.Parse)
            .ToArray();

    private static async Task<IReadOnlyList<T>> All<T>(
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

    private async ValueTask StageAsync(
        IHostTransaction transaction,
        IReadOnlyList<OwnerEventEnvelope> envelopes,
        CancellationToken ct)
    {
        foreach (var envelope in envelopes)
            await outbox.EnqueueAsync(envelope, transaction, ct);
    }

    private static IReadOnlyList<OwnerEventEnvelope> ToEnvelopes(
        IReadOnlyList<OrganizationEvent> events) =>
        events.Select(value => new OwnerEventEnvelope(
            value.EventId, value.EventType, int.Parse(value.SchemaVersion),
            value.Producer, value.AggregateType, value.AggregateId,
            value.AggregateVersion, value.ActorId, value.ActorUsername,
            value.Before, value.After, value.Action, value.Summary,
            value.OccurredAt, value.CorrelationId ?? Guid.NewGuid().ToString("D"),
            value.CausationId, value.SiteId, value.AreaId)).ToArray();

    private static IReadOnlyList<OwnerEventEnvelope> ToEnvelopes(
        IReadOnlyList<CatalogConfigurationEvent> events) =>
        events.Select(value => new OwnerEventEnvelope(
            value.EventId, value.EventType, int.Parse(value.SchemaVersion),
            value.Producer, value.AggregateType, value.AggregateId,
            value.AggregateVersion, value.ActorId, value.ActorUsername,
            value.Before, value.After, value.Action, value.Summary,
            value.OccurredAt, value.CorrelationId ?? Guid.NewGuid().ToString("D"),
            value.CausationId, value.SiteId, value.AreaId)).ToArray();

    private static IReadOnlyList<OwnerEventEnvelope> ToEnvelopes(
        IReadOnlyList<SimulatorConfigurationEvent> events) =>
        events.Select(value => new OwnerEventEnvelope(
            value.EventId, value.EventType, int.Parse(value.SchemaVersion),
            value.Producer, value.AggregateType, value.AggregateId,
            value.AggregateVersion, value.ActorId, value.ActorUsername,
            value.Before, value.After, value.Action, value.Summary,
            value.OccurredAtUtc,
            value.CorrelationId ?? Guid.NewGuid().ToString("D"),
            value.CausationId,
            value.SiteIds.FirstOrDefault(), null)).ToArray();

    private static CommandExecutionResult DuplicationFailure(string code) =>
        Failure(code switch
        {
            "NotFound" => 404,
            "Forbidden" => 403,
            "Conflict" => 409,
            "Validation" => 422,
            _ => 422
        }, code);

    private static CommandExecutionResult DuplicationCreated(
        Guid? newId, string? code, string? name, string? status, long version,
        IReadOnlyList<string> reviewRelationships) =>
        CommandExecutionResult.Ok(201,
            JsonSerializer.Serialize(new
            {
                id = newId,
                code,
                name,
                status,
                version,
                reviewRelationships
            }),
            newId?.ToString("D"));

    private static CommandExecutionResult Failure(int statusCode, string errorCode) =>
        new(statusCode, JsonSerializer.Serialize(new { errorCode }), null);

    private static SiteManagementItem ToManagementItem(SiteSnapshot value) =>
        new(value.Id, value.Code, value.Name, value.Description, value.Timezone,
            value.Status.ToString(), value.Version);

    private static AreaManagementItem ToManagementItem(AreaSnapshot value) =>
        new(value.Id, value.SiteId, value.Code, value.Name, value.Description,
            value.Status.ToString(), value.Version);

    private static AssetManagementItem ToManagementItem(AssetSnapshot value) =>
        new(value.Id, value.SiteId, value.AreaId, value.Code, value.Name,
            value.Description, value.Status.ToString(), value.Version);

    private static PointManagementItem ToManagementItem(PointSnapshot value) =>
        new(value.Id, value.SiteId, value.AreaId, value.AssetId, value.Code,
            value.Description, value.MetricId, value.UnitId, value.DataOwnerUserId,
            value.Status.ToString(), value.Version);

    private static SourceManagementItem ToManagementItem(
        CatalogRuntimeDataSource value) =>
        new(value.Id, value.Code, value.Name, value.SourceType, value.Status,
            value.Version, value.SiteId?.ToString("D"));

    private static MappingManagementItem ToManagementItem(
        CatalogRuntimeMapping value) =>
        new(value.Id, value.DataSourceId, value.PointId.ToString("D"),
            value.Status, value.EffectiveFrom, value.EffectiveTo, value.Version);

    private async Task<SimulatorConfigurationManagementItem> ToConfigurationItemAsync(
        SimulatorConfigurationHead value, CancellationToken ct)
    {
        var versions = await configurations.ListVersionsAsync(
            value.ConfigurationId, ct);
        var draft = versions
            .Where(version => version.ConfigurationVersion >
                              value.CurrentConfigurationVersion)
            .Select(version => version.ConfigurationVersion)
            .DefaultIfEmpty()
            .Max();
        return new SimulatorConfigurationManagementItem(
            value.ConfigurationId, value.SourceId,
            value.CurrentConfigurationVersion, value.Version,
            draft > value.CurrentConfigurationVersion ? draft : null);
    }

    private sealed class CatalogCallerBridge(
        IConfigurationCallerSnapshotProvider provider) : ICatalogConfigurationCallerProvider
    {
        public async Task<CatalogConfigurationCallerSnapshot?> ResolveAsync(
            string userId, CancellationToken ct = default)
        {
            var value = await provider.ResolveAsync(userId, ct);
            return value is null
                ? null
                : new CatalogConfigurationCallerSnapshot(value.UserId,
                    value.Username, value.IsActive, value.Roles, value.SiteScopes,
                    value.AreaScopes ?? Array.Empty<string>());
        }
    }
}
