using IUMP.Modules.Audit.Contracts;

namespace IUMP.Modules.Audit.Application;

public sealed class AuditAuthorization
{
    public bool CanQuery(AuditCaller caller, AuditEventRecord? row = null)
    {
        if (!caller.IsActive) return false;
        if (caller.IsAdministrator) return true;
        if (!caller.HasAuditRead) return false;
        if (row is null) return caller.SiteIds.Count > 0 || caller.AreaIds.Count > 0;
        if (row.SiteId is null && row.AreaId is null) return false;
        return (row.SiteId is not null && caller.SiteIds.Contains(row.SiteId)) ||
            (row.AreaId is not null && caller.AreaIds.Contains(row.AreaId));
    }
}

public sealed class AuditQueryService(IAuditQueryRepository repository, AuditAuthorization authorization)
{
    public async Task<AuditQueryResult> QueryAsync(AuditQueryRequest request, AuditCaller caller, CancellationToken ct = default)
    {
        if (!authorization.CanQuery(caller)) return new(Array.Empty<AuditEventRecord>(), "FORBIDDEN", 0);
        if (request.Page < 1 || request.PageSize is < 1 or > 100)
            return new(Array.Empty<AuditEventRecord>(), "VALIDATION", 0);
        if (!string.IsNullOrWhiteSpace(request.KeysetCursor) &&
            !AuditKeysetCursor.TryDecode(request.KeysetCursor, out _))
            return new(Array.Empty<AuditEventRecord>(), "VALIDATION", 0);
        // Scope is applied before paging/keyset so an unauthorized row can never consume a page slot.
        if (!caller.IsAdministrator &&
            ((request.SiteId is not null && !caller.SiteIds.Contains(request.SiteId)) ||
             (request.AreaId is not null && !caller.AreaIds.Contains(request.AreaId))))
            return new(Array.Empty<AuditEventRecord>(), null, 0);
        var scopedSites = caller.IsAdministrator
            ? request.SiteId is null ? request.ScopeSiteIds : new HashSet<string> { request.SiteId }
            : request.SiteId is null
                ? caller.SiteIds
                : caller.SiteIds.Contains(request.SiteId) ? new HashSet<string> { request.SiteId } : [];
        var scopedAreas = caller.IsAdministrator
            ? request.AreaId is null ? request.ScopeAreaIds : new HashSet<string> { request.AreaId }
            : request.AreaId is null
                ? caller.AreaIds
                : caller.AreaIds.Contains(request.AreaId) ? new HashSet<string> { request.AreaId } : [];
        var scopedRequest = request with
        {
            ScopeSiteIds = scopedSites,
            ScopeAreaIds = scopedAreas,
            CorrelationId = caller.CanReadCorrelation ? request.CorrelationId : null
        };
        var rows = await repository.QueryAsync(scopedRequest, ct);
        var ordered = rows.Where(row => authorization.CanQuery(caller, row))
            .OrderByDescending(row => row.OccurredAtUtc).ThenByDescending(row => row.AuditEventId)
            .Where(row => IsAfterCursor(row, request.KeysetCursor))
            .ToArray();
        var hasMore = ordered.Length > request.PageSize;
        var visible = ordered.Take(request.PageSize)
            .Select(row => AuditRedaction.ForCaller(row, caller))
            .ToArray();
        return new(visible, null, visible.Length)
        {
            NextCursor = hasMore && visible.Length > 0
                ? new AuditKeysetCursor(visible[^1].OccurredAtUtc, visible[^1].AuditEventId).Encode()
                : null
        };
    }

    private static bool IsAfterCursor(AuditEventRecord row, string? keysetCursor)
    {
        if (!AuditKeysetCursor.TryDecode(keysetCursor, out var cursor)) return true;
        return row.OccurredAtUtc < cursor.OccurredAtUtc ||
            (row.OccurredAtUtc == cursor.OccurredAtUtc && row.AuditEventId.CompareTo(cursor.AuditEventId) < 0);
    }
}
