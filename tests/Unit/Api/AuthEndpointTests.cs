using System.Security.Cryptography;
using System.Text.Json;
using IUMP.Modules.IAM.Domain;
using IUMP.Modules.IAM.Application;
using IUMP.Modules.IAM.Contracts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Builder;
using IUMP.Api;

namespace IUMP.Tests.Unit.Api;

public sealed class DeterministicCredentialVerifier : ICredentialVerifier
{
    private readonly string _expected;
    public DeterministicCredentialVerifier(string match = "any") { _expected = match; }
    public bool Verify(string password, string storedHash) => password == _expected;
}

public sealed class FakeAntiforgery : IAntiforgery
{
    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext)
    {
        return new AntiforgeryTokenSet("req-token", "cookie-token", "X-XSRF-TOKEN", ".IUMP.Xsrf");
    }

    public AntiforgeryTokenSet GetTokens(HttpContext httpContext)
    {
        return new AntiforgeryTokenSet("req-token", "cookie-token", "X-XSRF-TOKEN", ".IUMP.Xsrf");
    }

    public void SetCookieTokenAndHeader(HttpContext httpContext)
    {
    }

    public Task<bool> IsRequestValidAsync(HttpContext httpContext)
    {
        return Task.FromResult(true);
    }

    public Task ValidateRequestAsync(HttpContext httpContext)
    {
        return Task.CompletedTask;
    }
}

