using System.Collections.ObjectModel;
using System.Text.Json;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;

namespace IUMP.Modules.Catalog.Application;

public enum CatalogResource { Metric, Unit, Compatibility, DataSource, Mapping }

public sealed record CatalogCallerSnapshot(
    string UserId,
    string Username,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> SiteScopes,
    IReadOnlyCollection<string> AreaScopes)
{
    public bool HasRole(string role) => Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
    public bool HasSiteScope(string siteId) => SiteScopes.Any(s => string.Equals(s, siteId, StringComparison.OrdinalIgnoreCase));
}

public sealed record CatalogAuthorizationDecision(bool IsAllowed, string Code, string? Error)
{
    public static CatalogAuthorizationDecision Allowed() => new(true, "Allowed", null);
    public static CatalogAuthorizationDecision Forbidden(string? error = null) =>
        new(false, "Forbidden", error ?? "Caller is not authorized for this Catalog mutation.");
    public static CatalogAuthorizationDecision NotFound() =>
        new(false, "NotFound", "The target is not visible in the caller scope.");
}

public interface ICatalogCallerSnapshotProvider
{
    Task<CatalogCallerSnapshot?> ResolveAsync(string userId, CancellationToken ct = default);
}

public interface ICatalogAuthorization
{
    Task<CatalogAuthorizationDecision> AuthorizeAsync(
        string requestedByUserId,
        CatalogResource resource,
        string? targetSiteId = null,
        CancellationToken ct = default);

    Task<CatalogCallerSnapshot?> ResolveCallerAsync(string requestedByUserId, CancellationToken ct = default) =>
        Task.FromResult<CatalogCallerSnapshot?>(null);
}

public sealed class CatalogRoleScopeAuthorization : ICatalogAuthorization
{
    private readonly ICatalogCallerSnapshotProvider _provider;

    public CatalogRoleScopeAuthorization(ICatalogCallerSnapshotProvider provider) => _provider = provider;

    public async Task<CatalogAuthorizationDecision> AuthorizeAsync(string requestedByUserId,
        CatalogResource resource, string? targetSiteId = null, CancellationToken ct = default)
    {
        var caller = await _provider.ResolveAsync(requestedByUserId, ct);
        if (caller is null || !caller.IsActive) return CatalogAuthorizationDecision.Forbidden();
        if (caller.HasRole("Administrator")) return CatalogAuthorizationDecision.Allowed();
        if (!caller.HasRole("Engineer")) return CatalogAuthorizationDecision.Forbidden();
        if (resource is not (CatalogResource.Metric or CatalogResource.Unit or CatalogResource.Compatibility or
            CatalogResource.DataSource or CatalogResource.Mapping))
            return CatalogAuthorizationDecision.Forbidden();
        if (string.IsNullOrWhiteSpace(targetSiteId))
            return caller.SiteScopes.Count > 0 ? CatalogAuthorizationDecision.Allowed() : CatalogAuthorizationDecision.Forbidden();
        return caller.HasSiteScope(targetSiteId) ? CatalogAuthorizationDecision.Allowed() : CatalogAuthorizationDecision.NotFound();
    }

    public Task<CatalogCallerSnapshot?> ResolveCallerAsync(string requestedByUserId, CancellationToken ct = default) =>
        _provider.ResolveAsync(requestedByUserId, ct);
}

public sealed record CatalogEvent(
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
    string? AreaId)
{
    public string? Data => JsonSerializer.Serialize(new { before = Before, after = After, action = Action, summary = Summary });
}

public sealed record CatalogCommandContext(
    string ActorUserId,
    string? CorrelationId,
    string? CausationId);

