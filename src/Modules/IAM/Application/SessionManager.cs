using System.Security.Cryptography;
using IUMP.Modules.IAM.Domain;

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
