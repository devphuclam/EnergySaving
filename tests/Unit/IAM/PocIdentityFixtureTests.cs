using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Application;
using IUMP.Modules.IAM.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.IAM;

public static class PocIdentityFixtureTests
{
    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();

        await FiveDeterministicUsers(failures);
        await NoPreSiteScope(failures);
        await PostSiteFixtureAssignsScopes(failures);
        await FixtureIsIdempotent(failures);

        return failures;
    }

    private static FakeIamCommandRepository CreateRepoWithUsers()
    {
        var repo = new FakeIamCommandRepository();
        var adminId = UserId.Parse("00000000-0000-0000-0000-000000000001");
        var engineerId = UserId.Parse("00000000-0000-0000-0000-000000000002");
        var operatorId = UserId.Parse("00000000-0000-0000-0000-000000000003");
        var managerId = UserId.Parse("00000000-0000-0000-0000-000000000004");
        var viewerId = UserId.Parse("00000000-0000-0000-0000-000000000005");

        repo.SeedUser(new User(adminId, "admin", "hash", UserStatus.Active, new[] { Role.Administrator }));
        repo.SeedUser(new User(engineerId, "engineer", "hash", UserStatus.Active, new[] { Role.Engineer }));
        repo.SeedUser(new User(operatorId, "operator", "hash", UserStatus.Active, new[] { Role.Operator }));
        repo.SeedUser(new User(managerId, "manager", "hash", UserStatus.Active, new[] { Role.Manager }));
        repo.SeedUser(new User(viewerId, "viewer", "hash", UserStatus.Active, new[] { Role.Viewer }));

        repo.SeedRole(adminId, Role.Administrator);
        repo.SeedRole(engineerId, Role.Engineer);
        repo.SeedRole(operatorId, Role.Operator);
        repo.SeedRole(managerId, Role.Manager);
        repo.SeedRole(viewerId, Role.Viewer);

        repo.SeedCapability(new Capability(CapabilityId.New(), "AUDIT_READ", "Audit Review"));

        return repo;
    }

    private static async Task FiveDeterministicUsers(List<string> failures)
    {
        var repo = CreateRepoWithUsers();
        var fixture = new PocIdentityFixture(repo);
        var users = fixture.GetDeterministicUsers();

        if (users.Count != 5)
            failures.Add("T016-FAIL: PocIdentityFixture must provide exactly 5 deterministic users.");

        var expectedRoles = new[] { Role.Administrator, Role.Engineer, Role.Operator, Role.Manager, Role.Viewer };
        foreach (var role in expectedRoles)
        {
            if (!users.Any(u => u.Roles.Contains(role)))
                failures.Add($"T016-FAIL: Fixture must include a user with role {role}.");
        }
    }

    private static async Task NoPreSiteScope(List<string> failures)
    {
        var repo = CreateRepoWithUsers();
        var fixture = new PocIdentityFixture(repo);
        var users = fixture.GetDeterministicUsers();

        var hasAdmin = users.Any(u => u.Roles.Contains(Role.Administrator));
        var hasEngineer = users.Any(u => u.Roles.Contains(Role.Engineer));

        if (!hasAdmin || !hasEngineer)
            failures.Add("T016-FAIL: Deterministic users must include at minimum Admin and Engineer.");

        if (users.Any(u => u.Status != UserStatus.Active))
            failures.Add("T016-FAIL: All deterministic users must be Active.");
    }

    private static async Task PostSiteFixtureAssignsScopes(List<string> failures)
    {
        var repo = CreateRepoWithUsers();
        var fixture = new PocIdentityFixture(repo);
        var siteId = Guid.NewGuid();

        var applied = await fixture.ApplyPostSiteFixtureAsync(siteId);

        if (!applied)
            failures.Add("T016-FAIL: Post-Site fixture must apply when given a valid Site ID.");

        var engineerUser = fixture.GetDeterministicUsers().First(u => u.HasRole(Role.Engineer));
        var scopes = await repo.GetScopesForUserAsync(engineerUser.Id);
        if (!scopes.Any(s => s.SiteId == siteId))
            failures.Add("T016-FAIL: Post-Site fixture must assign a scope for Engineer.");
    }

    private static async Task FixtureIsIdempotent(List<string> failures)
    {
        var repo = CreateRepoWithUsers();
        var fixture = new PocIdentityFixture(repo);
        var siteId = Guid.NewGuid();

        await fixture.ApplyPostSiteFixtureAsync(siteId);

        var engineerUser = fixture.GetDeterministicUsers().First(u => u.HasRole(Role.Engineer));
        var scopesAfterFirst = await repo.GetScopesForUserAsync(engineerUser.Id);
        var countBeforeSecond = scopesAfterFirst.Count;

        await fixture.ApplyPostSiteFixtureAsync(siteId);

        var scopesAfterSecond = await repo.GetScopesForUserAsync(engineerUser.Id);
        if (scopesAfterSecond.Count != countBeforeSecond)
            failures.Add("T016-FAIL: Post-Site fixture must be idempotent (scope count unchanged).");
    }
}
