using IUMP.Modules.Catalog.Application;
using IUMP.Modules.Catalog.Contracts;
using IUMP.Modules.Catalog.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Catalog;

public sealed class FakeCatalogAuthorization : ICatalogAuthorization
{
    private readonly Dictionary<string, CatalogCallerSnapshot> _callers = new(StringComparer.OrdinalIgnoreCase);
    public FakeCatalogAuthorization Add(CatalogCallerSnapshot caller) { _callers[caller.UserId] = caller; return this; }
    public Task<CatalogCallerSnapshot?> ResolveCallerAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult(_callers.TryGetValue(userId, out var caller) ? caller : null);
    public Task<CatalogAuthorizationDecision> AuthorizeAsync(string userId, CatalogResource resource, string? targetSiteId = null, CancellationToken ct = default)
    {
        if (!_callers.TryGetValue(userId, out var caller) || !caller.IsActive) return Task.FromResult(CatalogAuthorizationDecision.Forbidden());
        if (caller.HasRole("Administrator")) return Task.FromResult(CatalogAuthorizationDecision.Allowed());
        if (!caller.HasRole("Engineer")) return Task.FromResult(CatalogAuthorizationDecision.Forbidden());
        if (string.IsNullOrWhiteSpace(targetSiteId)) return Task.FromResult(caller.SiteScopes.Count > 0 ? CatalogAuthorizationDecision.Allowed() : CatalogAuthorizationDecision.Forbidden());
        return Task.FromResult(caller.HasSiteScope(targetSiteId) ? CatalogAuthorizationDecision.Allowed() : CatalogAuthorizationDecision.NotFound());
    }
}

