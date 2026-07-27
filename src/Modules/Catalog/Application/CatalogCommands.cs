using IUMP.Modules.Catalog.Domain;
using IUMP.Modules.Catalog.Contracts;


namespace IUMP.Modules.Catalog.Application;

public sealed record CatalogEvent(
    string EventType,
    string AggregateType,
    string AggregateId,
    string? Data,
    string? CorrelationId,
    long AggregateVersion,
    DateTime OccurredAt);

public sealed record CreateMetricCommand(
    string Code,
    string Name,
    string RequestedByUserId);

public sealed record UpdateMetricStatusCommand(
    MetricId MetricId,
    bool Activate,
    string RequestedByUserId);

public sealed record CreateUnitCommand(
    string Code,
    string Symbol,
    string RequestedByUserId);

public sealed record UpdateUnitStatusCommand(
    UnitId UnitId,
    bool Activate,
    string RequestedByUserId);

public sealed record SetMetricUnitCompatibilityCommand(
    MetricId MetricId,
    UnitId UnitId,
    bool IsCanonical,
    string RequestedByUserId);

public sealed record CreateDataSourceCommand(
    string Code,
    string Name,
    SourceType SourceType,
    string RequestedByUserId);

public sealed record TransitionDataSourceCommand(
    DataSourceId DataSourceId,
    SourceStatus TargetStatus,
    string RequestedByUserId);

public sealed record CreateMappingCommand(
    DataSourceId DataSourceId,
    string PointId,
    DateTime EffectiveFrom,
    string RequestedByUserId);

public sealed record UpdateMappingStatusCommand(
    MappingId MappingId,
    string Action,
    string RequestedByUserId);

public interface ICatalogAuthorization
{
    Task<bool> CanManageCatalogAsync(string userId, CancellationToken ct = default);
}

public sealed class CatalogCommandHandler
{
    private readonly ICatalogCommandRepository _repo;
    private readonly ICatalogAuthorization _auth;
    private readonly List<CatalogEvent> _events = new();
    private const string CatalogAggregate = "Catalog";
    private const string MetricAggregate = "Metric";
    private const string UnitAggregate = "Unit";
    private const string DataSourceAggregate = "DataSource";
    private const string MappingAggregate = "Mapping";

    public IReadOnlyList<CatalogEvent> Events => _events.AsReadOnly();

    public CatalogCommandHandler(ICatalogCommandRepository repo, ICatalogAuthorization auth)
    {
        _repo = repo;
        _auth = auth;
    }

    public async Task<Result> HandleAsync(CreateMetricCommand cmd, string? correlationId = null, CancellationToken ct = default)
    {
        if (!await _auth.CanManageCatalogAsync(cmd.RequestedByUserId, ct))
            return Result.Failure("Forbidden", "User lacks catalog management permission.");

        if (string.IsNullOrWhiteSpace(cmd.Code) || cmd.Code.Length > 20)
            return Result.Failure("Validation", "Metric code must be 1-20 characters.");
        if (string.IsNullOrWhiteSpace(cmd.Name) || cmd.Name.Length > 100)
            return Result.Failure("Validation", "Metric name must be 1-100 characters.");

        var existing = await _repo.FindMetricByCodeAsync(cmd.Code, ct);
        if (existing != null)
            return Result.Failure("Conflict", $"Metric with code '{cmd.Code}' already exists.");

        var metric = new Metric(MetricId.New(), cmd.Code, cmd.Name, MetricStatus.Active, 1);
        await _repo.AddMetricAsync(metric, ct);
        _events.Add(new CatalogEvent("MetricCreated", MetricAggregate, metric.Id.ToString()!,
            null, correlationId, 1, DateTime.UtcNow));
        return Result.Success();
    }

    public async Task<Result> HandleAsync(UpdateMetricStatusCommand cmd, string? correlationId = null, CancellationToken ct = default)
    {
        if (!await _auth.CanManageCatalogAsync(cmd.RequestedByUserId, ct))
            return Result.Failure("Forbidden", "User lacks catalog management permission.");

        var metric = await _repo.GetMetricAsync(cmd.MetricId, ct);
        if (metric == null)
            return Result.Failure("NotFound", "Metric not found.");

        if (cmd.Activate) metric.Activate(); else metric.Inactivate();
        await _repo.UpdateMetricAsync(metric, ct);
        _events.Add(new CatalogEvent(cmd.Activate ? "MetricActivated" : "MetricInactivated",
            MetricAggregate, metric.Id.ToString()!, null, correlationId, metric.Version, DateTime.UtcNow));
        return Result.Success();
    }

