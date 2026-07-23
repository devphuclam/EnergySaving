# Audit Contract

## Append and ownership

AppendAuditEvent requires actor ID/username snapshot, UTC time, correlation ID, object type/ID,
action, important before/after values, summary and Site/Area scope snapshot. Audit owns the write.
Business modules commit their own state and publish events to integration.outbox_event; Audit
consumes through integration.inbox_message idempotently. Producers never insert another schema.

Covered operations include Site/Area/Asset/Point create/update/lifecycle, Source/Mapping lifecycle
and permitted deletion, configuration version creation, Simulator Start/Pause/Resume/Stop, user
role/scope/status/capability changes, authentication success/failure/revocation and required
authorization decisions. Credentials and hashes are excluded.

Audit is append-only: no update/delete contract exists and database permissions enforce it. Event
subject snapshots preserve readable evidence if a Draft-unused Source/Mapping is later deleted; an
Audit snapshot alone does not block that deletion.

## Authorized query

GET /api/v1/audit-events supports filters by object, action, actor, correlation ID and time.
AuditReview requires the `AUDIT_READ` capability:

- Administrator has implicit `AUDIT_READ` (no explicit user_capability row required).
- All other users require an explicit active `user_capability` assignment to `AUDIT_READ`.
- Viewer never receives it automatically.
- Manager does not receive it automatically unless explicitly chosen by seeded POC policy.
- Data Owner assignment never grants `AUDIT_READ`.

Results are Site/Area scoped unless Administrator.

## Timeliness and event envelope

Audit events must become query-visible within five seconds of configuration/control execution under
supported POC load. Events are eventType.v1 with schemaVersion=1, event ID and causation/correlation
IDs; inbox deduplicates retries. Authorization denial evidence contains no target data.
