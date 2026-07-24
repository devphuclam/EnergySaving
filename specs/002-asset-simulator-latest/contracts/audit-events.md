# Audit Contract

## Mandatory delivery path

Complete Audit functionality is:

`Business command -> owner-module event -> integration.outbox_event -> OutboxDispatcherWorker ->
Audit integration.inbox_message claim/dedup -> IAuditEventConsumer ->
IAuditAppendRepository.AppendIfAbsentAsync -> audit.audit_event -> IAuditQueryService ->
authorized API/UI`

Payload construction or outbox insertion alone is not Audit persistence. Producers never call the
Audit repository and never insert another schema.

The owner mutation and `ITransactionalOutboxWriter.EnqueueAsync` commit atomically with Integration
last. The Worker claims outbox rows through `IOutboxClaimRepository` in a short Integration
transaction using `FOR UPDATE SKIP LOCKED`. Before consumption it claims or reclaims
`(consumer_name='Audit.v1', event_id)` through `IInboxDeduplicationRepository`. Audit append and
inbox completion then commit together through the host transaction coordinator: Audit first,
Integration last.

Delivery is at-least-once and the outcome is idempotent. The inbox primary key and unique
`audit.audit_event.source_event_id` ensure one source event creates at most one row for the Audit
consumer. A Completed inbox row short-circuits replay. A payload hash mismatch for an existing
consumer/event identity is a poison conflict and never overwrites prior evidence.

## Ports and adapters

| Owner | Public port / runtime component | Responsibility |
|---|---|---|
| Owner module | domain-event factory | Safe, versioned owner event after accepted business change |
| Integration | `ITransactionalOutboxWriter` / PostgreSQL adapter | Enqueue envelope in owner mutation transaction |
| Integration | `IOutboxClaimRepository` / PostgreSQL adapter | Claim/renew lease, publish, reschedule, mark Failed |
| Worker | `IOutboxDispatcher` / `OutboxDispatcherWorker` | Resolve required consumers and deliver every registered consumer |
| Integration | `IInboxDeduplicationRepository` / PostgreSQL adapter | Claim/reclaim, validate payload hash, complete, reschedule, mark Failed |
| Audit | `IAuditEventConsumer` | Validate/map versioned event to immutable Audit record |
| Audit | `IAuditAppendRepository` / PostgreSQL adapter | Append-if-absent; no update/delete surface |
| Audit | `IAuditQueryRepository` / PostgreSQL adapter | Apply authorization/scope before filters and paging |
| Audit | `IAuditQueryService` | Authorize and expose stable query DTOs |
| Operations | `IDurableJobScheduler`, `IJobClaimRepository` / PostgreSQL adapter | Durable dispatcher wakeups, reconciliation, replay, leases and retries |

`integration.outbox_event`, `integration.inbox_message`, and `operations.job` are the existing R0
tables and are reused, not recreated. Operations jobs are wakeups/control records, not a duplicate
event payload source.

## Append data and ownership

Each `audit_event` records its own ID, unique source event ID, source event type/schema version and
producer, aggregate ID/version, actor ID/username snapshot, object type/ID, action, important
redacted before/after JSON, safe summary, Site/Area scope snapshots, event occurrence time, Audit
recorded time, correlation ID, and causation ID. Credentials, tokens, hashes, and secrets are
excluded.

Covered operations include Site/Area/Asset/Point create/update/lifecycle, Source/Mapping lifecycle
and permitted deletion, configuration version creation, Simulator Start/Pause/Resume/Stop, user
role/scope/status/capability changes, authentication success/failure/revocation, and required
authorization decisions.

Audit is append-only: no update/delete port exists and database permissions deny update/delete.
There is no restrictive FK to the business subject. Snapshots preserve readable evidence after a
permitted Draft-unused Source/Mapping deletion; Audit evidence alone does not block deletion.

## Retry, poison, reconciliation, and replay

- Polling starts within one second; leases are 30 seconds and renewed while active.
- Maximum delivery attempts are 10. Retry delays are 250 ms, 1 s, 2 s, 5 s, then 30 s capped with
  bounded jitter.
- A transient failure reschedules the outbox/inbox/job using its existing or additive
  `next_attempt_at` and increments `attempt_count`.
- Exhaustion or a non-retryable envelope/hash/schema error moves the applicable record to existing
  `Failed`, stores only a redacted error, and emits poison/backlog metrics. Failed is not Published
  or Completed.
- Reconciliation reclaims expired leases, reschedules eligible failures, detects Published outbox
  events without a Completed Audit inbox/append, and reports identity/hash conflicts.
- Operator-authorized replay retains the original event ID, payload, correlation ID, and causation
  ID. It resets delivery state after correction; it never creates a replacement business event or
  duplicate Audit row.

The dispatcher marks an outbox event Published only after all required registered consumers have
Completed. A crash between consumers may invoke earlier consumers again; their Completed inbox rows
make this safe.

Correlation ID is inherited unchanged from the initiating command through owner event, outbox,
Worker logs, inbox, and Audit row. Causation ID identifies the initiating command or immediately
triggering event. Retries and replay preserve both and the source event ID.

## Authorized query

`GET /api/v1/audit-events` supports object, action, actor, correlation ID, and UTC time filters with
stable keyset order `(occurred_at DESC, audit_event_id DESC)`.

- An active Administrator has global access.
- Every other active user requires an active explicit `AUDIT_READ` capability **and** authorized
  Site/Area scope.
- Site scope includes descendant Areas/Assets/Points; Area scope includes only that Area and its
  descendants.
- Global/unscoped events are Administrator-only.
- Viewer and Manager do not receive `AUDIT_READ` automatically; Data Owner never gains it.

The PostgreSQL query adapter applies capability/scope restrictions before caller filters and paging.
Unauthorized/out-of-scope queries reveal neither rows nor counts.

Audit rows must become query-visible within five seconds of configuration/control execution under
supported POC load. This criterion measures the entire mandatory path, not event creation alone.
