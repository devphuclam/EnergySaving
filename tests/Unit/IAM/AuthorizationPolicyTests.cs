using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Application;

namespace IUMP.Tests.Unit.IAM;

public static class AuthorizationPolicyTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();

        AdministratorIsGloballyAllowed(failures);
        ScopedEngineerWithinScope(failures);
        EngineerWithoutScopeCannotCreateRootSite(failures);
        OutOfScopeReturnsNotFound(failures);
        ServerPrincipalResolution(failures);

        return failures;
    }

    private static void AdministratorIsGloballyAllowed(List<string> failures)
    {
        var adminId = UserId.New();
        var adminScopes = Array.Empty<Scope>();
        var adminContext = new CallerContext(adminId, "admin", Role.Administrator,
            Array.Empty<string>(), adminScopes, "corr-1");

        var decision = new AuthorizationDecision();

        var result = decision.Check(adminContext, "MANAGE_SITE");
        if (result != AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Administrator must be Allowed for any capability globally.");

        var targetResult = decision.CheckTarget(adminContext, Guid.NewGuid());
        if (targetResult != AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Administrator must be Allowed for any target Site.");
    }

    private static void ScopedEngineerWithinScope(List<string> failures)
    {
        var siteId = Guid.NewGuid();
        var engineerId = UserId.New();
        var engineerScope = new Scope(ScopeId.New(), engineerId, siteId, null);
        var engineerContext = new CallerContext(engineerId, "engineer", Role.Engineer,
            Array.Empty<string>(), new[] { engineerScope }, "corr-2");

        var decision = new AuthorizationDecision();

        var withinScope = decision.CheckTarget(engineerContext, siteId);
        if (withinScope != AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Engineer must be Allowed within assigned Site scope.");

        var outOfScope = decision.CheckTarget(engineerContext, Guid.NewGuid());
        if (outOfScope == AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Engineer must NOT be Allowed outside assigned Site scope.");
    }

    private static void EngineerWithoutScopeCannotCreateRootSite(List<string> failures)
    {
        var engineerId = UserId.New();
        var noScopeContext = new CallerContext(engineerId, "engineer-noscope", Role.Engineer,
            Array.Empty<string>(), Array.Empty<Scope>(), "corr-3");

        var decision = new AuthorizationDecision();

        var result = decision.Check(noScopeContext, "CREATE_ROOT_SITE");
        if (result == AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Engineer without Site scope must NOT be Allowed to create root Site.");
    }

    private static void OutOfScopeReturnsNotFound(List<string> failures)
    {
        var operatorId = UserId.New();
        var siteA = Guid.NewGuid();
        var opScope = new Scope(ScopeId.New(), operatorId, siteA, null);
        var operatorContext = new CallerContext(operatorId, "operator", Role.Operator,
            Array.Empty<string>(), new[] { opScope }, "corr-4");

        var decision = new AuthorizationDecision();

        var siteB = Guid.NewGuid();
        var targetResult = decision.CheckTarget(operatorContext, siteB);
        if (targetResult == AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Out-of-scope target must NOT return Allowed.");
    }

    private static void ServerPrincipalResolution(List<string> failures)
    {
        var viewerId = UserId.New();
        var siteId = Guid.NewGuid();
        var viewerScope = new Scope(ScopeId.New(), viewerId, siteId, null);
        var viewerContext = new CallerContext(viewerId, "viewer", Role.Viewer,
            Array.Empty<string>(), new[] { viewerScope }, "corr-5");

        var decision = new AuthorizationDecision();

        var mutate = decision.Check(viewerContext, "MANAGE_ASSET");
        if (mutate == AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Viewer must not be Allowed to manage assets.");

        var readOnly = decision.CheckTarget(viewerContext, siteId);
        if (readOnly != AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Viewer must be Allowed to read within assigned scope.");
    }
}