public static class AuthEndpointTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();

        RouteMetadataTests(failures);
        AntiforgeryOptionsTests(failures);
        LoginHandlerTests(failures);
        LogoutHandlerTests(failures);
        MeHandlerTests(failures);
        AntiforgeryHandlerTests(failures);

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

    private sealed class TestAuthService : IAuthService
    {
        public LoginResult Login(LoginRequest request, DateTime now) =>
            new(true, null, "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1", now.AddHours(8));
        public MeSnapshot? ResolveMe(string tokenHash) =>
            new("uid", "testuser", new[] { "Engineer" }, Array.Empty<string>(), Array.Empty<string>());
        public bool RevokeSession(string tokenHash, DateTime now) => true;
    }

    private static DefaultHttpContext MakeHttpContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        ctx.RequestServices = services.BuildServiceProvider();
        return ctx;
    }

    private static string ReadBody(DefaultHttpContext ctx)
    {
        ctx.Response.Body.Position = 0;
        return new StreamReader(ctx.Response.Body).ReadToEnd();
    }

    private static string GetSetCookieHeader(DefaultHttpContext ctx)
    {
        ctx.Response.Headers.TryGetValue("Set-Cookie", out var values);
        return values.FirstOrDefault() ?? "";
    }

    private static async Task ExecuteAndVerifyLoginBody(IAuthService auth, DefaultHttpContext ctx,
        List<string> failures, string label)
    {
        var policy = new AuthenticationPolicy();
        var result = AuthEndpointHandlers.HandleLogin(
            new LoginRequest("testuser", "any"), auth, ctx, policy);
        await result.ExecuteAsync(ctx);

        var body = ReadBody(ctx);
        var cookieHeader = GetSetCookieHeader(ctx);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (cookieHeader.Contains(".IUMP.Auth"))
            failures.Add($"{label}: Login response must NOT leak token in body (cookie present: {cookieHeader})");

        if (root.TryGetProperty("token", out _))
            failures.Add($"{label}: Login response body must not contain a token property.");

        if (root.TryGetProperty("tokenHash", out _))
            failures.Add($"{label}: Login response body must not contain a tokenHash property.");

        if (root.TryGetProperty("message", out var msg) && msg.GetString() == "Authenticated.")
        {
            // expected - body contains only message
        }
    }

    private static void RouteMetadataTests(List<string> failures)
    {
        var attr = new RequireAntiforgeryCheckAttribute();
        if (attr is not IAntiforgeryMetadata meta)
            failures.Add("T032-RED: RequireAntiforgeryCheckAttribute must implement IAntiforgeryMetadata.");
        else if (!meta.RequiresValidation)
            failures.Add("T032-RED: RequireAntiforgeryCheckAttribute.RequiresValidation must be true.");

        var authAttr = new Microsoft.AspNetCore.Authorization.AuthorizeAttribute();
        if (string.IsNullOrEmpty(authAttr.Policy) && string.IsNullOrEmpty(authAttr.Roles) && string.IsNullOrEmpty(authAttr.AuthenticationSchemes))
        {
        }
    }

    private static void AntiforgeryOptionsTests(List<string> failures)
    {
        var services = new ServiceCollection();
        services.AddAntiforgery();
        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        if (options.Cookie.Name == ".IUMP.Xsrf")
            failures.Add("T032-RED: Antiforgery cookie default must NOT be .IUMP.Xsrf (not configured yet).");
    }

    private static void LoginHandlerTests(List<string> failures)
    {
        var auth = CreateAuthService();
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

        var result = auth.Login(new LoginRequest("activeuser", "any"), now);
        if (!result.IsSuccess)
        {
            failures.Add("T032-RED: Cannot test login handler without valid LoginResult.");
            return;
        }

        var ctx = MakeHttpContext();
        var policy = new AuthenticationPolicy();

        var beforeCookieHeader = GetSetCookieHeader(ctx);
        if (!string.IsNullOrEmpty(beforeCookieHeader))
            failures.Add("T032-RED: Set-Cookie should be empty before ExecuteAsync.");

        var handlerResult = AuthEndpointHandlers.HandleLogin(
            new LoginRequest("activeuser", "any"), auth, ctx, policy);
        handlerResult.ExecuteAsync(ctx).GetAwaiter().GetResult();

        var cookieHeader = GetSetCookieHeader(ctx);
        if (!cookieHeader.Contains(".IUMP.Auth", StringComparison.OrdinalIgnoreCase))
            failures.Add($"T032-RED: Login must set .IUMP.Auth cookie. Got: '{cookieHeader}'");

        if (!cookieHeader.Contains("HttpOnly", StringComparison.OrdinalIgnoreCase))
            failures.Add($"T032-RED: .IUMP.Auth cookie must be HttpOnly. Got: '{cookieHeader}'");

        if (!cookieHeader.Contains("SameSite=Lax", StringComparison.OrdinalIgnoreCase))
            failures.Add($"T032-RED: .IUMP.Auth cookie must be SameSite=Lax. Got: '{cookieHeader}'");

        var body = ReadBody(ctx);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var tokenInBody = body.Contains(result.TokenCookieValue!, StringComparison.Ordinal);
        if (tokenInBody)
            failures.Add("T032-RED: Login response body must not contain the raw token.");

        if (body.Contains("tokenHash", StringComparison.OrdinalIgnoreCase))
            failures.Add("T032-RED: Login response body must not contain tokenHash.");

        if (!root.TryGetProperty("message", out var msg) || msg.GetString() != "Authenticated.")
            failures.Add("T032-RED: Login response body must contain {\"message\":\"Authenticated.\"}");
    }

    private static void LogoutHandlerTests(List<string> failures)
    {
        var auth = new TestAuthService();
        var ctx = MakeHttpContext();
        ctx.Request.Headers["Cookie"] = ".IUMP.Auth=a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1";
        var antiforgery = new FakeAntiforgery();

        var result = AuthEndpointHandlers.HandleLogout(ctx, auth, antiforgery);
        result.ExecuteAsync(ctx).GetAwaiter().GetResult();

        var body = ReadBody(ctx);
        var cookieHeader = GetSetCookieHeader(ctx);

        if (!cookieHeader.Contains(".IUMP.Auth"))
            failures.Add("T032-RED: Logout must delete .IUMP.Auth cookie.");

        if (!cookieHeader.Contains("expires=", StringComparison.OrdinalIgnoreCase) &&
            !cookieHeader.Contains("Max-Age=0", StringComparison.OrdinalIgnoreCase))
            failures.Add("T032-RED: Logout cookie deletion must include expiry or Max-Age=0.");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (!root.TryGetProperty("message", out var msg) || msg.GetString() != "Logged out.")
            failures.Add("T032-RED: Logout response body must contain {\"message\":\"Logged out.\"}");
    }

    private static void MeHandlerTests(List<string> failures)
    {
        var auth = CreateAuthService();
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
        var loginResult = auth.Login(new LoginRequest("activeuser", "any"), now);
        if (!loginResult.IsSuccess)
        {
            failures.Add("T032-RED: Cannot test /me without successful login.");
            return;
        }

        var ctx = MakeHttpContext();
        ctx.Request.Headers["Cookie"] = $".IUMP.Auth={loginResult.TokenCookieValue}";

        var result = AuthEndpointHandlers.HandleMe(ctx, auth);
        result.ExecuteAsync(ctx).GetAwaiter().GetResult();

        var body = ReadBody(ctx);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (!root.TryGetProperty("userId", out _))
            failures.Add("T032-RED: /me response must include userId.");

        if (!root.TryGetProperty("username", out _))
            failures.Add("T032-RED: /me response must include username.");

        if (!root.TryGetProperty("roles", out _))
            failures.Add("T032-RED: /me response must include roles array.");

        if (!root.TryGetProperty("scopes", out _))
            failures.Add("T032-RED: /me response must include scopes array.");

        if (!root.TryGetProperty("capabilities", out _))
            failures.Add("T032-RED: /me response must include capabilities array.");
    }

    private static void AntiforgeryHandlerTests(List<string> failures)
    {
        var ctx = MakeHttpContext();
        var antiforgery = new FakeAntiforgery();

        var result = AuthEndpointHandlers.HandleAntiforgery(ctx, antiforgery);
        result.ExecuteAsync(ctx).GetAwaiter().GetResult();

        var body = ReadBody(ctx);
        var cookieHeader = GetSetCookieHeader(ctx);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (!root.TryGetProperty("token", out var token) || token.GetString() != "req-token")
            failures.Add("T032-RED: Antiforgery handler must return request token.");

        if (!cookieHeader.Contains(".IUMP.Xsrf"))
            failures.Add("T032-RED: Antiforgery handler must set .IUMP.Xsrf cookie.");
    }
}
