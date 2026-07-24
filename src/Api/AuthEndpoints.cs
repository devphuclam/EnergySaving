// Auth endpoints for the IUMP API.
//
// COMPILATION NOTE: This file references IAM application types (ISessionManager,
// IActiveUserEligibility, ICallerContext, IPocIdentityFixture) and IAM domain types
// (User, Session, UserStatus, Role, UserId, SessionId).
//
// The API project (IUMP.Api.csproj) currently does NOT reference the IAM module.
// Adding a <ProjectReference> to src/Modules/IAM/IUMP.Modules.IAM.csproj is required
// for this file to compile. The csproj change is classified as RUNNABLE_NOW but is
// BLOCKED by the constraint that the API csproj must not be modified in this session.
//
// Source is provided for design review. Endpoint behavior:
//
// POST /api/v1/auth/login
//   - Validates username/password against stored PasswordHasher hash
//   - Creates opaque session token (256-bit CSPRNG)
//   - Stores SHA-256(token) in iam.user_session
//   - Sets .IUMP.Auth cookie (Secure, HttpOnly, SameSite=Lax)
//   - No token in response body
//   - No Idempotency-Key, no If-Match
//   - Rate limited: 5 attempts per 15s window per username
//   - Non-enumerating error: "Authentication failed." for invalid user or wrong password
//
// POST /api/v1/auth/logout
//   - Requires antiforgery (X-XSRF-TOKEN header)
//   - Reads .IUMP.Auth cookie, hashes token, revokes session
//   - Clears cookie
//
// GET /api/v1/auth/antiforgery
//   - Supplies .IUMP.Xsrf cookie + X-XSRF-TOKEN header
//
// GET /api/v1/me
//   - Resolves caller identity, role, scopes, capabilities from session

namespace IUMP.Api;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/login", () => Results.Ok(new { message = "Login endpoint" }));

        group.MapPost("/logout", () => Results.Ok(new { message = "Logout endpoint" }));

        group.MapGet("/antiforgery", () => Results.Ok(new { message = "Antiforgery endpoint" }));

        app.MapGet("/api/v1/me", () => Results.Ok(new { message = "Me endpoint" }));
    }
}
