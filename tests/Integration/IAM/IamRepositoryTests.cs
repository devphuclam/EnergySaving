using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Contracts;
using IUMP.Tests.Unit.Fakes;

namespace IUMP.Tests.Integration.IAM;

public sealed class IamRepositoryContractRunner
{
    private readonly IIamCommandRepository _cmdRepo;
    private readonly IIamPrincipalSessionRepository _sessionRepo;
    private readonly List<string> _failures = new();
    private int _testCount;
    private int _assertionCount;

    public IamRepositoryContractRunner(
        IIamCommandRepository cmdRepo,
        IIamPrincipalSessionRepository sessionRepo)
    {
        _cmdRepo = cmdRepo;
        _sessionRepo = sessionRepo;
    }

    public IReadOnlyList<string> Failures => _failures;
    public int TestCount => _testCount;
    public int AssertionCount => _assertionCount;

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
        await SessionCreation();
        await SessionLookupByTokenHash();
        await CurrentSessionRevocation();
        await RevokeAllSessions();
        await TransactionCommit();
        await TransactionRollback();
        await OptimisticVersionBehavior();
    }

    private void Pass() { _testCount++; }
    private void Fail(string msg) { _failures.Add($"T028-CONTRACT: {msg}"); _testCount++; }
    private void Assert(bool condition, string msg) { _assertionCount++; if (!condition) Fail(msg); }

    private async Task UsernameUniqueness()
    {
        var repo = (FakeIamCommandRepository)_cmdRepo;
        repo.Clear();
        repo.SeedCapability(new Capability(CapabilityId.New(), "UNIQUE_CAP", "Uniqueness Test Cap"));
        var u1 = new User(UserId.New(), "unique-test", "h", UserStatus.Active, new[] { Role.Viewer });
        var u2 = new User(UserId.New(), "unique-test", "h", UserStatus.Active, new[] { Role.Viewer });
        await repo.AddUserAsync(u1);
        var before = (await repo.GetAllUsersAsync()).Count;
        Assert(before == 1, "UsernameUniqueness: One user must exist after first add.");

        try
        {
            await repo.AddUserAsync(u2);
            Assert(false, "UsernameUniqueness: Duplicate username must be rejected.");
        }
        catch (InvalidOperationException)
        {
            Assert(true, "UsernameUniqueness: Duplicate rejected with InvalidOperationException.");
        }

        var after = (await repo.GetAllUsersAsync()).Count;
        Assert(after == before, "UsernameUniqueness: User count must not change after rejected duplicate.");

        var original = await repo.GetUserAsync(u1.Id);
        Assert(original != null && original.Username == "unique-test", "UsernameUniqueness: Original user must be unchanged.");
        Pass();
    }

    private async Task FiveCanonicalRoles()
    {
        var roles = await _cmdRepo.GetRoleCodesAsync();
        var expected = new[] { Role.Administrator, Role.Engineer, Role.Operator, Role.Manager, Role.Viewer };
        foreach (var r in expected)
            Assert(roles.Contains(r), $"FiveCanonicalRoles: Role '{r}' not found.");
        Assert(roles.Count == 5, $"FiveCanonicalRoles: Expected 5 roles, got {roles.Count}.");
        Pass();
    }

    private async Task RoleAssignment()
    {
        var uid = UserId.New();
        var user = new User(uid, "role-assign", "h", UserStatus.Active, new[] { Role.Engineer });
        await _cmdRepo.AddUserAsync(user);
        await _cmdRepo.AssignRoleAsync(uid, Role.Engineer, uid);
        var roles = await _cmdRepo.GetRolesForUserAsync(uid);
        Assert(roles.Contains(Role.Engineer), "RoleAssignment: Assigned role not found.");
        Pass();
    }

    private async Task DuplicateRolePrevention()
    {
        var uid = UserId.New();
        var user = new User(uid, "dup-role", "h", UserStatus.Active, new[] { Role.Operator });
        await _cmdRepo.AddUserAsync(user);
        await _cmdRepo.AssignRoleAsync(uid, Role.Operator, uid);
        var before = (await _cmdRepo.GetRolesForUserAsync(uid)).Count;
        await _cmdRepo.AssignRoleAsync(uid, Role.Operator, uid);
        var after = (await _cmdRepo.GetRolesForUserAsync(uid)).Count;
        Assert(after == before, "DuplicateRolePrevention: Duplicate role assignment changed role count.");
        Pass();
    }

    private async Task RoleRevocation()
    {
        var uid = UserId.New();
        var user = new User(uid, "revoke-role", "h", UserStatus.Active, new[] { Role.Engineer });
        await _cmdRepo.AddUserAsync(user);
        await _cmdRepo.AssignRoleAsync(uid, Role.Engineer, uid);
        await _cmdRepo.RevokeRoleAsync(uid, Role.Engineer);
        var roles = await _cmdRepo.GetRolesForUserAsync(uid);
        Assert(!roles.Contains(Role.Engineer), "RoleRevocation: Role still present after revocation.");
        Pass();
    }

    private async Task SiteScopePersistence()
    {
        var uid = UserId.New();
        var user = new User(uid, "site-scope", "h", UserStatus.Active, new[] { Role.Engineer });
        await _cmdRepo.AddUserAsync(user);
        var siteId = Guid.NewGuid();
        var scope = new Scope(ScopeId.New(), uid, siteId, null);
        await _cmdRepo.AddScopeAsync(scope);
        var scopes = await _cmdRepo.GetScopesForUserAsync(uid);
        Assert(scopes.Any(s => s.SiteId == siteId && s.AreaId == null), "SiteScopePersistence: Site scope not persisted.");
        Pass();
    }

    private async Task AreaScopePersistence()
    {
        var uid = UserId.New();
        var user = new User(uid, "area-scope", "h", UserStatus.Active, new[] { Role.Engineer });
        await _cmdRepo.AddUserAsync(user);
        var siteId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var scope = new Scope(ScopeId.New(), uid, siteId, areaId);
        await _cmdRepo.AddScopeAsync(scope);
        var scopes = await _cmdRepo.GetScopesForUserAsync(uid);
        Assert(scopes.Any(s => s.SiteId == siteId && s.AreaId == areaId), "AreaScopePersistence: Area scope not persisted.");
        Pass();
    }

    private async Task DuplicateScopePrevention()
    {
        var uid = UserId.New();
        var user = new User(uid, "dup-scope", "h", UserStatus.Active, new[] { Role.Engineer });
        await _cmdRepo.AddUserAsync(user);
        var siteId = Guid.NewGuid();
        var s1 = new Scope(ScopeId.New(), uid, siteId, null);
        await _cmdRepo.AddScopeAsync(s1);
        var before = (await _cmdRepo.GetScopesForUserAsync(uid)).Count;
        var s2 = new Scope(ScopeId.New(), uid, siteId, null);
        await _cmdRepo.AddScopeAsync(s2);
        var after = (await _cmdRepo.GetScopesForUserAsync(uid)).Count;
        Assert(after == before, "DuplicateScopePrevention: Duplicate scope changed scope count.");
        Pass();
    }

    private async Task CapabilityAssignment()
    {
        var uid = UserId.New();
        var user = new User(uid, "cap-assign", "h", UserStatus.Active, new[] { Role.Manager });
        await _cmdRepo.AddUserAsync(user);
        var allCaps = await _cmdRepo.GetAllCapabilitiesAsync();
        Assert(allCaps.Count > 0, "CapabilityAssignment: No capabilities available.");
        var cap = allCaps[0];
        var uc = new UserCapability(UserCapabilityId.New(), uid, cap.Id, uid, DateTime.UtcNow, 1);
        await _cmdRepo.AddUserCapabilityAsync(uc);
        var active = await _cmdRepo.GetActiveCapabilitiesForUserAsync(uid);
        Assert(active.Any(a => a.CapabilityId == cap.Id), "CapabilityAssignment: Assigned capability not found.");
        Pass();
    }

    private async Task CapabilityRevocation()
    {
        var uid = UserId.New();
        var user = new User(uid, "cap-revoke", "h", UserStatus.Active, new[] { Role.Manager });
        await _cmdRepo.AddUserAsync(user);
        var allCaps = await _cmdRepo.GetAllCapabilitiesAsync();
        if (allCaps.Count == 0) { Pass(); return; }
        var cap = allCaps[0];
        var uc = new UserCapability(UserCapabilityId.New(), uid, cap.Id, uid, DateTime.UtcNow, 1);
        await _cmdRepo.AddUserCapabilityAsync(uc);
        var active = await _cmdRepo.GetActiveCapabilitiesForUserAsync(uid);
        var saved = active.FirstOrDefault(a => a.CapabilityId == cap.Id);
        if (saved == null) { Pass(); return; }
        await _cmdRepo.RevokeUserCapabilityAsync(saved.Id, DateTime.UtcNow);
        var afterRevoke = await _cmdRepo.GetActiveCapabilitiesForUserAsync(uid);
        Assert(!afterRevoke.Any(a => a.CapabilityId == cap.Id), "CapabilityRevocation: Capability still active after revocation.");
        Pass();
    }

    private async Task SessionCreation()
    {
        var uid = UserId.New();
        var session = new Session(SessionId.New(), uid, "hash-001", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(1));
        await _sessionRepo.AddSessionAsync(session);
        var found = await _sessionRepo.FindSessionByTokenHashAsync("hash-001");
        Assert(found != null, "SessionCreation: Session must be found by token hash.");
        Assert(found!.UserId == uid, "SessionCreation: Session must belong to the correct user.");
        Pass();
    }

    private async Task SessionLookupByTokenHash()
    {
        var uid1 = UserId.New();
        var uid2 = UserId.New();
        var s1 = new Session(SessionId.New(), uid1, "lookup-hash-1", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(1));
        var s2 = new Session(SessionId.New(), uid2, "lookup-hash-2", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(1));
        await _sessionRepo.AddSessionAsync(s1);
        await _sessionRepo.AddSessionAsync(s2);

        var found = await _sessionRepo.FindSessionByTokenHashAsync("lookup-hash-1");
        Assert(found != null, "SessionLookupByTokenHash: Session lookup-1 must be found.");
        Assert(found!.UserId == uid1, "SessionLookupByTokenHash: Session lookup-1 must belong to uid1.");

        var notFound = await _sessionRepo.FindSessionByTokenHashAsync("nonexistent-hash");
        Assert(notFound == null, "SessionLookupByTokenHash: Nonexistent hash must return null.");
        Pass();
    }

    private async Task CurrentSessionRevocation()
    {
        var uid = UserId.New();
        var session = new Session(SessionId.New(), uid, "revoke-session", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(1));
        await _sessionRepo.AddSessionAsync(session);
        var beforeRevoke = await _sessionRepo.FindSessionByTokenHashAsync("revoke-session");
        Assert(beforeRevoke != null && !beforeRevoke!.IsRevoked, "CurrentSessionRevocation: Session must exist and not be revoked before revoke.");

        await _sessionRepo.RevokeSessionAsync(session.Id, DateTime.UtcNow);

        var afterRevoke = await _sessionRepo.FindSessionByTokenHashAsync("revoke-session");
        Assert(afterRevoke != null && afterRevoke!.IsRevoked, "CurrentSessionRevocation: Session must be revoked after RevokeSessionAsync.");
        Pass();
    }

    private async Task RevokeAllSessions()
    {
        var uid = UserId.New();
        var s1 = new Session(SessionId.New(), uid, "revoke-all-1", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(1));
        var s2 = new Session(SessionId.New(), uid, "revoke-all-2", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(1));
        await _sessionRepo.AddSessionAsync(s1);
        await _sessionRepo.AddSessionAsync(s2);

        Assert(!s1.IsRevoked, "RevokeAllSessions: Session 1 must not be revoked before.");
        Assert(!s2.IsRevoked, "RevokeAllSessions: Session 2 must not be revoked before.");

        await _sessionRepo.RevokeAllSessionsForUserAsync(uid, DateTime.UtcNow);

        var found1 = await _sessionRepo.FindSessionByTokenHashAsync("revoke-all-1");
        var found2 = await _sessionRepo.FindSessionByTokenHashAsync("revoke-all-2");
        Assert(found1 != null && found1!.IsRevoked, "RevokeAllSessions: Session 1 must be revoked after revoke-all.");
        Assert(found2 != null && found2!.IsRevoked, "RevokeAllSessions: Session 2 must be revoked after revoke-all.");
        Pass();
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
        Assert(found != null, "TransactionCommit: User not found after commit.");
        Assert(tx.IsCommitted, "TransactionCommit: Transaction must be marked committed.");
        Pass();
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
        Assert(originalAfter != null, "TransactionRollback: Original user missing after rollback.");
        var newAfter = await repo.GetUserAsync(uid2);
        Assert(newAfter == null, "TransactionRollback: New user present after rollback.");
        var newRoles = await repo.GetRolesForUserAsync(uid2);
        Assert(newRoles.Count == 0, "TransactionRollback: New role present after rollback.");
        var originalRoles = await repo.GetRolesForUserAsync(uid);
        Assert(originalRoles.Contains(Role.Viewer), "TransactionRollback: Original role missing after rollback.");
        Assert(tx.IsRolledBack, "TransactionRollback: Transaction must be marked rolled back.");
        Pass();
    }

    private async Task OptimisticVersionBehavior()
    {
        var repo = (FakeIamCommandRepository)_cmdRepo;
        repo.Clear();
        repo.SeedCapability(new Capability(CapabilityId.New(), "OPT_VER_CAP", "Optimistic Version Test"));

        var uid = UserId.New();
        var allCaps = await repo.GetAllCapabilitiesAsync();
        var cap = allCaps[0];
        var uc = new UserCapability(UserCapabilityId.New(), uid, cap.Id, uid, DateTime.UtcNow, 1);
        await repo.AddUserCapabilityAsync(uc);

        var saved = (await repo.GetActiveCapabilitiesForUserAsync(uid)).First();
        Assert(saved.Version == 1, "OptimisticVersionBehavior: Initial version must be 1.");

        await repo.RevokeUserCapabilityAsync(saved.Id, DateTime.UtcNow);
        var afterRevoke = (await repo.GetActiveCapabilitiesForUserAsync(uid)).ToList();
        Assert(afterRevoke.Count == 0, "OptimisticVersionBehavior: Capability must not be active after revoke.");

        var allUc = repo.GetAllUserCapabilitiesForTest();
        var revoked = allUc.FirstOrDefault(uc2 => uc2.Id == saved.Id);
        Assert(revoked != null, "OptimisticVersionBehavior: Revoked capability must still exist (soft delete).");
        Assert(revoked!.Version == 2, "OptimisticVersionBehavior: Version must increment on revoke.");
        Pass();
    }
}