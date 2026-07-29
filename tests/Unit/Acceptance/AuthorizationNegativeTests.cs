using IUMP.Api.Infrastructure;

namespace IUMP.Tests.Unit.Acceptance;

public static class AuthorizationNegativeTests
{
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }
    public static int FailureCount { get; private set; }

    public static List<string> Run()
    {
        var failures = new List<string>();
        var assertions = 0;
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var hiddenObject = Guid.NewGuid();
        var port = new FakeScopedResourcePort([
            new(hiddenObject, siteB, "hidden"),
            new(Guid.NewGuid(), siteA, "visible")
        ]);
        var gateway = new AuthorizationAcceptanceGateway(port);

        Assert(gateway.Get(null, hiddenObject).Status == 401,
            "unauthenticated query must return safe 401", failures, ref assertions);

        var viewer = Principal("viewer", siteA, capabilities: new HashSet<string> { "READ" });
        var forbidden = gateway.Command(viewer, siteA, "MANAGE_POINT");
        Assert(forbidden.Status == 403 && forbidden.Body == """{"errorCode":"FORBIDDEN"}""",
            "authenticated principal without capability must receive safe 403", failures, ref assertions);

        var outOfScope = gateway.Get(viewer, hiddenObject);
        Assert(outOfScope.Status == 404 && outOfScope.Body == """{"errorCode":"NOT_FOUND"}""",
            "out-of-scope object must use the authoritative non-enumerating 404", failures, ref assertions);

        var admin = Principal("administrator", capabilities: new HashSet<string> { "AUDIT_READ" }, administrator: true);
        Assert(gateway.Get(admin, hiddenObject).Status == 200,
            "Administrator must have global object behavior", failures, ref assertions);

        var engineer = Principal("engineer", siteA, capabilities: new HashSet<string> { "MANAGE_POINT", "READ" });
        Assert(gateway.Command(engineer, siteA, "MANAGE_POINT").Status == 200 &&
               gateway.Command(engineer, siteB, "MANAGE_POINT").Status == 404,
            "Engineer must be allowed only inside server-resolved scope", failures, ref assertions);

        var manager = Principal("manager", siteA, capabilities: new HashSet<string> { "READ" });
        Assert(gateway.Get(manager, port.VisibleId).Status == 200 &&
               gateway.Command(manager, siteA, "MANAGE_POINT").Status == 403,
            "Manager must have scoped read and no ungranted mutation", failures, ref assertions);

        var auditor = Principal("viewer", siteA, capabilities: new HashSet<string> { "AUDIT_READ" });
        Assert(gateway.Audit(auditor, siteA).Status == 200 &&
               gateway.Audit(Principal("viewer", siteA), siteA).Status == 403 &&
               gateway.Audit(auditor, siteB).Status == 404,
            "AUDIT_READ must still be capability- and scope-bound", failures, ref assertions);

        var inactiveUser = engineer with { IsUserActive = false };
        var inactiveSession = engineer with { IsSessionActive = false };
        Assert(gateway.Get(inactiveUser, port.VisibleId).Status == 401 &&
               gateway.Get(inactiveSession, port.VisibleId).Status == 401,
            "inactive user or session must be treated as unauthenticated", failures, ref assertions);

        var clientHeaders = new Dictionary<string, string>
        {
            ["X-Role"] = "administrator",
            ["X-Site-Id"] = siteB.ToString("D"),
            ["X-Capability"] = "MANAGE_POINT"
        };
        Assert(gateway.Get(viewer, hiddenObject, clientHeaders).Status == 404 &&
               gateway.Command(viewer, siteA, "MANAGE_POINT", clientHeaders).Status == 403,
            "client role, scope and capability headers must be ignored", failures, ref assertions);

        port.ResetTrace();
        gateway.List(viewer, pageSize: 1);
        Assert(port.Trace.SequenceEqual(["filter", "lookup", "page"]),
            "scope filtering must occur before lookup and paging", failures, ref assertions);

        var body = gateway.Get(viewer, hiddenObject).Body;
        Assert(!body.Contains(hiddenObject.ToString("D"), StringComparison.OrdinalIgnoreCase) &&
               !body.Contains("count", StringComparison.OrdinalIgnoreCase) &&
               !body.Contains("scope", StringComparison.OrdinalIgnoreCase) &&
               !body.Contains(siteB.ToString("D"), StringComparison.OrdinalIgnoreCase),
            "denial body must not expose hidden IDs, counts or scope metadata", failures, ref assertions);

        TestCount = assertions;
        AssertionCount = assertions;
        FailureCount = failures.Count;
        return failures;
    }

    private static AcceptancePrincipal Principal(string role, Guid? siteId = null,
        IReadOnlySet<string>? capabilities = null, bool administrator = false)
    {
        var sites = siteId.HasValue ? new HashSet<string> { siteId.Value.ToString("D") } : new HashSet<string>();
        return new AcceptancePrincipal(
            new ServerPrincipal(Guid.NewGuid(), role, sites, new HashSet<string>(), administrator),
            capabilities ?? new HashSet<string>(), true, true);
    }

    private static void Assert(bool condition, string message, List<string> failures, ref int assertions)
    {
        assertions++;
        if (!condition) failures.Add($"T224-FAIL: {message}.");
    }

    private sealed record AcceptancePrincipal(
        ServerPrincipal ServerPrincipal,
        IReadOnlySet<string> Capabilities,
        bool IsUserActive,
        bool IsSessionActive);

    private sealed record AcceptanceResponse(int Status, string Body);
    private sealed record ScopedResource(Guid Id, Guid SiteId, string Label);

    private sealed class AuthorizationAcceptanceGateway(FakeScopedResourcePort port)
    {
        public AcceptanceResponse Get(AcceptancePrincipal? principal, Guid id,
            IReadOnlyDictionary<string, string>? ignoredClientHeaders = null)
        {
            if (!IsAuthenticated(principal)) return Safe(401, "UNAUTHORIZED");
            var row = port.FindInScope(id, principal!.ServerPrincipal);
            return row is null ? Safe(404, "NOT_FOUND") : new(200, $$"""{"id":"{{row.Id:D}}","label":"{{row.Label}}"}""");
        }

        public AcceptanceResponse Command(AcceptancePrincipal? principal, Guid siteId, string capability,
            IReadOnlyDictionary<string, string>? ignoredClientHeaders = null)
        {
            if (!IsAuthenticated(principal)) return Safe(401, "UNAUTHORIZED");
            if (!principal!.ServerPrincipal.HasScope(siteId.ToString("D"), null)) return Safe(404, "NOT_FOUND");
            return principal.Capabilities.Contains(capability) || principal.ServerPrincipal.IsAdministrator
                ? new(200, """{"status":"ACCEPTED"}""") : Safe(403, "FORBIDDEN");
        }

        public AcceptanceResponse Audit(AcceptancePrincipal? principal, Guid siteId)
        {
            if (!IsAuthenticated(principal)) return Safe(401, "UNAUTHORIZED");
            if (!principal!.ServerPrincipal.HasScope(siteId.ToString("D"), null)) return Safe(404, "NOT_FOUND");
            return principal.Capabilities.Contains("AUDIT_READ") || principal.ServerPrincipal.IsAdministrator
                ? new(200, """{"items":[]}""") : Safe(403, "FORBIDDEN");
        }

        public IReadOnlyList<object> List(AcceptancePrincipal principal, int pageSize) =>
            port.ListInScope(principal.ServerPrincipal, pageSize);

        private static bool IsAuthenticated(AcceptancePrincipal? principal) =>
            principal is { IsUserActive: true, IsSessionActive: true };

        private static AcceptanceResponse Safe(int status, string code) =>
            new(status, $$"""{"errorCode":"{{code}}"}""");
    }

    private sealed class FakeScopedResourcePort(IReadOnlyList<ScopedResource> seed)
    {
        public Guid VisibleId => seed.Single(x => x.Label == "visible").Id;
        public List<string> Trace { get; } = [];

        public ScopedResource? FindInScope(Guid id, ServerPrincipal principal) =>
            seed.Where(x => principal.HasScope(x.SiteId.ToString("D"), null)).SingleOrDefault(x => x.Id == id);

        public IReadOnlyList<object> ListInScope(ServerPrincipal principal, int pageSize)
        {
            Trace.Add("filter");
            var scoped = seed.Where(x => principal.HasScope(x.SiteId.ToString("D"), null));
            Trace.Add("lookup");
            var projected = scoped.Select(x => (object)new { x.Id, x.Label });
            Trace.Add("page");
            return projected.Take(pageSize).ToList();
        }

        public void ResetTrace() => Trace.Clear();
    }
}
