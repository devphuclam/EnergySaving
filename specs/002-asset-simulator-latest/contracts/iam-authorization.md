# IAM and Authorization Contracts

## HTTP surface

- `POST /api/v1/auth/login`: local POC authentication; returns an opaque session/token and no
  credential hash.
- `GET /api/v1/me`: resolved user ID, status, roles, Site scopes, and Area scopes.
- Administration seed/bootstrap is migration/configuration driven for VS-01; complete user
  administration and password-reset UI are out of scope.

## `ICallerContext`

Provides authenticated `userId`, username, roles, Site/Area scopes, and correlation ID for every
request. Resolution is server-side on every authenticated request; UI claims are never authority.

## `IAuthorizationDecision`

Input: caller, capability, target Site/Area identifiers. Output: Allow/Deny plus safe reason code.
Administrator is globally allowed. Engineer may manage configuration in assigned scope. Operator,
Manager, and Viewer may read permitted hierarchy/current status but cannot mutate configuration.
All commands and queries call this contract before target data is returned or changed.

## `IActiveUserEligibility`

Input: `userId`, required Site/Area. Output: existence, Active status, and scope compatibility.
Organization uses it when assigning/activating a Point Data Owner. Data Owner assignment grants no
additional role.

## Events

`UserStatusChanged`, `UserRoleAssignmentsChanged`, and `UserScopesChanged` are owner-versioned facts
for cache invalidation/audit where a consumer exists. Authorization-denied evidence contains actor,
capability, scope snapshot, timestamp, and correlation ID without leaking target details.