    public async Task<Result> HandleAsync(CreateUnitCommand cmd, string? correlationId = null, CancellationToken ct = default)
    {
        if (!await _auth.CanManageCatalogAsync(cmd.RequestedByUserId, ct))
            return Result.Failure("Forbidden", "User lacks catalog management permission.");

        if (string.IsNullOrWhiteSpace(cmd.Code) || cmd.Code.Length > 10)
            return Result.Failure("Validation", "Unit code must be 1-10 characters.");
        if (string.IsNullOrWhiteSpace(cmd.Symbol) || cmd.Symbol.Length > 10)
            return Result.Failure("Validation", "Unit symbol must be 1-10 characters.");

        var existing = await _repo.FindUnitByCodeAsync(cmd.Code, ct);
        if (existing != null)
            return Result.Failure("Conflict", $"Unit with code '{cmd.Code}' already exists.");

        var unit = new MetricUnit(UnitId.New(), cmd.Code, cmd.Symbol, MetricUnitStatus.Active, 1);
        await _repo.AddUnitAsync(unit, ct);
        _events.Add(new CatalogEvent("UnitCreated", UnitAggregate, unit.Id.ToString()!,
            null, correlationId, 1, DateTime.UtcNow));
        return Result.Success();
    }

    public async Task<Result> HandleAsync(UpdateUnitStatusCommand cmd, string? correlationId = null, CancellationToken ct = default)
    {
        if (!await _auth.CanManageCatalogAsync(cmd.RequestedByUserId, ct))
            return Result.Failure("Forbidden", "User lacks catalog management permission.");

        var unit = await _repo.GetUnitAsync(cmd.UnitId, ct);
        if (unit == null)
            return Result.Failure("NotFound", "Unit not found.");

        if (cmd.Activate) unit.Activate(); else unit.Inactivate();
        await _repo.UpdateUnitAsync(unit, ct);
        _events.Add(new CatalogEvent(cmd.Activate ? "UnitActivated" : "UnitInactivated",
            UnitAggregate, unit.Id.ToString()!, null, correlationId, unit.Version, DateTime.UtcNow));
        return Result.Success();
    }

    public async Task<Result> HandleAsync(SetMetricUnitCompatibilityCommand cmd, string? correlationId = null, CancellationToken ct = default)
    {
        if (!await _auth.CanManageCatalogAsync(cmd.RequestedByUserId, ct))
            return Result.Failure("Forbidden", "User lacks catalog management permission.");

        var metric = await _repo.GetMetricAsync(cmd.MetricId, ct);
        var unit = await _repo.GetUnitAsync(cmd.UnitId, ct);
        if (metric == null) return Result.Failure("NotFound", "Metric not found.");
        if (unit == null) return Result.Failure("NotFound", "Unit not found.");

        var existing = await _repo.GetCompatibilityAsync(cmd.MetricId, cmd.UnitId, ct);
        if (existing != null)
        {
            existing.SetCanonical(cmd.IsCanonical);
            await _repo.UpdateCompatibilityAsync(existing, ct);
            _events.Add(new CatalogEvent("CompatibilityUpdated", CatalogAggregate,
                $"{cmd.MetricId}:{cmd.UnitId}", null, correlationId, existing.Version, DateTime.UtcNow));
        }
        else
        {
            var compat = new MetricUnitCompatibility(cmd.MetricId, cmd.UnitId, cmd.IsCanonical, 1);
            await _repo.AddCompatibilityAsync(compat, ct);
            _events.Add(new CatalogEvent("CompatibilityCreated", CatalogAggregate,
                $"{cmd.MetricId}:{cmd.UnitId}", null, correlationId, 1, DateTime.UtcNow));
        }
        return Result.Success();
    }

