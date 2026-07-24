using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Application;

namespace IUMP.Tests.Unit.IAM;

public static class PocIdentityFixtureTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();

        FiveDeterministicUsers(failures);
        NoPreSiteScope(failures);
        PostSiteFixtureAssignsScopes(failures);
        FixtureIsIdempotent(failures);

        return failures;
    }

    private static void FiveDeterministicUsers(List<string> failures)
    {
        var fixture = new PocIdentityFixture();
        var users = fixture.GetDeterministicUsers();

        if (users.Count != 5)
            failures.Add("T016-FAIL: PocIdentityFixture must provide exactly 5 deterministic users.");

        var expectedRoles = new[] { Role.Administrator, Role.Engineer, Role.Operator, Role.Manager, Role.Viewer };
        foreach (var role in expectedRoles)
        {
            if (!users.Any(u => u.Role == role))
                failures.Add($"T016-FAIL: Fixture must include a user with role {role}.");
        }
    }

    private static void NoPreSiteScope(List<string> failures)
    {
        var fixture = new PocIdentityFixture();
        var users = fixture.GetDeterministicUsers();

        var hasAdmin = users.Any(u => u.Role == Role.Administrator);
        var hasEngineer = users.Any(u => u.Role == Role.Engineer);

        if (!hasAdmin || !hasEngineer)
            failures.Add("T016-FAIL: Deterministic users must include at minimum Admin and Engineer.");

        if (users.Any(u => u.Status != UserStatus.Active))
            failures.Add("T016-FAIL: All deterministic users must be Active.");
    }

    private static void PostSiteFixtureAssignsScopes(List<string> failures)
    {
        var fixture = new PocIdentityFixture();
        var siteId = Guid.NewGuid();

        var applied = fixture.ApplyPostSiteFixture(siteId);

        if (!applied)
            failures.Add("T016-FAIL: Post-Site fixture must apply when given a valid Site ID.");
    }

    private static void FixtureIsIdempotent(List<string> failures)
    {
        var fixture = new PocIdentityFixture();

        if (!fixture.IsIdempotent)
            failures.Add("T016-FAIL: Post-Site fixture must be idempotent.");
    }
}
