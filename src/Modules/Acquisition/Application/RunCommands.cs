using IUMP.Modules.Acquisition.Contracts;

namespace IUMP.Modules.Acquisition.Application;

public sealed class SimulatorRunCommandService
{
    private readonly IRunCallerSnapshotProvider _callers;
    private readonly ISimulatorStartSnapshotProvider _snapshots;
    private readonly IAcquisitionRunRepository _runs;
    private readonly ISimulatorRunUnitOfWork _unitOfWork;
    private readonly ISimulatorRunOwnerEventWriter _events;
    private readonly ISimulatorValueGenerator _generator;
    private readonly IUtcClock _clock;

    public SimulatorRunCommandService(
        IRunCallerSnapshotProvider callers,
        ISimulatorStartSnapshotProvider snapshots,
        IAcquisitionRunRepository runs,
        ISimulatorRunUnitOfWork unitOfWork,
        ISimulatorRunOwnerEventWriter events,
        ISimulatorValueGenerator generator,
        IUtcClock clock)
    {
        _callers = callers;
        _snapshots = snapshots;
        _runs = runs;
        _unitOfWork = unitOfWork;
        _events = events;
        _generator = generator;
        _clock = clock;
    }

    public async Task<RunCommandResult> StartAsync(StartSimulatorCommand command,
        CancellationToken ct = default)
    {
        var caller = await _callers.ResolveAsync(command.ActorUserId, ct);
        var existing = await _runs.GetCurrentBySourceAsync(command.SourceId, ct);
        if (existing is not null)
        {
            SimulatorStartSnapshot? selectedSnapshot = null;
            if (command.Selection is { } selected)
            {
                selectedSnapshot = await _snapshots.ResolveAsync(
                    command.SourceId, _clock.UtcNow, selected, ct);
                if (selectedSnapshot is null || selectedSnapshot.RequestedSelection != selected)
                    return RunCommandResult.Failure("NOT_FOUND", "The target is not visible.");
                if (existing.ConfigurationId != selected.ConfigurationId ||
                    existing.ConfigurationVersion != selected.ConfigurationVersion)
                    return RunCommandResult.Failure(
                        "PROVIDER_VERSION_DRIFT", "The selected configuration is not pinned by the current Run.");
            }
            var pinnedPoints = await _runs.ListPointStatesAsync(existing.RunId, ct);
            if (selectedSnapshot is not null)
            {
                var requestedPoints = selectedSnapshot.Points
                    .Select(point => $"{point.PointId:D}|{point.SiteId}|{point.AreaId}")
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var existingPoints = pinnedPoints
                    .Select(point => $"{point.PointId:D}|{point.SiteId}|{point.AreaId}")
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                if (!requestedPoints.SequenceEqual(existingPoints, StringComparer.Ordinal))
                    return RunCommandResult.Failure(
                        "PROVIDER_VERSION_DRIFT", "The selected hierarchy is not pinned by the current Run.");
            }
            var existingAuthorization = Authorize(
                caller, pinnedPoints.Select(point => (point.SiteId, point.AreaId)));
            if (!existingAuthorization.Allowed)
                return RunCommandResult.Failure(
                    existingAuthorization.Code, existingAuthorization.Message);
            return existing.Status == SimulatorRunStatus.Running
                ? RunCommandResult.Success(existing.RunId, existing.Version)
                : RunCommandResult.Failure(
                    "PRECONDITION_FAILED", "A nonterminal Run already exists.");
        }

        var now = _clock.UtcNow;
        var snapshot = await _snapshots.ResolveAsync(command.SourceId, now, command.Selection, ct);
        if (snapshot is null || snapshot.SourceId != command.SourceId ||
            command.Selection is not null && snapshot.RequestedSelection != command.Selection)
            return RunCommandResult.Failure("NOT_FOUND", "The target is not visible.");
        var authorization = Authorize(
            caller, snapshot.Points.Select(point => (point.SiteId, point.AreaId)));
        if (!authorization.Allowed)
            return RunCommandResult.Failure(authorization.Code, authorization.Message);
        var validation = ValidateStart(snapshot, now);
        if (validation is not null)
            return RunCommandResult.Failure(validation.Value.Code, validation.Value.Message);

        List<(SimulatorStartPointSnapshot Point, byte[] State)> initialized;
        try
        {
            initialized = snapshot.Points
                .OrderBy(point => point.PointId)
                .Select(point => (
                    Point: point,
                    State: _generator.Initialize(
                        snapshot.DeterministicSeed,
                        point.PointId,
                        snapshot.ConfigurationId,
                        snapshot.ConfigurationVersion,
                        snapshot.AlgorithmVersion)))
                .ToList();
        }
        catch (ArgumentException)
        {
            return RunCommandResult.Failure(
                "CONFIGURATION_INVALID", "The immutable configuration is invalid.");
        }

        var runId = Guid.NewGuid();
        var run = new SimulatorRun(
            runId, snapshot.SourceId, snapshot.SourceVersion, snapshot.ConfigurationId,
            snapshot.ConfigurationVersion, snapshot.AlgorithmId, snapshot.AlgorithmVersion,
            SimulatorRunStatus.Running, 1, 0, 0, 0, null, null, now, now, null, null, null,
            caller!.UserId, caller.Username, command.CorrelationId, command.CausationId);
        var points = initialized
            .Select(item => new SimulatorRunPointState(
                runId, item.Point.PointId, item.Point.PointVersion, item.Point.MappingId,
                item.Point.MappingVersion, item.Point.MetricId, item.Point.UnitId,
                item.Point.UnitCode, snapshot.SourceVersion, 0, item.State,
                now, item.Point.SiteId, item.Point.AreaId, null, null, 0, null, 1))
            .ToList();

        await using var tx = await _unitOfWork.BeginAsync(ct);
        try
        {
            foreach (var siteId in snapshot.Points.Select(point => point.SiteId)
                         .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
                await tx.LockAsync(SimulatorStartLockTarget.OrganizationSite, siteId, ct);
            foreach (var areaId in snapshot.Points.Select(point => point.AreaId)
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Select(value => value!)
                         .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
                await tx.LockAsync(SimulatorStartLockTarget.OrganizationArea, areaId, ct);
            foreach (var assetId in snapshot.Points.Select(point => point.AssetId)
                         .Distinct().OrderBy(value => value))
                await tx.LockAsync(
                    SimulatorStartLockTarget.OrganizationAsset, assetId.ToString("D"), ct);
            foreach (var pointId in snapshot.Points.Select(point => point.PointId)
                         .Distinct().OrderBy(value => value))
                await tx.LockAsync(
                    SimulatorStartLockTarget.OrganizationPoint, pointId.ToString("D"), ct);
            await tx.LockAsync(SimulatorStartLockTarget.CatalogSourceMapping, snapshot.SourceId.ToString("D"), ct);
            foreach (var mappingId in snapshot.Points.Select(point => point.MappingId)
                         .Distinct().OrderBy(value => value))
                await tx.LockAsync(
                    SimulatorStartLockTarget.CatalogSourceMapping, mappingId.ToString("D"), ct);
            await tx.LockAsync(SimulatorStartLockTarget.AcquisitionRun, snapshot.SourceId.ToString("D"), ct);
            if (!await _snapshots.RecheckAsync(snapshot, tx, now, ct))
            {
                await tx.RollbackAsync(CancellationToken.None);
                return RunCommandResult.Failure("PROVIDER_VERSION_DRIFT", "Provider state changed before commit.");
            }

            await _runs.CreateAsync(run, points, tx, ct);
            await tx.LockAsync(SimulatorStartLockTarget.IntegrationOutbox, runId.ToString("D"), ct);
            await _events.StageAsync(SimulatorRunEventFactory.Create(null, run,
                points.Select(point => point.SiteId), caller, "Start", now, command.CorrelationId,
                command.CausationId), tx, ct);
            await tx.CommitAsync(ct);
            return RunCommandResult.Success(runId, run.Version);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            var code = ex.Message.Contains("VERSION_CONFLICT", StringComparison.Ordinal)
                ? "VERSION_CONFLICT" : "DOMAIN_CONFLICT";
            return RunCommandResult.Failure(code, ex.Message);
        }
    }

    public async Task<RunCommandResult> ChangeStatusAsync(ChangeSimulatorRunStatusCommand command,
        CancellationToken ct = default)
    {
        var run = await _runs.GetAsync(command.RunId, ct);
        if (run is null) return RunCommandResult.Failure("NOT_FOUND", "The target is not visible.");
        var points = await _runs.ListPointStatesAsync(run.RunId, ct);
        var caller = await _callers.ResolveAsync(command.ActorUserId, ct);
        var authorization = Authorize(
            caller, points.Select(point => (point.SiteId, point.AreaId)));
        if (!authorization.Allowed)
            return RunCommandResult.Failure(authorization.Code, authorization.Message);
        if (run.Version != command.ExpectedVersion)
            return RunCommandResult.Failure("VERSION_CONFLICT", "ExpectedVersion is stale.");
        if (run.Status == command.TargetStatus)
            return RunCommandResult.Success(run.RunId, run.Version);
        if (!IsValidTransition(run.Status, command.TargetStatus))
            return RunCommandResult.Failure("PRECONDITION_FAILED", "The requested Run transition is invalid.");

        var now = _clock.UtcNow;
        await using var tx = await _unitOfWork.BeginAsync(ct);
        try
        {
            await tx.LockAsync(SimulatorStartLockTarget.AcquisitionRun, run.RunId.ToString("D"), ct);
            var changed = await _runs.ChangeStatusAsync(run.RunId, run.Version, command.TargetStatus,
                now, null, null, tx, ct);
            await tx.LockAsync(SimulatorStartLockTarget.IntegrationOutbox, run.RunId.ToString("D"), ct);
            var action = command.TargetStatus switch
            {
                SimulatorRunStatus.Paused => "Pause",
                SimulatorRunStatus.Running => "Resume",
                SimulatorRunStatus.Stopped => "Stop",
                _ => throw new InvalidOperationException("UNSUPPORTED_RUN_STATUS")
            };
            await _events.StageAsync(SimulatorRunEventFactory.Create(run, changed,
                points.Select(point => point.SiteId), caller!, action, now, command.CorrelationId,
                command.CausationId), tx, ct);
            await tx.CommitAsync(ct);
            return RunCommandResult.Success(changed.RunId, changed.Version);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            var code = ex.Message.Contains("VERSION_CONFLICT", StringComparison.Ordinal)
                ? "VERSION_CONFLICT" : "PRECONDITION_FAILED";
            return RunCommandResult.Failure(code, ex.Message);
        }
    }

    public Task<IReadOnlyList<SimulatorRun>> RecoverRunningAsync(CancellationToken ct = default) =>
        _runs.ListRunningAsync(ct);

    private static (bool Allowed, string Code, string Message) Authorize(
        RunCallerSnapshot? caller,
        IEnumerable<(string SiteId, string? AreaId)>? trustedScopes)
    {
        if (caller is null || !caller.IsActive)
            return (false, "FORBIDDEN", "Caller is not authorized.");
        if (caller.HasRole("Administrator"))
            return (true, "OK", string.Empty);
        if (!caller.HasRole("Engineer"))
            return (false, "FORBIDDEN", "Caller is not authorized.");
        var scopes = trustedScopes?.Distinct().ToList() ??
            new List<(string SiteId, string? AreaId)>();
        if (scopes.Count == 0 ||
            scopes.Any(scope => !caller.HasScope(scope.SiteId, scope.AreaId)))
            return (false, "NOT_FOUND", "The target is not visible.");
        return (true, "OK", string.Empty);
    }

    private static (string Code, string Message)? ValidateStart(SimulatorStartSnapshot snapshot, DateTime now)
    {
        if (!string.Equals(snapshot.SourceType, "Simulator", StringComparison.Ordinal))
            return ("PRECONDITION_FAILED", "Source is not a Simulator.");
        if (!string.Equals(snapshot.SourceStatus, "Active", StringComparison.Ordinal))
            return ("SOURCE_NOT_ACTIVE", "Source must be Active.");
        if (snapshot.SourceId == Guid.Empty || snapshot.ConfigurationId == Guid.Empty ||
            snapshot.SourceVersion <= 0 || snapshot.ConfigurationVersion <= 0 ||
            snapshot.IntervalSeconds <= 0 ||
            snapshot.AlgorithmVersion != SimulatorConfigurationConstants.AlgorithmVersion ||
            !string.Equals(snapshot.AlgorithmId, SimulatorConfigurationConstants.AlgorithmId,
                StringComparison.Ordinal) ||
            !Enum.IsDefined(snapshot.Scenario) ||
            snapshot.Scenario is not (SimulatorScenario.Constant or SimulatorScenario.Normal))
            return ("CONFIGURATION_INVALID", "The immutable configuration is invalid.");
        if (!double.IsFinite(snapshot.MinimumValue) || !double.IsFinite(snapshot.MaximumValue) ||
            (snapshot.Scenario == SimulatorScenario.Constant && snapshot.MinimumValue != snapshot.MaximumValue) ||
            (snapshot.Scenario == SimulatorScenario.Normal && snapshot.MinimumValue >= snapshot.MaximumValue))
            return ("CONFIGURATION_INVALID", "The immutable configuration is invalid.");
        if (snapshot.Points.Count == 0)
            return ("MAPPING_MISSING", "At least one effective Mapping is required.");
        var pointIds = new HashSet<Guid>();
        var mappingIds = new HashSet<Guid>();
        foreach (var point in snapshot.Points)
        {
            if (point.PointId == Guid.Empty || point.MappingId == Guid.Empty ||
                point.AssetId == Guid.Empty || point.MetricId == Guid.Empty ||
                point.UnitId == Guid.Empty || string.IsNullOrWhiteSpace(point.SiteId) ||
                string.IsNullOrWhiteSpace(point.AreaId) || string.IsNullOrWhiteSpace(point.UnitCode))
                return ("CONFIGURATION_INVALID", "The immutable configuration is invalid.");
            if (!pointIds.Add(point.PointId))
                return ("CONFIGURATION_INVALID", "Duplicate Point identity.");
            if (!mappingIds.Add(point.MappingId))
                return ("CONFIGURATION_INVALID", "Duplicate Mapping identity.");
            if (point.PointVersion <= 0 || point.SiteVersion <= 0 || point.AreaVersion <= 0 ||
                point.AssetVersion <= 0 || point.MappingVersion <= 0)
                return ("CONFIGURATION_INVALID", "Provider versions must be positive.");
            if (!string.Equals(point.MappingStatus, "Active", StringComparison.Ordinal) ||
                point.EffectiveFromUtc > now || point.EffectiveToUtc is { } until && until <= now)
                return ("MAPPING_NOT_ACTIVE", "Mapping must be effective and Active.");
            if (!string.Equals(point.PointStatus, "Active", StringComparison.Ordinal))
                return ("POINT_NOT_ACTIVE", "Point must be Active.");
            if (!string.Equals(point.SiteStatus, "Active", StringComparison.Ordinal) ||
                !string.Equals(point.AreaStatus, "Active", StringComparison.Ordinal) ||
                !string.Equals(point.AssetStatus, "Active", StringComparison.Ordinal))
                return ("ANCESTOR_NOT_ACTIVE", "Point ancestors must be Active.");
        }
        return null;
    }

    private static bool IsValidTransition(SimulatorRunStatus current, SimulatorRunStatus target) =>
        (current, target) switch
        {
            (SimulatorRunStatus.Running, SimulatorRunStatus.Paused) => true,
            (SimulatorRunStatus.Running, SimulatorRunStatus.Stopped) => true,
            (SimulatorRunStatus.Paused, SimulatorRunStatus.Running) => true,
            (SimulatorRunStatus.Paused, SimulatorRunStatus.Stopped) => true,
            _ => false
        };
}

public static class SimulatorRunEventFactory
{
    private static readonly string[] AllowedFields =
    {
        "runId", "sourceId", "status", "version", "configurationId", "configurationVersion",
        "algorithmId", "algorithmVersion", "generatedCount", "acceptedCount", "rejectedCount",
        "latestErrorCode"
    };

    public static SimulatorRunOwnerEvent Create(
        SimulatorRun? before,
        SimulatorRun after,
        IEnumerable<string> siteIds,
        RunCallerSnapshot actor,
        string action,
        DateTime occurredAtUtc,
        string correlationId,
        string? causationId)
    {
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("OccurredAtUtc must be UTC.", nameof(occurredAtUtc));
        var previous = before is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : Fields(before);
        var current = Fields(after);
        if (previous.Keys.Any(key => !AllowedFields.Contains(key, StringComparer.Ordinal)) ||
            current.Keys.Any(key => !AllowedFields.Contains(key, StringComparer.Ordinal)))
            throw new InvalidOperationException("EVENT_ALLOWLIST_VIOLATION");
        return new SimulatorRunOwnerEvent(
            Guid.NewGuid(), "SimulatorRunStateChanged.v1", 1, "IUMP.Acquisition", "SimulatorRun",
            after.RunId, after.Version, actor.UserId, actor.Username, action,
            $"Simulator Run {action.ToLowerInvariant()} accepted.", occurredAtUtc, correlationId,
            causationId, siteIds.Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(site => site, StringComparer.Ordinal).ToList(), previous, current);
    }

    private static Dictionary<string, object?> Fields(SimulatorRun run) => new(StringComparer.Ordinal)
    {
        ["runId"] = run.RunId.ToString("D"),
        ["sourceId"] = run.SourceId.ToString("D"),
        ["status"] = run.Status.ToString(),
        ["version"] = run.Version,
        ["configurationId"] = run.ConfigurationId.ToString("D"),
        ["configurationVersion"] = run.ConfigurationVersion,
        ["algorithmId"] = run.AlgorithmId,
        ["algorithmVersion"] = run.AlgorithmVersion,
        ["generatedCount"] = run.GeneratedCount,
        ["acceptedCount"] = run.AcceptedCount,
        ["rejectedCount"] = run.RejectedCount,
        ["latestErrorCode"] = run.LatestErrorCode
    };
}
