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
        var adminContext = new CallerContext(
            UserId.New(), "admin", Role.Administrator,
            Array.Empty<string>(), Array.Empty<Scope>(), "corr-1");

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
        var engineerScope = new Scope(ScopeId.New(), UserId.New(), siteId, null);
        var engineerContext = new CallerContext(
            UserId.New(), "engineer", Role.Engineer,
            Array.Empty<string>(), new[] { engineerScope }, "corr-2");

        var decision = new AuthorizationDecision();

        var withinScope = decision.CheckTarget(engineerContext, siteId);
        if (withinScope != AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Engineer must be Allowed within assigned Site scope.");

        var outOfScope = decision.CheckTarget(engineerContext, Guid.NewGuid());
        if (outOfScope != AuthorizationResult.NotFound)
            failures.Add("T014-FAIL: Out-of-scope target must return NotFound, not Forbidden or Allowed.");
    }

    private static void EngineerWithoutScopeCannotCreateRootSite(List<string> failures)
    {
        var noScopeContext = new CallerContext(
            UserId.New(), "engineer-noscope", Role.Engineer,
            Array.Empty<string>(), Array.Empty<Scope>(), "corr-3");

        var decision = new AuthorizationDecision();

        var result = decision.Check(noScopeContext, "CREATE_ROOT_SITE");
        if (result == AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Engineer without Site scope must NOT be Allowed to create root Site.");
    }

    private static void OutOfScopeReturnsNotFound(List<string> failures)
    {
        var siteA = Guid.NewGuid();
        var opScope = new Scope(ScopeId.New(), UserId.New(), siteA, null);
        var operatorContext = new CallerContext(
            UserId.New(), "operator", Role.Operator,
            Array.Empty<string>(), new[] { opScope }, "corr-4");

        var decision = new AuthorizationDecision();

        var siteB = Guid.NewGuid();
        var targetResult = decision.CheckTarget(operatorContext, siteB);
        if (targetResult != AuthorizationResult.NotFound)
            failures.Add("T014-FAIL: Out-of-scope target must return NotFound exactly.");
    }

    private static void ServerPrincipalResolution(List<string> failures)
    {
        var siteId = Guid.NewGuid();
        var viewerScope = new Scope(ScopeId.New(), UserId.New(), siteId, null);
        var viewerContext = new CallerContext(
            UserId.New(), "viewer", Role.Viewer,
            Array.Empty<string>(), new[] { viewerScope }, "corr-5");

        var decision = new AuthorizationDecision();

        var mutate = decision.Check(viewerContext, "MANAGE_ASSET");
        if (mutate == AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Viewer must not be Allowed to manage assets.");

        var readOnly = decision.CheckTarget(viewerContext, siteId);
        if (readOnly != AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Viewer must be Allowed to read within assigned scope.");

        var outOfScope = decision.CheckTarget(viewerContext, Guid.NewGuid());
        if (outOfScope == AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Viewer must not be Allowed for out-of-scope targets.");
        if (outOfScope != AuthorizationResult.NotFound)
            failures.Add("T014-FAIL: Out-of-scope must be NotFound, not Forbidden.");
    }
}
