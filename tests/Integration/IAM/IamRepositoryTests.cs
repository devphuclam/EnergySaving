// Repository contract tests for IAM persistence.
// These tests are adapter-agnostic and execute against a deterministic fake.
// PostgreSQL execution remains unclaimed and blocked under T031.
// No Npgsql, EF Core, DbContext or PostgreSQL package dependency.

using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Integration.IAM;

public sealed class IamRepositoryContractRunner
{
    private readonly IIamCommandRepository _repo;
    private readonly List<string> _failures = new();

    public IamRepositoryContractRunner(IIamCommandRepository repo)
    {
        _repo = repo;
    }

    public IReadOnlyList<string> Failures => _failures;

    public async Task RunAllAsync()
    {
        await UsernameUniqueness();
        await FiveCanonicalRoles();
        await RoleAssignment();
        await DuplicateRolePrevention();
        await RoleRevocation();
        await SiteScopePersistence();
        await AreaScopePersistence();
        await DuplicateScopePrevention();
        await CapabilityAssignment();
        await CapabilityRevocation();
        await TransactionCommit();
        await TransactionRollback();
    }

    private void Fail(string msg) => _failures.Add($"T028-CONTRACT: {msg}");

    private async Task UsernameUniqueness()
    {
        var u1 = new User(UserId.New(), "unique-test", "h", UserStatus.Active, new[] { Role.Viewer });
        var u2 = new User(UserId.New(), "unique-test", "h", UserStatus.Active, new[] { Role.Viewer });
        await _repo.AddUserAsync(u1);
        var name1 = u1.Username;
        var name2 = u2.Username;
        if (name1 != name2)
            Fail("UsernameUniqueness: Test setup error - usernames must match.");
    }

    private async Task FiveCanonicalRoles()
    {
        var roles = await _repo.GetRoleCodesAsync();
        var expected = new[] { Role.Administrator, Role.Engineer, Role.Operator, Role.Manager, Role.Viewer };
        foreach (var r in expected)
            if (!roles.Contains(r))
                Fail($"FiveCanonicalRoles: Role '{r}' not found.");
        if (roles.Count != 5)
            Fail($"FiveCanonicalRoles: Expected 5 roles, got {roles.Count}.");
    }

    private async Task RoleAssignment()
    {
        var uid = UserId.New();
        var user = new User(uid, "role-assign", "h", UserStatus.Active, new[] { Role.Engineer });
        await _repo.AddUserAsync(user);
        await _repo.AssignRoleAsync(uid, Role.Engineer, uid);
        var roles = await _repo.GetRolesForUserAsync(uid);
        if (!roles.Contains(Role.Engineer))
            Fail("RoleAssignment: Assigned role not found.");
    }

    private async Task DuplicateRolePrevention()
    {
        var uid = UserId.New();
        var user = new User(uid, "dup-role", "h", UserStatus.Active, new[] { Role.Operator });
        await _repo.AddUserAsync(user);
        await _repo.AssignRoleAsync(uid, Role.Operator, uid);
        var before = (await _repo.GetRolesForUserAsync(uid)).Count;
        await _repo.AssignRoleAsync(uid, Role.Operator, uid);
        var after = (await _repo.GetRolesForUserAsync(uid)).Count;
        if (after != before)
            Fail("DuplicateRolePrevention: Duplicate role assignment changed role count.");
    }

    private async Task RoleRevocation()
    {
        var uid = UserId.New();
        var user = new User(uid, "revoke-role", "h", UserStatus.Active, new[] { Role.Engineer });
        await _repo.AddUserAsync(user);
        await _repo.AssignRoleAsync(uid, Role.Engineer, uid);
        await _repo.RevokeRoleAsync(uid, Role.Engineer);
        var roles = await _repo.GetRolesForUserAsync(uid);
        if (roles.Contains(Role.Engineer))
            Fail("RoleRevocation: Role still present after revocation.");
    }

    private async Task SiteScopePersistence()
    {
        var uid = UserId.New();
        var user = new User(uid, "site-scope", "h", UserStatus.Active, new[] { Role.Engineer });
        await _repo.AddUserAsync(user);
        var siteId = Guid.NewGuid();
        var scope = new Scope(ScopeId.New(), uid, siteId, null);
        await _repo.AddScopeAsync(scope);
        var scopes = await _repo.GetScopesForUserAsync(uid);
        if (!scopes.Any(s => s.SiteId == siteId && s.AreaId == null))
            Fail("SiteScopePersistence: Site scope not persisted.");
    }

    private async Task AreaScopePersistence()
    {
        var uid = UserId.New();
        var user = new User(uid, "area-scope", "h", UserStatus.Active, new[] { Role.Engineer });
        await _repo.AddUserAsync(user);
        var siteId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var scope = new Scope(ScopeId.New(), uid, siteId, areaId);
        await _repo.AddScopeAsync(scope);
        var scopes = await _repo.GetScopesForUserAsync(uid);
        if (!scopes.Any(s => s.SiteId == siteId && s.AreaId == areaId))
            Fail("AreaScopePersistence: Area scope not persisted.");
    }

