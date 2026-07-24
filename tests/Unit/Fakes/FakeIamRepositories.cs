using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Contracts;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeIamTransaction : IIamTransaction
{
    private readonly FakeIamRepositorySnapshot _snapshot;
    private readonly FakeIamCommandRepository _repo;
    public bool IsCommitted { get; private set; }
    public bool IsRolledBack { get; private set; }

    public FakeIamTransaction(FakeIamCommandRepository repo)
    {
        _repo = repo;
        _snapshot = repo.CreateSnapshot();
    }

    public Task CommitAsync(CancellationToken ct = default)
    {
        IsCommitted = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken ct = default)
    {
        IsRolledBack = true;
        _repo.RestoreSnapshot(_snapshot);
        return Task.CompletedTask;
    }
}

public sealed class FakeIamRepositorySnapshot
{
    public Dictionary<Guid, User> Users { get; } = new();
    public List<Scope> Scopes { get; } = new();
    public List<Capability> Capabilities { get; } = new();
    public List<UserCapability> UserCapabilities { get; } = new();
    public Dictionary<Guid, List<Role>> UserRoles { get; } = new();
}

public sealed class FakeIamCommandRepository : IIamCommandRepository
{
    private readonly Dictionary<Guid, User> _users = new();
    private readonly List<Scope> _scopes = new();
    private readonly List<Capability> _capabilities = new();
    private readonly List<UserCapability> _userCapabilities = new();
    private readonly Dictionary<Guid, List<Role>> _userRoles = new();

    public FakeIamRepositorySnapshot CreateSnapshot()
    {
        var snap = new FakeIamRepositorySnapshot();
        foreach (var kv in _users)
            snap.Users[kv.Key] = kv.Value;
        snap.Scopes.AddRange(_scopes);
        snap.Capabilities.AddRange(_capabilities);
        snap.UserCapabilities.AddRange(_userCapabilities);
        foreach (var kv in _userRoles)
            snap.UserRoles[kv.Key] = new List<Role>(kv.Value);
        return snap;
    }

    public void RestoreSnapshot(FakeIamRepositorySnapshot snap)
    {
        _users.Clear();
        foreach (var kv in snap.Users)
            _users[kv.Key] = kv.Value;
        _scopes.Clear();
        _scopes.AddRange(snap.Scopes);
        _capabilities.Clear();
        _capabilities.AddRange(snap.Capabilities);
        _userCapabilities.Clear();
        _userCapabilities.AddRange(snap.UserCapabilities);
        _userRoles.Clear();
        foreach (var kv in snap.UserRoles)
            _userRoles[kv.Key] = new List<Role>(kv.Value);
    }

    public Task<User?> GetUserAsync(UserId userId, CancellationToken ct = default)
    {
        _users.TryGetValue(userId.Value, out var user);
        return Task.FromResult(user);
    }

    public Task<User?> FindUserByUsernameAsync(string username, CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(u => u.Username == username);
        return Task.FromResult(user);
    }

