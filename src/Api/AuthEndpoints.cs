using IUMP.Modules.IAM.Contracts;

namespace IUMP.Api;

public static class AuthEndpoints
{
    private static readonly AuthenticationPolicy _authPolicy = new();

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/login", (LoginRequest request, IAuthService auth, HttpContext ctx) =>
        {
            var normalized = request.Username?.ToLowerInvariant() ?? "";
            var now = DateTime.UtcNow;

            if (_authPolicy.IsRateLimited(normalized, now))
            {
                _authPolicy.RecordFailedAttempt(normalized, now);
                return Results.Json(
                    new { error = AuthenticationPolicy.PublicError },
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            var result = auth.Login(request, now);

            if (!result.IsSuccess)
            {
                _authPolicy.RecordFailedAttempt(normalized, now);
                return Results.Json(
                    new { error = result.Error ?? AuthenticationPolicy.PublicError },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            _authPolicy.RecordSuccessfulAttempt(normalized);

            ctx.Response.Cookies.Append(".IUMP.Auth", result.TokenCookieValue!, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = result.ExpiresAt
            });

            return Results.Ok(new { message = "Authenticated." });
        });

        group.MapPost("/logout", (HttpContext ctx, IAuthService auth) =>
        {
            var cookieValue = ctx.Request.Cookies[".IUMP.Auth"];
            if (!string.IsNullOrWhiteSpace(cookieValue))
            {
                var rawBytes = Convert.FromHexString(cookieValue);
                var tokenHash = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(rawBytes)).ToLowerInvariant();
                auth.RevokeSession(tokenHash, DateTime.UtcNow);
            }

            ctx.Response.Cookies.Delete(".IUMP.Auth");
            return Results.Ok(new { message = "Logged out." });
        }).RequireAuthorization();

        group.MapGet("/antiforgery", (HttpContext ctx) =>
        {
            return Results.Ok(new { message = "Antiforgery token endpoint." });
        });

        app.MapGet("/api/v1/me", (HttpContext ctx, IAuthService auth) =>
        {
            var cookieValue = ctx.Request.Cookies[".IUMP.Auth"];
            if (string.IsNullOrWhiteSpace(cookieValue))
                return Results.Json(new { error = "Unauthenticated." }, statusCode: 401);

            var rawBytes = Convert.FromHexString(cookieValue);
            var tokenHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(rawBytes)).ToLowerInvariant();

            var me = auth.ResolveMe(tokenHash);
            if (me == null)
                return Results.Json(new { error = "Unauthenticated." }, statusCode: 401);

            return Results.Ok(me);
        });
    }
}
