using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Application;
using IUMP.Modules.IAM.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Unit.IAM;

public sealed class TestPocCredentialHashProvider : IPocCredentialHashProvider
{
    public string GetPasswordHash(string username) => "AQAAAAIAAYagAAAAE-test-hash-only";
}

public static class PocIdentityFixtureTests
{
    private static readonly IPocCredentialHashProvider TestHash = new TestPocCredentialHashProvider();

    public static async Task<List<string>> Run()
    {
        var failures = new List<string>();

        await FixtureDefaultDisabled(failures);
        await NoCommittedCredentialHash(failures);
        await FixtureWithHashCreatesUsers(failures);
        await NoPreSiteScope(failures);
        await PostSiteFixtureAssignsScopes(failures);
        await FixtureIsIdempotent(failures);
        await TransactionRollbackRestoresState(failures);

        return failures;
    }

    private static FakeIamCommandRepository CreateEmptyRepo()
    {
        return new FakeIamCommandRepository();
    }

    private static FakeIamCommandRepository CreateRepoWithSeed()
    {
        var repo = new FakeIamCommandRepository();
        repo.SeedCapability(new Capability(CapabilityId.New(), "AUDIT_READ", "Audit Review"));
        return repo;
    }

    private static async Task FixtureDefaultDisabled(List<string> failures)
    {
        var repo = CreateEmptyRepo();
        var fixture = new PocIdentityFixture(repo);
        if (fixture.IsFixtureEnabled)
            failures.Add("T016-FAIL: PocIdentityFixture must be disabled by default.");

        var users = fixture.GetDeterministicUsers();
        if (users.Count != 0)
            failures.Add("T016-FAIL: Disabled fixture with NullPocCredentialHashProvider must return 0 users.");
    }

    private static async Task NoCommittedCredentialHash(List<string> failures)
    {
        var repo = CreateEmptyRepo();
        var fixture = new PocIdentityFixture(repo, TestHash, true);
        var users = fixture.GetDeterministicUsers();
        foreach (var user in users)
        {
            if (user.PasswordHash == "AQAAAAIAAYagAAAAEJ7U8Kx8mHs5jKZy6JqV0c7f3e2d1b9a4c5d6e7f8g9h0i1j2k3l4m5n6o7p8q9r0s1t2u3v4w5x6y7z8")
                failures.Add($"T016-FAIL: POC user '{user.Username}' must not have the committed hash literal.");
        }
    }

    private static async Task FixtureWithHashCreatesUsers(List<string> failures)
    {
        var repo = CreateEmptyRepo();
        var fixture = new PocIdentityFixture(repo, TestHash, true);
        var users = fixture.GetDeterministicUsers();

        if (users.Count != 5)
            failures.Add("T016-FAIL: Explicitly enabled fixture with hash provider must return 5 users.");

        var expectedRoles = new[] { Role.Administrator, Role.Engineer, Role.Operator, Role.Manager, Role.Viewer };
        foreach (var role in expectedRoles)
        {
            if (!users.Any(u => u.Roles.Contains(role)))
                failures.Add($"T016-FAIL: Fixture must include a user with role {role}.");
        }
    }

    private static async Task NoPreSiteScope(List<string> failures)
    {
        var repo = CreateEmptyRepo();
        var fixture = new PocIdentityFixture(repo, TestHash, true);
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
        var repo = CreateRepoWithSeed();
        var fixture = new PocIdentityFixture(repo, TestHash, true);
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
        var repo = CreateRepoWithSeed();
        var fixture = new PocIdentityFixture(repo, TestHash, true);
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

    private static async Task TransactionRollbackRestoresState(List<string> failures)
    {
        var repo = new FakeIamCommandRepository();
        var userId = UserId.New();

        var originalUser = new User(userId, "rollback-test", "hash", UserStatus.Active, new[] { Role.Viewer });
        repo.SeedUser(originalUser);
        repo.SeedRole(userId, Role.Viewer);

        var tx = (FakeIamTransaction)(await repo.BeginTransactionAsync());

        var newUserId = UserId.New();
        var newUser = new User(newUserId, "new-rollback", "hash", UserStatus.Active, new[] { Role.Engineer });
        await repo.AddUserAsync(newUser);
        await repo.AssignRoleAsync(newUserId, Role.Engineer, userId);
        await repo.AddScopeAsync(new Scope(ScopeId.New(), newUserId, Guid.NewGuid(), null));

        await tx.RollbackAsync();

        var originalAfter = await repo.GetUserAsync(userId);
        if (originalAfter == null)
            failures.Add("T016-FAIL: Original user must exist after transaction rollback.");

        var newAfter = await repo.GetUserAsync(newUserId);
        if (newAfter != null)
            failures.Add("T016-FAIL: New user must NOT exist after transaction rollback.");

        var newRoles = await repo.GetRolesForUserAsync(newUserId);
        if (newRoles.Count > 0)
            failures.Add("T016-FAIL: New user's roles must be removed after rollback.");

        var newScopes = await repo.GetScopesForUserAsync(newUserId);
        if (newScopes.Count > 0)
            failures.Add("T016-FAIL: New user's scopes must be removed after rollback.");

        var originalRoles = await repo.GetRolesForUserAsync(userId);
        if (!originalRoles.Contains(Role.Viewer))
            failures.Add("T016-FAIL: Original user's roles must be preserved after rollback.");
    }
}
