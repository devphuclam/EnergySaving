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
