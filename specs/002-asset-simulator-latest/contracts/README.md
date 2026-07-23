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
| Canonical ingestion, Latest and Source Status | Telemetry | Worker, API |
| Immutable evidence and AuditReview query | Audit | API and authorized reviewers |

Synchronous ports answer facts needed to accept/reject the current operation. Later effects use
integration.outbox_event and integration.inbox_message. Audit consumes committed events through its
own inbox; an originating module never inserts another schema. No consumer writes a provider schema.

## Common result and error

Successful mutations return resource ID, status, new version and correlationId. Lists are
scope-filtered before paging. Errors have errorCode, message, correlationId and field/record details.
Canonical codes are UNAUTHENTICATED, FORBIDDEN, NOT_FOUND, VALIDATION_FAILED, PRECONDITION_FAILED,
DOMAIN_CONFLICT, VERSION_CONFLICT, DUPLICATE and DEPENDENT_HISTORY.

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

### Login / logout / query commands

- No aggregate If-Match.
- Login uses Idempotency-Key to prevent duplicate session creation.
- Logout and queries do not require If-Match.

## Session implementation defaults

| Setting | Working default |
|---|---|
| Cookie name | `.IUMP.Auth` |
| Secure | `true` (always); developer override via `ASPNETCORE_ENVIRONMENT=Development` |
| SameSite mode | `Lax` |
| HttpOnly | `true` |
| Idle timeout | 20 minutes |
| Absolute timeout | 8 hours |
| Session-token random entropy | 256 bits (32 bytes CSPRNG) |
| Hash stored in `user_session` | SHA-256 of session token |
| Session rotation | New token issued after login; old session revoked immediately |
| Logout | Server revokes `user_session`, clears cookie |
| Revocation | Server sets `revoked_at`; next request resolves `revoked_at < now` as invalid |
| Antiforgery cookie name | `.IUMP.Xsrf` |
| Antiforgery header name | `X-XSRF-TOKEN` |
| API allowed origins | Same-origin only (no CORS for POC) |
| Web allowed origins | Same-origin only |
| Local HTTPS requirement | Required in Production; Development permits HTTP with explicit `Cookie.SecurePolicy=Never` |
| Data Protection key ring | Persistent, stored under `%ProgramData%/IUMP/DataProtection-Keys/` on Windows |
| Key protection | DPAPI `ProtectKeysWithDpapi()` for local POC; `ProtectKeysWithDpapiNG()` for domain service account when available |
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
5. **Telemetry** — identity registry, raw Measurement, Latest projection
6. **Integration** — outbox_event row

### Applied rules

- **Point activation**: IAM user (Data Owner), Organization Point/ancestor rows, Catalog
  Metric/Unit/Mapping rows are locked before commit. Activation fails atomically if any advisory
  lock or row version changed between validation and commit.
- **Mapping activation**: Catalog Mapping row, Organization Point row locked in order. Exclusion
  constraint prevents effective-period overlap.
- **Simulator Start**: Acquisition Run, Organization Point, Catalog Source/Mapping rows locked
  deterministically. Fails if any ancestor is no longer Active.
- **Point decommission vs Simulator Start**: Both lock Organization Point row first, then
  Acquisition Run row. Mutual consistency is guaranteed — both cannot pass on stale checks.
- **Asset decommission**: Organization Asset row (with children check), then child Point rows
  in ID order.
- **Point decommission**: Organization Point row, then Acquisition Run status query (locked).
- **Canonical Measurement ingestion**: Telemetry locks identity registry row (or takes
  predicate lock), validates owner state via advisory snapshot version check inside the same
  transaction.
- **Source/Mapping hard-delete**: Catalog rows locked, dependency query against Acquisition
  and Telemetry via public ports (not cross-schema queries).

### Isolation and failure handling

- **Isolation level**: `REPEATABLE READ` for commands that lock multiple rows.
- **Row-lock behavior**: `SELECT ... FOR UPDATE` on referenced rows in the global order.
- **Retry on serialization/deadlock**: Up to 3 immediate retries with exponential backoff
  (50ms, 150ms, 450ms). After exhaustion, the error is returned to the caller as
  `PRECONDITION_FAILED` with detail `TRANSIENT_LOCK_FAILURE`.
- **Safe lock timeout**: `lock_timeout` set to 2 seconds per statement. Exceeding this is
  classified as a transient infrastructure error, not a business rejection. Monitoring alerts
  on sustained lock timeout.
