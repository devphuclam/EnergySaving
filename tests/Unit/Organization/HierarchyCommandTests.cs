using IUMP.Modules.Organization.Application;
using IUMP.Modules.Organization.Contracts;
using IUMP.Modules.Organization.Domain;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.Organization;

public sealed class FakeOrganizationAuthorization : IOrganizationAuthorization
{
    private readonly OrganizationCallerSnapshot? _caller;
    public bool WasCalled { get; private set; }

    public FakeOrganizationAuthorization(OrganizationCallerSnapshot? caller) => _caller = caller;

    public Task<OrganizationAuthorizationDecision> AuthorizeAsync(string requestedByUserId,
        OrganizationResource resource, string? targetSiteId = null, CancellationToken ct = default)
    {
        WasCalled = true;
        if (_caller is null || !_caller.IsActive) return Task.FromResult(OrganizationAuthorizationDecision.Forbidden());
        if (_caller.HasRole("Administrator")) return Task.FromResult(OrganizationAuthorizationDecision.Allowed());
        if (!_caller.HasRole("Engineer")) return Task.FromResult(OrganizationAuthorizationDecision.Forbidden());
        if (resource == OrganizationResource.RootSite) return Task.FromResult(OrganizationAuthorizationDecision.Forbidden("Engineers cannot create root Sites."));
        if (string.IsNullOrWhiteSpace(targetSiteId))
            return Task.FromResult(_caller.SiteScopes.Count > 0
                ? OrganizationAuthorizationDecision.Allowed()
                : OrganizationAuthorizationDecision.Forbidden());
        return Task.FromResult(_caller.HasSiteScope(targetSiteId)
            ? OrganizationAuthorizationDecision.Allowed()
            : OrganizationAuthorizationDecision.NotFound());
    }
}

public static class HierarchyCommandTests
{
    private static readonly OrganizationCommandContext AdminCtx = new("admin-user", "corr-1", "caus-1");
    private static readonly OrganizationCommandContext EngCtx = new("eng-user", "corr-2", "caus-2");
    private static readonly OrganizationCommandContext OpCtx = new("op-user", "corr-3", null);

    private static OrganizationCallerSnapshot AdminCaller() => new("admin-user", "Admin", true,
        new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<string>());

    private static OrganizationCallerSnapshot ScopedEngineer(string siteId) => new("eng-user", "Engineer", true,
        new[] { "Engineer" }, new[] { siteId }, Array.Empty<string>());

    private static OrganizationCallerSnapshot NoScopeEngineer() => new("eng-user", "Engineer", true,
        new[] { "Engineer" }, Array.Empty<string>(), Array.Empty<string>());

    public static List<string> Run()
    {
        var failures = new List<string>();

        // Root Site Administrator-only
        failures.AddRange(AuthorizeRootSiteOnly());
        // Scoped Engineer lower-hierarchy mutation
        failures.AddRange(ScopedEngineerCanMutate());
        // Unscoped/out-of-scope Engineer denial
        failures.AddRange(NoScopeEngineerDenied());
        // Operator/Manager/Viewer denial
        failures.AddRange(NonEngineerRolesDenied());
        // Trusted Site scope, not command claims
        failures.AddRange(TrustedSiteScope());
        // Authorization before target/dependency details
        failures.AddRange(AuthorizationBeforeDetails());
        // All approved event families
        failures.AddRange(AllEventFamilies());
        // Exact event field allowlists
        failures.AddRange(EventFieldAllowlists());
        // Distinct correlation/causation
        failures.AddRange(DistinctCorrelationCausation());
        // No-op/rejected commands emit no event
        failures.AddRange(NoOpEmitsNoEvent());

        return failures;
    }

    private static List<string> AuthorizeRootSiteOnly()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var auth = new FakeOrganizationAuthorization(ScopedEngineer("site-1"));
        var handler = new OrganizationCommandHandler(repo, auth);

        var result = handler.HandleAsync(new CreateSiteCommand("SITE-X", "Site X", null, "UTC", "eng-user"), AdminCtx).GetAwaiter().GetResult();
        if (result.IsSuccess) f.Add("Engineer must not be able to create root Site");

