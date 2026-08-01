namespace IUMP.Api.Infrastructure;

public sealed record TelemetryHierarchySelection(
    Guid SiteId,
    Guid? AreaId,
    Guid? AssetId,
    Guid PointId);

public sealed record TelemetryOptionsQuery(
    int Page,
    int PageSize,
    Guid? SiteId = null,
    Guid? AreaId = null,
    Guid? AssetId = null)
{
    public int EffectivePage => Math.Clamp(Page, 1, 100_000);
    public int EffectivePageSize => Math.Clamp(PageSize, 1, 500);
}

public sealed record TelemetrySiteOption(Guid SiteId, string Code, string Name);
public sealed record TelemetryAreaOption(Guid AreaId, Guid SiteId, string Code, string Name);
public sealed record TelemetryAssetOption(Guid AssetId, Guid SiteId, Guid AreaId, string Code, string Name);
public sealed record TelemetryPointOption(
    Guid PointId,
    Guid SiteId,
    Guid AreaId,
    Guid AssetId,
    string Code,
    string Name,
    string Metric,
    string Unit);

public sealed record TelemetryWorkspaceOptions(
    IReadOnlyList<TelemetrySiteOption> Sites,
    IReadOnlyList<TelemetryAreaOption> Areas,
    IReadOnlyList<TelemetryAssetOption> Assets,
    IReadOnlyList<TelemetryPointOption> Points,
    Guid? SelectedPointId = null,
    int ScopedCount = 0);

public sealed record TelemetrySourceSummary(Guid SourceId, string Code, string Name);

public sealed record TelemetryHealthSummary(
    Guid PointId,
    Guid SourceId,
    string Status,
    DateTime? LastAcceptedReceivedAtUtc,
    string? RunStatus,
    long Generated,
    long Accepted,
    long Rejected,
    DateTime EvaluatedAtUtc,
    int? ExpectedIntervalSeconds = null,
    int? NoDataAfterSeconds = null);

public sealed record TelemetryRunSummary(
    Guid RunId,
    string Status,
    long Generated,
    long Accepted,
    long Rejected,
    DateTime? LastProductionAtUtc);

public enum TelemetryDataState
{
    NoSelection,
    Data,
    NoData,
    NotConfigured,
    Ambiguous,
    HierarchyConflict
}

public sealed record TelemetryWorkspaceCurrent(
    TelemetryHierarchySelection Selection,
    TelemetryPointOption Point,
    TelemetryDataState DataState,
    bool HasData,
    double? Value,
    string? Quality,
    string? ReasonCode,
    DateTime? SourceTimestampUtc,
    DateTime? ReceivedAtUtc,
    TelemetrySourceSummary? Source,
    TelemetryHealthSummary? Health,
    TelemetryRunSummary? Run,
    DateTime QueriedAtUtc,
    string? ErrorCode = null)
{
    public static TelemetryWorkspaceCurrent NoData(
        TelemetryHierarchySelection selection,
        DateTime queriedAtUtc,
        TelemetryPointOption? point = null) => new(
        selection,
        point ?? new TelemetryPointOption(
            selection.PointId, selection.SiteId, selection.AreaId ?? Guid.Empty,
            selection.AssetId ?? Guid.Empty, selection.PointId.ToString("D"),
            selection.PointId.ToString("D"), "", ""),
        TelemetryDataState.NoData,
        false, null, null, "NO_DATA", null, null, null, null, null, queriedAtUtc);
}

public static class TelemetrySelectionRules
{
    public static string? Validate(TelemetryHierarchySelection selection, Guid? knownPointId = null)
    {
        if (selection.SiteId == Guid.Empty || selection.PointId == Guid.Empty)
            return "SELECTION_REQUIRED";
        if (knownPointId is { } pointId && pointId != selection.PointId)
            return "POINT_HIERARCHY_MISMATCH";
        if (selection.AreaId is null)
            return "AREA_SELECTION_REQUIRED";
        if (selection.AssetId is null)
            return "ASSET_SELECTION_REQUIRED";
        return null;
    }
}

public static class TelemetryRefreshPolicy
{
    public static TimeSpan DefaultInterval => TimeSpan.FromSeconds(10);
    public const bool CanDisable = true;
    public const bool HasManualRefresh = true;
}

public interface ITelemetryWorkspaceQueryPort
{
    Task<TelemetryWorkspaceOptions> GetOptionsAsync(
        ServerPrincipal principal,
        TelemetryOptionsQuery query,
        CancellationToken ct = default);

    Task<TelemetryWorkspaceCurrent> GetCurrentAsync(
        TelemetryHierarchySelection selection,
        ServerPrincipal principal,
        CancellationToken ct = default);
}

public sealed class TelemetryHierarchyConflictException(string code = "HIERARCHY_CONFLICT") : Exception(code);
