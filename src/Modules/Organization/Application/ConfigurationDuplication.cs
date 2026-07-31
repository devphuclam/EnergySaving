using System.Collections.ObjectModel;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;

namespace IUMP.Modules.Organization.Application;

public sealed record OrganizationDuplicationOutcome(
    bool IsSuccess,
    string Code,
    string? Error,
    Guid? NewId = null,
    string? ProposedCode = null,
    string? ProposedName = null,
    string? Status = null,
    long Version = 0,
    IReadOnlyList<string> ReviewRelationships = null!)
{
    public static OrganizationDuplicationOutcome Success(Guid newId, string code, string name,
        IReadOnlyList<string> relationships) =>
        new(true, "OK", null, newId, code, name, "Draft", 1, relationships);

    public static OrganizationDuplicationOutcome Failure(string code, string error) =>
        new(false, code, error, null, null, null, null, 0, Array.Empty<string>());
}

/// <summary>
/// Owner-domain duplicate-to-Draft behavior. A duplicate always receives a new identity,
/// a unique proposed code/name, Draft status, and version 1. It never copies Active state,
/// optimistic versions, lifecycle history, Audit, sessions, credentials, or secrets;
/// parent references are returned as reviewable relationships for explicit review.
/// </summary>
public sealed class OrganizationDuplicationService
{
    private readonly IOrganizationCommandRepository _repo;
    private readonly IOrganizationAuthorization _auth;
    private readonly List<OrganizationEvent> _events = new();
    private OrganizationCallerSnapshot? _currentCaller;

    public IReadOnlyList<OrganizationEvent> Events => _events.AsReadOnly();

