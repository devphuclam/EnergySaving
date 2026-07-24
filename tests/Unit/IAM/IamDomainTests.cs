using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Application;

namespace IUMP.Tests.Unit.IAM;

public static class IamDomainTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();

        UserRecordConstruction(failures);
        UserStatusLifecycle(failures);
        UserRolesCollection(failures);
        RoleEnumValues(failures);
        ScopeConstruction(failures);
        CapabilityConstruction(failures);
        SessionLifecycle(failures);
        ActiveUserEligibility(failures);
        EligibilityNoScope(failures);
        EligibilityScopeMismatch(failures);
        DataOwnerEligibilityNoSiteId(failures);
        DataOwnerEligibilityWithSiteId(failures);

        return failures;
    }

    private static void UserRecordConstruction(List<string> failures)
    {
        var id = UserId.New();
        var user = new User(id, "testuser", "hash", UserStatus.Active, new[] { Role.Engineer });

        if (user.Id != id)
            failures.Add("T013-FAIL: User.Id must match constructor argument.");
        if (user.Username != "testuser")
            failures.Add("T013-FAIL: User.Username must match constructor argument.");
        if (user.PasswordHash != "hash")
            failures.Add("T013-FAIL: User.PasswordHash must match constructor argument.");
        if (user.Status != UserStatus.Active)
            failures.Add("T013-FAIL: User.Status must be Active after construction.");
        if (!user.Roles.Contains(Role.Engineer))
            failures.Add("T013-FAIL: User.Roles must contain the constructor role.");
    }

    private static void UserStatusLifecycle(List<string> failures)
    {
        var user = new User(UserId.New(), "status-test", "hash", UserStatus.Active, new[] { Role.Operator });

        if (!user.IsActive())
            failures.Add("T013-FAIL: Active user must be IsActive.");

        user.Disable();

        if (user.Status != UserStatus.Disabled)
            failures.Add("T013-FAIL: After Disable(), Status must be Disabled.");
        if (user.IsActive())
            failures.Add("T013-FAIL: Disabled user must not be IsActive.");
    }

    private static void UserRolesCollection(List<string> failures)
    {
        var user = new User(UserId.New(), "multi-role", "hash", UserStatus.Active,
            new[] { Role.Engineer, Role.Operator });

        if (user.Roles.Count != 2)
            failures.Add("T013-FAIL: User with 2 roles must have Roles.Count == 2.");
        if (!user.HasRole(Role.Engineer))
            failures.Add("T013-FAIL: HasRole must return true for assigned role.");
        if (!user.HasRole(Role.Operator))
            failures.Add("T013-FAIL: HasRole must return true for second assigned role.");
        if (user.HasRole(Role.Administrator))
            failures.Add("T013-FAIL: HasRole must return false for unassigned role.");
    }

    private static void RoleEnumValues(List<string> failures)
    {
        var roles = Enum.GetNames<Role>();
        var expected = new[] { "Administrator", "Engineer", "Operator", "Manager", "Viewer" };

        foreach (var role in expected)
        {
            if (!roles.Contains(role))
                failures.Add($"T013-FAIL: Role enum must contain '{role}'.");
        }

        if (roles.Length != 5)
            failures.Add("T013-FAIL: There must be exactly 5 base roles.");
    }

    private static void ScopeConstruction(List<string> failures)
    {
        var userId = UserId.New();
        var siteId = Guid.NewGuid();

        var siteScope = new Scope(ScopeId.New(), userId, siteId, null);

        if (siteScope.UserId != userId)
            failures.Add("T013-FAIL: Scope.UserId must match constructor argument.");
        if (siteScope.SiteId != siteId)
            failures.Add("T013-FAIL: Scope.SiteId must match constructor argument.");
        if (siteScope.AreaId != null)
            failures.Add("T013-FAIL: Site-level Scope must have null AreaId.");
        if (!siteScope.IsSiteScope)
            failures.Add("T013-FAIL: Site-level Scope must be IsSiteScope.");
        if (siteScope.IsAreaScope)
            failures.Add("T013-FAIL: Site-level Scope must not be IsAreaScope.");

        var areaId = Guid.NewGuid();
        var areaScope = new Scope(ScopeId.New(), userId, siteId, areaId);

        if (areaScope.AreaId != areaId)
            failures.Add("T013-FAIL: Area-level Scope must have AreaId.");
        if (!areaScope.IsAreaScope)
            failures.Add("T013-FAIL: Area-level Scope must be IsAreaScope.");
    }

    private static void CapabilityConstruction(List<string> failures)
    {
        var cap = new Capability(CapabilityId.New(), "AUDIT_READ", "Audit Review");

        if (cap.Code != "AUDIT_READ")
            failures.Add("T013-FAIL: Capability.Code must match constructor argument.");
        if (cap.Name != "Audit Review")
            failures.Add("T013-FAIL: Capability.Name must match constructor argument.");
    }

    private static void SessionLifecycle(List<string> failures)
    {
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
        var userId = UserId.New();
        var session = new Session(SessionId.New(), userId, "tokenhash",
            now, now.AddMinutes(20), now.AddHours(8));

        if (session.UserId != userId)
            failures.Add("T013-FAIL: Session.UserId must match constructor argument.");
        if (session.TokenHash != "tokenhash")
            failures.Add("T013-FAIL: Session.TokenHash must match constructor argument.");
        if (session.IsRevoked)
            failures.Add("T013-FAIL: New session must not be revoked.");
        if (!session.IsValid(now))
            failures.Add("T013-FAIL: New session must be valid at creation time.");

        var expiredTime = now.AddHours(9);
        if (!session.IsExpired(expiredTime))
            failures.Add("T013-FAIL: Session past absolute expiry must be IsExpired.");

        var touchedTime = now.AddMinutes(10);
        session.Touch(touchedTime, TimeSpan.FromMinutes(20));
        if (!session.IsValid(touchedTime.AddMinutes(19)))
            failures.Add("T013-FAIL: Touched session must be valid within idle window.");

        session.Revoke(now.AddHours(2));
        if (!session.IsRevoked)
            failures.Add("T013-FAIL: Revoked session must be IsRevoked.");
        if (session.IsValid(now.AddHours(2)))
            failures.Add("T013-FAIL: Revoked session must not be valid.");
    }

    private static void ActiveUserEligibility(List<string> failures)
    {
        var activeUserId = UserId.New();
        var disabledUserId = UserId.New();

        var activeUser = new User(activeUserId, "activeuser", "hash", UserStatus.Active, new[] { Role.Engineer });
        var disabledUser = new User(disabledUserId, "disableduser", "hash", UserStatus.Disabled, new[] { Role.Engineer });

        var eligibility = new ActiveUserEligibility(new[] { activeUser, disabledUser });

        var activeResult = eligibility.Check(activeUserId);
        if (activeResult != EligibilityResult.Eligible)
            failures.Add("T013-FAIL: Active user eligibility check must return Eligible.");

        var disabledResult = eligibility.Check(disabledUserId);
        if (disabledResult == EligibilityResult.Eligible)
            failures.Add("T013-FAIL: Disabled user eligibility must NOT return Eligible.");
    }

    private static void EligibilityNoScope(List<string> failures)
    {
        var userId = UserId.New();
        var user = new User(userId, "noscope", "hash", UserStatus.Active, new[] { Role.Engineer });

        var eligibility = new ActiveUserEligibility(new[] { user });

        var result = eligibility.Check(userId, siteId: Guid.NewGuid());
        if (result != EligibilityResult.NoScope)
            failures.Add("T013-FAIL: Active user without any scope must return NoScope.");
    }

    private static void EligibilityScopeMismatch(List<string> failures)
    {
        var userId = UserId.New();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var user = new User(userId, "scoped-user", "hash", UserStatus.Active, new[] { Role.Engineer });
        var scope = new Scope(ScopeId.New(), userId, siteA, null);

        var eligibility = new ActiveUserEligibility(new[] { user }, new[] { scope });

        var match = eligibility.Check(userId, siteId: siteA);
        if (match != EligibilityResult.Eligible)
            failures.Add("T013-FAIL: User with matching Site scope must return Eligible.");

        var mismatch = eligibility.Check(userId, siteId: siteB);
        if (mismatch != EligibilityResult.ScopeMismatch)
            failures.Add("T013-FAIL: User with scope for site A accessing site B must return ScopeMismatch.");
    }

    private static void DataOwnerEligibilityNoSiteId(List<string> failures)
    {
        var userId = UserId.New();
        var user = new User(userId, "dataowner", "hash", UserStatus.Active, new[] { Role.Engineer });
        var eligibility = new ActiveUserEligibility(new[] { user });

        var isEligible = eligibility.IsDataOwnerEligible(userId);

        if (!isEligible)
            failures.Add("T013-FAIL: Active user must be Data Owner eligible when no siteId is provided.");

        var nonExistent = new UserId(Guid.NewGuid());
        var notEligible = eligibility.IsDataOwnerEligible(nonExistent);

        if (notEligible)
            failures.Add("T013-FAIL: Non-existent user must NOT be Data Owner eligible.");
    }

    private static void DataOwnerEligibilityWithSiteId(List<string> failures)
    {
        var userId = UserId.New();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var user = new User(userId, "scoped-dataowner", "hash", UserStatus.Active, new[] { Role.Engineer });
        var scope = new Scope(ScopeId.New(), userId, siteA, null);

        var eligibility = new ActiveUserEligibility(new[] { user }, new[] { scope });

        var withMatchingSite = eligibility.IsDataOwnerEligible(userId, siteA);
        if (!withMatchingSite)
            failures.Add("T013-FAIL: Active user with scope for site A must be Data Owner eligible for site A.");

        var withOtherSite = eligibility.IsDataOwnerEligible(userId, siteB);
        if (withOtherSite)
            failures.Add("T013-FAIL: Active user without scope for site B must NOT be Data Owner eligible for site B.");
    }
}