public sealed record CreateMetricCommand(string Code, string Name, string RequestedByUserId, string? TargetSiteId = null);
public sealed record UpdateMetricStatusCommand(MetricId MetricId, bool Activate, string RequestedByUserId, string? TargetSiteId = null);
public sealed record CreateUnitCommand(string Code, string Symbol, string RequestedByUserId, string? TargetSiteId = null);
public sealed record UpdateUnitStatusCommand(UnitId UnitId, bool Activate, string RequestedByUserId, string? TargetSiteId = null);
public sealed record SetMetricUnitCompatibilityCommand(MetricId MetricId, UnitId UnitId, bool IsCanonical, string RequestedByUserId, string? TargetSiteId = null);
public sealed record CreateDataSourceCommand(string Code, string Name, SourceType SourceType, string RequestedByUserId, string? TargetSiteId = null);
public sealed record TransitionDataSourceCommand(DataSourceId DataSourceId, SourceStatus TargetStatus, string RequestedByUserId, string? TargetSiteId = null);
public sealed record CreateMappingCommand(DataSourceId DataSourceId, string PointId, DateTime EffectiveFrom, string RequestedByUserId, string? TargetSiteId = null, DateTime? EffectiveTo = null);
public sealed record UpdateMappingStatusCommand(MappingId MappingId, string Action, string RequestedByUserId, string? TargetSiteId = null);

public sealed class CatalogCommandHandler
{
    private readonly ICatalogCommandRepository _repo;
    private readonly ICatalogAuthorization _auth;
    private readonly ICatalogPointReadinessQuery _readiness;
    private readonly List<CatalogEvent> _events = new();
    private CatalogCallerSnapshot? _currentCaller;

    public IReadOnlyList<CatalogEvent> Events => _events.AsReadOnly();

    public CatalogCommandHandler(ICatalogCommandRepository repo, ICatalogAuthorization auth, ICatalogPointReadinessQuery? readiness = null)
    {
        _repo = repo;
        _auth = auth;
        _readiness = readiness ?? new NullPointReadinessQuery();
    }

    public async Task<Result> HandleAsync(CreateMetricCommand cmd, string? correlationId = null, CancellationToken ct = default)
        => await HandleAsync(cmd, new CatalogCommandContext(cmd.RequestedByUserId, correlationId, correlationId), ct);

    public async Task<Result> HandleAsync(CreateMetricCommand cmd, CatalogCommandContext ctx, CancellationToken ct = default)
    {
        var denied = await Authorize(ctx.ActorUserId, CatalogResource.Metric, cmd.TargetSiteId, ct);
        if (denied is not null) return denied;
        if (string.IsNullOrWhiteSpace(cmd.Code) || cmd.Code.Length > 50 || string.IsNullOrWhiteSpace(cmd.Name) || cmd.Name.Length > 200)
            return Result.Failure("Validation", "Metric code and name are required and bounded.");
        if (await _repo.FindMetricByCodeAsync(cmd.Code, ct) is not null) return Result.Failure("Conflict", "Metric code already exists.");
        try
        {
            var metric = new Metric(MetricId.New(), cmd.Code, cmd.Name, MetricStatus.Active, 1);
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.AddMetricAsync(metric, ct);
            await tx.CommitAsync(ct);
            AddEvent("MetricStatusChanged.v1", "Metric", metric.Id.ToString(), metric.Version, ctx,
                "Created", "Metric created", null, new { code = metric.Code, name = metric.Name, status = metric.Status.ToString() }, cmd.TargetSiteId);
            return Result.Success();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result.Failure(ex is InvalidOperationException ? "Conflict" : "Validation", ex.Message);
        }
    }

    public async Task<Result> HandleAsync(UpdateMetricStatusCommand cmd, string? correlationId = null, CancellationToken ct = default)
        => await HandleAsync(cmd, new CatalogCommandContext(cmd.RequestedByUserId, correlationId, correlationId), ct);

    public async Task<Result> HandleAsync(UpdateMetricStatusCommand cmd, CatalogCommandContext ctx, CancellationToken ct = default)
    {
        var denied = await Authorize(ctx.ActorUserId, CatalogResource.Metric, cmd.TargetSiteId, ct);
        if (denied is not null) return denied;
        var metric = await _repo.GetMetricAsync(cmd.MetricId, ct);
        if (metric is null) return Result.Failure("NotFound", "Metric not found.");
        var before = metric.Status.ToString();
        var changed = cmd.Activate ? metric.Activate() : metric.Inactivate();
        if (!changed) return Result.Success();
        try
        {
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.UpdateMetricAsync(metric, ct);
            await tx.CommitAsync(ct);
            AddEvent("MetricStatusChanged.v1", "Metric", metric.Id.ToString(), metric.Version, ctx,
                cmd.Activate ? "Activated" : "Inactivated", "Metric status changed", new { status = before }, new { status = metric.Status.ToString() }, cmd.TargetSiteId);
            return Result.Success();
        }
        catch (InvalidOperationException ex) { return Result.Failure("VersionConflict", ex.Message); }
    }