    public Task AddUserAsync(User user, CancellationToken ct = default)
    {
        if (_users.Values.Any(u => u.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A user with username '{user.Username}' already exists.");
        _users[user.Id.Value] = user;
        return Task.CompletedTask;
    }

    public Task UpdateUserAsync(User user, CancellationToken ct = default)
    {
        _users[user.Id.Value] = user;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<User>> GetAllUsersAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<User>>(_users.Values.ToList());
    }

    public Task<IReadOnlyList<Role>> GetRoleCodesAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<Role>>(Enum.GetValues<Role>().ToList());
    }

    public Task AssignRoleAsync(UserId userId, Role role, UserId assignedBy, CancellationToken ct = default)
    {
        if (!_userRoles.ContainsKey(userId.Value))
            _userRoles[userId.Value] = new List<Role>();
        if (!_userRoles[userId.Value].Contains(role))
            _userRoles[userId.Value].Add(role);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Role>> GetRolesForUserAsync(UserId userId, CancellationToken ct = default)
    {
        if (_userRoles.TryGetValue(userId.Value, out var roles))
            return Task.FromResult<IReadOnlyList<Role>>(roles.ToList());
        return Task.FromResult<IReadOnlyList<Role>>(Array.Empty<Role>());
    }

    public Task RevokeRoleAsync(UserId userId, Role role, CancellationToken ct = default)
    {
        if (_userRoles.TryGetValue(userId.Value, out var roles))
            roles.Remove(role);
        return Task.CompletedTask;
    }

    public Task AddScopeAsync(Scope scope, CancellationToken ct = default)
    {
        _scopes.RemoveAll(s => s.UserId == scope.UserId && s.SiteId == scope.SiteId && s.AreaId == scope.AreaId);
        _scopes.Add(scope);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Scope>> GetScopesForUserAsync(UserId userId, CancellationToken ct = default)
    {
        var userScopes = _scopes.Where(s => s.UserId == userId).ToList();
        return Task.FromResult<IReadOnlyList<Scope>>(userScopes);
    }

    public Task AddUserCapabilityAsync(UserCapability capability, CancellationToken ct = default)
    {
        _userCapabilities.RemoveAll(uc => uc.UserId == capability.UserId && uc.CapabilityId == capability.CapabilityId);
        _userCapabilities.Add(capability);
        return Task.CompletedTask;
    }

    public Task RevokeUserCapabilityAsync(UserCapabilityId capabilityId, DateTime revokedAt, CancellationToken ct = default)
    {
        var cap = _userCapabilities.FirstOrDefault(uc => uc.Id == capabilityId);
        if (cap != null)
            cap.Revoke(revokedAt);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Capability>> GetAllCapabilitiesAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<Capability>>(_capabilities.ToList());
    }

    public Task<IReadOnlyList<UserCapability>> GetActiveCapabilitiesForUserAsync(UserId userId, CancellationToken ct = default)
    {
        var caps = _userCapabilities.Where(uc => uc.UserId == userId && uc.IsActive).ToList();
        return Task.FromResult<IReadOnlyList<UserCapability>>(caps);
    }

    public Task<IIamTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IIamTransaction>(new FakeIamTransaction(this));
    }

    public void SeedCapability(Capability capability)
    {
        _capabilities.RemoveAll(c => c.Code == capability.Code);
        _capabilities.Add(capability);
    }

    public void SeedUser(User user)
    {
        _users[user.Id.Value] = user;
    }

    public void SeedScope(Scope scope)
    {
        _scopes.Add(scope);
    }

    public void SeedRole(UserId userId, Role role)
    {
        if (!_userRoles.ContainsKey(userId.Value))
            _userRoles[userId.Value] = new List<Role>();
        if (!_userRoles[userId.Value].Contains(role))
            _userRoles[userId.Value].Add(role);
    }

    public void Clear()
    {
        _users.Clear();
        _scopes.Clear();
        _capabilities.Clear();
        _userCapabilities.Clear();
        _userRoles.Clear();
    }

    public IReadOnlyList<UserCapability> GetAllUserCapabilitiesForTest()
    {
        return _userCapabilities.ToList();
    }
}

public sealed class FakeIamPrincipalSessionRepository : IIamPrincipalSessionRepository
{
    private readonly Dictionary<Guid, Session> _sessions = new();

    public Task AddSessionAsync(Session session, CancellationToken ct = default)
    {
        _sessions[session.Id.Value] = session;
        return Task.CompletedTask;
    }

    public Task<Session?> FindSessionByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var session = _sessions.Values.FirstOrDefault(s => s.TokenHash == tokenHash);
        return Task.FromResult(session);
    }

    public Task<IReadOnlyList<Session>> GetSessionsForUserAsync(UserId userId, CancellationToken ct = default)
    {
        var sessions = _sessions.Values.Where(s => s.UserId == userId).ToList();
        return Task.FromResult<IReadOnlyList<Session>>(sessions);
    }

    public Task RevokeSessionAsync(SessionId sessionId, DateTime revokedAt, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(sessionId.Value, out var session))
            session.Revoke(revokedAt);
        return Task.CompletedTask;
    }

    public Task RevokeAllSessionsForUserAsync(UserId userId, DateTime revokedAt, CancellationToken ct = default)
    {
        foreach (var session in _sessions.Values.Where(s => s.UserId == userId))
            session.Revoke(revokedAt);
        return Task.CompletedTask;
    }
}
