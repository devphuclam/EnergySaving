# Audit Contract

## `AppendAuditEvent`

Required input: actor ID and username snapshot, UTC occurrence time, correlation ID, object type and
ID, action, important before/after values, human-readable summary, and Site/Area scope snapshot.
Creation is part of the originating transaction when within the same owner boundary, or consumes an
outbox-backed control/config event idempotently.

Covered operations:

- create, update, and lifecycle changes for Site, Area, Asset, and Measurement Point;
- create, update, activate/inactivate/supersede, and permitted delete for mappings;
- Simulator start, pause, resume, and stop;
- required authorization decisions.

Audit is append-only: no update/delete contract exists, and database permissions enforce it.
`eventId` is the inbox idempotency key. Before/after values exclude credentials and secrets.
Subject and actor snapshots preserve readable evidence if a Draft-unused source/mapping is later
deleted.

## HTTP read surface

`GET /api/v1/audit-events` supports authorized filters by object, action, actor, correlation ID, and
time. Reviewer access is satisfied by the authorized read capability assigned to the appropriate
Manager/Viewer policy; Data Owner assignment alone grants nothing. Results are Site/Area scoped
unless the caller is Administrator.

## Timeliness

Same-transaction entries are immediately visible. Outbox-delivered entries must become visible
within five seconds (`SC-006`) under the supported POC load, with retry and duplicate suppression.
