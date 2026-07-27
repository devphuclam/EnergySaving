using System.Collections.ObjectModel;
using System.Text.Json;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;

namespace IUMP.Modules.Organization.Application;

public enum OrganizationResource { RootSite, SiteChild, Area, Asset, Point }

public sealed record OrganizationEvent(
    Guid EventId,
    string EventType,
    string SchemaVersion,
    string Producer,
    string AggregateType,
    string AggregateId,
    long AggregateVersion,
    string ActorId,
    string ActorUsername,
    IReadOnlyDictionary<string, object?> Before,
    IReadOnlyDictionary<string, object?> After,
    string Action,
    string Summary,
    DateTime OccurredAt,
    string? CorrelationId,
    string? CausationId,
    string? SiteId,
    string? AreaId);

public sealed record OrganizationCommandContext(
    string ActorUserId,
    string? CorrelationId,
    string? CausationId);

public sealed record CreateSiteCommand(string Code, string Name, string? Description, string Timezone, string RequestedByUserId);
public sealed record UpdateSiteCommand(SiteId SiteId, string Name, string? Description, string Timezone, long ExpectedVersion, string RequestedByUserId);
public sealed record UpdateSiteStatusCommand(SiteId SiteId, string Action, long ExpectedVersion, string RequestedByUserId);
public sealed record CreateAreaCommand(SiteId SiteId, string Code, string Name, string? Description, string RequestedByUserId);
public sealed record UpdateAreaCommand(AreaId AreaId, string Name, string? Description, long ExpectedVersion, string RequestedByUserId);
public sealed record UpdateAreaStatusCommand(AreaId AreaId, string Action, long ExpectedVersion, string RequestedByUserId);
public sealed record CreateAssetCommand(SiteId SiteId, AreaId AreaId, string Code, string Name, string? Description, string RequestedByUserId);
public sealed record UpdateAssetCommand(AssetId AssetId, string Name, string? Description, long ExpectedVersion, string RequestedByUserId);
public sealed record UpdateAssetStatusCommand(AssetId AssetId, string Action, long ExpectedVersion, string RequestedByUserId);
public sealed record DecommissionAssetCommand(AssetId AssetId, long ExpectedVersion, string RequestedByUserId);
public sealed record CreatePointCommand(SiteId SiteId, AreaId AreaId, AssetId AssetId, string Code, string? Description,
    string MetricId, string UnitId, string DataOwnerUserId, int ExpectedIntervalSeconds, int NoDataAfterSeconds, string RequestedByUserId);
public sealed record UpdatePointConfigurationCommand(PointId PointId, string? Description, string MetricId, string UnitId,
    string DataOwnerUserId, int ExpectedIntervalSeconds, int NoDataAfterSeconds, long ExpectedVersion, string RequestedByUserId);
public sealed record UpdatePointStatusCommand(PointId PointId, string Action, long ExpectedVersion, string RequestedByUserId);
public sealed record DecommissionPointCommand(PointId PointId, long ExpectedVersion, string RequestedByUserId);

public interface IOrganizationAuthorization
{
    Task<OrganizationAuthorizationDecision> AuthorizeAsync(
        string requestedByUserId,
        OrganizationResource resource,
        string? targetSiteId = null,
        CancellationToken ct = default);

    Task<OrganizationCallerSnapshot?> ResolveCallerAsync(string requestedByUserId, CancellationToken ct = default);
}

public sealed class OrganizationRoleScopeAuthorization : IOrganizationAuthorization
{
    private readonly IOrganizationCallerSnapshotProvider _provider;

    public OrganizationRoleScopeAuthorization(IOrganizationCallerSnapshotProvider provider) => _provider = provider;

    public async Task<OrganizationAuthorizationDecision> AuthorizeAsync(
        string requestedByUserId, OrganizationResource resource, string? targetSiteId = null, CancellationToken ct = default)
    {
        var caller = await _provider.ResolveAsync(requestedByUserId, ct);
        if (caller is null || !caller.IsActive) return OrganizationAuthorizationDecision.Forbidden();
        if (caller.HasRole("Administrator")) return OrganizationAuthorizationDecision.Allowed();
        if (!caller.HasRole("Engineer")) return OrganizationAuthorizationDecision.Forbidden();

        // Administrator-only for root Site
        if (resource == OrganizationResource.RootSite)
            return OrganizationAuthorizationDecision.Forbidden("Engineers cannot create root Sites.");

        if (string.IsNullOrWhiteSpace(targetSiteId))
            return caller.SiteScopes.Count > 0 ? OrganizationAuthorizationDecision.Allowed() : OrganizationAuthorizationDecision.Forbidden();
        return caller.HasSiteScope(targetSiteId) ? OrganizationAuthorizationDecision.Allowed() : OrganizationAuthorizationDecision.NotFound();
    }