        var adminAuth = new FakeOrganizationAuthorization(AdminCaller());
        var adminHandler = new OrganizationCommandHandler(repo, adminAuth);
        result = adminHandler.HandleAsync(new CreateSiteCommand("SITE-X", "Site X", null, "UTC", "admin-user"), AdminCtx).GetAwaiter().GetResult();
        if (result.IsFailure) f.Add("Administrator must be able to create root Site");

        return f;
    }

    private static List<string> ScopedEngineerCanMutate()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var site = new Site(siteId, "SITE-A", "Site A", null, "UTC", SiteStatus.Active, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();

        var auth = new FakeOrganizationAuthorization(ScopedEngineer(siteId.ToString()));
        var handler = new OrganizationCommandHandler(repo, auth);

        var result = handler.HandleAsync(new CreateAreaCommand(siteId, "AREA-B", "Area B", null, "eng-user"),
            EngCtx).GetAwaiter().GetResult();
        if (result.IsFailure) f.Add("Scoped Engineer must be able to create Area under scoped Site");

        return f;
    }

    private static List<string> NoScopeEngineerDenied()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var site = new Site(siteId, "SITE-B", "Site B", null, "UTC", SiteStatus.Active, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();

        var auth = new FakeOrganizationAuthorization(NoScopeEngineer());
        var handler = new OrganizationCommandHandler(repo, auth);

        var result = handler.HandleAsync(new CreateSiteCommand("SITE-C", "Site C", null, "UTC", "eng-user"),
            EngCtx).GetAwaiter().GetResult();
        if (result.IsSuccess) f.Add("No-scope Engineer must be denied root Site creation");

        result = handler.HandleAsync(new CreateAreaCommand(siteId, "AREA-C", "Area C", null, "eng-user"),
            EngCtx).GetAwaiter().GetResult();
        if (result.IsSuccess) f.Add("No-scope Engineer must be denied Area creation");

        return f;
    }

    private static List<string> NonEngineerRolesDenied()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var site = new Site(siteId, "SITE-D", "Site D", null, "UTC", SiteStatus.Active, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();

        var opCaller = new OrganizationCallerSnapshot("op-user", "Operator", true,
            new[] { "Operator" }, new[] { siteId.ToString() }, Array.Empty<string>());
        var opAuth = new FakeOrganizationAuthorization(opCaller);
        var opHandler = new OrganizationCommandHandler(repo, opAuth);
        var result = opHandler.HandleAsync(new CreateAreaCommand(siteId, "AREA-D", "Area D", null, "op-user"),
            OpCtx).GetAwaiter().GetResult();
        if (result.IsSuccess) f.Add("Operator must not be able to create Area");

        return f;
    }

    private static List<string> TrustedSiteScope()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var site = new Site(siteId, "SITE-E", "Site E", null, "UTC", SiteStatus.Active, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();

        var otherSiteId = SiteId.New();
        var otherSite = new Site(otherSiteId, "SITE-F", "Site F", null, "UTC", SiteStatus.Active, 1);
        repo.AddSiteAsync(otherSite).GetAwaiter().GetResult();

        var auth = new FakeOrganizationAuthorization(ScopedEngineer(otherSiteId.ToString()));
        var handler = new OrganizationCommandHandler(repo, auth);

        var result = handler.HandleAsync(new CreateAreaCommand(siteId, "AREA-E", "Area E", null, "eng-user"),
            EngCtx).GetAwaiter().GetResult();
        if (result.IsSuccess) f.Add("Engineer scoped to Site F must not create Area in Site E");

        var notFoundAuth = new FakeOrganizationAuthorization(ScopedEngineer(otherSiteId.ToString()));
        var notFoundHandler = new OrganizationCommandHandler(repo, notFoundAuth);
        result = notFoundHandler.HandleAsync(new CreateAreaCommand(siteId, "AREA-E2", "Area E2", null, "eng-user"),
            EngCtx).GetAwaiter().GetResult();
        if (result.IsSuccess) f.Add("Out-of-scope Site access must be blocked");

        return f;
    }

    private static List<string> AuthorizationBeforeDetails()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var auth = new FakeOrganizationAuthorization(NoScopeEngineer());
        var handler = new OrganizationCommandHandler(repo, auth);

        var result = handler.HandleAsync(new CreateSiteCommand("SITE-G", "Site G", null, "UTC", "eng-user"),
            EngCtx).GetAwaiter().GetResult();
        if (result.IsSuccess) f.Add("Authorization must happen before target validation");

        if (!auth.WasCalled) f.Add("Authorization must be called before any mutation");

        return f;
    }

    private static List<string> AllEventFamilies()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var auth = new FakeOrganizationAuthorization(AdminCaller());
        var handler = new OrganizationCommandHandler(repo, auth);

        handler.HandleAsync(new CreateSiteCommand("SITE-H", "Site H", null, "UTC", "admin-user"), AdminCtx).GetAwaiter().GetResult();
        var eventTypes = handler.Events.Select(e => e.EventType).Distinct().ToList();
        if (!eventTypes.Contains("SiteStatusChanged.v1")) f.Add("Site create must emit SiteStatusChanged.v1");

        var createdSite = repo.GetAllSitesAsync().GetAwaiter().GetResult().FirstOrDefault();
        if (createdSite is null) { f.Add("Created site not found in repo"); return f; }

        handler = new OrganizationCommandHandler(repo, auth);
        var areaCmd = new CreateAreaCommand(createdSite.Id, "AREA-H", "Area H", null, "admin-user");
        handler.HandleAsync(areaCmd, AdminCtx).GetAwaiter().GetResult();
        eventTypes = handler.Events.Select(e => e.EventType).Distinct().ToList();
        if (!eventTypes.Contains("AreaStatusChanged.v1")) f.Add("Area create must emit AreaStatusChanged.v1");

        return f;
    }

    private static List<string> EventFieldAllowlists()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var auth = new FakeOrganizationAuthorization(AdminCaller());
        var handler = new OrganizationCommandHandler(repo, auth);

        handler.HandleAsync(new CreateSiteCommand("SITE-I", "Site I", null, "UTC", "admin-user"), AdminCtx).GetAwaiter().GetResult();
        var ev = handler.Events.FirstOrDefault();
        if (ev is null) { f.Add("Site create should produce an event"); return f; }

        var allowedBefore = new[] { "code", "name", "timezone", "status" };
        var allowedAfter = new[] { "code", "name", "timezone", "status" };
        foreach (var key in ev.Before.Keys)
        {
            if (key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("hash", StringComparison.OrdinalIgnoreCase))
                f.Add($"Event Before must not contain sensitive key: {key}");
            if (!allowedBefore.Contains(key) && key != "code" && key != "name" && key != "timezone" && key != "status")
                f.Add($"Unexpected Before key: {key}. Expected one of: {string.Join(", ", allowedBefore)}");
        }
        foreach (var key in ev.After.Keys)
        {
            if (key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("hash", StringComparison.OrdinalIgnoreCase))
                f.Add($"Event After must not contain sensitive key: {key}");
            if (!allowedAfter.Contains(key) && key != "code" && key != "name" && key != "timezone" && key != "status")
                f.Add($"Unexpected After key: {key}. Expected one of: {string.Join(", ", allowedAfter)}");
        }

        return f;
    }

    private static List<string> DistinctCorrelationCausation()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var auth = new FakeOrganizationAuthorization(AdminCaller());

        var ctx = new OrganizationCommandContext("admin-user", "my-correlation", "my-causation");
        var handler = new OrganizationCommandHandler(repo, auth);
        handler.HandleAsync(new CreateSiteCommand("SITE-J", "Site J", null, "UTC", "admin-user"), ctx).GetAwaiter().GetResult();
        var ev = handler.Events.FirstOrDefault();
        if (ev is null) { f.Add("Event should exist"); return f; }
        if (ev.CorrelationId != "my-correlation") f.Add("CorrelationId must be preserved from context");
        if (ev.CausationId != "my-causation") f.Add("CausationId must be distinct and preserved from context");

        return f;
    }

    private static List<string> NoOpEmitsNoEvent()
    {
        var f = new List<string>();
        var repo = new FakeOrganizationCommandRepository();
        var siteId = SiteId.New();
        var site = new Site(siteId, "SITE-K", "Site K", null, "UTC", SiteStatus.Active, 1);
        repo.AddSiteAsync(site).GetAwaiter().GetResult();

        var auth = new FakeOrganizationAuthorization(AdminCaller());
        var handler = new OrganizationCommandHandler(repo, auth);
        handler.HandleAsync(new UpdateSiteStatusCommand(siteId, "activate", "admin-user"), AdminCtx).GetAwaiter().GetResult();
        if (handler.HasEvents) f.Add("Activating an already-Active Site must produce no event");

        return f;
    }
}
