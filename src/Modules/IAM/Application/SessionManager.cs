using System.Security.Cryptography;
using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Contracts;

namespace IUMP.Modules.IAM.Application;

public interface ISessionManager
{
    Session CreateSession(UserId userId, string tokenHash, DateTime now);
    Session? LookupSession(string tokenHash);
    void RevokeSession(SessionId sessionId, DateTime now);
    void RevokeAllSessions(UserId userId, DateTime now);
    bool IsSessionValid(Session session, DateTime now);
    string HashToken(byte[] token);
}

public sealed class SessionManager : ISessionManager
{
    private readonly Dictionary<Guid, Session> _sessions = new();

    public SessionManager()
    {
    }

    public Session CreateSession(UserId userId, string tokenHash, DateTime now)
    {
        var id = SessionId.New();
        var idleExpiry = now.AddMinutes(20);
        var absoluteExpiry = now.AddHours(8);
        var session = new Session(id, userId, tokenHash, now, idleExpiry, absoluteExpiry);
        _sessions[id.Value] = session;
        return session;
    }

    public Session? LookupSession(string tokenHash)
    {
        return _sessions.Values.FirstOrDefault(s => s.TokenHash == tokenHash);
    }

    public void RevokeSession(SessionId sessionId, DateTime now)
    {
        if (_sessions.TryGetValue(sessionId.Value, out var session))
            session.Revoke(now);
    }

    public void RevokeAllSessions(UserId userId, DateTime now)
    {
        foreach (var session in _sessions.Values.Where(s => s.UserId == userId))
            session.Revoke(now);
    }

    public bool IsSessionValid(Session session, DateTime now)
    {
        return session.IsValid(now);
    }

    public string HashToken(byte[] token)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(token);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed class AuthHandler : IAuthService
{
    private readonly IActiveUserEligibility _eligibility;
    private readonly ISessionManager _sessionManager;
    private readonly ICredentialVerifier _credentialVerifier;

    public AuthHandler(
        IActiveUserEligibility eligibility,
        ISessionManager sessionManager,
        ICredentialVerifier credentialVerifier)
    {
        _eligibility = eligibility;
        _sessionManager = sessionManager;
        _credentialVerifier = credentialVerifier;
    }

    public LoginResult Login(LoginRequest request, DateTime now)
    {
        var normalized = request.Username?.ToLowerInvariant() ?? "";

        var user = _eligibility.FindByUsername(normalized);
        if (user == null || user.Status == UserStatus.Disabled)
            return new LoginResult(false, "Authentication failed.", null, null);

        if (!_credentialVerifier.Verify(request.Password ?? "", user.PasswordHash))
            return new LoginResult(false, "Authentication failed.", null, null);

        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        var tokenHash = _sessionManager.HashToken(tokenBytes);
        var rawToken = Convert.ToHexString(tokenBytes).ToLowerInvariant();

        var session = _sessionManager.CreateSession(user.Id, tokenHash, now);

        return new LoginResult(true, null, rawToken, session.AbsoluteExpiresAt);
    }

    public MeSnapshot? ResolveMe(string tokenHash)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            return null;

        var session = _sessionManager.LookupSession(tokenHash);
        if (session == null)
            return null;

        var now = DateTime.UtcNow;
        if (!_sessionManager.IsSessionValid(session, now))
            return null;

        var user = _eligibility.FindByUserId(session.UserId);
        if (user == null || user.Status == UserStatus.Disabled)
            return null;

        var roles = user.Roles.Select(r => r.ToString()).ToList() as IReadOnlyList<string>
            ?? Array.Empty<string>();

        var scopes = _eligibility.GetScopesForUser(user.Id)
            .Select(s => s.SiteId?.ToString("D") ?? "")
            .ToList() as IReadOnlyList<string>
            ?? Array.Empty<string>();

        var caps = new List<string>();
        if (user.Roles.Contains(Role.Administrator))
            caps.Add("AUDIT_READ");

        return new MeSnapshot(
            user.Id.Value.ToString("D"),
            user.Username,
            roles,
            scopes,
            caps);
    }

    public bool RevokeSession(string tokenHash, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            return false;

        var session = _sessionManager.LookupSession(tokenHash);
        if (session == null)
            return false;

        _sessionManager.RevokeSession(session.Id, now);
        return true;
    }
}