    public Task<OrganizationCallerSnapshot?> ResolveCallerAsync(string requestedByUserId, CancellationToken ct = default) =>
        _provider.ResolveAsync(requestedByUserId, ct);
}

public interface IRunningSimulatorQuery
{
    Task<bool> HasRunningSimulatorAsync(string pointId, CancellationToken ct = default);
}

public sealed class OrganizationCommandHandler
{
    private readonly IOrganizationCommandRepository _repo;
    private readonly IOrganizationAuthorization _auth;
    private readonly IRunningSimulatorQuery _simQuery;
    private readonly List<OrganizationEvent> _events = new();
    private OrganizationCallerSnapshot? _currentCaller;

    public IReadOnlyList<OrganizationEvent> Events => _events.AsReadOnly();
    public bool HasEvents => _events.Count > 0;

    public OrganizationCommandHandler(IOrganizationCommandRepository repo, IOrganizationAuthorization auth,
        IRunningSimulatorQuery simQuery)
    {
        _repo = repo;
        _auth = auth;
        _simQuery = simQuery ?? throw new ArgumentNullException(nameof(simQuery));
    }

    public async Task<Result> HandleAsync(CreateSiteCommand cmd, OrganizationCommandContext ctx, CancellationToken ct = default)
    {
        var denied = await Authorize(ctx.ActorUserId, OrganizationResource.RootSite, null, ct);
        if (denied is not null) return denied;
        if (string.IsNullOrWhiteSpace(cmd.Code) || cmd.Code.Length > 50 || string.IsNullOrWhiteSpace(cmd.Name) || cmd.Name.Length > 200)
            return Result.Failure("Validation", "Site code and name are required and bounded.");
        if (string.IsNullOrWhiteSpace(cmd.Timezone)) return Result.Failure("Validation", "Timezone is required.");
        var normalized = Domain.Site.NormalizeCode(cmd.Code);
        if (await _repo.FindSiteByCodeAsync(normalized, ct) is not null)
            return Result.Failure("Conflict", "Site code already exists.");
        try
        {
            var site = new Domain.Site(Domain.SiteId.New(), cmd.Code, cmd.Name, cmd.Description, cmd.Timezone, SiteStatus.Draft, 1);
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.AddSiteAsync(site, ct);
            await tx.CommitAsync(ct);
            AddEvent("SiteStatusChanged.v1", "Site", site.Id.ToString(), site.Version, ctx,
                "Created", "Site created", EmptySnap(),
                SiteSnapshot(site), site.Id.ToString());
            return Result.Success();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result.Failure(ex is InvalidOperationException ? "Conflict" : "Validation", ex.Message);
        }
    }

    public async Task<Result> HandleAsync(UpdateSiteStatusCommand cmd, OrganizationCommandContext ctx, CancellationToken ct = default)
    {
        var denied = await Authorize(ctx.ActorUserId, OrganizationResource.RootSite, cmd.SiteId.ToString(), ct);
        if (denied is not null) return denied;
        var site = await _repo.GetSiteAsync(cmd.SiteId, ct);
        if (site is null) return Result.Failure("NotFound", "Site not found.");
        if (cmd.ExpectedVersion != site.Version) return VersionConflict();
        var before = site.Status.ToString();
        var changed = cmd.Action.ToLowerInvariant() switch
        {
            "activate" => site.TryActivate(),
            "inactivate" => site.TryInactivate(),
            "reactivate" => site.TryReactivate(),
            _ => false
        };
        if (!changed) return Result.Failure(cmd.Action.ToLowerInvariant() switch
        {
            "activate" when site.Status == SiteStatus.Active => "Validation",
            "inactivate" when site.Status == SiteStatus.Inactive => "Validation",
            _ => "InvalidTransition"
        }, "No state change performed.");
        try
        {
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.UpdateSiteAsync(site, ct);
            await tx.CommitAsync(ct);
            AddEvent("SiteStatusChanged.v1", "Site", site.Id.ToString(), site.Version, ctx,
                cmd.Action + "d", "Site status changed",
                SiteSnapshot(site, before), SiteSnapshot(site), site.Id.ToString());
            return Result.Success();
        }
        catch (InvalidOperationException ex) { return Result.Failure("VERSION_CONFLICT", ex.Message); }
    }