    public async Task<Result> HandleAsync(CreateUnitCommand cmd, string? correlationId = null, CancellationToken ct = default)
        => await HandleAsync(cmd, new CatalogCommandContext(cmd.RequestedByUserId, correlationId, correlationId), ct);

    public async Task<Result> HandleAsync(CreateUnitCommand cmd, CatalogCommandContext ctx, CancellationToken ct = default)
    {
        var denied = await Authorize(ctx.ActorUserId, CatalogResource.Unit, cmd.TargetSiteId, ct);
        if (denied is not null) return denied;
        if (string.IsNullOrWhiteSpace(cmd.Code) || string.IsNullOrWhiteSpace(cmd.Symbol)) return Result.Failure("Validation", "Unit code and symbol are required.");
        if (await _repo.FindUnitByCodeAsync(cmd.Code, ct) is not null) return Result.Failure("Conflict", "Unit code already exists.");
        try
        {
            var unit = new MetricUnit(UnitId.New(), cmd.Code, cmd.Symbol, MetricUnitStatus.Active, 1);
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.AddUnitAsync(unit, ct);
            await tx.CommitAsync(ct);
            AddEvent("UnitStatusChanged.v1", "Unit", unit.Id.ToString(), unit.Version, ctx,
                "Created", "Unit created", null, new { code = unit.Code, symbol = unit.Symbol, status = unit.Status.ToString() }, cmd.TargetSiteId);
            return Result.Success();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { return Result.Failure(ex is InvalidOperationException ? "Conflict" : "Validation", ex.Message); }
    }

    public async Task<Result> HandleAsync(UpdateUnitStatusCommand cmd, string? correlationId = null, CancellationToken ct = default)
        => await HandleAsync(cmd, new CatalogCommandContext(cmd.RequestedByUserId, correlationId, correlationId), ct);

    public async Task<Result> HandleAsync(UpdateUnitStatusCommand cmd, CatalogCommandContext ctx, CancellationToken ct = default)
    {
        var denied = await Authorize(ctx.ActorUserId, CatalogResource.Unit, cmd.TargetSiteId, ct);
        if (denied is not null) return denied;
        var unit = await _repo.GetUnitAsync(cmd.UnitId, ct);
        if (unit is null) return Result.Failure("NotFound", "Unit not found.");
        var before = unit.Status.ToString();
        var changed = cmd.Activate ? unit.Activate() : unit.Inactivate();
        if (!changed) return Result.Success();
        try
        {
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.UpdateUnitAsync(unit, ct);
            await tx.CommitAsync(ct);
            AddEvent("UnitStatusChanged.v1", "Unit", unit.Id.ToString(), unit.Version, ctx,
                cmd.Activate ? "Activated" : "Inactivated", "Unit status changed", new { status = before }, new { status = unit.Status.ToString() }, cmd.TargetSiteId);
            return Result.Success();
        }
        catch (InvalidOperationException ex) { return Result.Failure("VersionConflict", ex.Message); }
    }

    public async Task<Result> HandleAsync(SetMetricUnitCompatibilityCommand cmd, string? correlationId = null, CancellationToken ct = default)
        => await HandleAsync(cmd, new CatalogCommandContext(cmd.RequestedByUserId, correlationId, correlationId), ct);

    public async Task<Result> HandleAsync(SetMetricUnitCompatibilityCommand cmd, CatalogCommandContext ctx, CancellationToken ct = default)
    {
        var denied = await Authorize(ctx.ActorUserId, CatalogResource.Compatibility, cmd.TargetSiteId, ct);
        if (denied is not null) return denied;
        if (await _repo.GetMetricAsync(cmd.MetricId, ct) is null || await _repo.GetUnitAsync(cmd.UnitId, ct) is null)
            return Result.Failure("NotFound", "Metric or Unit not found.");
        var existing = await _repo.GetCompatibilityAsync(cmd.MetricId, cmd.UnitId, ct);
        if (existing is not null)
        {
            var before = existing.IsCanonical;
            if (!existing.SetCanonical(cmd.IsCanonical)) return Result.Success();
            try
            {
                await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
                await _repo.UpdateCompatibilityAsync(existing, ct);
                await tx.CommitAsync(ct);
                AddEvent("MetricUnitCompatibilityChanged.v1", "MetricUnitCompatibility", $"{cmd.MetricId}:{cmd.UnitId}", existing.Version,
                    ctx, "CanonicalChanged", "Metric/Unit compatibility changed", new { isCanonical = before }, new { isCanonical = existing.IsCanonical }, cmd.TargetSiteId);
                return Result.Success();
            }
            catch (InvalidOperationException ex) { return Result.Failure(ex.Message.Contains("canonical", StringComparison.OrdinalIgnoreCase) ? "Conflict" : "VersionConflict", ex.Message); }
        }
        try
        {
            var compat = new MetricUnitCompatibility(cmd.MetricId, cmd.UnitId, cmd.IsCanonical, 1);
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.AddCompatibilityAsync(compat, ct);
            await tx.CommitAsync(ct);
            AddEvent("MetricUnitCompatibilityChanged.v1", "MetricUnitCompatibility", $"{cmd.MetricId}:{cmd.UnitId}", compat.Version,
                ctx, "Created", "Metric/Unit compatibility created", null, new { isCanonical = compat.IsCanonical }, cmd.TargetSiteId);
            return Result.Success();
        }
        catch (InvalidOperationException ex) { return Result.Failure("Conflict", ex.Message); }
    }

    public async Task<Result> HandleAsync(CreateDataSourceCommand cmd, string? correlationId = null, CancellationToken ct = default)
        => await HandleAsync(cmd, new CatalogCommandContext(cmd.RequestedByUserId, correlationId, correlationId), ct);

    public async Task<Result> HandleAsync(CreateDataSourceCommand cmd, CatalogCommandContext ctx, CancellationToken ct = default)
    {
        var denied = await Authorize(ctx.ActorUserId, CatalogResource.DataSource, cmd.TargetSiteId, ct);
        if (denied is not null) return denied;
        if (await _repo.FindDataSourceByCodeAsync(cmd.Code, ct) is not null) return Result.Failure("Conflict", "Data source code already exists.");
        try
        {
            var source = new DataSource(DataSourceId.New(), cmd.Code, cmd.Name, cmd.SourceType, SourceStatus.Draft, 1);
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.AddDataSourceAsync(source, ct);
            await tx.CommitAsync(ct);
            AddEvent("DataSourceStatusChanged.v1", "DataSource", source.Id.ToString(), source.Version, ctx,
                "Created", "Data source created", null, new { code = source.Code, status = source.Status.ToString() }, cmd.TargetSiteId);
            return Result.Success();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { return Result.Failure(ex is InvalidOperationException ? "Conflict" : "Validation", ex.Message); }
    }

    public async Task<Result> HandleAsync(TransitionDataSourceCommand cmd, string? correlationId = null, CancellationToken ct = default)
        => await HandleAsync(cmd, new CatalogCommandContext(cmd.RequestedByUserId, correlationId, correlationId), ct);

    public async Task<Result> HandleAsync(TransitionDataSourceCommand cmd, CatalogCommandContext ctx, CancellationToken ct = default)
    {
        var denied = await Authorize(ctx.ActorUserId, CatalogResource.DataSource, cmd.TargetSiteId, ct);
        if (denied is not null) return denied;
        var source = await _repo.GetDataSourceAsync(cmd.DataSourceId, ct);
        if (source is null) return Result.Failure("NotFound", "Data source not found.");
        var before = source.Status.ToString();
        if (!source.TryTransitionTo(cmd.TargetStatus)) return Result.Failure("InvalidTransition", "Data source transition is not allowed.");
        try
        {
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.UpdateDataSourceAsync(source, ct);
            await tx.CommitAsync(ct);
            AddEvent("DataSourceStatusChanged.v1", "DataSource", source.Id.ToString(), source.Version, ctx,
                cmd.TargetStatus.ToString(), "Data source status changed", new { status = before }, new { status = source.Status.ToString() }, cmd.TargetSiteId);
            return Result.Success();
        }
        catch (InvalidOperationException ex) { return Result.Failure("VersionConflict", ex.Message); }
    }

    public async Task<Result> HandleAsync(CreateMappingCommand cmd, string? correlationId = null, CancellationToken ct = default)
        => await HandleAsync(cmd, new CatalogCommandContext(cmd.RequestedByUserId, correlationId, correlationId), ct);

    public async Task<Result> HandleAsync(CreateMappingCommand cmd, CatalogCommandContext ctx, CancellationToken ct = default)
    {
        var denied = await Authorize(ctx.ActorUserId, CatalogResource.Mapping, cmd.TargetSiteId, ct);
        if (denied is not null) return denied;
        var source = await _repo.GetDataSourceAsync(cmd.DataSourceId, ct);
        if (source is null) return Result.Failure("NotFound", "Data source not found.");
        if (source.IsDecommissioned) return Result.Failure("Validation", "Cannot map to a decommissioned data source.");
        try
        {
            var mapping = new SourcePointMapping(MappingId.New(), cmd.DataSourceId, cmd.PointId, MappingStatus.Draft, cmd.EffectiveFrom, cmd.EffectiveTo, 1);
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.AddMappingAsync(mapping, ct);
            await tx.CommitAsync(ct);
            AddEvent("SourcePointMappingChanged.v1", "SourcePointMapping", mapping.Id.ToString(), mapping.Version, ctx,
                "Created", "Source-point mapping created", null, new { pointId = mapping.PointId, status = mapping.Status.ToString() }, cmd.TargetSiteId);
            return Result.Success();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { return Result.Failure(ex is InvalidOperationException ? "Conflict" : "Validation", ex.Message); }
    }

    public async Task<Result> HandleAsync(UpdateMappingStatusCommand cmd, string? correlationId = null, CancellationToken ct = default)
        => await HandleAsync(cmd, new CatalogCommandContext(cmd.RequestedByUserId, correlationId, correlationId), ct);

    public async Task<Result> HandleAsync(UpdateMappingStatusCommand cmd, CatalogCommandContext ctx, CancellationToken ct = default)
    {
        var mapping = await _repo.GetMappingAsync(cmd.MappingId, ct);
        if (mapping is null) return Result.Failure("NotFound", "Mapping not found.");

        var readiness = await _readiness.GetPointReadinessAsync(mapping.PointId, ct);
        if (readiness is null || !readiness.Exists)
            return Result.Failure("NotFound", "Point not found in Organization scope.");

        var siteId = readiness.SiteId;
        var denied = await Authorize(ctx.ActorUserId, CatalogResource.Mapping, siteId, ct);
        if (denied is not null) return denied;

        var action = cmd.Action.Trim().ToLowerInvariant();
        if (action == "activate")
        {
            if (!readiness.IsConfigurationReady)
                return Result.Failure("InvalidAction", "Point is not configuration-ready for Mapping activation.");
        }

        var before = mapping.Status.ToString();
        var changed = action switch
        {
            "activate" => mapping.TryActivate(),
            "inactivate" => mapping.TryInactivate(),
            "supersede" => mapping.TrySupersede(),
            _ => throw new ArgumentException($"Unknown mapping action '{cmd.Action}'.", nameof(cmd.Action))
        };
        if (!changed) return Result.Failure("InvalidAction", $"Cannot {cmd.Action} mapping in {before} status.");
        try
        {
            await using var tx = new AsyncTransaction(await _repo.BeginTransactionAsync(ct));
            await _repo.UpdateMappingAsync(mapping, ct);
            await tx.CommitAsync(ct);
            AddEvent("SourcePointMappingChanged.v1", "SourcePointMapping", mapping.Id.ToString(), mapping.Version, ctx,
                action, "Source-point mapping status changed", new { status = before }, new { status = mapping.Status.ToString() }, siteId);
            return Result.Success();
        }
        catch (InvalidOperationException ex) { return Result.Failure("Conflict", ex.Message); }
        catch (ArgumentException ex) { return Result.Failure("Validation", ex.Message); }
    }

    private async Task<Result?> Authorize(string userId, CatalogResource resource, string? targetSiteId, CancellationToken ct)
    {
        var decision = await _auth.AuthorizeAsync(userId, resource, targetSiteId, ct);
        _currentCaller = decision.IsAllowed ? await _auth.ResolveCallerAsync(userId, ct) : null;
        return decision.IsAllowed ? null : Result.Failure(decision.Code, decision.Error);
    }

    private void AddEvent(string eventType, string aggregateType, string aggregateId, long version, CatalogCommandContext ctx,
        string action, string summary, object? before, object? after, string? siteId)
    {
        var beforeMap = ToAllowlistedMap(before);
        var afterMap = ToAllowlistedMap(after);
        _events.Add(new CatalogEvent(Guid.NewGuid(), eventType, "1", "IUMP.Catalog", aggregateType, aggregateId, version,
            ctx.ActorUserId, _currentCaller?.Username ?? ctx.ActorUserId, beforeMap, afterMap, action, summary, DateTime.UtcNow,
            ctx.CorrelationId, ctx.CausationId, siteId, null));
    }

    private static IReadOnlyDictionary<string, object?> ToAllowlistedMap(object? value)
    {
        if (value is null) return new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());
        var map = value.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(value));
        return new ReadOnlyDictionary<string, object?>(map);
    }

    private sealed class AsyncTransaction : IAsyncDisposable
    {
        private readonly ICatalogTransaction _inner;
        private bool _committed;
        public AsyncTransaction(ICatalogTransaction inner) => _inner = inner;
        public async Task CommitAsync(CancellationToken ct) { await _inner.CommitAsync(ct); _committed = true; }
        public async ValueTask DisposeAsync()
        {
            if (!_committed) await _inner.RollbackAsync();
        }
    }

    private sealed class NullPointReadinessQuery : ICatalogPointReadinessQuery
    {
        public Task<PointReadinessSnapshot?> GetPointReadinessAsync(string pointId, CancellationToken ct = default)
            => Task.FromResult<PointReadinessSnapshot?>(null);
    }
}

public sealed record CatalogSeedRunResult(int MetricsAdded, int UnitsAdded, int CompatibilitiesAdded, int VersionsChanged);

public sealed class CatalogSeedApplicationService
{
    private readonly ICatalogCommandRepository _repo;
    public CatalogSeedApplicationService(ICatalogCommandRepository repo) => _repo = repo;

