# Contracts: Asset Simulator Latest

These contracts define R1/VS-01 boundaries. They are design inputs, not generated source code.
HTTP routes use /api/v1.

## Ownership

| Contract | Provider | Consumers |
|---|---|---|
| Principal, role, scope, session and active-user eligibility | IAM | API, Organization, Acquisition, Audit |
| Hierarchy lifecycle, Point readiness and decommission dependency | Organization | API, Acquisition, Telemetry |
| Metric/Unit, Source and Mapping | Catalog | Organization, Acquisition, Telemetry |
| Simulator configuration and Run | Acquisition | API, Worker, Telemetry |
| Canonical ingestion, immutable terminal outcome registry, Latest and Source Status | Telemetry | Worker, API |
| Immutable evidence and AuditReview query | Audit | API and authorized reviewers |
| API command idempotency, transactional outbox and consumer inbox | Integration | API/Worker composition roots and module application services |
| Durable scheduling, leases, retries and reconciliation | Operations | Worker and module application services |

Synchronous ports answer facts needed to accept/reject the current operation. Later effects use
integration.outbox_event and integration.inbox_message. Audit consumes committed events through its
own inbox; an originating module never inserts another schema. No consumer writes a provider schema.
Detailed runtime contracts are `integration.md`, `operations.md`, `audit-events.md`, and
`persistence-adapters.md`.

## Common result and error

Successful mutations return resource ID, status, new version and correlationId. Lists are
scope-filtered before paging. Errors have errorCode, message, correlationId and field/record details.
Canonical codes are UNAUTHENTICATED, FORBIDDEN, NOT_FOUND, VALIDATION_FAILED, PRECONDITION_FAILED,
DOMAIN_CONFLICT, VERSION_CONFLICT, DUPLICATE, IDEMPOTENCY_CONFLICT, IDEMPOTENCY_IN_PROGRESS,
DEPENDENT_HISTORY and TRANSIENT_DATABASE_CONFLICT.

Known capability denials may return FORBIDDEN. An out-of-scope object lookup is indistinguishable
NOT_FOUND with no payload.

## API concurrency and idempotency

### Create commands

- **Idempotency-Key** header REQUIRED.
- **No If-Match**: no aggregate version exists before creation.
- Duplicate idempotency key returns the original result with original status code and version.

### Update / lifecycle / delete commands

- **If-Match** header REQUIRED carrying the aggregate version as bigint.
- Stale version returns the one canonical **VERSION_CONFLICT** response.
- Idempotency-Key is also REQUIRED for retry safety.

### Login

- No aggregate If-Match.
- No Idempotency-Key.

### Logout

- No aggregate If-Match.
- Antiforgery required (cookie-authenticated state change).

### Query

- No aggregate If-Match.
- No Idempotency-Key.

### Durable command mechanism

`integration.command_idempotency` is the sole API-command registry. Its unique scope is
`(caller_user_id, operation_code, idempotency_key)`. The API composition root uses the
Integration-owned `ICommandIdempotencyStore`; consumer modules never write Integration tables.
`integration.inbox_message` is only consumer-event deduplication.

The V1 fingerprint is SHA-256 over a typed, length-prefixed UTF-8 encoding beginning
`IUMP:COMMAND-IDEMPOTENCY:V1`. Fields are ordered as operation code, caller UUID, target scope
type/UUID, target aggregate type/UUID, expected version, then the command DTO's frozen
operation-specific field order. Each name/value uses an unsigned 32-bit big-endian byte length;
`0xffffffff` denotes null. UUIDs are lowercase dashed, strings Unicode NFC without semantic
trimming, booleans lowercase, integers invariant base-10, decimals invariant minimal decimal, and
timestamps UTC RFC3339 with seven fractional digits. The expected version includes If-Match.
Idempotency-Key, cookies, credentials, authorization/antiforgery headers, correlation/trace IDs,
retry metadata, JSON whitespace/property order, and transport header order are excluded.

- Same tuple and fingerprint, Completed: replay the original HTTP status, body or stable result
  reference, resource ID/version, allowlisted `Location`/`ETag`, and original correlation ID.
- Same tuple and fingerprint, live Pending: bounded wait/reload; if still live, HTTP 409
  `IDEMPOTENCY_IN_PROGRESS` with `Retry-After`.
- Same tuple and fingerprint, expired Pending: one caller reclaims with optimistic version and
  resumes; reconciliation must establish that no owner mutation was committed.
- Same tuple and different fingerprint: HTTP 409 `IDEMPOTENCY_CONFLICT`, without disclosing stored
  request or result.
- Authentication, antiforgery, rate-limit, and request-shape failures happen before registration.
  Deterministic business outcomes may be Completed; transient infrastructure failures remain
  reclaimable Pending.
- Pending lease is 30 seconds. Completed records are retained 24 hours; cleanup uses `expires_at`.
  An expired Pending row is reclaimed or removed only after reconciliation proves no committed
  owner mutation/outbox/result. Raw requests, session material, hashes, credentials, and secrets
  are never stored.

Registration is a short committed Integration transaction so Pending survives a pre-mutation
crash. Business execution is a host-coordinated PostgreSQL transaction: provider modules lock and
mutate in global order; Integration is last and atomically verifies the reservation, writes the
outbox, and records Completed with the owner mutation. A unique-race loser reloads the winner in a
new bounded transaction. This differs from Acquisition production attempts, Telemetry measurement
identity, Integration inbox deduplication, and Operations job scheduling. See `integration.md`.

## Session implementation defaults

