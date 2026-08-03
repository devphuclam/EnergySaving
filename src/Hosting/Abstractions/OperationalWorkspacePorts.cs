using IUMP.BuildingBlocks.Persistence;
using System.Text.Json.Serialization;

namespace IUMP.Api.Infrastructure;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkspaceLanding
{
    SetupWizard,
    ContinueSetup,
    Dashboard,
    NoAuthorizedScope,
    DependencyError
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkspaceRoleMode { Administrator, Engineer, ReadOnly }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkspaceStep
{
    SiteAndEngineer,
    Area,
    Asset,
    MeasurementPoint,
    DataSource,
    Mapping,
    SimulatorConfiguration,
    ValidateAndActivate
}

public sealed record WorkspaceSiteSummary(
    Guid SiteId, string Code, string Name, string Status, long Version);

public sealed record WorkspaceEngineerCandidate(
    Guid UserId, string Username, string Status, IReadOnlyList<Guid> AssignedSiteIds);

public sealed record WorkspaceChainSelection(
    Guid? SiteId,
    long? SiteVersion,
    Guid? AreaId,
    long? AreaVersion,
    Guid? AssetId,
    long? AssetVersion,
    Guid? PointId,
    long? PointVersion,
    Guid? SourceId,
    long? SourceVersion,
    Guid? MappingId,
    long? MappingVersion,
    Guid? ConfigurationId,
    long? ConfigurationVersion);

public sealed record WorkspaceStatusRequest
{
    private WorkspaceStatusRequest(string? mode, Guid? selectedSiteId)
    {
        Mode = mode;
        SelectedSiteId = selectedSiteId;
    }

    public string? Mode { get; }

    public Guid? SelectedSiteId { get; }

    public bool IsNew => string.Equals(Mode, "new", StringComparison.OrdinalIgnoreCase);

    public static WorkspaceStatusRequest NewSetup() => new("new", null);

    public static WorkspaceStatusRequest ForSite(Guid siteId) => new(null, siteId);

