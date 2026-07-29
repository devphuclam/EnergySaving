using IUMP.BuildingBlocks.Persistence;
using IUMP.Api;
using IUMP.Modules.Integration.Contracts;

namespace IUMP.Api.Infrastructure;

/// <summary>Identity established by the server authentication middleware.</summary>
public sealed record ServerPrincipal(Guid UserId, string Username, IReadOnlySet<string> SiteIds,
    IReadOnlySet<string> AreaIds, bool IsAdministrator = false)
{
    public bool HasScope(string? siteId, string? areaId) => IsAdministrator ||
        (siteId is not null && SiteIds.Contains(siteId)) || (areaId is not null && AreaIds.Contains(areaId));
}

public interface IServerPrincipalAccessor
{
    ServerPrincipal? Current { get; }
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
    Task<CommandExecutionResult> CreateSiteAsync(ConfigurationCommandRequest request, ServerPrincipal principal, CancellationToken ct = default);
    Task<CommandExecutionResult> UpdateSiteAsync(ConfigurationCommandRequest request, ServerPrincipal principal, CancellationToken ct = default);
}

public interface IConfigurationQueryPort
{
    Task<IReadOnlyList<object>> ListAsync(string resource, ServerPrincipal principal, CancellationToken ct = default);
}

public interface ISimulatorCommandPort
{
    Task<CommandExecutionResult> ExecuteAsync(string operationCode, Guid targetId, ServerPrincipal principal, CancellationToken ct = default);
}

public interface ISimulatorQueryPort
{
    Task<object> GetRunAsync(Guid runId, ServerPrincipal principal, CancellationToken ct = default);
}

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

public interface ITransactionalCommandMutation
{
    Task<CommandExecutionResult> ExecuteAsync(IHostTransaction transaction, CancellationToken ct = default);
}