| Setting | Working default |
|---|---|
| Cookie name | `.IUMP.Auth` |
| Cookie content | Opaque random session token (256-bit CSPRNG). Not an ASP.NET encrypted identity ticket |
| Secure | `true` (always); developer override via `ASPNETCORE_ENVIRONMENT=Development` |
| SameSite mode | `Lax` |
| HttpOnly | `true` |
| Idle timeout | 20 minutes |
| Absolute timeout | 8 hours |
| Session-token random entropy | 256 bits (32 bytes CSPRNG) |
| Hash stored in `user_session` | SHA-256 of session token |
| Session rotation | New opaque token after login; old session remains valid (multiple sessions allowed) |
| Logout | Revokes current `user_session` by setting `revoked_at`, clears cookie |
| Revocation | `revoked_at IS NOT NULL` means revoked; idle/absolute expiry or Disabled user also means invalid |
| Multiple sessions | Allowed per user; each login creates a new independent session |
| Antiforgery cookie name | `.IUMP.Xsrf` |
| Antiforgery header name | `X-XSRF-TOKEN` |
| API allowed origins | Same-origin only (no CORS for POC) |
| Web allowed origins | Same-origin only |
| Local HTTPS requirement | Required in Production; Development permits HTTP with explicit `Cookie.SecurePolicy=Never` |
| Data Protection key store | Required for ASP.NET Data Protection (antiforgery, framework-protected values). Directory must be pre-provisioned and writable by the API service account. Application must not request elevation or alter system ACLs |
| Key storage (Windows) | DPAPI `ProtectKeysWithDpapi()` using a pre-provisioned directory. Development may use an approved user-writable local path configured outside the repository (e.g. `%LOCALAPPDATA%/IUMP/DataProtection-Keys/`) |
| Key storage classification | Missing required provisioning/approval is BLOCKED_BY_COMPANY_APPROVAL. No keys are committed |
| Rate-limit window | 15 seconds |
| Rate-limit attempt threshold | 5 failed attempts per window per username |

No encryption keys, real passwords or bootstrap credentials are committed.

## Reliability and events

Commands are retriable with idempotency keys. Events carry eventId, immutable eventType.v1,
occurredAt, correlationId, causationId, aggregate ID/version and schemaVersion=1. Consumers
deduplicate by eventId using inbox_message. Ordering is guaranteed per aggregate; version gaps
trigger retry/reconciliation.

## Cross-module transaction and lock protocol

The composition root owns the unit-of-work boundary. Each provider module performs its own reads
and row locks through its public adapter. A consumer never directly queries or writes another schema.

### Global lock order

Provider rows required for a strict invariant are locked in this deterministic order to prevent
deadlock:

1. **IAM** — user row, role assignment
2. **Organization** — Site, Area, Asset, Point rows
3. **Catalog** — Metric, Unit, Source, Mapping rows
4. **Acquisition** — Run, run-point-state, production-attempt rows
5. **Telemetry** — terminal identity/result registry, Accepted raw Measurement, Latest projection
6. **Integration** — command_idempotency and outbox_event rows

### Applied flow orders

Each flow uses provider-owned adapters inside one host-coordinated PostgreSQL transaction.
Consumers must not directly query or write another schema. `SELECT ... FOR UPDATE` on strict
invariant rows in the exact order below.

**Point activation:**
IAM user/scope → Organization hierarchy/Point → Catalog Metric/Unit/Mapping → Integration outbox

**Mapping activation:**
Organization Point → Catalog Source/Mapping and overlap rows → Integration outbox

**Simulator Start:**
Organization Point/ancestors → Catalog Source/Mapping → Acquisition Run/Run-Point rows → Integration outbox

**Point decommission:**
Organization Point → Acquisition Running-Run dependency → Integration outbox

**Asset decommission:**
Organization Asset → child Organization Points ordered by Point ID → Integration outbox

**Telemetry ingestion:**
Organization operational Point snapshot → Catalog Source/Mapping/Metric/Unit → Telemetry terminal
identity/result registry, Accepted raw Measurement and Latest → Integration outbox

Before this locked new-result flow, Telemetry may perform an immutable non-locking registry lookup.
A matching fingerprint returns Duplicate plus the exact stored Accepted/Rejected result. A different
fingerprint returns IDEMPOTENCY_CONFLICT. This preflight performs no write and does not change the
global lock order. Missing/malformed Measurement identity and untrusted producer failures occur
before registry reservation.

**Source/Mapping hard delete:**
Organization reference where required → Catalog Source/Mapping → Acquisition dependencies → Telemetry dependencies → Integration outbox

### Isolation and failure handling

- **Isolation level**: `REPEATABLE READ` for commands that lock multiple rows.
- **Row-lock behavior**: `SELECT ... FOR UPDATE` on referenced rows in the global order.
- **Retry on serialization/deadlock**: Up to 3 immediate retries with exponential backoff
  (50ms, 150ms, 450ms). After exhaustion, the caller receives:
  - HTTP 503 Service Unavailable
  - `errorCode`: `TRANSIENT_DATABASE_CONFLICT`
  - `retryable`: `true`
  - Safe correlation ID
  - `Retry-After` header when appropriate
- **Lock timeout**: `lock_timeout` set to 2 seconds per statement. Exceeding this is also a
  transient infrastructure error returning `TRANSIENT_DATABASE_CONFLICT`, never
  `PRECONDITION_FAILED`. Monitoring alerts on sustained lock timeout.

`PRECONDITION_FAILED` remains for business-state failures such as: inactive parent, missing
Mapping, incompatible Unit, Running Simulator blocking decommission.
