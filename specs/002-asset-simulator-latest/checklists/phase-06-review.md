# Phase 6 Standards and Specification Review (T129)

Repository: `devphuclam/EnergySaving`

Parent baseline: `05cb231066655bd5259e4dc2a478b8dc44c52c05`

Review surface: tracked diff from the parent baseline plus every Phase 6 untracked file. Review was
read-only and split into independent Standards and Spec axes. The Phase 6 checkpoint follows this
review; T131 and later were outside scope.

## Standards

| ID | Severity | Finding | Resolution | State |
|---|---|---|---|---|
| STD-01 | High | Worker consumed Acquisition repositories/UoW and performed business writes across the module boundary. | Added the narrow `ISimulatorProductionCoordinator` application contract. Worker now delegates through that contract; Acquisition owns Run, lease, attempt, counter, status and event writes. | CLOSED |
| STD-02 | High | Point failures were swallowed into a count without structured observability or correlation detail. | Worker now emits structured lifecycle/error logs. Coordinator returns classified per-Point failures with Run, Point and correlation identity; unhandled cycle failures are logged and rethrown. | CLOSED |
| STD-03 | High | `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE` is absent from the constitution/harness classification list. | Finding withdrawn for this implementation: the value exists in the fixed baseline for T052/T074/T090/T104, Phase 5 accepted it for T104, and the authoritative Phase 6 directive explicitly requires it for T127. Governance taxonomy synchronization is separate, unauthorized work. | WITHDRAWN |
| STD-04 | Medium | Large positional records may be a Data Clump/Primitive Obsession smell. | Judgement-call finding closed without change: the records are explicit provider-neutral persistence/event contracts and splitting them in this narrow phase would add mapping surface without changing invariants. | CLOSED |
| STD-05 | Low | Transaction begin/commit/rollback shapes are repeated across distinct mutations. | Judgement-call finding closed without extraction: Start, lifecycle, reserve, finalize and owner-drift Stop deliberately expose different atomic boundaries and lock plans. | CLOSED |

Standards result: **PASS** — unresolved Critical `0`, High `0`, Medium `0`, Low `0`.

## Spec

| ID | Severity | Finding | Resolution | State |
|---|---|---|---|---|
| SPEC-01 | High | Owner eligibility ran before existing-Pending lookup and could strand an immutable retry payload. | Coordinator now claims, loads Pending first, and bypasses owner eligibility for the retry path. T111 proves an existing Pending dispatches unchanged even when owner state is inactive. | CLOSED |
| SPEC-02 | High | Start locked Point only and recheck was not transaction-aware. | Start now locks Site, Area, Asset and Point, then Catalog and Acquisition in deterministic order; provider recheck receives the active transaction. T110 asserts the lock trace. | CLOSED |
| SPEC-03 | High | A fixed 30-second lease could expire during slow Telemetry dispatch. | `RenewLeaseAsync` returns the refreshed versioned lease. Coordinator maintains a heartbeat, classifies `LEASE_LOST`, and releases the latest handle with `CancellationToken.None`. T111 proves delayed dispatch blocks a competing reclaim. | CLOSED |
| SPEC-04 | Medium | Owner-drift Stop did not stage `SimulatorRunStateChanged.v1`. | Acquisition now stages the safe Stop event in the same transaction as status/error mutation. | CLOSED |
| SPEC-05 | Medium | T108 omitted unknown algorithm-ID and rounding/clamp boundary coverage. | Added independent literal cached-spare tests for ties-to-even and round-then-clamp, plus unknown algorithm ID/version rejection. | CLOSED |

Spec result: **PASS** — unresolved Critical `0`, High `0`, Medium `0`, Low `0`; scope creep
findings `0`.

## Two-axis summary

- Standards findings: 5 reviewed; worst original severity High; all closed or withdrawn.
- Spec findings: 5 reviewed; worst original severity High; all closed.
- T129 gate: **PASS** because unresolved Critical and High findings are both zero.
