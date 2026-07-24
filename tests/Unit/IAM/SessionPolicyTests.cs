using System.Security.Cryptography;
using System.Text;
using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Application;

namespace IUMP.Tests.Unit.IAM;

public static class SessionPolicyTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();

        SessionHashing(failures);
        SessionExpiry(failures);
        DisabledUserInvalidation(failures);
        LogoutRevokesCurrentSession(failures);
        MultipleSessionsAllowed(failures);
        AdministratorRevokeAll(failures);

        return failures;
    }

    private static void SessionHashing(List<string> failures)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var manager = new SessionManager();
        var hash = manager.HashToken(tokenBytes);

        if (string.IsNullOrEmpty(hash))
            failures.Add("T017-FAIL: Session token hash must not be null or empty.");

        using var sha256 = SHA256.Create();
        var expectedHash = Convert.ToHexString(sha256.ComputeHash(tokenBytes)).ToLowerInvariant();

        if (hash != expectedHash)
            failures.Add("T017-FAIL: Session token hash must be lowercase SHA-256 hex.");

        if (hash.Length != 64)
            failures.Add("T017-FAIL: SHA-256 hash must be 64 hex characters.");
    }

    private static void SessionExpiry(List<string> failures)
    {
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
        var manager = new SessionManager();
        var userId = UserId.New();

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var tokenHash = manager.HashToken(tokenBytes);
        var session = manager.CreateSession(userId, tokenHash, now);

        if (session.UserId != userId)
            failures.Add("T017-FAIL: Created session must reference correct UserId.");

        if (!manager.IsSessionValid(session, now))
            failures.Add("T017-FAIL: New session must be valid at creation time.");

        var idleExpiredTime = now.AddMinutes(21);
        if (session.IsValid(idleExpiredTime))
            failures.Add("T017-FAIL: Session must be invalid after idle timeout.");

        var absoluteExpiredTime = now.AddHours(9);
        if (session.IsValid(absoluteExpiredTime))
            failures.Add("T017-FAIL: Session must be invalid after absolute timeout.");
    }

    private static void DisabledUserInvalidation(List<string> failures)
    {
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
        var manager = new SessionManager();
        var userId = UserId.New();

        var user = new User(userId, "toshow", "hash", UserStatus.Active, Role.Engineer);
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var tokenHash = manager.HashToken(tokenBytes);
        var session = manager.CreateSession(user.Id, tokenHash, now);

        if (!manager.IsSessionValid(session, now))
            failures.Add("T017-FAIL: Active user's session must be valid.");

        user.Disable();

        if (user.IsActive())
            failures.Add("T017-FAIL: Disabled user must not be IsActive.");
    }

    private static void LogoutRevokesCurrentSession(List<string> failures)
    {
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
        var manager = new SessionManager();
        var userId = UserId.New();

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var tokenHash = manager.HashToken(tokenBytes);
        var session = manager.CreateSession(userId, tokenHash, now);

        manager.RevokeSession(session.Id, now);

        if (manager.IsSessionValid(session, now))
            failures.Add("T017-FAIL: Revoked session must not be valid.");
    }

    private static void MultipleSessionsAllowed(List<string> failures)
    {
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
        var manager = new SessionManager();
        var userId = UserId.New();

        var token1 = RandomNumberGenerator.GetBytes(32);
        var token2 = RandomNumberGenerator.GetBytes(32);
        var session1 = manager.CreateSession(userId, manager.HashToken(token1), now);
        var session2 = manager.CreateSession(userId, manager.HashToken(token2), now);

        if (session1.Id == session2.Id)
            failures.Add("T017-FAIL: Multiple sessions must have distinct IDs.");

        if (!manager.IsSessionValid(session1, now) || !manager.IsSessionValid(session2, now))
            failures.Add("T017-FAIL: All concurrent sessions must be valid.");
    }

    private static void AdministratorRevokeAll(List<string> failures)
    {
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
        var manager = new SessionManager();
        var userId = UserId.New();

        var sessions = new List<Session>();
        for (int i = 0; i < 3; i++)
        {
            var token = RandomNumberGenerator.GetBytes(32);
            sessions.Add(manager.CreateSession(userId, manager.HashToken(token), now));
        }

        foreach (var s in sessions)
        {
            manager.RevokeAllSessions(userId, now);
        }

        foreach (var s in sessions)
        {
            if (manager.IsSessionValid(s, now))
                failures.Add("T017-FAIL: Revoke-all must invalidate all sessions for the user.");
        }
    }
}
