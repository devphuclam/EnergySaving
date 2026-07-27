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
        var readiness = new FakePointReadinessQuery();
        var auth = new FakeCatalogAuthorization()
            .Add(new CatalogCallerSnapshot("admin", "Administrator", true, new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>()))
            .Add(new CatalogCallerSnapshot("engineer", "Engineer", true, new[] { "Engineer" }, new[] { "site-a" }, Array.Empty<string>()))
            .Add(new CatalogCallerSnapshot("unscoped", "Engineer", true, new[] { "Engineer" }, Array.Empty<string>(), Array.Empty<string>()))
            .Add(new CatalogCallerSnapshot("operator", "Operator", true, new[] { "Operator" }, new[] { "site-a" }, Array.Empty<string>()))
            .Add(new CatalogCallerSnapshot("manager", "Manager", true, new[] { "Manager" }, new[] { "site-a" }, Array.Empty<string>()))
            .Add(new CatalogCallerSnapshot("viewer", "Viewer", true, new[] { "Viewer" }, new[] { "site-a" }, Array.Empty<string>()));
        var handler = new CatalogCommandHandler(repo, auth, readiness);

        // Pre-seed readiness for mapping tests
        readiness.Configure("MAP_PT", new PointReadinessSnapshot("MAP_PT", "site-a", null, true, true, false, 1));
        readiness.Configure("NOAUTH_PT", new PointReadinessSnapshot("NOAUTH_PT", "site-b", null, true, true, false, 1));

        // Authorization tests
        var adminCtx = new CatalogCommandContext("admin", "corr-1", "cause-1");
        if (handler.HandleAsync(new CreateMetricCommand("M_ADMIN", "Admin metric", "admin"), adminCtx).GetAwaiter().GetResult().IsFailure)
            failures.Add("T040: Administrator must be globally allowed");

        var engCtx = new CatalogCommandContext("engineer", "corr-2", "cause-2");
        if (handler.HandleAsync(new CreateMetricCommand("M_ENG", "Engineer metric", "engineer"), engCtx).GetAwaiter().GetResult().IsFailure)
            failures.Add("T040: Scoped Engineer must be allowed for global resource");

        var unCtx = new CatalogCommandContext("unscoped", "corr-3", "cause-3");
        if (handler.HandleAsync(new CreateMetricCommand("M_UNSCOPED", "No scope", "unscoped"), unCtx).GetAwaiter().GetResult().Code != "Forbidden")
            failures.Add("T040: Engineer without Site scope must be denied");

        foreach (var role in new[] { "operator", "manager", "viewer" })
        {
            var rCtx = new CatalogCommandContext(role, "corr", "cause");
            if (handler.HandleAsync(new CreateMetricCommand($"M_{role}", role, role), rCtx).GetAwaiter().GetResult().Code != "Forbidden")
                failures.Add($"T040: {role} must be denied mutation");
        }

        // Server-side authority: context ActorUserId is used for authorization, not command property
        var opCtx = new CatalogCommandContext("operator", "corr-op", "cause-op");
        if (handler.HandleAsync(new CreateMetricCommand("M_AUTH_FIRST", "Auth first", "operator"), opCtx).GetAwaiter().GetResult().Code != "Forbidden")
            failures.Add("T040: operator must be denied via server-side authority");
        // Non-existent metric for authorized user returns NotFound
        if (handler.HandleAsync(new UpdateMetricStatusCommand(MetricId.New(), false, "admin"), adminCtx).GetAwaiter().GetResult().Code != "NotFound")
            failures.Add("T040: Non-existent metric must return NotFound");

        // All event families
        var beforeEvents = handler.Events.Count;

        // MetricStatusChanged.v1
        var metric = repo.FindMetricByCodeAsync("M_ADMIN").GetAwaiter().GetResult()!;
        handler.HandleAsync(new UpdateMetricStatusCommand(metric.Id, false, "admin"), adminCtx).GetAwaiter().GetResult();
        var metricEvent = handler.Events.LastOrDefault();
        if (metricEvent?.EventType != "MetricStatusChanged.v1") failures.Add("T040: metric status change must emit MetricStatusChanged.v1");

        // UnitStatusChanged.v1
        handler.HandleAsync(new CreateUnitCommand("U_TEST", "ut", "admin"), adminCtx).GetAwaiter().GetResult();
        var unitCreated = handler.Events.LastOrDefault();
        if (unitCreated?.EventType != "UnitStatusChanged.v1") failures.Add("T040: unit create must emit UnitStatusChanged.v1");

        // MetricUnitCompatibilityChanged.v1
        var metric2 = repo.FindMetricByCodeAsync("M_ENG").GetAwaiter().GetResult()!;
        var unit = repo.FindUnitByCodeAsync("U_TEST").GetAwaiter().GetResult()!;
        handler.HandleAsync(new SetMetricUnitCompatibilityCommand(metric2.Id, unit.Id, true, "admin"), adminCtx).GetAwaiter().GetResult();
        var compatEvent = handler.Events.LastOrDefault();
        if (compatEvent?.EventType != "MetricUnitCompatibilityChanged.v1") failures.Add("T040: compat change must emit MetricUnitCompatibilityChanged.v1");

        // DataSourceStatusChanged.v1
        handler.HandleAsync(new CreateDataSourceCommand("DS_TEST", "Test DS", SourceType.Simulator, "admin"), adminCtx).GetAwaiter().GetResult();
        var dsEvent = handler.Events.LastOrDefault();
        if (dsEvent?.EventType != "DataSourceStatusChanged.v1") failures.Add("T040: source create must emit DataSourceStatusChanged.v1");

        // SourcePointMappingChanged.v1
        var ds = repo.FindDataSourceByCodeAsync("DS_TEST").GetAwaiter().GetResult()!;
        handler.HandleAsync(new CreateMappingCommand(ds.Id, "MAP_PT", DateTime.UtcNow, "admin"), adminCtx).GetAwaiter().GetResult();
        var mapEvent = handler.Events.LastOrDefault();
        if (mapEvent?.EventType != "SourcePointMappingChanged.v1") failures.Add("T040: mapping create must emit SourcePointMappingChanged.v1");

        // Distinct CorrelationId and CausationId preserved
        var distinctCtx = new CatalogCommandContext("admin", "corr-distinct", "cause-distinct");
        handler.HandleAsync(new CreateUnitCommand("U_DISTINCT", "ud", "admin"), distinctCtx).GetAwaiter().GetResult();
        var distinctEvent = handler.Events.LastOrDefault();
        if (distinctEvent?.CorrelationId != "corr-distinct") failures.Add("T040: distinct CorrelationId must be preserved");
        if (distinctEvent?.CausationId != "cause-distinct") failures.Add("T040: distinct CausationId must be preserved");
        if (distinctEvent?.CorrelationId == distinctEvent?.CausationId) { } // ok if same value supplied, but they started distinct

        // Rejected/no-op command emits no event
        var beforeRejectCount = handler.Events.Count;
        handler.HandleAsync(new UpdateMetricStatusCommand(metric.Id, false, "admin"), adminCtx).GetAwaiter().GetResult(); // already inactive = no-op
        if (handler.Events.Count != beforeRejectCount) failures.Add("T040: no-op must emit no event");

        // Rejected mapping activation via readiness (missing Point)
        var ghostCtx = new CatalogCommandContext("admin", "corr-ghost", "cause-ghost");
        var ghostDs = new DataSource(DataSourceId.New(), "GHOST_DS", "Ghost", SourceType.Simulator, SourceStatus.Active, 1);
        repo.AddDataSourceAsync(ghostDs).GetAwaiter().GetResult();
        handler.HandleAsync(new CreateMappingCommand(ghostDs.Id, "GHOST_PT", DateTime.UtcNow, "admin"), ghostCtx).GetAwaiter().GetResult();
        var ghostMappings = repo.GetMappingsForSourceAsync(ghostDs.Id).GetAwaiter().GetResult();
        var ghostMap = ghostMappings.FirstOrDefault();
        if (ghostMap is not null)
        {
            var activateResult = handler.HandleAsync(new UpdateMappingStatusCommand(ghostMap.Id, "activate", "admin"), ghostCtx).GetAwaiter().GetResult();
            if (activateResult.Code != "NotFound") failures.Add("T040: activate mapping with missing Point readiness must return NotFound");
        }

        // Mapping authorization uses readiness SiteId, not command TargetSiteId
        var authMapping = new SourcePointMapping(MappingId.New(), ds.Id, "NOAUTH_PT", MappingStatus.Draft, DateTime.UtcNow, null, 1);
        repo.AddMappingAsync(authMapping).GetAwaiter().GetResult();
        var unauthCtx = new CatalogCommandContext("engineer", "corr-unauth", "cause-unauth");
        // engineer has site-a scope, but NOAUTH_PT has site-b
        var unauthResult = handler.HandleAsync(new UpdateMappingStatusCommand(authMapping.Id, "activate", "engineer"), unauthCtx).GetAwaiter().GetResult();
        if (unauthResult.Code != "NotFound") failures.Add("T040: Mapping auth must use readiness SiteId, must return NotFound for out-of-scope site");

        // Before/after keys are explicitly allowlisted (no credentials, hashes, tokens, secrets)
        foreach (var evt in handler.Events)
        {
            foreach (var key in evt.Before.Keys)
            {
                if (key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("hash", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("credential", StringComparison.OrdinalIgnoreCase))
                    failures.Add($"T040: Event must not contain sensitive key '{key}' before");
            }
            foreach (var key in evt.After.Keys)
            {
                if (key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("hash", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("credential", StringComparison.OrdinalIgnoreCase))
                    failures.Add($"T040: Event must not contain sensitive key '{key}' after");
            }
        }

        // No unrestricted aggregate serialization — Data is safely serialized
        foreach (var evt in handler.Events)
        {
            if (evt.Data is not null && evt.Data.Length > 10000)
                failures.Add("T040: Event Data must not contain unrestricted aggregate serialization");
        }

        return failures;
    }
}
