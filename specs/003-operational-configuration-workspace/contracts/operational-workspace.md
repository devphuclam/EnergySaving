# Contract: Operational Workspace

All routes require an authenticated server principal except existing login/antiforgery routes.
Scope is resolved server-side. Error bodies contain safe codes and optional correlation IDs, never
roles or scopes supplied by the browser, credentials, connection strings, or resource metadata
outside authorized scope.

## GET `/api/v1/operational-workspace/status`

Returns the caller’s state-aware landing and resumable setup projection.

### Response 200

```json
{
  "landing": "SetupWizard",
  "roleMode": "Administrator",
  "authorizedSites": [
    {
      "siteId": "00000000-0000-0000-0000-000000000000",
      "code": "SITE_A",
      "name": "Site A",
      "status": "Draft",
      "version": 1
    }
  ],
  "selectedSiteId": null,
  "completedSteps": [],
  "nextStep": "site-and-engineer",
  "validationFailures": [],
  "operationalChainCount": 0,
  "incompleteChainCount": 0,
  "dependency": {
    "status": "Available",
    "errorCode": null,
    "correlationId": null
  }
}
```

`landing` values:

- `SetupWizard`
- `ContinueSetup`
- `Dashboard`
- `NoAuthorizedScope`
- `DependencyError`

`roleMode` values:

- `Administrator`
- `Engineer`
- `ReadOnly`

Step IDs:

1. `site-and-engineer`
2. `area`
3. `asset`
4. `measurement-point`
5. `data-source`
6. `mapping`
7. `simulator-configuration`
8. `validate-and-activate`

### Safe outcomes

| Status | Meaning |
|---:|---|
| 401 | No valid server session |
| 403 | Authenticated but workspace access capability is unavailable |
| 403 | `mode=new` requested by a non-Administrator |
| 404 | `selectedSiteId` is unavailable or outside the caller's authorized scope |
| 400 | Malformed or mutually exclusive mode/selection query |
| 503 | API/database dependency unavailable; no fallback data |

Optional query parameters select an explicit Administrator setup context:

```text
mode=new
selectedSiteId=<uuid>
```

`mode=new` is Administrator-only and returns an empty, server-derived `SetupWizard` projection
whose next step is `site-and-engineer`; it never creates a Site or command-idempotency record.
`selectedSiteId` is authorized and resolved on the server, returns only that Site and its eligible
chain/counts, and is safe across refreshes and chain ordering. The two parameters are mutually
exclusive. An omitted query preserves the default landing behavior, including the operational
Dashboard when an eligible operational chain exists. The browser must not use localStorage or list
position as setup authority.

The status query never creates command-idempotency records.

## GET `/api/v1/operational-dashboard`

Returns an authorized read-only composition for the landing dashboard. The API resolves scope
from the authenticated server principal and applies that scope before every count, list, or page.
It never returns global totals for a caller with no authorized scope and never starts a Simulator.

### Response 200

```json
{
  "state": "Ready",
  "roleMode": "Engineer",
  "sites": { "count": 1, "items": [] },
  "sources": { "count": 1, "items": [] },
  "points": { "count": 1, "items": [] },
  "runs": { "count": 0, "items": [] },
  "latest": { "count": 1, "items": [] },
  "health": { "count": 1, "items": [] },
  "incompleteSetup": { "count": 0, "nextStep": null },
  "recentAudit": { "items": [], "nextCursor": null },
  "runtime": { "status": "Available", "simulatorRunning": false },
  "dependency": { "status": "Available", "errorCode": null, "correlationId": null }
}
```

`state` is one of `Ready`, `NoAuthorizedScope`, `DependencyError`, `Forbidden`, or
`RuntimeError`. `runtime.status` is the server-observed availability of the approved
database/runtime dependency; `simulatorRunning` is an observed Run state and is never a command.
Summary item arrays contain only public, scope-authorized identifiers and display fields.

### Safe outcomes

| Status | Meaning |
|---:|---|
| 401 | No valid server session |
| 403 | Dashboard capability is unavailable |
| 503 | API/database dependency unavailable; no fallback or demo data |

## GET `/api/v1/audit-events` query contract

Audit review accepts `fromUtc`, `toUtc`, `actorId`, `action`, `entityType` (serialized as
`objectType` for compatibility), `entityId`, `siteId`, `areaId`, `correlationId`, `pageSize`, and
an opaque `cursor`. Filters, authorization, and the strict `(occurredAtUtc,auditEventId)` keyset
are evaluated before paging. A caller may receive a correlation ID only with the audit-correlation
permission (`AUDIT_CORRELATION`, granted to Administrators only); otherwise it is omitted. Before/after maps are a server-side redacted safe diff:
passwords, hashes, tokens, credentials, connection strings, and secret references are removed or
replaced with `[REDACTED]` before serialization. Out-of-scope rows are indistinguishable from
not-found rows. The response's `itemCount` is the count in the returned page, not a total-record
count, and `nextCursor` is present only when another page exists.

