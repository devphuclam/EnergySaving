# Contracts: Asset Simulator Latest

These contracts define R1/VS-01 boundaries. They are technology-neutral design inputs, not generated
source code. HTTP routes are versioned under `/api/v1`; commands use authenticated principals,
`correlationId`, and an `expectedVersion` for mutable aggregates.

## Ownership

| Contract | Provider | Primary consumers |
|---|---|---|
| Principal, role, scope, active-user eligibility | IAM | API, Organization, Acquisition, Audit |
| Hierarchy lifecycle and Point readiness/scope | Organization | API, Acquisition, Telemetry |
| Metric/Unit compatibility, Source and mapping | Catalog | Organization, Acquisition, Telemetry |
| Simulator configuration and run | Acquisition | API, Worker, Telemetry |
| Canonical ingestion, Latest, Source Health | Telemetry | Worker, API |
| Immutable control/config evidence | Audit | API/query consumers |

Synchronous ports answer facts required to accept or reject the current operation. Cross-module
effects that may complete later use the existing transactional outbox/inbox. No consumer writes a
provider's schema.

## Common Result and Error Shape

Successful mutations return the resource ID, status, new version, and correlation ID. List results
are scope-filtered before paging. Errors use:

```json
{
  "code": "DOMAIN_CONFLICT",
  "message": "A specific safe message",
  "correlationId": "uuid",
  "fieldErrors": [{ "field": "name", "code": "REQUIRED" }]
}
```

Canonical codes: `UNAUTHENTICATED`, `FORBIDDEN`, `NOT_FOUND`, `VALIDATION_FAILED`,
`PRECONDITION_FAILED`, `DOMAIN_CONFLICT`, `VERSION_CONFLICT`, `DUPLICATE`, and
`DEPENDENT_HISTORY`. A scoped caller receives no target payload on forbidden/not-found responses.

## Reliability

- Commands are retriable only with an idempotency key or stable resource identity.
- Events carry `eventId`, `eventType`, `occurredAt`, `correlationId`, `causationId`, aggregate ID,
  aggregate version, and schema version.
- Consumers deduplicate by event ID. Ordering is guaranteed only per aggregate; version gaps trigger
  retry/reconciliation rather than blind application.
