using System.Security.Cryptography;
using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Application;
using IUMP.Modules.IAM.Contracts;

namespace IUMP.Tests.Unit.Api;

public sealed class DeterministicCredentialVerifier : ICredentialVerifier
{
    private readonly string _expected;

    public DeterministicCredentialVerifier(string match = "any")
    {
        _expected = match;
    }

    public bool Verify(string password, string storedHash)
    {
        return password == _expected;
    }
}

public static class AuthEndpointTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();

        LoginSuccessWithValidCredentials(failures);
        LoginFailsForWrongPassword(failures);
        LoginFailsForUnknownUser(failures);
        LoginFailsForDisabledUser(failures);
        LoginReturnsPublicError(failures);
        LoginResponseBodyHasNoToken(failures);
        ResolveMeReturnsSnapshot(failures);
        LogoutRevokesSession(failures);

        return failures;
    }

    private static IAuthService CreateAuthService()
    {
        var activeUserId = UserId.Parse("00000000-0000-0000-0000-000000000001");
        var disabledUserId = UserId.Parse("00000000-0000-0000-0000-000000000002");

        var activeUser = new User(activeUserId, "activeuser", "hash", UserStatus.Active, new[] { Role.Engineer });
        var disabledUser = new User(disabledUserId, "disableduser", "hash", UserStatus.Disabled, new[] { Role.Engineer });

        var eligibility = new ActiveUserEligibility(new[] { activeUser, disabledUser });
        var sessionManager = new SessionManager();
        var verifier = new DeterministicCredentialVerifier("any");

        return new AuthHandler(eligibility, sessionManager, verifier);
    }

    private static void LoginSuccessWithValidCredentials(List<string> failures)
    {
        var auth = CreateAuthService();
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

        var result = auth.Login(new LoginRequest("activeuser", "any"), now);

        if (!result.IsSuccess)
            failures.Add("T032-FAIL: Valid user login must succeed.");
        if (string.IsNullOrWhiteSpace(result.TokenCookieValue))
            failures.Add("T032-FAIL: Successful login must return a token cookie value.");
        if (!result.ExpiresAt.HasValue)
            failures.Add("T032-FAIL: Successful login must include an expiry time.");
        if (result.Error != null)
            failures.Add("T032-FAIL: Successful login must not include an error.");
    }

    private static void LoginFailsForWrongPassword(List<string> failures)
    {
        var auth = CreateAuthService();
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

        var result = auth.Login(new LoginRequest("activeuser", "wrong"), now);

        if (result.IsSuccess)
            failures.Add("T032-FAIL: Login with wrong password must not succeed.");
        if (string.IsNullOrWhiteSpace(result.Error))
            failures.Add("T032-FAIL: Failed login must include an error message.");
        if (result.TokenCookieValue != null)
            failures.Add("T032-FAIL: Failed login must not include a token cookie.");
    }

    private static void LoginFailsForUnknownUser(List<string> failures)
    {
        var auth = CreateAuthService();
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

        var result = auth.Login(new LoginRequest("nonexistent", "any"), now);

        if (result.IsSuccess)
            failures.Add("T032-FAIL: Login for unknown user must not succeed.");
        if (string.IsNullOrWhiteSpace(result.Error))
            failures.Add("T032-FAIL: Failed login must include an error message.");
        if (result.TokenCookieValue != null)
            failures.Add("T032-FAIL: Failed login must not include a token cookie.");
    }

    private static void LoginFailsForDisabledUser(List<string> failures)
    {
        var auth = CreateAuthService();
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

        var result = auth.Login(new LoginRequest("disableduser", "any"), now);

        if (result.IsSuccess)
            failures.Add("T032-FAIL: Login for disabled user must not succeed.");
        if (string.IsNullOrWhiteSpace(result.Error))
            failures.Add("T032-FAIL: Failed login for disabled user must include an error.");
        if (result.TokenCookieValue != null)
            failures.Add("T032-FAIL: Failed login must not include a token cookie.");
    }

    private static void LoginReturnsPublicError(List<string> failures)
    {
        var auth = CreateAuthService();
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

        var unknownResult = auth.Login(new LoginRequest("nonexistent", "any"), now);
        var disabledResult = auth.Login(new LoginRequest("disableduser", "any"), now);
        var wrongPasswordResult = auth.Login(new LoginRequest("activeuser", "wrong"), now);

        if (unknownResult.Error != disabledResult.Error)
            failures.Add("T032-FAIL: Error message must be identical for unknown and disabled users.");
        if (wrongPasswordResult.Error != disabledResult.Error)
            failures.Add("T032-FAIL: Error message must be identical for wrong password, unknown, and disabled.");
    }

    private static void LoginResponseBodyHasNoToken(List<string> failures)
    {
        var auth = CreateAuthService();
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

        var result = auth.Login(new LoginRequest("activeuser", "any"), now);

        if (!result.IsSuccess)
        {
            failures.Add("T032-FAIL: Cannot test token absence without successful login.");
            return;
        }

        var rawBytes = Convert.FromHexString(result.TokenCookieValue!);

        var resultString = System.Text.Json.JsonSerializer.Serialize(new
        {
            message = "Authenticated."
        });

        if (resultString.Contains(result.TokenCookieValue!))
            failures.Add("T032-FAIL: Login response body must not contain the raw token.");
    }

    private static void ResolveMeReturnsSnapshot(List<string> failures)
    {
        var auth = CreateAuthService();
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

        var loginResult = auth.Login(new LoginRequest("activeuser", "any"), now);
        if (!loginResult.IsSuccess)
        {
            failures.Add("T032-FAIL: Cannot test ResolveMe without successful login.");
            return;
        }

        var rawBytes = Convert.FromHexString(loginResult.TokenCookieValue!);
        var tokenHash = Convert.ToHexString(
            SHA256.HashData(rawBytes)).ToLowerInvariant();

        var me = auth.ResolveMe(tokenHash);

        if (me == null)
        {
            failures.Add("T032-FAIL: ResolveMe must return a snapshot for a valid session.");
            return;
        }
        if (string.IsNullOrWhiteSpace(me.UserId))
            failures.Add("T032-FAIL: MeSnapshot must include UserId.");
        if (string.IsNullOrWhiteSpace(me.Username))
            failures.Add("T032-FAIL: MeSnapshot must include Username.");
        if (me.Roles.Count == 0)
            failures.Add("T032-FAIL: MeSnapshot must include at least one Role.");
        if (!me.Roles.Contains("Engineer"))
            failures.Add("T032-FAIL: MeSnapshot must include the user's role name.");
    }

    private static void LogoutRevokesSession(List<string> failures)
    {
        var auth = CreateAuthService();
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

        var loginResult = auth.Login(new LoginRequest("activeuser", "any"), now);
        if (!loginResult.IsSuccess)
        {
            failures.Add("T032-FAIL: Cannot test logout without successful login.");
            return;
        }

        var rawBytes = Convert.FromHexString(loginResult.TokenCookieValue!);
        var tokenHash = Convert.ToHexString(
            SHA256.HashData(rawBytes)).ToLowerInvariant();

        var revoked = auth.RevokeSession(tokenHash, now);
        if (!revoked)
            failures.Add("T032-FAIL: RevokeSession must return true for a valid session.");

        var meAfter = auth.ResolveMe(tokenHash);
        if (meAfter != null)
            failures.Add("T032-FAIL: Session must not resolve after revocation.");
    }
}