    public static WorkspaceStatusRequest FromQuery(string? mode, Guid? selectedSiteId)
    {
        if (mode is null && selectedSiteId is null)
            throw new ArgumentException("A status selection is required.");
        if (mode is not null && selectedSiteId is not null)
            throw new ArgumentException("Status mode and Site selection are mutually exclusive.");
        if (mode is not null && !mode.Equals("new", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only the new status mode is supported.");
        return mode is not null ? new WorkspaceStatusRequest(mode, null) : new WorkspaceStatusRequest(null, selectedSiteId);
    }
}

internal sealed record WorkspacePersistedSite(
    Guid Id,
    string Code,
    string Name,
    string Status,
    long Version,
    bool EngineerAssigned,
    bool IsAuthorized);

internal sealed record WorkspacePersistedArea(
    Guid Id, Guid SiteId, string Status, long Version);

internal sealed record WorkspacePersistedAsset(
    Guid Id, Guid SiteId, Guid AreaId, string Status, long Version);

internal sealed record WorkspacePersistedPoint(
    Guid Id, Guid SiteId, Guid AreaId, Guid AssetId, string Status, long Version);

internal sealed record WorkspacePersistedSource(
    Guid Id, Guid SiteId, string Status, long Version);

internal sealed record WorkspacePersistedMapping(
    Guid Id, Guid SourceId, Guid PointId, string Status, long Version);

internal sealed record WorkspacePersistedConfiguration(
    Guid Id, Guid SourceId, long Version);

internal sealed record WorkspacePersistedSnapshot(
    IReadOnlyList<WorkspacePersistedSite> Sites,
    IReadOnlyList<WorkspacePersistedArea> Areas,
    IReadOnlyList<WorkspacePersistedAsset> Assets,
    IReadOnlyList<WorkspacePersistedPoint> Points,
    IReadOnlyList<WorkspacePersistedSource> Sources,
    IReadOnlyList<WorkspacePersistedMapping> Mappings,
    IReadOnlyList<WorkspacePersistedConfiguration> Configurations);

public sealed record WorkspaceValidationFailure(
    WorkspaceStep Step, string? Field, string ErrorCode, string MessageKey);

public sealed record OperationalWorkspaceStatus(
    WorkspaceLanding Landing,
    WorkspaceRoleMode RoleMode,
    IReadOnlyList<WorkspaceSiteSummary> AuthorizedSites,
    Guid? SelectedSiteId,
    IReadOnlyList<WorkspaceStep> CompletedSteps,
    WorkspaceStep? NextStep,
    IReadOnlyList<WorkspaceValidationFailure> ValidationFailures,
    int OperationalChainCount,
    int IncompleteChainCount,
    bool SimulatorAutoStart,
    string DependencyStatus = "Available",
    string? ErrorCode = null,
    string? CorrelationId = null,
    WorkspaceChainSelection? Chain = null,
    IReadOnlyList<string>? ActivationSteps = null,
    Guid? CurrentUserId = null);

public sealed record WorkspaceChainValidation(
    bool Valid,
    IReadOnlyList<WorkspaceValidationFailure> Failures,
    IReadOnlyDictionary<string, long> Versions,
    IReadOnlyList<string> ActivationSteps,
    bool SimulatorAutoStart = false);

public static class OperationalWorkspaceStatusBuilder
{
    private static readonly WorkspaceStep[] Steps = Enum.GetValues<WorkspaceStep>();

    public static OperationalWorkspaceStatus Build(
        bool isAdministrator,
        bool hasAuthorizedScope,
        bool hasAnySite,
        int completedStepCount,
        int operationalChainCount,
        bool dependencyAvailable,
        IReadOnlyList<WorkspaceSiteSummary> sites)
    {
        var bounded = Math.Clamp(completedStepCount, 0, Steps.Length);
        var completed = Steps.Take(bounded).ToArray();
        var landing = !dependencyAvailable
            ? WorkspaceLanding.DependencyError
            : !isAdministrator && !hasAuthorizedScope
                ? WorkspaceLanding.NoAuthorizedScope
                : operationalChainCount > 0
                    ? WorkspaceLanding.Dashboard
                    : hasAnySite || bounded > 0
                        ? WorkspaceLanding.ContinueSetup
                        : WorkspaceLanding.SetupWizard;
        var visibleOperationalChainCount = !isAdministrator && !hasAuthorizedScope
            ? 0
            : operationalChainCount;
        var visibleIncompleteChainCount = !isAdministrator && !hasAuthorizedScope
            ? 0
            : operationalChainCount > 0 ? 0 : hasAnySite ? 1 : 0;
        return new OperationalWorkspaceStatus(
            landing,
            isAdministrator ? WorkspaceRoleMode.Administrator :
                hasAuthorizedScope ? WorkspaceRoleMode.Engineer : WorkspaceRoleMode.ReadOnly,
            sites,
            sites.Count == 1 ? sites[0].SiteId : null,
            completed,
            bounded < Steps.Length ? Steps[bounded] : null,
            Array.Empty<WorkspaceValidationFailure>(),
            visibleOperationalChainCount,
            visibleIncompleteChainCount,
            false,
            dependencyAvailable ? "Available" : "Unavailable",
            dependencyAvailable ? null : "DEPENDENCY_UNAVAILABLE");
    }

    /// <summary>
    /// Derives every eligible authorized chain by persisted relationships. Resume selection is
    /// deterministic: operational chains win; otherwise the most completed chain wins, followed
    /// by the stable Site/Area/Asset/Point/Source/Mapping identity tuple.
    /// </summary>
    internal static OperationalWorkspaceStatus BuildFromSnapshot(
        bool isAdministrator,
        bool hasAuthorizedScope,
        bool dependencyAvailable,
        WorkspacePersistedSnapshot snapshot,
        WorkspaceStatusRequest? request = null)
    {
        if (request?.IsNew == true)
            return isAdministrator
                ? NewSetupStatus()
                : Build(false, hasAuthorizedScope, false, 0, 0,
                    dependencyAvailable, []);

        var sites = snapshot.Sites
            .Where(value => value.IsAuthorized)
            .OrderBy(value => value.Id)
            .ToArray();
        if (request?.SelectedSiteId is { } selectedSiteId)
        {
            if (sites.All(value => value.Id != selectedSiteId))
                return SelectionNotFound(isAdministrator, hasAuthorizedScope);
            sites = sites.Where(value => value.Id == selectedSiteId).ToArray();
        }
        var summaries = sites.Select(value => new WorkspaceSiteSummary(
            value.Id, value.Code, value.Name, value.Status, value.Version)).ToArray();
        if (!dependencyAvailable || (!isAdministrator && !hasAuthorizedScope) ||
            sites.Length == 0)
            return Build(
                isAdministrator, hasAuthorizedScope, sites.Length > 0, 0, 0,
                dependencyAvailable, summaries);

        var candidates = new List<ChainCandidate>();
        foreach (var site in sites)
            BuildSiteCandidates(site, snapshot, isAdministrator, candidates);

        var operationalCount = candidates.Count(value => value.Operational);
        var incompleteCount = candidates.Count - operationalCount;
        var selected = candidates
            .OrderByDescending(value => value.Operational)
            .ThenByDescending(value => value.CompletedStepCount)
            .ThenBy(value => value.StableIdentity, StringComparer.Ordinal)
            .FirstOrDefault();
        if (selected is null)
            return Build(
                isAdministrator, hasAuthorizedScope, false, 0, 0, true, summaries);

        var completed = Steps.Take(selected.CompletedStepCount).ToArray();
        var failures = ValidationFailures(selected);
        return new OperationalWorkspaceStatus(
            operationalCount > 0
                ? WorkspaceLanding.Dashboard
                : WorkspaceLanding.ContinueSetup,
            isAdministrator
                ? WorkspaceRoleMode.Administrator
                : WorkspaceRoleMode.Engineer,
            summaries,
            selected.Site.Id,
            completed,
            selected.CompletedStepCount < Steps.Length
                ? Steps[selected.CompletedStepCount]
                : null,
            failures,
            operationalCount,
            incompleteCount,
            false,
            "Available",
            null,
            null,
            new WorkspaceChainSelection(
                selected.Site.Id,
                selected.Site.Version,
                selected.Area?.Id,
                selected.Area?.Version,
                selected.Asset?.Id,
                selected.Asset?.Version,
                selected.Point?.Id,
                selected.Point?.Version,
                selected.Source?.Id,
                selected.Source?.Version,
                selected.Mapping?.Id,
                selected.Mapping?.Version,
                selected.Configuration?.Id,
                selected.Configuration?.Version),
            ActivationSteps(selected));
    }

    private static OperationalWorkspaceStatus NewSetupStatus() => new(
        WorkspaceLanding.SetupWizard,
        WorkspaceRoleMode.Administrator,
        [],
        null,
        [],
        WorkspaceStep.SiteAndEngineer,
        [],
        0,
        0,
        false,
        "Available");

    private static OperationalWorkspaceStatus SelectionNotFound(
        bool isAdministrator,
        bool hasAuthorizedScope) => new(
            isAdministrator
                ? WorkspaceLanding.SetupWizard
                : WorkspaceLanding.NoAuthorizedScope,
            isAdministrator
                ? WorkspaceRoleMode.Administrator
                : hasAuthorizedScope
                    ? WorkspaceRoleMode.Engineer
                    : WorkspaceRoleMode.ReadOnly,
            [],
            null,
            [],
            null,
            [],
            0,
            0,
            false,
            "Available",
            "NOT_FOUND");

    private static void BuildSiteCandidates(
        WorkspacePersistedSite site,
        WorkspacePersistedSnapshot snapshot,
        bool isAdministrator,
        ICollection<ChainCandidate> output)
    {
        var areas = snapshot.Areas
            .Where(value => value.SiteId == site.Id)
            .OrderBy(value => value.Id)
            .ToArray();
        if (areas.Length == 0)
        {
            output.Add(Candidate(site, null, null, null, null, null, null));
            return;
        }

        var pointBranches = new List<HierarchyBranch>();
        foreach (var area in areas)
        {
            var assets = snapshot.Assets
                .Where(value => value.SiteId == site.Id && value.AreaId == area.Id)
                .OrderBy(value => value.Id)
                .ToArray();
            if (assets.Length == 0)
            {
                output.Add(Candidate(site, area, null, null, null, null, null));
                continue;
            }

            foreach (var asset in assets)
            {
                var points = snapshot.Points
                    .Where(value =>
                        value.SiteId == site.Id &&
                        value.AreaId == area.Id &&
                        value.AssetId == asset.Id)
                    .OrderBy(value => value.Id)
                    .ToArray();
                if (points.Length == 0)
                {
                    output.Add(Candidate(site, area, asset, null, null, null, null));
                    continue;
                }

                pointBranches.AddRange(points.Select(
                    point => new HierarchyBranch(area, asset, point)));
            }
        }

        if (pointBranches.Count == 0)
            return;

        var representedPoints = new HashSet<Guid>();
        var representedSources = new HashSet<Guid>();
        foreach (var branch in pointBranches.OrderBy(value => value.StableIdentity))
        {
            var mapped = from mapping in snapshot.Mappings
                         where mapping.PointId == branch.Point.Id
                         join source in snapshot.Sources
                             on mapping.SourceId equals source.Id
                         where source.SiteId == site.Id
                         orderby source.Id, mapping.Id
                         select (Source: source, Mapping: mapping);
            foreach (var pair in mapped)
            {
                var configuration = snapshot.Configurations
                    .Where(value => value.SourceId == pair.Source.Id)
                    .OrderBy(value => value.Id)
                    .FirstOrDefault();
                output.Add(Candidate(
                    site, branch.Area, branch.Asset, branch.Point,
                    pair.Source, pair.Mapping, configuration));
                representedPoints.Add(branch.Point.Id);
                representedSources.Add(pair.Source.Id);
            }
        }

        // A pre-Mapping Source may resume against the hierarchy only when the Site has one
        // unambiguous Point branch. With multiple branches no persisted relationship selects a
        // Point, so advancing by stable/list position would combine unrelated entities.
        if (pointBranches.Count == 1)
        {
            var onlyBranch = pointBranches[0];
            foreach (var source in snapshot.Sources
                         .Where(value =>
                             value.SiteId == site.Id &&
                             !representedSources.Contains(value.Id))
                         .OrderBy(value => value.Id))
            {
                output.Add(Candidate(
                    site, onlyBranch.Area, onlyBranch.Asset, onlyBranch.Point,
                    source, null, null));
                representedPoints.Add(onlyBranch.Point.Id);
            }
        }

        foreach (var branch in pointBranches
                     .Where(value => !representedPoints.Contains(value.Point.Id))
                     .OrderBy(value => value.StableIdentity))
            output.Add(Candidate(
                site, branch.Area, branch.Asset, branch.Point,
                null, null, null));

        ChainCandidate Candidate(
            WorkspacePersistedSite persistedSite,
            WorkspacePersistedArea? area,
            WorkspacePersistedAsset? asset,
            WorkspacePersistedPoint? point,
            WorkspacePersistedSource? source,
            WorkspacePersistedMapping? mapping,
            WorkspacePersistedConfiguration? configuration)
        {
            var completed = 0;
            if (IsActive(persistedSite.Status) &&
                (!isAdministrator || persistedSite.EngineerAssigned))
                completed = 1;
            if (completed == 1 && area is not null) completed = 2;
            if (completed == 2 && asset is not null) completed = 3;
            if (completed == 3 && point is not null) completed = 4;
            if (completed == 4 && source is not null) completed = 5;
            if (completed == 5 && mapping is not null) completed = 6;
            if (completed == 6 && configuration is not null) completed = 7;
            var operational = completed == 7 &&
                IsActive(persistedSite.Status) &&
                IsActive(area?.Status) &&
                IsActive(asset?.Status) &&
                IsActive(point?.Status) &&
                IsActive(source?.Status) &&
                IsActive(mapping?.Status);
            if (operational) completed = 8;
            return new ChainCandidate(
                persistedSite, area, asset, point, source, mapping,
                configuration, completed, operational);
        }
    }

    private static IReadOnlyList<WorkspaceValidationFailure> ValidationFailures(
        ChainCandidate selected)
    {
        var failures = new List<WorkspaceValidationFailure>();
        Add(selected.Area?.Status, WorkspaceStep.Area, "areaId", "AREA_INELIGIBLE");
        Add(selected.Asset?.Status, WorkspaceStep.Asset, "assetId", "ASSET_INELIGIBLE");
        Add(selected.Point?.Status, WorkspaceStep.MeasurementPoint, "pointId", "POINT_INELIGIBLE");
        Add(selected.Source?.Status, WorkspaceStep.DataSource, "sourceId", "SOURCE_INELIGIBLE");
        Add(selected.Mapping?.Status, WorkspaceStep.Mapping, "mappingId", "MAPPING_INELIGIBLE");
        return failures;

        void Add(string? status, WorkspaceStep step, string field, string code)
        {
            if (status is not null && !IsActiveOrDraft(status))
                failures.Add(new WorkspaceValidationFailure(
                    step, field, code, $"setup.{step}.{code}".ToLowerInvariant()));
        }
    }

    private static IReadOnlyList<string> ActivationSteps(ChainCandidate selected) =>
        new[]
        {
            (Name: "site", Pending: !IsActive(selected.Site.Status)),
            (Name: "area", Pending: selected.Area is not null && !IsActive(selected.Area.Status)),
            (Name: "asset", Pending: selected.Asset is not null && !IsActive(selected.Asset.Status)),
            (Name: "data-source", Pending: selected.Source is not null && !IsActive(selected.Source.Status)),
            (Name: "mapping", Pending: selected.Mapping is not null && !IsActive(selected.Mapping.Status)),
            (Name: "measurement-point", Pending: selected.Point is not null && !IsActive(selected.Point.Status))
        }.Where(value => value.Pending).Select(value => value.Name).ToArray();

    private static bool IsActive(string? status) =>
        string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);

    private static bool IsActiveOrDraft(string status) =>
        IsActive(status) ||
        string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase);

