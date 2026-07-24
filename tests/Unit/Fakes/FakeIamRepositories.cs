using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Contracts;

namespace IUMP.Tests.Unit.Fakes;

public sealed class FakeIamCommandRepository : IIamCommandRepository
{
    private readonly Dictionary<Guid, User> _users = new();
    private readonly List<Scope> _scopes = new();
    private readonly List<Capability> _capabilities = new();
    private readonly List<UserCapability> _userCapabilities = new();

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
