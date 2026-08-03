using IUMP.BuildingBlocks.Persistence;
using IUMP.Modules.Integration.Contracts;
using System.Text.Json.Serialization;

namespace IUMP.Api.Infrastructure;

/// <summary>Identity established by the server authentication middleware.</summary>
public sealed record ServerPrincipal(Guid UserId, string Username, IReadOnlySet<string> SiteIds,
    IReadOnlySet<string> AreaIds, bool IsAdministrator = false,
    IReadOnlySet<string>? Roles = null,
    IReadOnlySet<string>? Capabilities = null)
{
    public bool HasScope(string? siteId, string? areaId) => IsAdministrator ||
        (siteId is not null && SiteIds.Contains(siteId)) || (areaId is not null && AreaIds.Contains(areaId));

    public bool HasRole(string role) =>
        (IsAdministrator &&
         role.Equals("Administrator", StringComparison.OrdinalIgnoreCase)) ||
        Roles?.Contains(role) == true;

    public bool HasCapability(string capability) =>
        IsAdministrator || Capabilities?.Contains(capability) == true;
}

public interface IServerPrincipalAccessor
{
    ServerPrincipal? Current { get; }
}

public sealed class RuntimeScopeDeniedException : Exception
{
    public RuntimeScopeDeniedException() : base("NOT_FOUND") { }
}

public interface IUtcClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemUtcClock : IUtcClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed record ConfigurationCommandRequest(Guid? TargetId, string Name, long? ExpectedVersion,
    IReadOnlyList<CommandFingerprintField> Fields, string? SiteId = null, string? AreaId = null);

public interface IConfigurationCommandPort
{
    Task<CommandExecutionResult> CreateSiteAsync(ConfigurationCommandRequest request, ServerPrincipal principal,
        IHostTransaction transaction, CancellationToken ct = default);
    Task<CommandExecutionResult> UpdateSiteAsync(ConfigurationCommandRequest request, ServerPrincipal principal,
        IHostTransaction transaction, CancellationToken ct = default);
    Task<CommandExecutionResult> ExecuteAsync(string operationCode, ConfigurationCommandRequest request,
        ServerPrincipal principal, IHostTransaction transaction, CancellationToken ct = default);
}

public interface IConfigurationQueryPort
{
    Task<IReadOnlyList<object>> ListAsync(string resource, ServerPrincipal principal, CancellationToken ct = default);
}

public interface ISimulatorCommandPort
{
    Task<CommandExecutionResult> ExecuteAsync(string operationCode, Guid targetId, long? expectedVersion,
        ServerPrincipal principal,
        IHostTransaction transaction, CancellationToken ct = default);
}

public interface ISimulatorQueryPort
{
    Task<object> GetRunAsync(Guid runId, ServerPrincipal principal, CancellationToken ct = default);
}

public sealed record LatestQueryResult(Guid PointId, double? NumericValue, string? UnitCode,
    string Status, bool IsNoData, string? ReasonCode = null,
    DateTime? SourceTimestampUtc = null, DateTime? ReceivedAtUtc = null,
    string? RunStatus = null, long Generated = 0, long Accepted = 0, long Rejected = 0);

public interface ITelemetryQueryPort
{
    Task<LatestQueryResult> GetLatestAsync(Guid pointId, ServerPrincipal principal, CancellationToken ct = default);
    Task<object> GetSourceHealthAsync(Guid pointId, ServerPrincipal principal, CancellationToken ct = default);
    Task<IReadOnlyList<LatestQueryResult>> GetCurrentAsync(Guid siteId, ServerPrincipal principal, CancellationToken ct = default);
}

public sealed record AuditQueryPage(IReadOnlyList<object> Items, string? ErrorCode = null,
    string? NextCursor = null, int TotalCount = 0);

public interface IAuditQueryPort
{
    Task<AuditQueryPage> QueryAsync(IReadOnlyDictionary<string, string?> filters, ServerPrincipal principal,
        string? cursor, int pageSize, CancellationToken ct = default);
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OperationalDashboardState { Ready, NoAuthorizedScope, DependencyError, Forbidden, RuntimeError }

public sealed record OperationalDashboardSummary(int Count, IReadOnlyList<object> Items);
public sealed record OperationalDashboardSetup(int Count, string? NextStep);
public sealed record OperationalDashboardAudit(IReadOnlyList<object> Items, string? NextCursor);
public sealed record OperationalDashboardRuntime(string Status, bool SimulatorRunning);
public sealed record OperationalDashboardDependency(string Status, string? ErrorCode, string? CorrelationId);

public sealed record OperationalDashboardSnapshot(
    OperationalDashboardState State,
    WorkspaceRoleMode RoleMode,
    OperationalDashboardSummary Sites,
    OperationalDashboardSummary Sources,
    OperationalDashboardSummary Points,
    OperationalDashboardSummary Runs,
    OperationalDashboardSummary Latest,
    OperationalDashboardSummary Health,
    OperationalDashboardSetup IncompleteSetup,
    OperationalDashboardAudit RecentAudit,
    OperationalDashboardRuntime Runtime,
    OperationalDashboardDependency Dependency);

public interface IOperationalDashboardQueryPort
{
    Task<OperationalDashboardSnapshot> GetAsync(ServerPrincipal principal, CancellationToken ct = default);
}

public interface ITransactionalCommandMutation
{
    Task<CommandExecutionResult> ExecuteAsync(IHostTransaction transaction, CancellationToken ct = default);
}

public sealed record CommandExecutionResult(int StatusCode, string Body, string? ResourceReference,
    string? Location = null, string? ETag = null, string? CorrelationId = null)
{
    public static CommandExecutionResult Ok(int statusCode, string body, string? resourceReference,
        string? location = null, string? etag = null, string? correlationId = null) =>
        new(statusCode, body, resourceReference, location, etag, correlationId);
}
