using IUMP.Modules.Acquisition.Application;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Acquisition;

public static class ConfigurationCommandTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        AuthorizationAndEvents(failures).GetAwaiter().GetResult();
        return failures;
    }

    private static async Task AuthorizationAndEvents(List<string> failures)
    {
        var repository = new FakeAcquisitionConfigurationRepository();
        var callers = new FakeCallerProvider();
        var scopes = new FakeSourceScopeQuery();
        var sourceId = Guid.NewGuid();
        scopes.Set(sourceId, new CatalogSourceScopeSnapshot(sourceId, "trusted-site", "trusted-area", 7));
        callers.Set(new ConfigurationCallerSnapshot("admin", "admin.user", true, new[] { "Administrator" }, Array.Empty<string>()));
        callers.Set(new ConfigurationCallerSnapshot("engineer", "engineer.user", true, new[] { "Engineer" }, new[] { "trusted-site" }));
        callers.Set(new ConfigurationCallerSnapshot("unscoped", "unscoped.user", true, new[] { "Engineer" }, Array.Empty<string>()));
        callers.Set(new ConfigurationCallerSnapshot("other", "other.user", true, new[] { "Operator" }, new[] { "trusted-site" }));
        var service = new SimulatorConfigurationService(repository, callers, scopes);

        var create = await service.CreateAsync(Command("admin", sourceId, "client-forged-site", "corr-create", "caus-create"));
        Assert(create.IsSuccess, failures, "Administrator can create globally and source identity is resolved server-side.");
        var head = await repository.GetBySourceIdAsync(sourceId);
        Assert(head is not null, failures, "Create persists a configuration head.");
        var firstEvent = service.Events.Single();
        Assert(firstEvent.EventType == SimulatorConfigurationConstants.EventType && firstEvent.SchemaVersion == "1" &&
            firstEvent.Producer == SimulatorConfigurationConstants.Producer && firstEvent.SiteId == "trusted-site" &&
            firstEvent.ActorUsername == "admin.user" && firstEvent.CorrelationId != firstEvent.CausationId,
            failures, "Owner event has exact envelope, trusted scope, actor snapshot and distinct correlation/causation.");
        var allowedFields = new[] { "sourceId", "configurationId", "configurationVersion", "intervalSeconds", "minimumValue", "maximumValue", "deterministicSeed", "scenarioType", "algorithmId", "algorithmVersion" };
        Assert(firstEvent.After.Keys.OrderBy(x => x).SequenceEqual(allowedFields.OrderBy(x => x)), failures, "Event after fields use the explicit safe allowlist.");
        Assert(!firstEvent.After.Keys.Any(k => k.Contains("password", StringComparison.OrdinalIgnoreCase) || k.Contains("secret", StringComparison.OrdinalIgnoreCase) || k.Contains("connection", StringComparison.OrdinalIgnoreCase)), failures, "Event contains no credentials, secrets or connection information.");

        var edit = await service.EditAsync(Edit("admin", head!.ConfigurationId, head.Version, sourceId, 30, 2, 4, "new-seed", SimulatorScenario.Normal, "corr-edit", "caus-edit"));
        Assert(edit.IsSuccess && service.Events.Count == 2, failures, "Edit creates exactly one next immutable version and one owner event.");
        var versions = await repository.ListVersionsAsync(head.ConfigurationId);
        Assert(versions.Count == 2 && versions[0].MinimumValue == 1 && versions[1].MinimumValue == 2, failures, "Previous version remains unchanged after edit.");

        var stale = await service.EditAsync(Edit("admin", head.ConfigurationId, 1, sourceId, 40, 5, 6, "stale", SimulatorScenario.Normal, "corr-stale", "caus-stale"));
        Assert(stale.Code == "VERSION_CONFLICT" && service.Events.Count == 2 && (await repository.ListVersionsAsync(head.ConfigurationId)).Count == 2, failures, "Stale ExpectedVersion emits no version and no event.");
        var noop = await service.EditAsync(Edit("admin", head.ConfigurationId, 2, sourceId, 30, 2, 4, "new-seed", SimulatorScenario.Normal, "corr-noop", "caus-noop"));
        Assert(noop.Code == "NO_OP" && service.Events.Count == 2 && (await repository.ListVersionsAsync(head.ConfigurationId)).Count == 2, failures, "No-op edit emits no version and no event.");

        var engineer = new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, scopes);
        var engineerResult = await engineer.CreateAsync(Command("engineer", Guid.NewGuid(), "forged", "corr", "caus"));
        Assert(engineerResult.Code == "FORBIDDEN", failures, "Engineer without a trusted source scope is denied without enumeration.");
        var outOfScopeSource = Guid.NewGuid(); scopes.Set(outOfScopeSource, new CatalogSourceScopeSnapshot(outOfScopeSource, "other-site", null, 1));
        var outOfScope = await engineer.CreateAsync(Command("engineer", outOfScopeSource, "trusted-site", "corr", "caus"));
        Assert(outOfScope.Code == "NOT_FOUND", failures, "Out-of-scope Engineer receives a non-enumerating failure.");
        var denied = await engineer.CreateAsync(Command("other", sourceId, "trusted-site", "corr", "caus"));
        Assert(denied.Code == "FORBIDDEN", failures, "Operator/Manager/Viewer roles cannot mutate configuration.");
    }

    private static SimulatorConfigurationCreateCommand Command(string actor, Guid sourceId, string clientSite, string correlation, string causation) =>
        new(sourceId, clientSite, 60, 1, 1, "seed", SimulatorScenario.Constant, SimulatorConfigurationConstants.AlgorithmId,
            SimulatorConfigurationConstants.AlgorithmVersion, actor, correlation, causation);

    private static SimulatorConfigurationEditCommand Edit(string actor, Guid configurationId, long expected, Guid sourceId,
        int interval, double min, double max, string seed, SimulatorScenario scenario, string correlation, string causation) =>
        new(configurationId, expected, "client-forged", interval, min, max, seed, scenario,
            SimulatorConfigurationConstants.AlgorithmId, SimulatorConfigurationConstants.AlgorithmVersion, actor, correlation, causation);

    private static void Assert(bool condition, List<string> failures, string message)
    {
        if (!condition) failures.Add($"T079: {message}");
    }

    private sealed class FakeCallerProvider : IConfigurationCallerSnapshotProvider
    {
        private readonly Dictionary<string, ConfigurationCallerSnapshot> _callers = new(StringComparer.Ordinal);
        public void Set(ConfigurationCallerSnapshot caller) => _callers[caller.UserId] = caller;
        public Task<ConfigurationCallerSnapshot?> ResolveAsync(string userId, CancellationToken ct = default) => Task.FromResult(_callers.GetValueOrDefault(userId));
    }

    private sealed class FakeSourceScopeQuery : ICatalogSourceScopeQuery
    {
        private readonly Dictionary<Guid, CatalogSourceScopeSnapshot> _scopes = new();
        public void Set(Guid sourceId, CatalogSourceScopeSnapshot scope) => _scopes[sourceId] = scope;
        public Task<CatalogSourceScopeSnapshot?> GetSourceScopeAsync(Guid sourceId, CancellationToken ct = default) => Task.FromResult(_scopes.GetValueOrDefault(sourceId));
    }
}
