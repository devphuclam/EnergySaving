# Phase 5 Standards and Specification Review (T106)

Scope: T094–T107 only, against parent baseline
`4e68ca46d124d867a0737b17711a069bd83417aa` and Constitution `1.1.0`.

## Standards review

| Finding | Severity | Evidence | Resolution | State |
|---|---|---|---|---|
| STD-501 | High | Organization initially had a reverse dependency cycle through Catalog/IAM; Phase 5 also needs a shared transaction seam. | Organization now consumes only provider-neutral BuildingBlocks and Integration contracts; the R0 boundary explicitly permits the provider-neutral `HostTransactionCoordinator` primitive under `BuildingBlocks/Persistence`, while Catalog/IAM remain composition-side owners. | Closed |
| STD-502 | High | Activation needed one deterministic transaction boundary and lock order. | `HostTransactionCoordinator` enforces IAM → Organization Site/Area/Asset/Point → Catalog Metric/Unit/Mapping → Integration outbox and rejects descending order; organization and outbox participants commit/rollback through that same coordinator. | Closed |
| STD-503 | High | Outbox behavior could publish before host commit. | `FakeTransactionalOutboxWriter` stages by host transaction ID and moves rows only on host commit; rollback discards staged rows and duplicate EventIds are rejected. | Closed |
| STD-504 | High | Cross-module activation facts must not expose internals. | Organization owns activation query ports; IAM/Catalog implementations are not introduced in Phase 5 and no provider-specific type appears in the contracts. | Closed |
| STD-505 | High | Event payload must not become Audit persistence. | `OwnerEventEnvelope` is a versioned enqueue contract only; snapshots contain exactly six safe Point fields and no credentials. | Closed |
| STD-506 | Medium | Phase boundary could drift into later Simulator/Telemetry work. | Architecture checks reject later-phase indicators and no API/Worker/Telemetry/Run composition changes were made. | Closed |
| STD-507 | Medium | P-016 requires bounded transient conflict handling. | Host lock acquisition uses a 2-second timeout, up to three attempts, 50/150/450ms backoff, and maps exhaustion to `TRANSIENT_DATABASE_CONFLICT`; the API HTTP 503 mapping remains outside this phase's API boundary. | Closed |
| STD-508 | High | An outbox writer must not be allowed to publish outside the host transaction. | Activation requires a host participant or bridges the optional Integration participant; writers without either seam fail before mutation with `OUTBOX_PARTICIPANT_REQUIRED`. | Closed |

## Specification review

| Finding | Severity | Evidence | Resolution | State |
|---|---|---|---|---|
| SPEC-501 | High | FR-005/FR-AP-003..005 require specific activation failures. | Orchestrator returns distinct outcomes/codes for owner, parent, Metric/Unit, compatibility, mapping, state, stale version, and provider drift. | Closed |
| SPEC-502 | High | P-016 requires exact global lock order and rollback. | Nine canonical lock targets are recorded and tested; rollback runs participants in reverse acquisition order. | Closed |
| SPEC-503 | High | P-021 requires `PointStatusChanged.v1` with actor and correlation metadata. | Envelope has schema 1, Organization producer, MeasurementPoint aggregate, actor username, UTC time, correlation/causation, trusted Site/Area IDs, and safe Before/After. | Closed |
| SPEC-504 | High | Active Point is a no-op; Decommissioned is terminal. | Preflight returns `NoOp` without mutation/event/outbox for Active and `INVALID_STATE` for Decommissioned. | Closed |
| SPEC-505 | Medium | T104 depends on PostgreSQL adapters not available under package policy. | T104 remains unchecked and is classified `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE`; no database substitute or false PASS is recorded. | Closed |
| SPEC-506 | Medium | T103 source must be runnable without the blocked adapter leaf. | T103 is explicitly provider-neutral and compile/contract-only (lock order and rollback); activation/outbox concurrency against PostgreSQL is isolated in T104 and its blocked evidence. | Closed |

## Result

- Critical findings: 0 unresolved
- High findings: 0 unresolved
- Medium findings: 0 unresolved
- Low findings: 0 unresolved
- Review status: **PASS** for the runnable Phase 5 scope.
- Release readiness: **NO**; this is a phase checkpoint, not release approval.