public static class CatalogCommandTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        var repo = new FakeCatalogCommandRepository();
        var readiness = new FakePointReadinessQuery()
            .Configure("SITE_B_POINT", new PointReadinessSnapshot("SITE_B_POINT", "site-b", "area-b", true, true, false, 1))
            .Configure("NON_PRODUCING", new PointReadinessSnapshot("NON_PRODUCING", "site-a", "area-a", true, true, false, 1))
            .Configure("PRODUCING", new PointReadinessSnapshot("PRODUCING", "site-a", "area-a", true, true, true, 1))
            .Configure("UNREADY", new PointReadinessSnapshot("UNREADY", "site-a", "area-a", true, false, false, 1));
        var auth = new FakeCatalogAuthorization()
            .Add(new CatalogCallerSnapshot("admin", "Administrator", true, new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>()))
            .Add(new CatalogCallerSnapshot("engineer", "Engineer", true, new[] { "Engineer" }, new[] { "site-a" }, Array.Empty<string>()))
            .Add(new CatalogCallerSnapshot("unscoped", "Engineer", true, new[] { "Engineer" }, Array.Empty<string>(), Array.Empty<string>()))
            .Add(new CatalogCallerSnapshot("operator", "Operator", true, new[] { "Operator" }, new[] { "site-a" }, Array.Empty<string>()));
        var handler = new CatalogCommandHandler(repo, auth, readiness);

        var adminCtx = new CatalogCommandContext("admin", "corr-admin", "cause-admin");
        var engineerCtx = new CatalogCommandContext("engineer", "corr-engineer", "cause-engineer");
        var sourceResult = handler.HandleAsync(new CreateDataSourceCommand("MAP_SOURCE", "Mapping source", SourceType.Simulator, "admin"), adminCtx).GetAwaiter().GetResult();
        if (sourceResult.IsFailure) failures.Add("Administrator must create a Data Source for mapping tests");
        var source = repo.FindDataSourceByCodeAsync("MAP_SOURCE").GetAwaiter().GetResult()!;
        var sourceEvent = handler.Events.LastOrDefault();
        if (sourceEvent is not null)
        {
            AssertKeys(sourceEvent.Before, Array.Empty<string>(), failures, "data source create before");
            AssertKeys(sourceEvent.After, new[] { "code", "sourceType", "status" }, failures, "data source create after");
        }

        var operatorDenied = handler.HandleAsync(new CreateMetricCommand("OPERATOR_DENIED", "Operator denied", "operator"), new CatalogCommandContext("operator", "corr-operator", "cause-operator")).GetAwaiter().GetResult();
        var unscopedDenied = handler.HandleAsync(new CreateMetricCommand("UNSCOPED_DENIED", "Unscoped denied", "unscoped"), new CatalogCommandContext("unscoped", "corr-unscoped", "cause-unscoped")).GetAwaiter().GetResult();
        if (operatorDenied.Code != "Forbidden" || unscopedDenied.Code != "Forbidden")
            failures.Add("Only Administrator or scoped Engineer may mutate Catalog resources");

        // Trusted Point Site authorization: command TargetSiteId is never authority.
        var beforeRejectedCreate = handler.Events.Count;
        var rejected = handler.HandleAsync(new CreateMappingCommand(source.Id, "SITE_B_POINT", DateTime.UtcNow, "engineer", "site-a"), engineerCtx).GetAwaiter().GetResult();
        if (rejected.Code != "NotFound") failures.Add("Engineer scoped to Site A must not create a mapping for trusted Site B");
        if (repo.GetMappingsForSourceAsync(source.Id).GetAwaiter().GetResult().Count != 0) failures.Add("Rejected mapping create must not mutate the repository");
        if (handler.Events.Count != beforeRejectedCreate) failures.Add("Rejected mapping create must not emit an event");

        var omittedTarget = handler.HandleAsync(new CreateMappingCommand(source.Id, "SITE_B_POINT", DateTime.UtcNow, "engineer"), engineerCtx).GetAwaiter().GetResult();
        if (omittedTarget.Code != "NotFound") failures.Add("Omitting TargetSiteId must not bypass trusted scope");
        var missingBefore = handler.Events.Count;
        var missing = handler.HandleAsync(new CreateMappingCommand(source.Id, "MISSING_POINT", DateTime.UtcNow, "admin", "site-a"), adminCtx).GetAwaiter().GetResult();
        if (missing.Code != "NotFound") failures.Add("Missing Point must return NotFound before mutation");
        if (handler.Events.Count != missingBefore || repo.GetMappingsForSourceAsync(source.Id).GetAwaiter().GetResult().Count != 0) failures.Add("Missing Point must create no mapping and no event");

        // Administrator remains global and uses trusted Site/Area snapshots in the event.
        var createResult = handler.HandleAsync(new CreateMappingCommand(source.Id, "NON_PRODUCING", DateTime.UtcNow, "admin", "wrong-site"), adminCtx).GetAwaiter().GetResult();
        if (createResult.IsFailure) failures.Add("Administrator must be globally allowed for mapping create");
        var createEvent = handler.Events.LastOrDefault();
        if (createEvent?.SiteId != "site-a" || createEvent.AreaId != "area-a") failures.Add("Mapping event must use trusted SiteId/AreaId");
        if (createEvent is not null) AssertKeys(createEvent.After, new[] { "pointId", "status", "effectiveFrom", "effectiveTo", "producingReady" }, failures, "mapping create after");

        // Correlation-only compatibility overload leaves causation null.
        var metric = repo.FindMetricByCodeAsync("MISSING").GetAwaiter().GetResult();
        var correlationOnly = handler.HandleAsync(new CreateMetricCommand("CORR_ONLY", "Correlation only", "admin"), "corr-only").GetAwaiter().GetResult();
        var correlationEvent = handler.Events.LastOrDefault();
        if (correlationOnly.IsFailure || correlationEvent?.CorrelationId != "corr-only" || correlationEvent.CausationId is not null)
            failures.Add("Correlation-only overload must leave CausationId null");
        if (correlationEvent is not null)
        {
            AssertKeys(correlationEvent.Before, Array.Empty<string>(), failures, "metric create before");
            AssertKeys(correlationEvent.After, new[] { "code", "name", "status" }, failures, "metric create after");
        }
        var explicitContextResult = handler.HandleAsync(new CreateUnitCommand("DISTINCT", "d", "admin"), new CatalogCommandContext("admin", "corr-distinct", "cause-distinct")).GetAwaiter().GetResult();
        var explicitEvent = handler.Events.LastOrDefault();
        if (explicitContextResult.IsFailure || explicitEvent?.CorrelationId != "corr-distinct" || explicitEvent.CausationId != "cause-distinct") failures.Add("Distinct correlation/causation must be preserved");
        if (explicitEvent is not null)
        {
            AssertKeys(explicitEvent.Before, Array.Empty<string>(), failures, "unit create before");
            AssertKeys(explicitEvent.After, new[] { "code", "symbol", "status" }, failures, "unit create after");
        }

        var compatibilityMetric = handler.HandleAsync(new CreateMetricCommand("COMP_METRIC", "Compatibility metric", "admin"), adminCtx).GetAwaiter().GetResult();
        var compatibilityUnit = handler.HandleAsync(new CreateUnitCommand("COMP_UNIT", "cu", "admin"), adminCtx).GetAwaiter().GetResult();
        var compatibilityMetricId = repo.FindMetricByCodeAsync("COMP_METRIC").GetAwaiter().GetResult()!.Id;
        var compatibilityUnitId = repo.FindUnitByCodeAsync("COMP_UNIT").GetAwaiter().GetResult()!.Id;
        var compatibilityResult = handler.HandleAsync(new SetMetricUnitCompatibilityCommand(compatibilityMetricId, compatibilityUnitId, true, "admin"), adminCtx).GetAwaiter().GetResult();
        var compatibilityEvent = handler.Events.LastOrDefault();
        if (compatibilityMetric.IsFailure || compatibilityUnit.IsFailure || compatibilityResult.IsFailure || compatibilityEvent is null)
            failures.Add("Administrator must create Metric/Unit compatibility for allowlist tests");
        if (compatibilityEvent is not null)
        {
            AssertKeys(compatibilityEvent.Before, Array.Empty<string>(), failures, "compatibility create before");
            AssertKeys(compatibilityEvent.After, new[] { "metricId", "unitId", "isCanonical" }, failures, "compatibility create after");
        }

        // Configuration-ready but non-producing Point activates and records false.
        var nonProducing = repo.GetMappingsForSourceAsync(source.Id).GetAwaiter().GetResult().Single();
        var activated = handler.HandleAsync(new UpdateMappingStatusCommand(nonProducing.Id, "activate", "admin"), adminCtx).GetAwaiter().GetResult();
        if (activated.IsFailure) failures.Add("Configuration-ready non-producing mapping must activate");
        var nonProducingEvent = handler.Events.LastOrDefault();
        if (nonProducingEvent?.After.GetValueOrDefault("producingReady") is not false) failures.Add("Non-producing mapping event must record producingReady=false");
        if (nonProducingEvent is not null)
        {
            AssertKeys(nonProducingEvent.Before, new[] { "pointId", "status", "effectiveFrom", "effectiveTo", "producingReady" }, failures, "mapping activation before");
            AssertKeys(nonProducingEvent.After, new[] { "pointId", "status", "effectiveFrom", "effectiveTo", "producingReady" }, failures, "mapping activation after");
        }

        var producingCreate = handler.HandleAsync(new CreateMappingCommand(source.Id, "PRODUCING", DateTime.UtcNow, "admin"), adminCtx).GetAwaiter().GetResult();
        var producingMapping = repo.GetMappingsForSourceAsync(source.Id).GetAwaiter().GetResult().Single(m => m.PointId == "PRODUCING");
        var producingActivate = handler.HandleAsync(new UpdateMappingStatusCommand(producingMapping.Id, "activate", "admin"), adminCtx).GetAwaiter().GetResult();
        var producingEvent = handler.Events.LastOrDefault();
        if (producingCreate.IsFailure || producingActivate.IsFailure || producingEvent?.After.GetValueOrDefault("producingReady") is not true) failures.Add("Production-ready mapping event must record producingReady=true");
        if (producingEvent is not null)
            AssertKeys(producingEvent.After, new[] { "pointId", "status", "effectiveFrom", "effectiveTo", "producingReady" }, failures, "producing mapping activation after");

        var unreadyCreate = handler.HandleAsync(new CreateMappingCommand(source.Id, "UNREADY", DateTime.UtcNow, "admin"), adminCtx).GetAwaiter().GetResult();
        var unreadyMapping = repo.GetMappingsForSourceAsync(source.Id).GetAwaiter().GetResult().Single(m => m.PointId == "UNREADY");
        var beforeUnready = handler.Events.Count;
        var unreadyActivate = handler.HandleAsync(new UpdateMappingStatusCommand(unreadyMapping.Id, "activate", "admin"), adminCtx).GetAwaiter().GetResult();
        if (!unreadyActivate.IsFailure || repo.GetMappingAsync(unreadyMapping.Id).GetAwaiter().GetResult()!.Status != MappingStatus.Draft || handler.Events.Count != beforeUnready)
            failures.Add("Configuration-unready mapping activation must reject without mutation or event");

        // Exact allowlists for each owner-event family produced above.
        foreach (var evt in handler.Events)
        {
            var allowed = evt.AggregateType switch
            {
                "Metric" => new[] { "code", "name", "status" },
                "Unit" => new[] { "code", "symbol", "status" },
                "MetricUnitCompatibility" => new[] { "metricId", "unitId", "isCanonical" },
                "DataSource" => new[] { "code", "sourceType", "status" },
                "SourcePointMapping" => new[] { "pointId", "status", "effectiveFrom", "effectiveTo", "producingReady" },
                _ => Array.Empty<string>()
            };
            if (evt.Before.Keys.Any(k => !allowed.Contains(k, StringComparer.Ordinal)) || evt.After.Keys.Any(k => !allowed.Contains(k, StringComparer.Ordinal)))
                failures.Add($"Event {evt.EventType} contains a key outside its explicit allowlist");
        }
        return failures;
    }

    private static void AssertKeys(IReadOnlyDictionary<string, object?> actual, string[] expected, List<string> failures, string label)
    {
        if (!actual.Keys.OrderBy(k => k).SequenceEqual(expected.OrderBy(k => k), StringComparer.Ordinal))
            failures.Add($"{label} keys must match the explicit allowlist");
    }
}
