using IUMP.Modules.IAM.Domain;

namespace IUMP.Modules.IAM.Contracts;

public interface IIamPrincipalSessionRepository
{
    Task AddSessionAsync(Session session, CancellationToken ct = default);
    Task<Session?> FindSessionByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<Session>> GetSessionsForUserAsync(UserId userId, CancellationToken ct = default);
    Task RevokeSessionAsync(SessionId sessionId, DateTime revokedAt, CancellationToken ct = default);
    Task RevokeAllSessionsForUserAsync(UserId userId, DateTime revokedAt, CancellationToken ct = default);
}

public interface ICredentialVerifier
{
    bool Verify(string password, string storedHash);
}

public interface IAuthService
{
    LoginResult Login(LoginRequest request, DateTime now);
    MeSnapshot? ResolveMe(string tokenHash);
    bool RevokeSession(string tokenHash, DateTime now);
}

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResult(
    bool IsSuccess,
    string? Error,
    string? TokenCookieValue,
    DateTime? ExpiresAt);

public sealed record MeSnapshot(
    string UserId,
    string Username,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> Capabilities);
