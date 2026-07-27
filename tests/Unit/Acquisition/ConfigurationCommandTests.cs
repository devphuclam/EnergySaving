using IUMP.Modules.Acquisition.Application;
using IUMP.Modules.Acquisition.Contracts;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;
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
        var readiness = new FakePointReadinessQuery();
        var mapped = new List<CatalogSourceMappedScopeSnapshot>
        {
            new(new MappingId(Guid.NewGuid()), 1, "point-1", "trusted-site", "trusted-area", ReadinessVersionTuple.Empty)
        };
        scopes.Set(sourceId, new CatalogSourceScopeSnapshot(sourceId, true, "Simulator", "Active", 1, mapped));
        callers.Set(new ConfigurationCallerSnapshot("admin", "admin.user", true, new[] { "Administrator" }, Array.Empty<string>()));
        callers.Set(new ConfigurationCallerSnapshot("engineer", "engineer.user", true, new[] { "Engineer" }, new[] { "trusted-site" }));
        callers.Set(new ConfigurationCallerSnapshot("unscoped", "unscoped.user", true, new[] { "Engineer" }, Array.Empty<string>()));
        callers.Set(new ConfigurationCallerSnapshot("other", "other.user", true, new[] { "Operator" }, new[] { "trusted-site" }));
        callers.Set(new ConfigurationCallerSnapshot("inactive", "inactive.user", false, new[] { "Engineer" }, new[] { "trusted-site" }));
        var service = new SimulatorConfigurationService(repository, callers, scopes);

        var create = await service.CreateAsync(Command("admin", sourceId, 42, "corr-create", "caus-create"));
        Assert(create.IsSuccess, failures, "Administrator can create globally and source identity is resolved server-side.");
        var head = await repository.GetBySourceIdAsync(sourceId);
        Assert(head is not null, failures, "Create persists a configuration head.");
        var firstEvent = service.Events.Single();
        Assert(firstEvent.EventType == SimulatorConfigurationConstants.EventType, failures, "EventType is correct.");
        Assert(firstEvent.SchemaVersion == "1", failures, "SchemaVersion is 1.");
        Assert(firstEvent.Producer == SimulatorConfigurationConstants.Producer, failures, "Producer is IUMP.Acquisition.");
        Assert(firstEvent.SiteIds.Count == 1 && firstEvent.SiteIds[0] == "trusted-site", failures, "Event has trusted SiteIds collection.");
        Assert(firstEvent.ActorUsername == "admin.user", failures, "ActorUsername is snapshot.");
        Assert(firstEvent.CorrelationId == "corr-create", failures, "CorrelationId is exact supplied value.");
        Assert(firstEvent.CausationId == "caus-create", failures, "CausationId is exact supplied value.");
        Assert(firstEvent.AggregateType == "SimulatorConfiguration", failures, "AggregateType is correct.");
        Assert(firstEvent.AggregateId == head!.ConfigurationId.ToString("D"), failures, "AggregateId matches head.");
        Assert(firstEvent.AggregateVersion == head.Version, failures, "AggregateVersion matches head version.");
        Assert(firstEvent.OccurredAtUtc.Kind == DateTimeKind.Utc, failures, "OccurredAtUtc is UTC.");

        var allowedFields = new[] { "sourceId", "configurationId", "configurationVersion", "intervalSeconds", "minimumValue", "maximumValue", "deterministicSeed", "deterministicSeedHex", "scenarioType", "algorithmId", "algorithmVersion" };
        Assert(firstEvent.After.Keys.OrderBy(x => x).SequenceEqual(allowedFields.OrderBy(x => x)), failures, "Event after fields use the explicit safe allowlist.");
        Assert(!firstEvent.After.Keys.Any(k => k.Contains("password", StringComparison.OrdinalIgnoreCase) || k.Contains("secret", StringComparison.OrdinalIgnoreCase) || k.Contains("connection", StringComparison.OrdinalIgnoreCase)), failures, "Event contains no credentials, secrets or connection information.");
        Assert(firstEvent.After["deterministicSeed"] is string ds && ds == "42", failures, "deterministicSeed is invariant decimal string.");
        Assert(firstEvent.After["deterministicSeedHex"] is string dh && dh == "000000000000002a", failures, "deterministicSeedHex is exact lowercase 16-hex.");

        var edit = await service.EditAsync(Edit("admin", head!.ConfigurationId, head.Version, 7, 30, 2, 4, SimulatorScenario.Normal, "corr-edit", "caus-edit"));
        Assert(edit.IsSuccess && service.Events.Count == 2, failures, "Edit creates exactly one next immutable version and one owner event.");
        var versions = await repository.ListVersionsAsync(head.ConfigurationId);
        Assert(versions.Count == 2 && versions[0].MinimumValue == 1 && versions[1].MinimumValue == 2, failures, "Previous version remains unchanged after edit.");

        var editEvent = service.Events.Last();
        Assert(editEvent.CorrelationId == "corr-edit", failures, "Edit event CorrelationId is exact supplied value.");
        Assert(editEvent.CausationId == "caus-edit", failures, "Edit event CausationId is exact supplied value.");
        Assert(editEvent.AggregateVersion == 2, failures, "Edit event AggregateVersion is incremented.");
        Assert(editEvent.Action == "Edited", failures, "Edit event Action is Edited.");
        Assert(editEvent.Before["deterministicSeed"] is string bds && bds == "42", failures, "Before has exact previous seed.");
        Assert(editEvent.After["deterministicSeed"] is string ads && ads == "7", failures, "After has exact new seed.");

        var stale = await service.EditAsync(Edit("admin", head.ConfigurationId, 1, 99, 40, 5, 6, SimulatorScenario.Normal, "corr-stale", "caus-stale"));
        Assert(stale.Code == "VERSION_CONFLICT" && service.Events.Count == 2 && (await repository.ListVersionsAsync(head.ConfigurationId)).Count == 2, failures, "Stale ExpectedVersion emits no version and no event.");
        var noop = await service.EditAsync(Edit("admin", head.ConfigurationId, 2, 7, 30, 2, 4, SimulatorScenario.Normal, "corr-noop", "caus-noop"));
        Assert(noop.Code == "NO_OP" && service.Events.Count == 2 && (await repository.ListVersionsAsync(head.ConfigurationId)).Count == 2, failures, "No-op edit emits no version and no event.");

        var engineerService = new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, scopes);
        var engineerResult = await engineerService.CreateAsync(Command("engineer", Guid.NewGuid(), 1, "corr", "caus"));
        Assert(engineerResult.Code == "FORBIDDEN", failures, "Engineer without a trusted source scope is denied without enumeration.");
        var outOfScopeSource = Guid.NewGuid();
        var outMapped = new List<CatalogSourceMappedScopeSnapshot>
        {
            new(new MappingId(Guid.NewGuid()), 1, "point-other", "other-site", "other-area", ReadinessVersionTuple.Empty)
        };
        scopes.Set(outOfScopeSource, new CatalogSourceScopeSnapshot(outOfScopeSource, true, "Simulator", "Active", 1, outMapped));
        var outOfScope = await engineerService.CreateAsync(Command("engineer", outOfScopeSource, 1, "corr", "caus"));
        Assert(outOfScope.Code == "NOT_FOUND", failures, "Out-of-scope Engineer receives a non-enumerating failure.");
        var denied = await engineerService.CreateAsync(Command("other", sourceId, 1, "corr", "caus"));
        Assert(denied.Code == "FORBIDDEN", failures, "Operator/Manager/Viewer roles cannot mutate configuration.");

        var inactiveResult = await new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, scopes)
            .CreateAsync(Command("inactive", sourceId, 1, "corr-inactive", "caus-inactive"));
        Assert(inactiveResult.Code == "FORBIDDEN", failures, "Inactive caller is denied.");

        var multiSiteSource = Guid.NewGuid();
        var multiSiteMapped = new List<CatalogSourceMappedScopeSnapshot>
        {
            new(new MappingId(Guid.NewGuid()), 1, "point-a", "site-a", "area-a1", ReadinessVersionTuple.Empty),
            new(new MappingId(Guid.NewGuid()), 1, "point-b", "site-b", "area-b1", ReadinessVersionTuple.Empty)
        };
        scopes.Set(multiSiteSource, new CatalogSourceScopeSnapshot(multiSiteSource, true, "Simulator", "Active", 1, multiSiteMapped));
        callers.Set(new ConfigurationCallerSnapshot("multi", "multi.eng", true, new[] { "Engineer" }, new[] { "site-a", "site-b" }));
        var multiOk = await new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, scopes)
            .CreateAsync(Command("multi", multiSiteSource, 1, "corr-multi", "caus-multi"));
        Assert(multiOk.IsSuccess, failures, "Multi-site Engineer with all scopes succeeds.");

        callers.Set(new ConfigurationCallerSnapshot("partial", "partial.eng", true, new[] { "Engineer" }, new[] { "site-a" }));
        var partial = await new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, scopes)
            .CreateAsync(Command("partial", multiSiteSource, 1, "corr-partial", "caus-partial"));
        Assert(partial.Code == "NOT_FOUND", failures, "Multi-site Engineer missing one scope is denied.");

        var noMappingSource = Guid.NewGuid();
        scopes.Set(noMappingSource, new CatalogSourceScopeSnapshot(noMappingSource, true, "Simulator", "Active", 1, Array.Empty<CatalogSourceMappedScopeSnapshot>()));
        callers.Set(new ConfigurationCallerSnapshot("admin2", "admin2.user", true, new[] { "Administrator" }, Array.Empty<string>()));
        var adminNoMapping = await new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, scopes)
            .CreateAsync(Command("admin2", noMappingSource, 1, "corr-nomap", "caus-nomap"));
        Assert(adminNoMapping.IsSuccess, failures, "Administrator can configure Source with no mappings.");
        var engNoMapping = await new SimulatorConfigurationService(new FakeAcquisitionConfigurationRepository(), callers, scopes)
            .CreateAsync(Command("engineer", noMappingSource, 1, "corr-engnomap", "caus-engnomap"));
        Assert(engNoMapping.Code == "NOT_FOUND", failures, "Engineer is denied for Source with no mappings.");
    }

    private static SimulatorConfigurationCreateCommand Command(string actor, Guid sourceId, ulong seed, string correlation, string causation) =>
        new(sourceId, seed, 60, 1, 1, SimulatorScenario.Constant, SimulatorConfigurationConstants.AlgorithmId,
            SimulatorConfigurationConstants.AlgorithmVersion, actor, correlation, causation);

    private static SimulatorConfigurationEditCommand Edit(string actor, Guid configurationId, long expected, ulong seed,
        int interval, double min, double max, SimulatorScenario scenario, string correlation, string causation) =>
        new(configurationId, expected, seed, interval, min, max, scenario,
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