    public async Task<CatalogSeedRunResult> ApplyAsync(CancellationToken ct = default)
    {
        var metricsAdded = 0;
        var unitsAdded = 0;
        var compatAdded = 0;
        var versionsChanged = 0;
        await using var tx = new TransactionScope(await _repo.BeginTransactionAsync(ct));
        foreach (var seed in CatalogSeedDefinitions.All)
        {
            var metric = await _repo.GetMetricAsync(seed.MetricId, ct) ?? await _repo.FindMetricByCodeAsync(seed.MetricCode, ct);
            if (metric is null)
            {
                metric = new Metric(seed.MetricId, seed.MetricCode, seed.MetricName, MetricStatus.Active, 1);
                await _repo.AddMetricAsync(metric, ct);
                metricsAdded++;
            }
            var unit = await _repo.GetUnitAsync(seed.UnitId, ct) ?? await _repo.FindUnitByCodeAsync(seed.UnitCode, ct);
            if (unit is null)
            {
                unit = new MetricUnit(seed.UnitId, seed.UnitCode, seed.UnitSymbol, MetricUnitStatus.Active, 1);
                await _repo.AddUnitAsync(unit, ct);
                unitsAdded++;
            }
            var compat = await _repo.GetCompatibilityAsync(metric.Id, unit.Id, ct);
            if (compat is null)
            {
                await _repo.AddCompatibilityAsync(new MetricUnitCompatibility(metric.Id, unit.Id, true, 1), ct);
                compatAdded++;
            }
            else if (!compat.IsCanonical)
            {
                compat.SetCanonical(true);
                await _repo.UpdateCompatibilityAsync(compat, ct);
                versionsChanged++;
            }
        }
        await tx.CommitAsync(ct);
        return new CatalogSeedRunResult(metricsAdded, unitsAdded, compatAdded, versionsChanged);
    }

    private sealed class TransactionScope : IAsyncDisposable
    {
        private readonly ICatalogTransaction _inner;
        private bool _committed;
        public TransactionScope(ICatalogTransaction inner) => _inner = inner;
        public async Task CommitAsync(CancellationToken ct) { await _inner.CommitAsync(ct); _committed = true; }
        public async ValueTask DisposeAsync()
        {
            if (!_committed) await _inner.RollbackAsync();
        }
    }
}