    public OrganizationDuplicationService(IOrganizationCommandRepository repo, IOrganizationAuthorization auth)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
    }

    public async Task<OrganizationDuplicationOutcome> DuplicateSiteAsync(
        SiteId sourceId, string actorUserId, CancellationToken ct = default)
    {
        var denied = await AuthorizeAsync(actorUserId, OrganizationResource.RootSite,
            sourceId.ToString(), null, ct);
        if (denied is not null) return denied;
        var site = await _repo.GetSiteAsync(sourceId, ct);
        if (site is null) return OrganizationDuplicationOutcome.Failure("NotFound", "Site not found.");
        try
        {
            var proposedCode = await UniqueCodeAsync(
                value => _repo.FindSiteByCodeAsync(value, ct).ContinueWith(task => task.Result is not null),
                site.Code, ct);
            var copy = new Site(SiteId.New(), proposedCode, site.Name, site.Description,
                site.Timezone, SiteStatus.Draft, 1);
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.AddSiteAsync(copy, ct);
            await tx.CommitAsync(ct);
            AddEvent("SiteStatusChanged.v1", "Site", copy.Id.ToString(), copy.Version, ctx(actorUserId),
                "Duplicated", "Site duplicated as Draft",
                SiteSnapshot(site), SiteSnapshot(copy), copy.Id.ToString());
            return OrganizationDuplicationOutcome.Success(copy.Id.Value, copy.Code, copy.Name, []);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return OrganizationDuplicationOutcome.Failure(
                ex is InvalidOperationException ? "Conflict" : "Validation", ex.Message);
        }
    }

    public async Task<OrganizationDuplicationOutcome> DuplicateAreaAsync(
        AreaId sourceId, string actorUserId, CancellationToken ct = default)
    {
        var scope = await _repo.GetAreaScopeAsync(sourceId, ct);
        if (scope is null) return OrganizationDuplicationOutcome.Failure("NotFound", "Area not found.");
        var denied = await AuthorizeAsync(actorUserId, OrganizationResource.SiteChild,
            scope.SiteId.ToString(), scope.AreaId?.ToString(), ct);
        if (denied is not null) return denied;
        var area = await _repo.GetAreaAsync(sourceId, ct);
        if (area is null) return OrganizationDuplicationOutcome.Failure("NotFound", "Area not found.");
        try
        {
            var proposedCode = await UniqueCodeAsync(
                value => _repo.FindAreaByCodeAsync(area.SiteId, value, ct)
                    .ContinueWith(task => task.Result is not null),
                area.Code, ct);
            var copy = new Area(AreaId.New(), area.SiteId, proposedCode, area.Name,
                area.Description, AreaStatus.Draft, 1);
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.AddAreaAsync(copy, ct);
            await tx.CommitAsync(ct);
            AddEvent("AreaStatusChanged.v1", "Area", copy.Id.ToString(), copy.Version, ctx(actorUserId),
                "Duplicated", "Area duplicated as Draft",
                AreaSnapshot(area), AreaSnapshot(copy), copy.SiteId.ToString(), copy.Id.ToString());
            return OrganizationDuplicationOutcome.Success(copy.Id.Value, copy.Code, copy.Name,
                [$"site:{copy.SiteId.Value:D}"]);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return OrganizationDuplicationOutcome.Failure(
                ex is InvalidOperationException ? "Conflict" : "Validation", ex.Message);
        }
    }

    public async Task<OrganizationDuplicationOutcome> DuplicateAssetAsync(
        AssetId sourceId, string actorUserId, CancellationToken ct = default)
    {
        var scope = await _repo.GetAssetScopeAsync(sourceId, ct);
        if (scope is null) return OrganizationDuplicationOutcome.Failure("NotFound", "Asset not found.");
        var denied = await AuthorizeAsync(actorUserId, OrganizationResource.SiteChild,
            scope.SiteId.ToString(), scope.AreaId?.ToString(), ct);
        if (denied is not null) return denied;
        var asset = await _repo.GetAssetAsync(sourceId, ct);
        if (asset is null) return OrganizationDuplicationOutcome.Failure("NotFound", "Asset not found.");
        try
        {
            var proposedCode = await UniqueCodeAsync(
                value => _repo.FindAssetByCodeAsync(asset.AreaId, value, ct)
                    .ContinueWith(task => task.Result is not null),
                asset.Code, ct);
            var copy = new Asset(AssetId.New(), asset.SiteId, asset.AreaId, proposedCode,
                asset.Name, asset.Description, AssetStatus.Draft, 1);
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.AddAssetAsync(copy, ct);
            await tx.CommitAsync(ct);
            AddEvent("AssetStatusChanged.v1", "Asset", copy.Id.ToString(), copy.Version, ctx(actorUserId),
                "Duplicated", "Asset duplicated as Draft",
                AssetSnapshot(asset), AssetSnapshot(copy), copy.SiteId.ToString(), copy.AreaId.ToString());
            return OrganizationDuplicationOutcome.Success(copy.Id.Value, copy.Code, copy.Name,
                [$"site:{copy.SiteId.Value:D}", $"area:{copy.AreaId.Value:D}"]);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return OrganizationDuplicationOutcome.Failure(
                ex is InvalidOperationException ? "Conflict" : "Validation", ex.Message);
        }
    }

    public async Task<OrganizationDuplicationOutcome> DuplicatePointAsync(
        PointId sourceId, string actorUserId, CancellationToken ct = default)
    {
        var scope = await _repo.GetPointScopeAsync(sourceId, ct);
        if (scope is null) return OrganizationDuplicationOutcome.Failure("NotFound", "Point not found.");
        var denied = await AuthorizeAsync(actorUserId, OrganizationResource.SiteChild,
            scope.SiteId.ToString(), scope.AreaId?.ToString(), ct);
        if (denied is not null) return denied;
        var point = await _repo.GetPointAsync(sourceId, ct);
        if (point is null) return OrganizationDuplicationOutcome.Failure("NotFound", "Point not found.");
        try
        {
            var proposedCode = await UniqueCodeAsync(
                value => _repo.IsPointCodeReservedAsync(point.SiteId, value, ct),
                point.Code, ct);
            var copy = new MeasurementPoint(PointId.New(), point.SiteId, point.AreaId, point.AssetId,
                proposedCode, point.Description, point.MetricId, point.UnitId, point.DataOwnerUserId,
                point.ExpectedIntervalSeconds, point.NoDataAfterSeconds, PointStatus.Draft, 1);
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.AddPointAsync(copy, ct);
            await tx.CommitAsync(ct);
            AddEvent("PointConfigurationChanged.v1", "Point", copy.Id.ToString(), copy.Version, ctx(actorUserId),
                "Duplicated", "Point duplicated as Draft",
                PointSnapshot(point), PointSnapshot(copy), copy.SiteId.ToString(), copy.AreaId.ToString());
            return OrganizationDuplicationOutcome.Success(copy.Id.Value, copy.Code, copy.Code,
            [
                $"site:{copy.SiteId.Value:D}", $"area:{copy.AreaId.Value:D}", $"asset:{copy.AssetId.Value:D}",
                $"metric:{copy.MetricId}", $"unit:{copy.UnitId}", $"dataOwner:{copy.DataOwnerUserId}"
            ]);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return OrganizationDuplicationOutcome.Failure(
                ex is InvalidOperationException ? "Conflict" : "Validation", ex.Message);
        }
    }

    private static async Task<string> UniqueCodeAsync(
        Func<string, Task<bool>> isTaken, string baseCode, CancellationToken ct)
    {
        var candidate = $"{baseCode}-COPY";
        var suffix = 2;
        while (await isTaken(candidate))
        {
            candidate = $"{baseCode}-COPY{suffix}";
            suffix++;
        }
        return candidate;
    }

    private static OrganizationCommandContext ctx(string actorUserId) =>
        new(actorUserId, null, null);

    private async Task<OrganizationDuplicationOutcome?> AuthorizeAsync(
        string userId, OrganizationResource resource, string? siteId, string? areaId, CancellationToken ct)
    {
        var decision = await _auth.AuthorizeTargetAsync(userId, resource, siteId, areaId, ct);
        _currentCaller = decision.IsAllowed ? await _auth.ResolveCallerAsync(userId, ct) : null;
        return decision.IsAllowed
            ? null
            : OrganizationDuplicationOutcome.Failure(decision.Code, decision.Error ?? "Not authorized.");
    }

    private void AddEvent(string eventType, string aggregateType, string aggregateId, long version,
        OrganizationCommandContext context, string action, string summary,
        IReadOnlyDictionary<string, object?> before, IReadOnlyDictionary<string, object?> after,
        string? siteId, string? areaId = null)
    {
        _events.Add(new OrganizationEvent(Guid.NewGuid(), eventType, "1", "IUMP.Organization",
            aggregateType, aggregateId, version,
            context.ActorUserId, _currentCaller?.Username ?? context.ActorUserId,
            before, after, action, summary, DateTime.UtcNow,
            context.CorrelationId, context.CausationId, siteId, areaId));
    }

    private static IReadOnlyDictionary<string, object?> SiteSnapshot(Site site) =>
        MakeSnap(("code", site.Code), ("name", site.Name), ("description", site.Description),
            ("timezone", site.Timezone), ("status", site.Status.ToString()));

    private static IReadOnlyDictionary<string, object?> AreaSnapshot(Area area) =>
        MakeSnap(("siteId", area.SiteId.ToString()), ("areaId", area.Id.ToString()), ("code", area.Code),
            ("name", area.Name), ("description", area.Description), ("status", area.Status.ToString()));

    private static IReadOnlyDictionary<string, object?> AssetSnapshot(Asset asset) =>
        MakeSnap(("siteId", asset.SiteId.ToString()), ("areaId", asset.AreaId.ToString()), ("code", asset.Code),
            ("name", asset.Name), ("description", asset.Description), ("status", asset.Status.ToString()));

    private static IReadOnlyDictionary<string, object?> PointSnapshot(MeasurementPoint point) =>
        MakeSnap(("siteId", point.SiteId.ToString()), ("areaId", point.AreaId.ToString()),
            ("assetId", point.AssetId.ToString()), ("code", point.Code), ("description", point.Description),
            ("metricId", point.MetricId), ("unitId", point.UnitId), ("dataOwnerUserId", point.DataOwnerUserId),
            ("expectedIntervalSeconds", point.ExpectedIntervalSeconds),
            ("noDataAfterSeconds", point.NoDataAfterSeconds), ("status", point.Status.ToString()));

    private static IReadOnlyDictionary<string, object?> MakeSnap(params (string Key, object? Value)[] values)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Event snapshot keys are required.");
            map[key] = value;
        }
        return new ReadOnlyDictionary<string, object?>(map);
    }

    private sealed class AsyncTransaction : IAsyncDisposable
    {
        private readonly IOrganizationTransaction _inner;
        private bool _committed;
        public AsyncTransaction(IOrganizationTransaction inner) => _inner = inner;
        public async Task CommitAsync(CancellationToken ct) { await _inner.CommitAsync(ct); _committed = true; }
        public async ValueTask DisposeAsync()
        {
            if (!_committed) await _inner.RollbackAsync();
        }
    }
}
