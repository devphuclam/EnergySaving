using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using IUMP.Modules.IAM.Contracts;

namespace IUMP.Api;

public sealed class CredentialVerifier : ICredentialVerifier
{
    private readonly PasswordHasher<string> _hasher = new();

    public bool Verify(string password, string storedHash)
    {
        var result = _hasher.VerifyHashedPassword(null!, storedHash, password);
        return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}

public static class AuthEndpointHandlers
{
    public const string AuthCookie = ".IUMP.Auth";
    public const string XsrfCookie = ".IUMP.Xsrf";
    public const string XsrfHeader = "X-XSRF-TOKEN";

    public static IResult HandleLogin(LoginRequest request, IAuthService auth, HttpContext ctx, AuthenticationPolicy policy)
    {
        var normalized = request.Username?.ToLowerInvariant() ?? "";
        var now = DateTime.UtcNow;

        if (policy.IsRateLimited(normalized, now))
        {
            policy.RecordFailedAttempt(normalized, now);
            return Results.Json(
                new { error = AuthenticationPolicy.PublicError },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var result = auth.Login(request, now);

        if (!result.IsSuccess)
        {
            policy.RecordFailedAttempt(normalized, now);
            return Results.Json(
                new { error = result.Error ?? AuthenticationPolicy.PublicError },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        policy.RecordSuccessfulAttempt(normalized);

        ctx.Response.Cookies.Append(AuthCookie, result.TokenCookieValue!, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = result.ExpiresAt
        });

        return Results.Ok(new { message = "Authenticated." });
    }

    public static IResult HandleLogout(HttpContext ctx, IAuthService auth)
    {
        var cookieValue = ctx.Request.Cookies[AuthCookie];
        if (!string.IsNullOrWhiteSpace(cookieValue))
        {
            var rawBytes = Convert.FromHexString(cookieValue);
            var tokenHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(rawBytes)).ToLowerInvariant();
            auth.RevokeSession(tokenHash, DateTime.UtcNow);
        }

        ctx.Response.Cookies.Delete(AuthCookie);
        return Results.Ok(new { message = "Logged out." });
    }

    public static IResult HandleAntiforgery(HttpContext ctx, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(ctx);
        var token = tokens.RequestToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            ctx.Response.Cookies.Append(XsrfCookie, token, new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Lax
            });
        }

        return Results.Ok(new { token });
    }

    public static IResult HandleMe(HttpContext ctx, IAuthService auth)
    {
        var cookieValue = ctx.Request.Cookies[AuthCookie];
        if (string.IsNullOrWhiteSpace(cookieValue))
            return Results.Json(new { error = "Unauthenticated." }, statusCode: 401);

        var rawBytes = Convert.FromHexString(cookieValue);
        var tokenHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(rawBytes)).ToLowerInvariant();

        var me = auth.ResolveMe(tokenHash);
        if (me == null)
            return Results.Json(new { error = "Unauthenticated." }, statusCode: 401);

        return Results.Ok(me);
    }
}

public static class AuthEndpoints
{
    private static readonly AuthenticationPolicy _authPolicy = new();

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/login", (LoginRequest request, IAuthService auth, HttpContext ctx) =>
            AuthEndpointHandlers.HandleLogin(request, auth, ctx, _authPolicy));

        group.MapPost("/logout", (HttpContext ctx, IAuthService auth) =>
            AuthEndpointHandlers.HandleLogout(ctx, auth))
            .RequireAuthorization();

        group.MapGet("/antiforgery", (HttpContext ctx, IAntiforgery antiforgery) =>
            AuthEndpointHandlers.HandleAntiforgery(ctx, antiforgery));

        app.MapGet("/api/v1/me", (HttpContext ctx, IAuthService auth) =>
            AuthEndpointHandlers.HandleMe(ctx, auth));
    }
}
