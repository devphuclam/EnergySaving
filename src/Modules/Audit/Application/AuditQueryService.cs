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
        // Scope is applied before paging/keyset so an unauthorized row can never consume a page slot.
        var scopedRequest = request with { ScopeSiteIds = caller.SiteIds, ScopeAreaIds = caller.AreaIds };
        var rows = await repository.QueryAsync(scopedRequest, ct);
        var visible = rows.Where(row => authorization.CanQuery(caller, row))
            .OrderByDescending(row => row.OccurredAtUtc).ThenByDescending(row => row.AuditEventId)
            .Where(row => IsAfterCursor(row, request.KeysetCursor))
            .Take(Math.Clamp(request.PageSize, 1, 100)).ToArray();
        return new(visible, null, visible.Length);
    }

    private static bool IsAfterCursor(AuditEventRecord row, string? keysetCursor)
    {
        if (string.IsNullOrWhiteSpace(keysetCursor)) return true;
        return !string.Equals(row.AuditEventId.ToString("D"), keysetCursor, StringComparison.OrdinalIgnoreCase);
    }
}
