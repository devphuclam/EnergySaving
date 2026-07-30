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
        return new OperationalWorkspaceStatus(
            landing,
            isAdministrator ? WorkspaceRoleMode.Administrator :
                hasAuthorizedScope ? WorkspaceRoleMode.Engineer : WorkspaceRoleMode.ReadOnly,
            sites,
            sites.Count == 1 ? sites[0].SiteId : null,
            completed,
            bounded < Steps.Length ? Steps[bounded] : null,
            Array.Empty<WorkspaceValidationFailure>(),
            operationalChainCount,
            operationalChainCount > 0 ? 0 : hasAnySite ? 1 : 0,
            false,
            dependencyAvailable ? "Available" : "Unavailable",
            dependencyAvailable ? null : "DEPENDENCY_UNAVAILABLE");
    }
}

public interface IOperationalWorkspaceQueryPort
{
    Task<OperationalWorkspaceStatus> GetStatusAsync(
        ServerPrincipal principal, CancellationToken ct = default);
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