## GET `/api/v1/operational-workspace/engineers`

Lists active existing Engineer accounts available for Administrator assignment. Password hashes,
sessions, tokens, capabilities unrelated to assignment, and unauthorized user metadata are absent.

### Response 200

```json
{
  "items": [
    {
      "userId": "00000000-0000-0000-0000-000000000002",
      "username": "engineer",
      "status": "Active",
      "assignedSiteIds": []
    }
  ]
}
```

### Safe outcomes

| Status | Meaning |
|---:|---|
| 401 | No valid server session |
| 403 | Caller is not Administrator |

## POST `/api/v1/operational-workspace/sites/{siteId}/engineers/{engineerUserId}`

Assigns one existing active Engineer to Site scope.

Required headers:

```text
Idempotency-Key: <opaque client-generated value>
X-XSRF-TOKEN: <antiforgery token>
```

The server resolves the Administrator principal. The body does not accept role, capability, actor,
or authoritative scope claims.

### Response 200/201

```json
{
  "siteId": "00000000-0000-0000-0000-000000000000",
  "engineerUserId": "00000000-0000-0000-0000-000000000002",
  "status": "Assigned",
  "replayed": false
}
```

Duplicate canonical retries replay the original response. A pre-existing identical assignment may
return the same successful business outcome without a duplicate scope or outbox/Audit event.

### Safe outcomes

| Status | Error code | Meaning |
|---:|---|---|
| 400 | `IDEMPOTENCY_KEY_REQUIRED` | Required mutation metadata missing |
| 401 | `UNAUTHENTICATED` | No valid server session |
| 403 | `FORBIDDEN` | Caller is not Administrator |
| 404 | `NOT_FOUND` | Site or eligible Engineer is unavailable; do not distinguish out-of-scope metadata |
| 409 | `IDEMPOTENCY_CONFLICT` | Same key, different canonical request |
| 422 | `ENGINEER_ASSIGNMENT_INVALID` | User is inactive or lacks Engineer role |

## GET `/api/v1/operational-workspace/chains/validate`

Query parameters:

```text
siteId=<uuid>
areaId=<uuid>
assetId=<uuid>
pointId=<uuid>
sourceId=<uuid>
mappingId=<uuid>
configurationId=<uuid>
```

This endpoint is read-only. It rechecks the complete authorized chain and does not use the command
idempotency registry.

### Response 200 — valid

```json
{
  "valid": true,
  "failures": [],
  "versions": {
    "site": 2,
    "area": 2,
    "asset": 2,
    "point": 1,
    "source": 2,
    "mapping": 2,
    "configuration": 1
  },
  "activationSteps": [
    "site",
    "area",
    "asset",
    "data-source",
    "mapping",
    "measurement-point"
  ],
  "simulatorAutoStart": false
}
```

### Response 200 — invalid

```json
{
  "valid": false,
  "failures": [
    {
      "step": "mapping",
      "field": "mappingId",
      "errorCode": "ACTIVE_MAPPING_REQUIRED",
      "messageKey": "setup.mapping.activeRequired"
    }
  ],
  "versions": {},
  "activationSteps": [],
  "simulatorAutoStart": false
}
```

### Safe outcomes

| Status | Meaning |
|---:|---|
| 400 | Malformed identifier |
| 401 | No valid server session |
| 404 | Chain or scope unavailable; no out-of-scope metadata |
| 503 | Dependency unavailable |

## Existing owner mutation reuse

Phase 1 reuses existing configuration routes for Site, Area, Asset, Point, Source, Mapping, and
Simulator Configuration create/update/lifecycle operations. Every mutation includes
`Idempotency-Key`; lifecycle/update includes the current `If-Match`. Existing command response
status/body/Location/ETag/correlation replay semantics remain authoritative.

Activation uses:

1. `POST /api/v1/sites/{siteId}/activate`
2. `POST /api/v1/areas/{areaId}/activate`
3. `POST /api/v1/assets/{assetId}/activate`
4. `POST /api/v1/data-sources/{sourceId}/activate`
5. `POST /api/v1/source-point-mappings/{mappingId}/activate`
6. `POST /api/v1/points/{pointId}/activate`

No Simulator Configuration activation route is added. No Simulator Start route is called by setup.

## Idempotency-key lifetime in the Web gateway

The gateway generates one opaque key per deliberate user mutation and retains that key while
retrying an uncertain outcome. Editing the command fields creates a new deliberate operation/key.
Keys are held in component/request state only; localStorage is not setup authority.
