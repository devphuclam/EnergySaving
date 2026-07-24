# IAM and Authorization Contracts

## HTTP surface

- POST /api/v1/auth/login validates with ASP.NET Core PasswordHasher. On success, generates a
  256-bit opaque random session token, stores SHA-256(token) in `iam.user_session`, and sets a
  Secure/HttpOnly/SameSite=Lax cookie (`.IUMP.Auth`) containing the raw opaque token. Returns no
  token body or hash. Does not use Idempotency-Key or If-Match.
- POST /api/v1/auth/logout revokes the current `user_session` by setting `revoked_at`, clears
  the cookie. Requires antiforgery (cookie-authenticated state change). No If-Match.
- GET /api/v1/auth/antiforgery supplies the token (`.IUMP.Xsrf` cookie, `X-XSRF-TOKEN` header)
  required for state-changing cookie requests.
- GET /api/v1/me resolves user ID, status, five base roles, canonical capabilities and canonical
  user_scope rows.
- Minimal Administrator commands assign roles, capabilities and Site/Area scopes. SSO, reset
  workflow and complete administration UI remain out of scope.

## Session implementation

Working defaults:

| Setting | Value |
|---|---|
| Cookie name | `.IUMP.Auth` |
| Cookie content | Opaque random session token (256-bit CSPRNG). Not an ASP.NET encrypted identity ticket |
| Secure | `true`; `ASPNETCORE_ENVIRONMENT=Development` permits `Cookie.SecurePolicy=Never` |
| SameSite | `Lax` |
| HttpOnly | `true` |
| Idle timeout | 20 minutes |
| Absolute timeout | 8 hours |
| Session-token entropy | 256 bits (32 bytes CSPRNG) |
| Hash stored in `user_session` | SHA-256 of session token. Original token is not stored |
| Session rotation | New opaque token after login; old session remains valid (multiple sessions allowed per user) |
| Logout | Revokes current `user_session` by setting `revoked_at`, clears cookie |
| Revocation | `revoked_at IS NOT NULL` means revoked; idle/absolute expiry or Disabled user also means invalid |
| Multiple sessions | Allowed per user; each successful login creates a new independent session |
| Disabled-user invalidation | Every authenticated request checks user status; Disabled immediately invalidates all sessions |
| Administrator revoke-all | Explicit Administrator command sets `revoked_at` on every session for the target user |
| Antiforgery cookie | `.IUMP.Xsrf` |
| Antiforgery header | `X-XSRF-TOKEN` |
| API origins | Same-origin only (no CORS) |
| Web origins | Same-origin only |
| HTTPS | Required in Production; Development permits HTTP |
| Data Protection key store | Required for ASP.NET Data Protection (antiforgery, framework-protected values). Directory must be pre-provisioned and writable by the API service account. Application must not request elevation or alter system ACLs |
| Key protection (Windows) | DPAPI `ProtectKeysWithDpapi()` using a pre-provisioned directory. Development may use an approved user-writable local path configured outside the repository (e.g. `%LOCALAPPDATA%/IUMP/DataProtection-Keys/`) |
| Key availability | Unavailable/unapproved storage is BLOCKED_BY_ENVIRONMENT. No keys are committed |
| Rate-limit window | 15 seconds |
| Rate-limit threshold | 5 failed attempts per window per username |

`iam.user_session` stores SHA-256(session token), user ID, issue time, idle/absolute expiry,
`revoked_at` (nullable) and status. Every authenticated request reads the opaque token from the
`.IUMP.Auth` cookie, hashes it, looks up the session, and validates Active user, roles and scopes
server-side. Disabled users and revoked scopes lose access immediately. The raw token never appears
in response JSON, query strings or logs. Login errors are non-enumerating and failed attempts use
bounded ASP.NET Core framework rate-limiting. Bootstrap credentials are injected from protected
environment state.

ICallerContext supplies userId, username, roles, canonical capabilities, Site/Area scopes and
correlation ID. Claims supplied by the UI are never authority.

## Capability model

`iam.capability` is a fixed seeded table:

| capability_id | code | name |
|---|---|---|
| (seeded UUID) | `AUDIT_READ` | Audit Review |

`iam.user_capability` assigns capabilities to users (not roles):

- `user_capability_id` (UUID PK)
- `user_id` (FK to `iam.user_account`)
- `capability_id` (FK to `iam.capability`)
- `assigned_by` (FK to `iam.user_account`)
- `assigned_at` (timestamptz)
- `revoked_at` (timestamptz, nullable)
- `version` (bigint)

Administrator has implicit/global `AUDIT_READ` without a `user_capability` row. All other users
require an explicit active `user_capability` assignment (where `revoked_at IS NULL`). Viewer
does not receive it automatically. Manager does not receive it automatically unless explicitly
chosen by the seeded POC policy. Data Owner never gains it implicitly.

No full permission-management UI is required. A minimal Administrator command
`POST /api/v1/admin/capabilities/assign` and `POST /api/v1/admin/capabilities/revoke` is
sufficient for POC.

## Authorization

IAuthorizationDecision takes caller, capability and target Site/Area. Administrator is globally
allowed. Only Administrator may create, update, activate or inactivate a root Site. Engineer may
manage Area, Asset, Point, Catalog Source/Mapping and Simulator only within an assigned Site scope.
Operator, Manager and Viewer have distinct read policies and cannot mutate configuration.

Known capability denial returns FORBIDDEN. Out-of-scope object lookup returns indistinguishable
NOT_FOUND with no target payload. Every query and command invokes this decision before target data is
returned or changed. AUDIT_READ is checked as a capability (Administrator implicit, others via
user_capability).

IActiveUserEligibility takes userId and required Site/Area and returns existence, Active status and
scope compatibility. Data Owner assignment grants no role, AuditReview capability or elevated
permission.

## Bootstrap and roles

Seed fixed roles and a protected Administrator identity first. Administrator creates the root Site,
then assigns Engineer Site scope. No deterministic seed inserts a user_scope row that references a
nonexistent Site. Base roles are exactly Administrator, Engineer, Operator, Manager and Viewer.
AuditReview is governed by the capability model (iam.capability + user_capability), not a sixth
role and not automatic for Viewer.

### Deterministic POC users (A - database fixture)

Five users with fixed credentials (delivered via protected environment, never committed):

- Administrator (global scope, implicit AUDIT_READ)
- Engineer (no Site scope — scope is assigned post-Site via B)
- Operator (no Site scope — assigned post-Site via B)
- Manager (no Site scope — assigned post-Site via B)
- Viewer (no Site scope — assigned post-Site via B)

### Post-Site POC fixture (B - application command)

After Administrator creates the test Site, an authenticated Administrator command:

- Locates or creates the deterministic test Site
- Assigns Engineer, Operator, Manager and Viewer to that Site scope
- Optionally assigns Area scope after Area exists
- Optionally grants AUDIT_READ to Manager per seeded POC policy
- Idempotent (re-run does not duplicate)
- Uses application/IAM commands, not direct SQL

This fixture is disabled or explicitly controlled outside development/POC.

## Events

UserStatusChanged.v1, UserRoleAssignmentsChanged.v1, UserCapabilitiesChanged.v1 and
UserScopesChanged.v1 are owner-versioned facts for session invalidation and Audit.
Authorization-denied evidence includes actor, capability, scope snapshot, timestamp and correlation
ID without leaking target details.
