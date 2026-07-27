using System.Globalization;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Catalog.Contracts;

namespace IUMP.Modules.Acquisition.Application;

public sealed class SimulatorConfigurationService
{
    private readonly IAcquisitionConfigurationRepository _repository;
    private readonly IConfigurationCallerSnapshotProvider _callers;
    private readonly ICatalogSourceScopeQuery _sourceScopes;
    private readonly List<SimulatorConfigurationEvent> _events = new();

    public SimulatorConfigurationService(IAcquisitionConfigurationRepository repository,
        IConfigurationCallerSnapshotProvider callers, ICatalogSourceScopeQuery sourceScopes)
    {
        _repository = repository;
        _callers = callers;
        _sourceScopes = sourceScopes;
    }

    public IReadOnlyList<SimulatorConfigurationEvent> Events => _events.AsReadOnly();

    public async Task<ConfigurationCommandResult> CreateAsync(SimulatorConfigurationCreateCommand command, CancellationToken ct = default)
    {
        var authorization = await AuthorizeAsync(command.ActorUserId, command.SourceId, ct);
        if (!authorization.Allowed) return ConfigurationCommandResult.Failure(authorization.Code, authorization.Error!);
        if (!TryBuildVersion(Guid.NewGuid(), 1, command, authorization.Caller!, out var version, out var error))
            return ConfigurationCommandResult.Failure("VALIDATION", error!);

        var configurationId = version.ConfigurationId;
        var head = new SimulatorConfigurationHead(configurationId, command.SourceId, 1, 1);
        using var tx = await _repository.BeginTransactionAsync(ct);
        try
        {
            await _repository.CreateAsync(head, version, ct);
            await tx.CommitAsync(ct);
            _events.Add(BuildEvent(head, version, authorization.TrustedSiteIds!, command, "Created", null));
            return ConfigurationCommandResult.Success();
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(ct);
            return ConfigurationCommandResult.Failure(ex.Message.Contains("VERSION_CONFLICT", StringComparison.Ordinal) ? "VERSION_CONFLICT" : "CONFLICT", ex.Message);
        }
    }

    public async Task<ConfigurationCommandResult> EditAsync(SimulatorConfigurationEditCommand command, CancellationToken ct = default)
    {
        var head = await _repository.GetHeadAsync(command.ConfigurationId, ct);
        if (head is null) return ConfigurationCommandResult.Failure("NOT_FOUND", "Configuration is not visible.");
        var authorization = await AuthorizeAsync(command.ActorUserId, head.SourceId, ct);
        if (!authorization.Allowed) return ConfigurationCommandResult.Failure(authorization.Code, authorization.Error!);
        if (command.ExpectedVersion != head.Version) return ConfigurationCommandResult.Failure("VERSION_CONFLICT", "ExpectedVersion is stale.");
        var current = await _repository.GetVersionAsync(head.ConfigurationId, head.CurrentConfigurationVersion, ct);
        if (current is null) return ConfigurationCommandResult.Failure("NOT_FOUND", "Configuration version is not visible.");
        if (!TryBuildVersion(head.ConfigurationId, checked(head.CurrentConfigurationVersion + 1), command, authorization.Caller!, out var next, out var error))
            return ConfigurationCommandResult.Failure("VALIDATION", error!);
        if (Equivalent(current, next)) return ConfigurationCommandResult.Failure("NO_OP", "No configuration change was requested.");

        using var tx = await _repository.BeginTransactionAsync(ct);
        try
        {
            await _repository.AppendVersionAsync(head.ConfigurationId, head.Version, next, ct);
            await tx.CommitAsync(ct);
            var updatedHead = new SimulatorConfigurationHead(head.ConfigurationId, head.SourceId,
                next.ConfigurationVersion, head.Version + 1);
            _events.Add(BuildEvent(updatedHead, next, authorization.TrustedSiteIds!, command, "Edited", current));
            return ConfigurationCommandResult.Success();
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(ct);
            return ConfigurationCommandResult.Failure(ex.Message.Contains("VERSION_CONFLICT", StringComparison.Ordinal) ? "VERSION_CONFLICT" : "CONFLICT", ex.Message);
        }
    }

