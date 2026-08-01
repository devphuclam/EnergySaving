namespace IUMP.Api.Infrastructure;

using IUMP.BuildingBlocks.Persistence;
using System.Collections.ObjectModel;

/// <summary>One authorized, currently eligible Source/configuration context.</summary>
public sealed record SimulatorSelectionOption(
    Guid SiteId,
    string SiteCode,
    string SiteName,
    Guid? AreaId,
    string? AreaCode,
    string? AreaName,
    Guid? AssetId,
    string? AssetCode,
    string? AssetName,
    Guid SourceId,
    string SourceCode,
    string SourceName,
    long SourceVersion,
    Guid ConfigurationId,
    long ConfigurationVersion,
    int IntervalSeconds,
    bool IsEligible,
    string? EligibilityCode);

/// <summary>All identity/version fields required to operate a selected context.</summary>
public sealed record SimulatorSelection(
    Guid SiteId,
    Guid? AreaId,
    Guid? AssetId,
    Guid SourceId,
    Guid ConfigurationId,
    long ConfigurationVersion);

public sealed record SimulatorRunHistoryItem(
    Guid RunId,
    Guid SourceId,
    Guid ConfigurationId,
    long ConfigurationVersion,
    string Status,
    long Version,
    long GeneratedCount,
    long AcceptedCount,
    long RejectedCount,
    DateTime? LastProductionAtUtc,
    int IntervalSeconds,
    DateTime CreatedAtUtc);

public sealed record SimulatorRunHistoryPage(
    IReadOnlyList<SimulatorRunHistoryItem> Items,
    int TotalCount,
    int Page,
    int PageSize,
    string? ErrorCode = null);

public sealed record SimulatorWorkspaceSnapshot(
    IReadOnlyList<SimulatorSelectionOption> Options,
    SimulatorSelection? Selection,
    SimulatorRunHistoryItem? CurrentRun,
    SimulatorRunHistoryPage History,
    string State,
    string? ErrorCode = null);

public interface ISimulatorWorkspaceQueryPort
{
    Task<SimulatorWorkspaceSnapshot> GetAsync(
        SimulatorSelection? selection,
        int page,
        int pageSize,
        ServerPrincipal principal,
        CancellationToken ct = default);
}

public interface ISimulatorWorkspaceCommandPort
{
    Task<CommandExecutionResult> ExecuteAsync(
        string operationCode,
        SimulatorSelection selection,
        Guid? runId,
        long? expectedVersion,
        ServerPrincipal principal,
        IHostTransaction transaction,
        CancellationToken ct = default);
}

/// <summary>Owner seam for a Start that carries the complete selected context into the transaction.</summary>
public interface ISimulatorSelectedStartCommandPort
{
    Task<CommandExecutionResult> ExecuteSelectedStartAsync(
        SimulatorSelection selection,
        ServerPrincipal principal,
        IHostTransaction transaction,
        CancellationToken ct = default);
}

public static class SimulatorWorkspaceSelectionRules
{
    public static bool IsExplicit(SimulatorSelection? selection) => selection is not null &&
        selection.SiteId != Guid.Empty && selection.SourceId != Guid.Empty &&
        selection.ConfigurationId != Guid.Empty && selection.ConfigurationVersion > 0;

    public static SimulatorSelectionOption? Resolve(
        IEnumerable<SimulatorSelectionOption> options,
        SimulatorSelection? selection)
    {
        if (!IsExplicit(selection)) return null;
        return options.FirstOrDefault(option =>
            option.SiteId == selection!.SiteId &&
            (selection.AreaId is null || option.AreaId == selection.AreaId) &&
            (selection.AssetId is null || option.AssetId == selection.AssetId) &&
            option.SourceId == selection.SourceId &&
            option.ConfigurationId == selection.ConfigurationId &&
            option.ConfigurationVersion == selection.ConfigurationVersion &&
            option.IsEligible);
    }

    public static IReadOnlyList<SimulatorSelectionOption> EmptyOptions { get; } =
        new ReadOnlyCollection<SimulatorSelectionOption>(Array.Empty<SimulatorSelectionOption>());
}