    private sealed record HierarchyBranch(
        WorkspacePersistedArea Area,
        WorkspacePersistedAsset Asset,
        WorkspacePersistedPoint Point)
    {
        public string StableIdentity => $"{Area.Id:D}/{Asset.Id:D}/{Point.Id:D}";
    }

    private sealed record ChainCandidate(
        WorkspacePersistedSite Site,
        WorkspacePersistedArea? Area,
        WorkspacePersistedAsset? Asset,
        WorkspacePersistedPoint? Point,
        WorkspacePersistedSource? Source,
        WorkspacePersistedMapping? Mapping,
        WorkspacePersistedConfiguration? Configuration,
        int CompletedStepCount,
        bool Operational)
    {
        public string StableIdentity =>
            $"{Site.Id:D}/{Area?.Id:D}/{Asset?.Id:D}/{Point?.Id:D}/" +
            $"{Source?.Id:D}/{Mapping?.Id:D}/{Configuration?.Id:D}";
    }
}

public interface IOperationalWorkspaceQueryPort
{
    Task<OperationalWorkspaceStatus> GetStatusAsync(
        ServerPrincipal principal,
        WorkspaceStatusRequest? request = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<WorkspaceEngineerCandidate>> ListEngineersAsync(
        ServerPrincipal principal, CancellationToken ct = default);
    Task<WorkspaceChainValidation> ValidateChainAsync(
        WorkspaceChainSelection requested,
        ServerPrincipal principal,
        CancellationToken ct = default);
}

public interface IOperationalWorkspaceCommandPort
{
    Task<CommandExecutionResult> AssignEngineerAsync(
        Guid siteId,
        Guid engineerUserId,
        ServerPrincipal principal,
        IHostTransaction transaction,
        CancellationToken ct = default);
}
