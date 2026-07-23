# Research: Asset Simulator Latest

**Feature**: `002-asset-simulator-latest`  
**Release boundary**: R1 / VS-01  
**Date**: 2026-07-23

## Sources and Authority

Research used the feature specification as the requirement authority, the constitution and accepted
ADRs as governance authority, and the following business documents as supporting design sources:
DOC-04 v0.2 (SRS), DOC-05 v0.2 (Architecture), DOC-06 v0.1 (Data and Integration), and DOC-07 v0.2
(Roadmap). `CONTEXT.md`, the module ownership manifest, the existing R0 solution, migration
`0001_r0_foundation.sql`, and verification scripts were inspected to ensure the design fits the
repository that actually exists.

The planning prompt mentions six success criteria, while `spec.md` defines eight. The specification
wins; all `SC-001..SC-008` are planned.

## Decision P-001: Latest eligibility by quality

**Decision**: A safely interpreted Measurement with quality `Good` or `Uncertain` is eligible to
advance Point Latest. A safely interpreted Measurement with quality `Bad` is persisted for
traceability but is not Latest-eligible. `No Data` is a derived Source Health state, not a stored
Measurement quality. The last observed health/result is retained separately from the current
derived health.

**Rationale**: This preserves observable evidence while preventing known-invalid values from
becoming the operational current value. It also avoids manufacturing Measurement rows to express
silence.

**Alternatives considered**:

- Only `Good` advances Latest: rejected because valid-but-qualified data would make Latest stale.
- `Bad` advances Latest: rejected because the current operational value could become known-invalid.
- Store synthetic `No Data` Measurements: rejected because absence is derived from elapsed time.

## Decision P-002: Clock-skew policy

**Decision**: The allowed future source-timestamp skew is configuration with an R1 default of
`300 seconds`. A safely parsed Measurement beyond that threshold is accepted, persisted with
`Uncertain` quality and reason `SOURCE_TIMESTAMP_FUTURE`, and remains Latest-eligible under P-001.
The configured value is bounded, versioned, audited, and evaluated using the server processing
clock.

**Rationale**: Simulator and industrial source clocks can drift. Downgrading quality preserves the
evidence and deterministic Latest behavior without silently losing data.

**Alternatives considered**:

- Hard reject future timestamps: rejected because it loses interpretable evidence.
- Clamp source time to processing time: rejected because it corrupts provenance.
- Use an unconfigurable constant: rejected because the business environment varies by deployment.

## Decision P-003: Total Latest ordering

**Decision**: For Latest-eligible Measurements of one Point, compare the tuple
`(source_timestamp, source_sequence, processing_timestamp, measurement_id)` in ascending order.
Only a strictly greater tuple may replace Point Latest. Missing source sequence is normalized to a
documented sentinel below any supplied sequence for that same source timestamp. The compare and
update occurs atomically in Telemetry.

**Rationale**: Source time expresses business recency; sequence resolves same-source-time batches;
processing time and immutable ID provide deterministic tie-breakers. Atomic compare-and-set prevents
late arrivals or concurrent retries from regressing Latest.

**Alternatives considered**:

- Processing time only: rejected because late arrival could overwrite a newer source reading.
- Source timestamp only: rejected because equal timestamps remain ambiguous.
- Last database write wins: rejected because concurrency makes the result nondeterministic.

## Decision P-004: Point and Simulator bootstrap

**Decision**: A Draft Point may receive an Active Simulator mapping after Metric/Unit compatibility
and hierarchy checks. The mapping is configuration-ready but produces no data until the Point is
Active. Point activation requires exactly one effective Active source mapping. Simulator Start also
rechecks that the Point and its ancestors are Active.

**Rationale**: The specification otherwise creates a circular dependency: activation requires a
mapping while the primary story configures a Simulator around an active Point.

**Alternatives considered**:

- Permit an active Point without a source: rejected because it violates readiness invariants.
- Permit generation for Draft Points: rejected because it bypasses hierarchy lifecycle control.

## Decision P-005: Accepted, rejected, and Bad

