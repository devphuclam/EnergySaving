using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Application;

namespace IUMP.Tests.Unit.IAM;

public static class AuthorizationPolicyTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();

        AdministratorIsGloballyAllowed(failures);
        AdministratorHasAuditCapability(failures);
        ScopedEngineerWithinScope(failures);
        EngineerWithoutScopeCannotCreateRootSite(failures);
        EngineerHasCapability(failures);
        OutOfScopeReturnsNotFound(failures);
        ServerPrincipalResolution(failures);
        CallerContextRolesCollection(failures);

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

    private static void AdministratorHasAuditCapability(List<string> failures)
    {
        var adminContext = new CallerContext(
            UserId.New(), "admin", new[] { Role.Administrator },
            new[] { "AUDIT_READ" }, Array.Empty<Scope>(), "corr-1");

        var decision = new AuthorizationDecision();

        var result = decision.Check(adminContext, "AUDIT_READ");
        if (result != AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Administrator with AUDIT_READ capability must be Allowed.");
    }

    private static void ScopedEngineerWithinScope(List<string> failures)
    {
        var siteId = Guid.NewGuid();
        var engineerScope = new Scope(ScopeId.New(), UserId.New(), siteId, null);
        var engineerContext = new CallerContext(
            UserId.New(), "engineer", new[] { Role.Engineer },
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
            UserId.New(), "engineer-noscope", new[] { Role.Engineer },
            Array.Empty<string>(), Array.Empty<Scope>(), "corr-3");

        var decision = new AuthorizationDecision();

        var result = decision.Check(noScopeContext, "CREATE_ROOT_SITE");
        if (result == AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Engineer without Site scope must NOT be Allowed to create root Site.");
    }

    private static void EngineerHasCapability(List<string> failures)
    {
        var engineerContext = new CallerContext(
            UserId.New(), "engineer", new[] { Role.Engineer },
            Array.Empty<string>(), Array.Empty<Scope>(), "corr-6");

        var decision = new AuthorizationDecision();

        var manageArea = decision.Check(engineerContext, "MANAGE_AREA");
        if (manageArea != AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Engineer must be Allowed MANAGE_AREA capability.");

        var manageUser = decision.Check(engineerContext, "MANAGE_USER");
        if (manageUser == AuthorizationResult.Allowed)
            failures.Add("T014-FAIL: Engineer must NOT be Allowed MANAGE_USER capability.");
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

    private static void CallerContextRolesCollection(List<string> failures)
    {
        var multiRoleContext = new CallerContext(
            UserId.New(), "multi", new[] { Role.Engineer, Role.Operator },
            Array.Empty<string>(), Array.Empty<Scope>(), "corr-7");

        if (multiRoleContext.Roles.Count != 2)
            failures.Add("T014-FAIL: CallerContext must preserve multiple roles.");
        if (multiRoleContext.PrimaryRole != Role.Engineer)
            failures.Add("T014-FAIL: CallerContext.PrimaryRole must be the first role.");
        if (!multiRoleContext.Roles.Contains(Role.Operator))
            failures.Add("T014-FAIL: CallerContext.Roles must contain all assigned roles.");
    }
}