    public async Task<Result> HandleAsync(UpdateSiteCommand cmd, OrganizationCommandContext ctx, CancellationToken ct = default)
    {
        var denied = await Authorize(ctx.ActorUserId, OrganizationResource.RootSite, cmd.SiteId.ToString(), ct);
        if (denied is not null) return denied;
        var site = await _repo.GetSiteAsync(cmd.SiteId, ct);
        if (site is null) return Result.Failure("NotFound", "Site not found.");
        if (cmd.ExpectedVersion != site.Version) return VersionConflict();
        Site before = Clone(site);
        try
        {
            var changed = site.TryUpdate(cmd.Name, cmd.Description, cmd.Timezone);
            if (!changed) return Result.Success();
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.UpdateSiteAsync(site, ct);
            await tx.CommitAsync(ct);
            AddEvent("SiteStatusChanged.v1", "Site", site.Id.ToString(), site.Version, ctx,
                "Updated", "Site configuration updated", SiteSnapshot(before), SiteSnapshot(site), site.Id.ToString());
            return Result.Success();
        }
        catch (ArgumentException ex) { return Result.Failure("Validation", ex.Message); }
        catch (InvalidOperationException ex) { return Result.Failure("VERSION_CONFLICT", ex.Message); }
    }

    public async Task<Result> HandleAsync(CreateAreaCommand cmd, OrganizationCommandContext ctx, CancellationToken ct = default)
    {
        var denied = await Authorize(ctx.ActorUserId, OrganizationResource.SiteChild, cmd.SiteId.ToString(), ct);
        if (denied is not null) return denied;
        var site = await _repo.GetSiteAsync(cmd.SiteId, ct);
        if (site is null) return Result.Failure("NotFound", "Parent Site not found.");
        if (site.Status == SiteStatus.Inactive) return Result.Failure("PARENT_NOT_CONFIGURABLE", "Area cannot be created under an Inactive Site.");
        var normalized = Domain.Site.NormalizeCode(cmd.Code);
        if (await _repo.FindAreaByCodeAsync(cmd.SiteId, normalized, ct) is not null)
            return Result.Failure("Conflict", "Area code already exists in this Site.");
        try
        {
            var area = new Domain.Area(Domain.AreaId.New(), cmd.SiteId, cmd.Code, cmd.Name, cmd.Description, AreaStatus.Draft, 1);
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.AddAreaAsync(area, ct);
            await tx.CommitAsync(ct);
            AddEvent("AreaStatusChanged.v1", "Area", area.Id.ToString(), area.Version, ctx,
                "Created", "Area created", EmptySnap(),
                AreaSnapshot(area), area.SiteId.ToString(), area.Id.ToString());
            return Result.Success();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { return Result.Failure(ex is InvalidOperationException ? "Conflict" : "Validation", ex.Message); }
    }

    public async Task<Result> HandleAsync(UpdateAreaStatusCommand cmd, OrganizationCommandContext ctx, CancellationToken ct = default)
    {
        var scope = await _repo.GetAreaScopeAsync(cmd.AreaId, ct);
        if (scope is null) return Result.Failure("NotFound", "Area not found.");
        var denied = await Authorize(ctx.ActorUserId, OrganizationResource.SiteChild, scope.SiteId.ToString(), ct);
        if (denied is not null) return denied;
        var area = await _repo.GetAreaAsync(cmd.AreaId, ct);
        if (area is null) return Result.Failure("NotFound", "Area not found.");
        if (cmd.ExpectedVersion != area.Version) return VersionConflict();
        // Area activation requires Active Site
        if (cmd.Action.Equals("activate", StringComparison.OrdinalIgnoreCase) && area.Status == AreaStatus.Draft)
        {
            var parentSite = await _repo.GetSiteAsync(area.SiteId, ct);
            if (parentSite is null || !parentSite.IsActive)
                return Result.Failure("PARENT_NOT_ACTIVE", "Cannot activate Area because parent Site is not Active.");
        }
        var before = area.Status.ToString();
        var changed = cmd.Action.ToLowerInvariant() switch
        {
            "activate" => area.TryActivate(),
            "inactivate" => area.TryInactivate(),
            "reactivate" => area.TryReactivate(),
            _ => false
        };
        if (!changed) return Result.Failure("InvalidTransition", "No state change performed.");
        try
        {
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.UpdateAreaAsync(area, ct);
            await tx.CommitAsync(ct);
            AddEvent("AreaStatusChanged.v1", "Area", area.Id.ToString(), area.Version, ctx,
                cmd.Action + "d", "Area status changed",
                AreaSnapshot(area, before), AreaSnapshot(area), area.SiteId.ToString(), area.Id.ToString());
            return Result.Success();
        }
        catch (InvalidOperationException ex) { return Result.Failure("VERSION_CONFLICT", ex.Message); }
    }

    public async Task<Result> HandleAsync(UpdateAreaCommand cmd, OrganizationCommandContext ctx, CancellationToken ct = default)
    {
        var scope = await _repo.GetAreaScopeAsync(cmd.AreaId, ct);
        if (scope is null) return Result.Failure("NotFound", "Area not found.");
        var denied = await Authorize(ctx.ActorUserId, OrganizationResource.SiteChild, scope.SiteId.ToString(), ct);
        if (denied is not null) return denied;
        var area = await _repo.GetAreaAsync(cmd.AreaId, ct);
        if (area is null) return Result.Failure("NotFound", "Area not found.");
        if (cmd.ExpectedVersion != area.Version) return VersionConflict();
        var before = Clone(area);
        try
        {
            if (!area.TryUpdate(cmd.Name, cmd.Description)) return Result.Success();
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.UpdateAreaAsync(area, ct);
            await tx.CommitAsync(ct);
            AddEvent("AreaStatusChanged.v1", "Area", area.Id.ToString(), area.Version, ctx,
                "Updated", "Area configuration updated", AreaSnapshot(before), AreaSnapshot(area), area.SiteId.ToString(), area.Id.ToString());
            return Result.Success();
        }
        catch (ArgumentException ex) { return Result.Failure("Validation", ex.Message); }
        catch (InvalidOperationException ex) { return Result.Failure("VERSION_CONFLICT", ex.Message); }
    }

    public async Task<Result> HandleAsync(CreateAssetCommand cmd, OrganizationCommandContext ctx, CancellationToken ct = default)
    {
        var areaScope = await _repo.GetAreaScopeAsync(cmd.AreaId, ct);
        if (areaScope is null) return Result.Failure("NotFound", "Parent Area not found.");
        var denied = await Authorize(ctx.ActorUserId, OrganizationResource.SiteChild, areaScope.SiteId.ToString(), ct);
        if (denied is not null) return denied;
        var area = await _repo.GetAreaAsync(cmd.AreaId, ct);
        if (area is null) return Result.Failure("NotFound", "Parent Area not found.");
        if (area.Status == AreaStatus.Inactive) return Result.Failure("PARENT_NOT_CONFIGURABLE", "Asset cannot be created under an Inactive Area.");
        if (cmd.SiteId != area.SiteId)
            return Result.Failure("NotFound", "Parent hierarchy does not match the requested scope.");
        var normalized = Domain.Site.NormalizeCode(cmd.Code);
        if (await _repo.FindAssetByCodeAsync(cmd.AreaId, normalized, ct) is not null)
            return Result.Failure("Conflict", "Asset code already exists in this Area.");
        try
        {
            var asset = new Domain.Asset(Domain.AssetId.New(), area.SiteId, area.Id, cmd.Code, cmd.Name, cmd.Description, AssetStatus.Draft, 1);
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.AddAssetAsync(asset, ct);
            await tx.CommitAsync(ct);
            AddEvent("AssetStatusChanged.v1", "Asset", asset.Id.ToString(), asset.Version, ctx,
                "Created", "Asset created", EmptySnap(),
                AssetSnapshot(asset), asset.SiteId.ToString(), asset.AreaId.ToString());
            return Result.Success();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { return Result.Failure(ex is InvalidOperationException ? "Conflict" : "Validation", ex.Message); }
    }

    public async Task<Result> HandleAsync(UpdateAssetStatusCommand cmd, OrganizationCommandContext ctx, CancellationToken ct = default)
    {
        var scope = await _repo.GetAssetScopeAsync(cmd.AssetId, ct);
        if (scope is null) return Result.Failure("NotFound", "Asset not found.");
        var denied = await Authorize(ctx.ActorUserId, OrganizationResource.SiteChild, scope.SiteId.ToString(), ct);
        if (denied is not null) return denied;
        var asset = await _repo.GetAssetAsync(cmd.AssetId, ct);
        if (asset is null) return Result.Failure("NotFound", "Asset not found.");
        if (cmd.ExpectedVersion != asset.Version) return VersionConflict();
        if (cmd.Action.Equals("activate", StringComparison.OrdinalIgnoreCase) && asset.Status == AssetStatus.Draft)
        {
            var parentArea = await _repo.GetAreaAsync(asset.AreaId, ct);
            if (parentArea is null || !parentArea.IsActive)
                return Result.Failure("PARENT_NOT_ACTIVE", "Cannot activate Asset because parent Area is not Active.");
        }
        var before = asset.Status.ToString();
        var changed = cmd.Action.ToLowerInvariant() switch
        {
            "activate" => asset.TryActivate(),
            "inactivate" => asset.TryInactivate(),
            "reactivate" => asset.TryReactivate(),
            _ => false
        };
        if (!changed) return Result.Failure("InvalidTransition", "No state change performed.");
        try
        {
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.UpdateAssetAsync(asset, ct);
            await tx.CommitAsync(ct);
            AddEvent("AssetStatusChanged.v1", "Asset", asset.Id.ToString(), asset.Version, ctx,
                cmd.Action + "d", "Asset status changed",
                AssetSnapshot(asset, before), AssetSnapshot(asset), asset.SiteId.ToString(), asset.AreaId.ToString());
            return Result.Success();
        }
        catch (InvalidOperationException ex) { return Result.Failure("VERSION_CONFLICT", ex.Message); }
    }

    public async Task<Result> HandleAsync(UpdateAssetCommand cmd, OrganizationCommandContext ctx, CancellationToken ct = default)
    {
        var scope = await _repo.GetAssetScopeAsync(cmd.AssetId, ct);
        if (scope is null) return Result.Failure("NotFound", "Asset not found.");
        var denied = await Authorize(ctx.ActorUserId, OrganizationResource.SiteChild, scope.SiteId.ToString(), ct);
        if (denied is not null) return denied;
        var asset = await _repo.GetAssetAsync(cmd.AssetId, ct);
        if (asset is null) return Result.Failure("NotFound", "Asset not found.");
        if (cmd.ExpectedVersion != asset.Version) return VersionConflict();
        var before = Clone(asset);
        try
        {
            if (!asset.TryUpdate(cmd.Name, cmd.Description)) return Result.Success();
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.UpdateAssetAsync(asset, ct);
            await tx.CommitAsync(ct);
            AddEvent("AssetStatusChanged.v1", "Asset", asset.Id.ToString(), asset.Version, ctx,
                "Updated", "Asset configuration updated", AssetSnapshot(before), AssetSnapshot(asset), asset.SiteId.ToString(), asset.AreaId.ToString());
            return Result.Success();
        }
        catch (ArgumentException ex) { return Result.Failure("Validation", ex.Message); }
        catch (InvalidOperationException ex) { return Result.Failure("VERSION_CONFLICT", ex.Message); }
    }

    public async Task<Result> HandleAsync(DecommissionAssetCommand cmd, OrganizationCommandContext ctx, CancellationToken ct = default)
    {
        var scope = await _repo.GetAssetScopeAsync(cmd.AssetId, ct);
        if (scope is null) return Result.Failure("NotFound", "Asset not found.");
        var denied = await Authorize(ctx.ActorUserId, OrganizationResource.SiteChild, scope.SiteId.ToString(), ct);
        if (denied is not null) return denied;
        var asset = await _repo.GetAssetAsync(cmd.AssetId, ct);
        if (asset is null) return Result.Failure("NotFound", "Asset not found.");
        if (cmd.ExpectedVersion != asset.Version) return VersionConflict();
        var children = await _repo.GetPointsForAssetAsync(cmd.AssetId, ct);
        var decision = DecommissionPolicy.EvaluateAsset(asset, children);
        if (!decision.IsAllowed)
            return Result.Failure(decision.Code, decision.Code == "ACTIVE_CHILD_POINT"
                ? "Cannot decommission Asset while child Point is Active."
                : "Asset cannot be decommissioned in its current state.");
        var before = asset.Status.ToString();
        if (!asset.TryDecommission()) return Result.Failure("InvalidTransition", "Asset cannot be decommissioned in its current state.");
        try
        {
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.UpdateAssetAsync(asset, ct);
            await tx.CommitAsync(ct);
            AddEvent("AssetStatusChanged.v1", "Asset", asset.Id.ToString(), asset.Version, ctx,
                "Decommissioned", "Asset decommissioned",
                AssetSnapshot(asset, before), AssetSnapshot(asset), asset.SiteId.ToString(), asset.AreaId.ToString());
            return Result.Success();
        }
        catch (InvalidOperationException ex) { return Result.Failure("VERSION_CONFLICT", ex.Message); }
    }

    public async Task<Result> HandleAsync(CreatePointCommand cmd, OrganizationCommandContext ctx, CancellationToken ct = default)
    {
        var assetScope = await _repo.GetAssetScopeAsync(cmd.AssetId, ct);
        if (assetScope is null) return Result.Failure("NotFound", "Parent Asset not found.");
        var denied = await Authorize(ctx.ActorUserId, OrganizationResource.SiteChild, assetScope.SiteId.ToString(), ct);
        if (denied is not null) return denied;
        var asset = await _repo.GetAssetAsync(cmd.AssetId, ct);
        if (asset is null) return Result.Failure("NotFound", "Parent Asset not found.");
        var area = await _repo.GetAreaAsync(asset.AreaId, ct);
        if (area is null) return Result.Failure("NotFound", "Parent Area not found.");
        if (asset.Status is AssetStatus.Inactive or AssetStatus.Decommissioned)
            return Result.Failure("PARENT_NOT_CONFIGURABLE", "Point cannot be created under this Asset status.");
        if (cmd.SiteId != asset.SiteId || cmd.AreaId != asset.AreaId || area.SiteId != asset.SiteId)
            return Result.Failure("NotFound", "Parent hierarchy does not match the requested scope.");
        var normalized = Domain.Site.NormalizeCode(cmd.Code);
        if (await _repo.IsPointCodeReservedAsync(asset.SiteId, normalized, ct))
            return Result.Failure("Conflict", "Point code already exists in this Site.");
        try
        {
            var point = new MeasurementPoint(Domain.PointId.New(), asset.SiteId, asset.AreaId, asset.Id,
                cmd.Code, cmd.Description, cmd.MetricId, cmd.UnitId, cmd.DataOwnerUserId,
                cmd.ExpectedIntervalSeconds, cmd.NoDataAfterSeconds, PointStatus.Draft, 1);
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.AddPointAsync(point, ct);
            await tx.CommitAsync(ct);
            AddEvent("PointConfigurationChanged.v1", "Point", point.Id.ToString(), point.Version, ctx,
                "Created", "Point created", EmptySnap(),
                PointSnapshot(point), point.SiteId.ToString(), point.AreaId.ToString());
            return Result.Success();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { return Result.Failure(ex is InvalidOperationException ? "Conflict" : "Validation", ex.Message); }
    }

    public async Task<Result> HandleAsync(UpdatePointStatusCommand cmd, OrganizationCommandContext ctx, CancellationToken ct = default)
    {
        var scope = await _repo.GetPointScopeAsync(cmd.PointId, ct);
        if (scope is null) return Result.Failure("NotFound", "Point not found.");
        var denied = await Authorize(ctx.ActorUserId, OrganizationResource.SiteChild, scope.SiteId.ToString(), ct);
        if (denied is not null) return denied;
        var point = await _repo.GetPointAsync(cmd.PointId, ct);
        if (point is null) return Result.Failure("NotFound", "Point not found.");
        if (cmd.ExpectedVersion != point.Version) return VersionConflict();
        if (!cmd.Action.Equals("inactivate", StringComparison.OrdinalIgnoreCase))
            return Result.Failure("PHASE5_REQUIRED", "Point activation/reactivation belongs to the Phase 5 orchestration.");
        var beforeStatus = point.Status;
        var before = beforeStatus.ToString();
        var changed = cmd.Action.ToLowerInvariant() switch
        {
            "inactivate" => point.TryInactivate(),
            _ => false
        };
        if (!changed) return Result.Failure("InvalidTransition", "No state change performed.");
        try
        {
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.UpdatePointAsync(point, ct);
            await tx.CommitAsync(ct);
            AddEvent("PointStatusChanged.v1", "Point", point.Id.ToString(), point.Version, ctx,
                cmd.Action + "d", "Point status changed",
                PointSnapshot(point, before), PointSnapshot(point), point.SiteId.ToString(), point.AreaId.ToString());
            return Result.Success();
        }
        catch (InvalidOperationException ex) { return Result.Failure("VERSION_CONFLICT", ex.Message); }
    }

    public async Task<Result> HandleAsync(DecommissionPointCommand cmd, OrganizationCommandContext ctx, CancellationToken ct = default)
    {
        var scope = await _repo.GetPointScopeAsync(cmd.PointId, ct);
        if (scope is null) return Result.Failure("NotFound", "Point not found.");
        var denied = await Authorize(ctx.ActorUserId, OrganizationResource.SiteChild, scope.SiteId.ToString(), ct);
        if (denied is not null) return denied;
        var point = await _repo.GetPointAsync(cmd.PointId, ct);
        if (point is null) return Result.Failure("NotFound", "Point not found.");
        if (cmd.ExpectedVersion != point.Version) return VersionConflict();
        bool hasRunning;
        try
        {
            hasRunning = await _simQuery.HasRunningSimulatorAsync(point.Id.ToString(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure("DEPENDENCY_UNAVAILABLE", "Running Simulator state could not be determined.");
        }
        var decision = DecommissionPolicy.EvaluatePoint(point, hasRunning);
        if (!decision.IsAllowed)
            return Result.Failure(decision.Code, decision.Code == "RUNNING_SIMULATOR"
                ? "Cannot decommission Point while a Simulator Run is Running."
                : "Point cannot be decommissioned in its current state.");
        var beforeStatus = point.Status;
        var before = beforeStatus.ToString();
        if (!point.TryDecommission()) return Result.Failure("InvalidTransition", "Point cannot be decommissioned.");
        try
        {
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.UpdatePointAsync(point, ct);
            var history = new PointLifecycleEntry(Guid.NewGuid().ToString(), point.Id.ToString(), point.Version,
                beforeStatus, PointStatus.Decommissioned, ctx.ActorUserId, _currentCaller?.Username ?? ctx.ActorUserId,
                "Decommissioned by command", DateTime.UtcNow, ctx.CorrelationId, ctx.CausationId);
            await _repo.AddLifecycleEntryAsync(history, ct);
            await tx.CommitAsync(ct);
            AddEvent("PointStatusChanged.v1", "Point", point.Id.ToString(), point.Version, ctx,
                "Decommissioned", "Point decommissioned",
                PointSnapshot(point, before), PointSnapshot(point), point.SiteId.ToString(), point.AreaId.ToString());
            return Result.Success();
        }
        catch (InvalidOperationException ex) { return Result.Failure("VERSION_CONFLICT", ex.Message); }
    }

    public async Task<Result> HandleAsync(UpdatePointConfigurationCommand cmd, OrganizationCommandContext ctx, CancellationToken ct = default)
    {
        var scope = await _repo.GetPointScopeAsync(cmd.PointId, ct);
        if (scope is null) return Result.Failure("NotFound", "Point not found.");
        var denied = await Authorize(ctx.ActorUserId, OrganizationResource.SiteChild, scope.SiteId.ToString(), ct);
        if (denied is not null) return denied;
        var point = await _repo.GetPointAsync(cmd.PointId, ct);
        if (point is null) return Result.Failure("NotFound", "Point not found.");
        if (cmd.ExpectedVersion != point.Version) return VersionConflict();
        var before = Clone(point);
        try
        {
            if (!point.TryUpdateConfiguration(cmd.Description, cmd.MetricId, cmd.UnitId, cmd.DataOwnerUserId,
                    cmd.ExpectedIntervalSeconds, cmd.NoDataAfterSeconds)) return Result.Success();
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.UpdatePointAsync(point, ct);
            await tx.CommitAsync(ct);
            AddEvent("PointConfigurationChanged.v1", "Point", point.Id.ToString(), point.Version, ctx,
                "Updated", "Point configuration updated", PointSnapshot(before), PointSnapshot(point), point.SiteId.ToString(), point.AreaId.ToString());
            return Result.Success();
        }
        catch (ArgumentException ex) { return Result.Failure("Validation", ex.Message); }
        catch (InvalidOperationException ex) { return Result.Failure("VERSION_CONFLICT", ex.Message); }
    }

    private async Task<Result?> Authorize(string userId, OrganizationResource resource, string? siteId, CancellationToken ct)
    {
        var decision = await _auth.AuthorizeAsync(userId, resource, siteId, ct);
        _currentCaller = decision.IsAllowed ? await _auth.ResolveCallerAsync(userId, ct) : null;
        return decision.IsAllowed ? null : Result.Failure(decision.Code, decision.Error);
    }

    private void AddEvent(string eventType, string aggregateType, string aggregateId, long version,
        OrganizationCommandContext ctx, string action, string summary,
        IReadOnlyDictionary<string, object?> before, IReadOnlyDictionary<string, object?> after,
        string? siteId, string? areaId = null)
    {
        _events.Add(new OrganizationEvent(Guid.NewGuid(), eventType, "1", "IUMP.Organization",
            aggregateType, aggregateId, version,
            ctx.ActorUserId, _currentCaller?.Username ?? ctx.ActorUserId,
            before, after, action, summary, DateTime.UtcNow,
            ctx.CorrelationId, ctx.CausationId, siteId, areaId));
    }

    private static IReadOnlyDictionary<string, object?> EmptySnap() =>
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

    private static Result VersionConflict() => Result.Failure("VERSION_CONFLICT", "ExpectedVersion does not match the current aggregate version.");

    private static Site Clone(Site value) => new(value.Id, value.Code, value.Name, value.Description, value.Timezone, value.Status, value.Version);
    private static Area Clone(Area value) => new(value.Id, value.SiteId, value.Code, value.Name, value.Description, value.Status, value.Version);
    private static Asset Clone(Asset value) => new(value.Id, value.SiteId, value.AreaId, value.Code, value.Name, value.Description, value.Status, value.Version);
    private static MeasurementPoint Clone(MeasurementPoint value) => new(value.Id, value.SiteId, value.AreaId, value.AssetId,
        value.Code, value.Description, value.MetricId, value.UnitId, value.DataOwnerUserId,
        value.ExpectedIntervalSeconds, value.NoDataAfterSeconds, value.Status, value.Version);

    private static IReadOnlyDictionary<string, object?> SiteSnapshot(Site site, string? status = null) =>
        MakeSnap(("code", site.Code), ("name", site.Name), ("description", site.Description),
            ("timezone", site.Timezone), ("status", status ?? site.Status.ToString()));

    private static IReadOnlyDictionary<string, object?> AreaSnapshot(Area area, string? status = null) =>
        MakeSnap(("siteId", area.SiteId.ToString()), ("areaId", area.Id.ToString()), ("code", area.Code),
            ("name", area.Name), ("description", area.Description), ("status", status ?? area.Status.ToString()));

    private static IReadOnlyDictionary<string, object?> AssetSnapshot(Asset asset, string? status = null) =>
        MakeSnap(("siteId", asset.SiteId.ToString()), ("areaId", asset.AreaId.ToString()), ("code", asset.Code),
            ("name", asset.Name), ("description", asset.Description), ("status", status ?? asset.Status.ToString()));

    private static IReadOnlyDictionary<string, object?> PointSnapshot(MeasurementPoint point, string? status = null) =>
        MakeSnap(("siteId", point.SiteId.ToString()), ("areaId", point.AreaId.ToString()), ("assetId", point.AssetId.ToString()),
            ("code", point.Code), ("description", point.Description), ("metricId", point.MetricId),
            ("unitId", point.UnitId), ("dataOwnerUserId", point.DataOwnerUserId),
            ("expectedIntervalSeconds", point.ExpectedIntervalSeconds), ("noDataAfterSeconds", point.NoDataAfterSeconds),
            ("status", status ?? point.Status.ToString()));

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