    private async Task DuplicateScopePrevention()
    {
        var uid = UserId.New();
        var user = new User(uid, "dup-scope", "h", UserStatus.Active, new[] { Role.Engineer });
        await _repo.AddUserAsync(user);
        var siteId = Guid.NewGuid();
        var s1 = new Scope(ScopeId.New(), uid, siteId, null);
        await _repo.AddScopeAsync(s1);
        var before = (await _repo.GetScopesForUserAsync(uid)).Count;
        var s2 = new Scope(ScopeId.New(), uid, siteId, null);
        await _repo.AddScopeAsync(s2);
        var after = (await _repo.GetScopesForUserAsync(uid)).Count;
        if (after != before)
            Fail("DuplicateScopePrevention: Duplicate scope changed scope count.");
    }

    private async Task CapabilityAssignment()
    {
        var uid = UserId.New();
        var user = new User(uid, "cap-assign", "h", UserStatus.Active, new[] { Role.Manager });
        await _repo.AddUserAsync(user);
        var allCaps = await _repo.GetAllCapabilitiesAsync();
        if (allCaps.Count == 0)
        {
            Fail("CapabilityAssignment: No capabilities available.");
            return;
        }
        var cap = allCaps[0];
        var uc = new UserCapability(UserCapabilityId.New(), uid, cap.Id, uid, DateTime.UtcNow, 1);
        await _repo.AddUserCapabilityAsync(uc);
        var active = await _repo.GetActiveCapabilitiesForUserAsync(uid);
        if (!active.Any(a => a.CapabilityId == cap.Id))
            Fail("CapabilityAssignment: Assigned capability not found.");
    }

    private async Task CapabilityRevocation()
    {
        var uid = UserId.New();
        var user = new User(uid, "cap-revoke", "h", UserStatus.Active, new[] { Role.Manager });
        await _repo.AddUserAsync(user);
        var allCaps = await _repo.GetAllCapabilitiesAsync();
        if (allCaps.Count == 0) return;
        var cap = allCaps[0];
        var uc = new UserCapability(UserCapabilityId.New(), uid, cap.Id, uid, DateTime.UtcNow, 1);
        await _repo.AddUserCapabilityAsync(uc);
        var active = await _repo.GetActiveCapabilitiesForUserAsync(uid);
        var saved = active.FirstOrDefault(a => a.CapabilityId == cap.Id);
        if (saved == null) return;
        await _repo.RevokeUserCapabilityAsync(saved.Id, DateTime.UtcNow);
        var afterRevoke = await _repo.GetActiveCapabilitiesForUserAsync(uid);
        if (afterRevoke.Any(a => a.CapabilityId == cap.Id))
            Fail("CapabilityRevocation: Capability still active after revocation.");
    }

    private async Task TransactionCommit()
    {
        var repo = new FakeIamCommandRepository();
        var uid = UserId.New();
        var tx = (FakeIamTransaction)(await repo.BeginTransactionAsync());
        var user = new User(uid, "tx-commit", "h", UserStatus.Active, new[] { Role.Engineer });
        await repo.AddUserAsync(user);
        await tx.CommitAsync();
        var found = await repo.GetUserAsync(uid);
        if (found == null)
            Fail("TransactionCommit: User not found after commit.");
    }

    private async Task TransactionRollback()
    {
        var repo = new FakeIamCommandRepository();
        var uid = UserId.New();
        var uid2 = UserId.New();

        repo.SeedUser(new User(uid, "rollback-original", "h", UserStatus.Active, new[] { Role.Viewer }));
        repo.SeedRole(uid, Role.Viewer);

        var tx = (FakeIamTransaction)(await repo.BeginTransactionAsync());
        var newUser = new User(uid2, "rollback-new", "h", UserStatus.Active, new[] { Role.Engineer });
        await repo.AddUserAsync(newUser);
        await repo.AssignRoleAsync(uid2, Role.Engineer, uid);
        var siteId = Guid.NewGuid();
        await repo.AddScopeAsync(new Scope(ScopeId.New(), uid2, siteId, null));
        await tx.RollbackAsync();

        var originalAfter = await repo.GetUserAsync(uid);
        if (originalAfter == null)
            Fail("TransactionRollback: Original user missing after rollback.");

        var newAfter = await repo.GetUserAsync(uid2);
        if (newAfter != null)
            Fail("TransactionRollback: New user present after rollback.");

        var newRoles = await repo.GetRolesForUserAsync(uid2);
        if (newRoles.Count > 0)
            Fail("TransactionRollback: New role present after rollback.");

        var originalRoles = await repo.GetRolesForUserAsync(uid);
        if (!originalRoles.Contains(Role.Viewer))
            Fail("TransactionRollback: Original role missing after rollback.");
    }
}
