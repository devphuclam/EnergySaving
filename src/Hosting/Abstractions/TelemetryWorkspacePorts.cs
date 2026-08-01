namespace IUMP.Api.Infrastructure;

using System.Text.Json.Serialization;

public sealed record TelemetryHierarchySelection(
    Guid SiteId,
    Guid? AreaId,
    Guid? AssetId,
    Guid PointId);

public enum TelemetryOptionLevel
{
    Sites,
    Areas,
    Assets,
    Points
}

public sealed record TelemetryOptionsQuery(
    TelemetryOptionLevel Level,
    long Page = 1,
    int PageSize = 100,
    Guid? SiteId = null,
    Guid? AreaId = null,
    Guid? AssetId = null,
    string? Search = null)
{
    public const int DefaultPageSize = 100;
    public const int MaximumPageSize = 100;
    public const int MaximumSearchLength = 100;
    public const long MaximumPage = 10_000_000;

    public string? Validate()
    {
        if (Level == TelemetryOptionLevel.Areas && SiteId is null)
            return "SITE_SELECTION_REQUIRED";
        if (Level == TelemetryOptionLevel.Assets && (SiteId is null || AreaId is null))
            return "AREA_SELECTION_REQUIRED";
        if (Level == TelemetryOptionLevel.Points &&
            (SiteId is null || AreaId is null || AssetId is null))
            return "COMPLETE_HIERARCHY_REQUIRED";
        if (Page < 1 || Page > MaximumPage)
            return "INVALID_PAGE";
        if (PageSize < 1 || PageSize > MaximumPageSize)
            return "INVALID_PAGE_SIZE";
        if (Search?.Length > MaximumSearchLength)
            return "INVALID_SEARCH";
        return TryGetOffset(out _) ? null : "INVALID_PAGE";
    }

    public bool TryGetOffset(out long offset)
    {
        try
        {
            offset = checked((Page - 1) * PageSize);
            return offset >= 0;
        }
        catch (OverflowException)
        {
            offset = 0;
            return false;
        }
    }
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
    long ScopedCount = 0,
    long Page = 1,
    int PageSize = TelemetryOptionsQuery.DefaultPageSize);

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

[JsonConverter(typeof(JsonStringEnumConverter))]
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