    public async Task<Result> HandleAsync(CreateDataSourceCommand cmd, string? correlationId = null, CancellationToken ct = default)
    {
        if (!await _auth.CanManageCatalogAsync(cmd.RequestedByUserId, ct))
            return Result.Failure("Forbidden", "User lacks catalog management permission.");

        if (string.IsNullOrWhiteSpace(cmd.Code) || cmd.Code.Length > 20)
            return Result.Failure("Validation", "DataSource code must be 1-20 characters.");

        var existing = await _repo.FindDataSourceByCodeAsync(cmd.Code, ct);
        if (existing != null)
            return Result.Failure("Conflict", $"DataSource with code '{cmd.Code}' already exists.");

        var ds = new DataSource(DataSourceId.New(), cmd.Code, cmd.Name, cmd.SourceType, SourceStatus.Draft, 1);
        await _repo.AddDataSourceAsync(ds, ct);
        _events.Add(new CatalogEvent("DataSourceCreated", DataSourceAggregate, ds.Id.ToString()!,
            null, correlationId, 1, DateTime.UtcNow));
        return Result.Success();
    }

    public async Task<Result> HandleAsync(TransitionDataSourceCommand cmd, string? correlationId = null, CancellationToken ct = default)
    {
        if (!await _auth.CanManageCatalogAsync(cmd.RequestedByUserId, ct))
            return Result.Failure("Forbidden", "User lacks catalog management permission.");

        var ds = await _repo.GetDataSourceAsync(cmd.DataSourceId, ct);
        if (ds == null)
            return Result.Failure("NotFound", "DataSource not found.");

        if (!ds.TryTransitionTo(cmd.TargetStatus))
            return Result.Failure("InvalidTransition", $"Cannot transition from {ds.Status} to {cmd.TargetStatus}.");

        await _repo.UpdateDataSourceAsync(ds, ct);
        _events.Add(new CatalogEvent("DataSourceTransitioned", DataSourceAggregate, ds.Id.ToString()!,
            null, correlationId, ds.Version, DateTime.UtcNow));
        return Result.Success();
    }

    public async Task<Result> HandleAsync(CreateMappingCommand cmd, string? correlationId = null, CancellationToken ct = default)
    {
        if (!await _auth.CanManageCatalogAsync(cmd.RequestedByUserId, ct))
            return Result.Failure("Forbidden", "User lacks catalog management permission.");

        var ds = await _repo.GetDataSourceAsync(cmd.DataSourceId, ct);
        if (ds == null) return Result.Failure("NotFound", "DataSource not found.");
        if (ds.IsDecommissioned) return Result.Failure("Validation", "Cannot map to a decommissioned DataSource.");

        if (string.IsNullOrWhiteSpace(cmd.PointId))
            return Result.Failure("Validation", "PointId is required.");

        var mapping = new SourcePointMapping(MappingId.New(), cmd.DataSourceId, cmd.PointId,
            MappingStatus.Draft, cmd.EffectiveFrom, null, 1);
        await _repo.AddMappingAsync(mapping, ct);
        _events.Add(new CatalogEvent("MappingCreated", MappingAggregate, mapping.Id.ToString()!,
            null, correlationId, 1, DateTime.UtcNow));
        return Result.Success();
    }

    public async Task<Result> HandleAsync(UpdateMappingStatusCommand cmd, string? correlationId = null, CancellationToken ct = default)
    {
        if (!await _auth.CanManageCatalogAsync(cmd.RequestedByUserId, ct))
            return Result.Failure("Forbidden", "User lacks catalog management permission.");

        var mapping = await _repo.GetMappingAsync(cmd.MappingId, ct);
        if (mapping == null)
            return Result.Failure("NotFound", "Mapping not found.");

        bool success = cmd.Action.ToLowerInvariant() switch
        {
            "activate" => mapping.TryActivate(),
            "inactivate" => mapping.TryInactivate(),
            "supersede" => mapping.TrySupersede(),
            _ => false
        };

        if (!success)
            return cmd.Action.ToLowerInvariant() switch
            {
                "activate" or "inactivate" or "supersede" => Result.Failure("InvalidAction", $"Cannot {cmd.Action} mapping in {mapping.Status} status."),
                _ => Result.Failure("Validation", $"Unknown action '{cmd.Action}'.")
            };

        await _repo.UpdateMappingAsync(mapping, ct);
        _events.Add(new CatalogEvent("MappingStatusChanged", MappingAggregate, mapping.Id.ToString()!,
            null, correlationId, mapping.Version, DateTime.UtcNow));
        return Result.Success();
    }
}