    private async Task<(bool Allowed, string Code, string? Error, ConfigurationCallerSnapshot? Caller, IReadOnlyList<string>? TrustedSiteIds)> AuthorizeAsync(string actorUserId, Guid sourceId, CancellationToken ct)
    {
        var caller = await _callers.ResolveAsync(actorUserId, ct);
        var scope = await _sourceScopes.GetSourceScopeAsync(sourceId, ct);
        if (caller is null || !caller.IsActive || scope is null || !scope.Exists)
            return (false, "FORBIDDEN", "The target is not visible in the caller scope.", caller, null);
        if (scope.SourceStatus == "Decommissioned" || scope.SourceType != "Simulator")
            return (false, "FORBIDDEN", "The target is not visible in the caller scope.", caller, null);
        var trustedSiteIds = scope.MappedScopes.Select(m => m.SiteId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (caller.HasRole("Administrator"))
            return (true, "OK", null, caller, trustedSiteIds);
        if (!caller.HasRole("Engineer"))
            return (false, "FORBIDDEN", "Caller is not authorized.", caller, null);
        if (trustedSiteIds.Count == 0)
            return (false, "NOT_FOUND", "The target is not visible in the caller scope.", caller, null);
        if (!trustedSiteIds.All(s => caller.HasSiteScope(s)))
            return (false, "NOT_FOUND", "The target is not visible in the caller scope.", caller, null);
        return (true, "OK", null, caller, trustedSiteIds);
    }

    private static bool TryBuildVersion(Guid configurationId, long configurationVersion, SimulatorConfigurationCreateCommand command,
        ConfigurationCallerSnapshot caller, out SimulatorConfigurationVersion version, out string? error)
    {
        return TryBuildVersion(configurationId, configurationVersion, command.IntervalSeconds, command.MinimumValue,
            command.MaximumValue, command.DeterministicSeed, command.ScenarioType, command.AlgorithmId,
            command.AlgorithmVersion, caller, command.CorrelationId, command.CausationId, out version, out error);
    }

    private static bool TryBuildVersion(Guid configurationId, long configurationVersion, SimulatorConfigurationEditCommand command,
        ConfigurationCallerSnapshot caller, out SimulatorConfigurationVersion version, out string? error)
    {
        return TryBuildVersion(configurationId, configurationVersion, command.IntervalSeconds, command.MinimumValue,
            command.MaximumValue, command.DeterministicSeed, command.ScenarioType, command.AlgorithmId,
            command.AlgorithmVersion, caller, command.CorrelationId, command.CausationId, out version, out error);
    }

    private static bool TryBuildVersion(Guid configurationId, long configurationVersion, int interval, double min, double max,
        ulong seed, SimulatorScenario scenario, string algorithmId, int algorithmVersion, ConfigurationCallerSnapshot caller,
        string? correlationId, string? causationId, out SimulatorConfigurationVersion version, out string? error)
    {
        version = null!;
        error = null;
        try
        {
            version = new SimulatorConfigurationVersion(configurationId, configurationVersion, interval, min, max, seed,
                scenario, algorithmId, algorithmVersion, caller.UserId, caller.Username, DateTime.UtcNow,
                correlationId, causationId);
            return true;
        }
        catch (ArgumentOutOfRangeException ex) { error = ex.Message; return false; }
        catch (ArgumentException ex) { error = ex.Message; return false; }
    }

    private static bool Equivalent(SimulatorConfigurationVersion left, SimulatorConfigurationVersion right) =>
        left.IntervalSeconds == right.IntervalSeconds && left.MinimumValue == right.MinimumValue &&
        left.MaximumValue == right.MaximumValue && left.DeterministicSeed == right.DeterministicSeed &&
        left.ScenarioType == right.ScenarioType && left.AlgorithmId == right.AlgorithmId &&
        left.AlgorithmVersion == right.AlgorithmVersion;

    private static SimulatorConfigurationEvent BuildEvent(SimulatorConfigurationHead head, SimulatorConfigurationVersion version,
        IReadOnlyList<string> siteIds, object command, string action, SimulatorConfigurationVersion? before)
    {
        var current = Fields(head, version);
        var previous = before is null ? new Dictionary<string, object?>() : Fields(head, before);
        return new SimulatorConfigurationEvent(Guid.NewGuid(), SimulatorConfigurationConstants.EventType, "1",
            SimulatorConfigurationConstants.Producer, "SimulatorConfiguration", head.ConfigurationId.ToString("D"),
            head.Version, version.CreatedByUserId, version.CreatedByUsername, action,
            action == "Created" ? "Simulator configuration created." : "Simulator configuration changed.",
            DateTime.UtcNow, version.CorrelationId, version.CausationId, siteIds, previous, current);
    }

    private static Dictionary<string, object?> Fields(SimulatorConfigurationHead head, SimulatorConfigurationVersion version) => new()
    {
        ["sourceId"] = head.SourceId.ToString("D"),
        ["configurationId"] = head.ConfigurationId.ToString("D"),
        ["configurationVersion"] = version.ConfigurationVersion,
        ["intervalSeconds"] = version.IntervalSeconds,
        ["minimumValue"] = version.MinimumValue,
        ["maximumValue"] = version.MaximumValue,
        ["deterministicSeed"] = version.DeterministicSeed.ToString(CultureInfo.InvariantCulture),
        ["deterministicSeedHex"] = version.DeterministicSeed.ToString("x16", CultureInfo.InvariantCulture),
        ["scenarioType"] = version.ScenarioType.ToString(),
        ["algorithmId"] = version.AlgorithmId,
        ["algorithmVersion"] = version.AlgorithmVersion
    };
}
