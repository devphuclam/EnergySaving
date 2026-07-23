# Contracts: Asset Simulator Latest

These contracts define R1/VS-01 boundaries. They are design inputs, not generated source code.
HTTP routes use /api/v1. Mutable commands use an authenticated principal, correlationId, an
idempotency key and If-Match carrying aggregate version bigint.

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
NOT_FOUND with no payload. Aggregate version is surfaced as an ETag and required as If-Match.

## Reliability and events

Commands are retriable with idempotency keys. Events carry eventId, immutable eventType.v1,
occurredAt, correlationId, causationId, aggregate ID/version and schemaVersion=1. Consumers
deduplicate by eventId using inbox_message. Ordering is guaranteed per aggregate; version gaps
trigger retry/reconciliation.
