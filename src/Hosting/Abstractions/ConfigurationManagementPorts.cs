using IUMP.BuildingBlocks.Persistence;

namespace IUMP.Api.Infrastructure;

/// <summary>Scoped search/filter/paging input for configuration management queries.
/// Scope is always resolved from the server principal before paging; client-supplied
/// scope values only narrow within the authorized scope.</summary>
public sealed record ManagementQueryFilter(
    string? Search = null,
    string? Status = null,
    string? SiteId = null,
    string? AreaId = null,
    int Page = 1,
    int PageSize = 20);

public sealed record ConfigurationManagementPage<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record SiteManagementItem(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string Timezone,
    string Status,
    long Version);

public sealed record AreaManagementItem(
    Guid Id,
    Guid SiteId,
    string Code,
    string Name,
    string? Description,
    string Status,
    long Version);

public sealed record AssetManagementItem(
    Guid Id,
    Guid SiteId,
    Guid AreaId,
    string Code,
    string Name,
    string? Description,
    string Status,
    long Version);

public sealed record PointManagementItem(
    Guid Id,
    Guid SiteId,
    Guid AreaId,
    Guid AssetId,
    string Code,
    string? Description,
    string MetricId,
    string UnitId,
    string DataOwnerUserId,
    string Status,
    long Version,
    int ExpectedIntervalSeconds = 0,
    int NoDataAfterSeconds = 0);

public sealed record SourceManagementItem(
    Guid Id,
    string Code,
    string Name,
    string SourceType,
    string Status,
    long Version,
    string? SiteId);

public sealed record MappingManagementItem(
    Guid Id,
    Guid DataSourceId,
    string PointId,
    string Status,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    long Version);

public sealed record SimulatorConfigurationManagementItem(
    Guid ConfigurationId,
    Guid SourceId,
    long CurrentConfigurationVersion,
    long Version,
    long? DraftConfigurationVersion = null,
    string? ScenarioType = null,
    int? IntervalSeconds = null,
    double? MinimumValue = null,
    double? MaximumValue = null,
    ulong? DeterministicSeed = null,
    bool RelationshipReviewed = false,
    bool ValidationRecorded = false,
    string? SourceCode = null,
    string? SourceName = null,
    string? SourceStatus = null,
    long? SourceVersion = null,
    IReadOnlyList<string>? ReviewRelationships = null,
    IReadOnlyList<string>? ExcludedFields = null,
    bool RelationshipReceiptStale = false,
    bool ValidationReceiptStale = false);

public static class ConfigurationManagementResources
{
    public const string Sites = "sites";
    public const string Areas = "areas";
    public const string Assets = "assets";
    public const string Points = "points";
    public const string DataSources = "data-sources";
    public const string SourcePointMappings = "source-point-mappings";
    public const string SimulatorConfigurations = "simulator-configurations";

    public static bool IsKnown(string resource) => resource is Sites or Areas or Assets or Points or
        DataSources or SourcePointMappings or SimulatorConfigurations;
}

public static class ConfigurationManagementSearch
{
    public static bool MatchesSimulatorConfiguration(
        Guid configurationId, Guid sourceId, long currentVersion, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        return configurationId.ToString("D").Contains(search, StringComparison.OrdinalIgnoreCase) ||
               sourceId.ToString("D").Contains(search, StringComparison.OrdinalIgnoreCase) ||
               currentVersion.ToString().Contains(search, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Typed paged management query seam. Scope-before-paging is enforced by the
/// composition adapter through the owner modules' query contracts.</summary>
public interface IConfigurationManagementQueryPort
{
    Task<ConfigurationManagementPage<object>> QueryAsync(
        string resource,
        ManagementQueryFilter filter,
        ServerPrincipal principal,
        CancellationToken ct = default);

    Task<object?> GetDetailAsync(
        string resource,
        Guid id,
        ServerPrincipal principal,
        CancellationToken ct = default);
}

/// <summary>Typed management command seam. Duplication always produces a new Draft;
/// version transitions are explicit and optimistic-concurrency safe.</summary>
public interface IConfigurationManagementCommandPort
{
    Task<CommandExecutionResult> CreateSiteAsync(
        ConfigurationCommandRequest request,
        ServerPrincipal principal,
        IHostTransaction transaction,
        CancellationToken ct = default);

    Task<CommandExecutionResult> UpdateSiteAsync(
        ConfigurationCommandRequest request,
        ServerPrincipal principal,
        IHostTransaction transaction,
        CancellationToken ct = default);

    Task<CommandExecutionResult> ExecuteAsync(
        string operationCode,
        ConfigurationCommandRequest request,
        ServerPrincipal principal,
        IHostTransaction transaction,
        CancellationToken ct = default);

    Task<CommandExecutionResult> ValidateAsync(
        string resource,
        Guid targetId,
        ServerPrincipal principal,
        IHostTransaction transaction,
        CancellationToken ct = default);

    Task<CommandExecutionResult> ReviewSimulatorConfigurationAsync(
        Guid configurationId,
        long draftConfigurationVersion,
        ServerPrincipal principal,
        IHostTransaction transaction,
        CancellationToken ct = default);

    Task<CommandExecutionResult> DuplicateAsync(
        string resource,
        Guid targetId,
        ServerPrincipal principal,
        IHostTransaction transaction,
        Guid? targetSourceId = null,
        CancellationToken ct = default);

    Task<CommandExecutionResult> ActivateSimulatorConfigurationVersionAsync(
        Guid configurationId,
        long expectedHeadVersion,
        long draftConfigurationVersion,
        ServerPrincipal principal,
        IHostTransaction transaction,
        CancellationToken ct = default);
}