**Decision**: A parseable, attributable record that violates a value-range/data-quality rule is
accepted and persisted as `Bad`; it increments the accepted count but cannot advance Latest.
Malformed, unattributable, unauthorized, or irreconcilably invalid input is rejected and is not a
Measurement. Duplicate identity is an idempotent duplicate outcome, not a second acceptance.

**Rationale**: This makes batch accounting and evidence retention testable and keeps rejection
reserved for records that cannot safely enter the canonical model.

## Decision P-006: Source Health precedence

**Decision**: Administrative and terminal states take precedence:
`Decommissioned > Suspended > derived elapsed-time state`. Otherwise the evaluator uses the last
accepted observation and configured interval/thresholds to derive `Healthy`, `Late`, or `NoData`.
Repeated evaluation of the same evidence is idempotent.

**Rationale**: Administrative intent must not be overwritten by a timer, and elapsed-time states
must remain reproducible.

## Decision P-007: Deterministic Simulator state

**Decision**: Random generator state and the next source sequence are maintained per
`SimulatorRun + Point`. Constant output ignores random state; Normal output uses the stored seed and
state. A new Start creates a new Run and resets state; Resume continues the same Run state. Worker
leases prevent simultaneous production for one Run.

**Rationale**: Per-point state makes multi-point runs reproducible and avoids scheduling order
changing the generated series.

## Decision P-008: Conditional deletion and evidence

**Decision**: Draft, unused configuration may be physically deleted only when it has no operational
history or active dependency. An audit reference alone does not create an undeletable foreign-key
cycle; audit retains an immutable subject snapshot. Once operational history exists, records are
inactivated or decommissioned rather than deleted.

**Rationale**: This reconciles CRUD requirements with audit immutability and database referential
integrity.

## Decision P-009: Lifecycle semantics

**Decision**: `Inactive` records may be reactivated through the same readiness checks.
`Decommissioned` is terminal. Asset decommission is blocked while any child Point is Active.
Top-down activation and bottom-up deactivation/decommission checks are enforced by the owning
Organization module.

**Rationale**: A single lifecycle vocabulary avoids accidental resurrection and orphaned active
children.

## Decision P-010: Effective source mapping

**Decision**: A Point has at most one mapping effective at an instant. Mapping intervals are
half-open `[effective_from, effective_to)`. A future mapping can remain Draft; activation uses
transactional overlap protection. Replaced mappings remain historical as Inactive/Superseded rows.

**Rationale**: Effective dating provides traceability and a deterministic source for every
Measurement without destructive overwrite.

## Architectural Findings

- Preserve the modular monolith with API and Worker as composition roots.
- Each module owns its schema and writes; no module writes another module's tables.
- Consumer-shaped synchronous ports are defined in the consuming Application layer and composed by
  the hosts. Cross-module facts that need eventual propagation use outbox/inbox.
- Business contracts do not belong in `BuildingBlocks`; it remains technical infrastructure.
- The exact module ownership manifest is authoritative where DOC-05 uses shorthand schema names.
- PostgreSQL is required for transactional, migration, concurrency, and integration verification.
  An in-memory substitute would invalidate the architecture guarantees.

## Delivery and Test Findings

- R0 provides outbox, inbox, job/lease, structure tests, and a Fast/Full harness, but not the R1
  domain schemas.
- Ordered migrations must establish IAM, hierarchy/catalog, acquisition configuration, Telemetry,
  Latest/Health projections, and audit/evidence constraints before data producers are enabled.
- Pure domain and authorization-policy tests can run Fast. Repository, migration, concurrency,
  idempotency, Worker lease, API/Worker integration, and end-to-end scenarios require Full with
  PostgreSQL.
- R1 implementation is constitution-gated: the current constitution contains a historical rule
  limiting source implementation to feature 001/R0. Planning and task generation change no source
  behavior, but implementation must not begin until that text is amended or explicitly superseded.

## Resolved Ambiguities

There are no unresolved clarification items remaining for planning. The success-criteria count,
Point/mapping bootstrap, deletion/audit interaction, Bad-vs-rejected accounting, health precedence,
multi-point determinism, and effective-date overlap have explicit decisions above. Environment and
constitution approvals are execution blockers, not unanswered product questions.
