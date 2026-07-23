# IAM and Authorization Contracts

## HTTP surface

- POST /api/v1/auth/login validates with ASP.NET Core PasswordHasher, sets a server-issued encrypted
  Secure/HttpOnly/SameSite cookie and returns no token body or hash.
- POST /api/v1/auth/logout revokes the server session and clears the cookie.
- GET /api/v1/auth/antiforgery supplies the token required for state-changing cookie requests.
- GET /api/v1/me resolves user ID, status, five base roles and canonical user_scope rows.
- Minimal Administrator commands assign roles and Site/Area scopes. SSO, reset workflow and complete
  administration UI remain out of scope.

## Session and caller context

iam.user_session stores a hashed session identifier, user ID, issue time, idle/absolute expiry,
revocation and status. Every authenticated request resolves the session and Active user, roles and
scopes server-side. Disabled users and revoked scopes lose access immediately. Tokens never appear
in query strings. Login errors are non-enumerating and failed attempts use bounded framework
rate-limiting. Bootstrap credentials are injected from protected environment state.

ICallerContext supplies userId, username, roles, canonical Site/Area scopes and correlation ID.
Claims supplied by the UI are never authority.

## Authorization

IAuthorizationDecision takes caller, capability and target Site/Area. Administrator is globally
allowed. Only Administrator may create, update, activate or inactivate a root Site. Engineer may
manage Area, Asset, Point, Catalog Source/Mapping and Simulator only within an assigned Site scope.
Operator, Manager and Viewer have distinct read policies and cannot mutate configuration.

Known capability denial returns FORBIDDEN. Out-of-scope object lookup returns indistinguishable
NOT_FOUND with no target payload. Every query and command invokes this decision before target data is
returned or changed.

IActiveUserEligibility takes userId and required Site/Area and returns existence, Active status and
scope compatibility. Data Owner assignment grants no role, AuditReview responsibility or elevated
permission.

## Bootstrap and roles

Seed fixed roles and a protected Administrator identity first. Administrator creates the root Site,
then assigns Engineer Site scope. No deterministic seed inserts a user_scope row that references a
nonexistent Site. Base roles are exactly Administrator, Engineer, Operator, Manager and Viewer.
AuditReview is a policy capability, not a sixth role and not automatic for Viewer.

## Events

UserStatusChanged.v1, UserRoleAssignmentsChanged.v1 and UserScopesChanged.v1 are owner-versioned
facts for session invalidation and Audit. Authorization-denied evidence includes actor, capability,
scope snapshot, timestamp and correlation ID without leaking target details.
