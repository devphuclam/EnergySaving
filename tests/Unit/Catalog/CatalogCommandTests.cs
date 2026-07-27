using IUMP.Modules.Catalog.Application;
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
        var auth = new FakeCatalogAuthorization()
            .Add(new CatalogCallerSnapshot("admin", "Administrator", true, new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>()))
            .Add(new CatalogCallerSnapshot("engineer", "Engineer", true, new[] { "Engineer" }, new[] { "site-a" }, Array.Empty<string>()))
            .Add(new CatalogCallerSnapshot("unscoped", "Engineer", true, new[] { "Engineer" }, Array.Empty<string>(), Array.Empty<string>()))
            .Add(new CatalogCallerSnapshot("operator", "Operator", true, new[] { "Operator" }, new[] { "site-a" }, Array.Empty<string>()))
            .Add(new CatalogCallerSnapshot("manager", "Manager", true, new[] { "Manager" }, new[] { "site-a" }, Array.Empty<string>()))
            .Add(new CatalogCallerSnapshot("viewer", "Viewer", true, new[] { "Viewer" }, new[] { "site-a" }, Array.Empty<string>()));
        var handler = new CatalogCommandHandler(repo, auth);

        if (handler.HandleAsync(new CreateMetricCommand("M_ADMIN", "Admin metric", "admin")).GetAwaiter().GetResult().IsFailure) failures.Add("Administrator must be globally allowed");
        if (handler.HandleAsync(new CreateMetricCommand("M_ENGINEER", "Engineer metric", "engineer", "site-a")).GetAwaiter().GetResult().IsFailure) failures.Add("scoped Engineer must be allowed");
        if (handler.HandleAsync(new CreateMetricCommand("M_UNSCOPED", "No scope", "unscoped")).GetAwaiter().GetResult().Code != "Forbidden") failures.Add("Engineer without Site scope must be denied");
        foreach (var role in new[] { "operator", "manager", "viewer" })
            if (handler.HandleAsync(new CreateMetricCommand($"M_{role}", role, role)).GetAwaiter().GetResult().Code != "Forbidden") failures.Add($"{role} must be denied mutation");
        if (handler.HandleAsync(new CreateMetricCommand("M_GHOST", "Ghost", "engineer", "site-b")).GetAwaiter().GetResult().Code != "NotFound") failures.Add("out-of-scope target must return NotFound");

        var metricId = repo.FindMetricByCodeAsync("M_ADMIN").GetAwaiter().GetResult()!.Id;
        var beforeEvents = handler.Events.Count;
        var status = handler.HandleAsync(new UpdateMetricStatusCommand(metricId, false, "admin", null), "corr-1").GetAwaiter().GetResult();
        if (status.IsFailure || handler.Events.Count != beforeEvents + 1) failures.Add("accepted status change must emit one owner event");
        var evt = handler.Events.LastOrDefault();
        if (evt is null || evt.EventType != "MetricStatusChanged.v1" || evt.SchemaVersion != "1" || evt.Producer != "IUMP.Catalog" || evt.ActorId != "admin" || evt.ActorUsername != "Administrator" || evt.CorrelationId != "corr-1" || evt.CausationId != "corr-1") failures.Add("owner event envelope is incomplete");
        if (evt is not null && evt.Data is not null && evt.Data.Contains("password", StringComparison.OrdinalIgnoreCase)) failures.Add("owner event must not expose credentials");
        var noOpCount = handler.Events.Count;
        if (handler.HandleAsync(new UpdateMetricStatusCommand(metricId, false, "admin")).GetAwaiter().GetResult().IsFailure || handler.Events.Count != noOpCount) failures.Add("no-op status command must emit no event");
        if (handler.HandleAsync(new UpdateMetricStatusCommand(MetricId.New(), true, "operator")).GetAwaiter().GetResult().Code != "Forbidden") failures.Add("authorization must occur before target details");
        return failures;
    }
}
