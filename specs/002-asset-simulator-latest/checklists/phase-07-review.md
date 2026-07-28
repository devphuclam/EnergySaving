# Phase 7 Standards and Specification Review

Baseline: `fdc56735dbd6c9c44599fdf498b010bab151f11e`  
Review target: uncommitted Phase 7 diff, T131-T149  
Final review date: 2026-07-28

## Standards findings

| ID | Severity | Source / test evidence | Resolution | State |
|---|---|---|---|---|
| STD-07-01 | High | Unique `(Run,Point,sequence)` race could reload only the losing Measurement ID | Recovery now reloads identity first, then the immutable slot winner and returns `MEASUREMENT_SLOT_CONFLICT`; T133/T145 cover it | Resolved |
| STD-07-02 | High | Fingerprint encoded timestamp ticks but not `DateTime.Kind` although non-UTC is rejected | V1 encoding includes the typed Kind value; T131 proves the distinction | Resolved |
| STD-07-03 | Medium | Provider-neutral fake/runner did not reject slot collisions | Fake and T145 now enforce and execute slot uniqueness and winner reload | Resolved |
| STD-07-04 | Medium | `ISourceHealthRepository` returned `object` | Replaced with the typed future `SourceHealthSnapshot` contract; no Phase 8 policy implemented | Resolved |

Standards reviewed: Telemetry ownership; exact UUIDv5 identity; distinct idempotency mechanisms;
terminal-only immutable registry; exact Duplicate replay; trusted-producer boundary; sequential
validation; Organization -> Catalog -> Telemetry -> Integration lock order; one flow transaction;
Accepted-only raw; stable Rejected; safe event allowlist; Acquisition finalization; no
package/database bypass; no Phase 8 leakage.

Final Standards result: unresolved Critical `0`; unresolved High `0`; actionable Medium `0`.
Low judgment-only notes are the required provider snapshot data clump and string-coded stable
contract codes; neither is a completion blocker.

## Specification findings

| ID | Severity | Source / test evidence | Resolution | State |
|---|---|---|---|---|
| SPEC-07-01 | High | Accepted mutation staged Latest before registry/raw, contrary to attachment §21 | Latest evaluation is read-only; mutation order is terminal -> raw -> Latest -> Integration event | Resolved |
| SPEC-07-02 | High | Fake/T145 lacked Accepted-only raw, registry/raw consistency, and race coverage | Fake validates terminal/raw shape; T145 executes 20 provider-neutral scenarios including matching/conflicting winner and rollback | Resolved |
| SPEC-07-03 | High | Provider validation omitted Source/Point identity and Metric mismatch | Snapshot and T132 now cover Source/Point mismatch, Metric missing/mismatch/inactive, Unit missing/mismatch/inactive/incompatible | Resolved |
| SPEC-07-04 | High | UUIDv5 sequence/version decimal formatting was culture-sensitive | Canonical name uses explicit invariant decimal formatting | Resolved |
| SPEC-07-05 | High | Acquisition finalizer accepted an already-converted Phase 6 result | Client now returns canonical disposition + exact original result; T143 converts and preserves metadata before one finalization | Resolved |
| SPEC-07-06 | Medium | T131 did not explicitly freeze retry metadata exclusion | T131 proves fingerprint accepts immutable request only and retry/lease/trace metadata is outside it | Resolved |
| SPEC-07-07 | Medium | T135 AggregateId assertion could pass for any nonempty ID | Assertion now requires exact Measurement ID | Resolved |
| SPEC-07-08 | High | Migration deferred trigger counted raw rows but did not compare Accepted provenance | Trigger now compares persisted ID, Source, Run, Point, Mapping/version, sequence, quality/reason and correlation/lineage | Resolved |

Specification coverage reviewed: FR-017, FR-018, FR-019, FR-020, FR-021; P-001, P-002,
P-013, P-015, P-015A, P-016, P-021; Telemetry contract; Simulator finalization contract; and
T131-T149. No unrequested Phase 8/API/runtime/PostgreSQL-adapter behavior was found.

Final Specification result: unresolved Critical `0`; unresolved High `0`; scope creep `0`.

## T150 decision

`PASS` — both review axes have zero unresolved Critical and High findings.
