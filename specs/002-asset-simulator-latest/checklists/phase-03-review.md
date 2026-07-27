# Phase 3 Standards/Specification review (T076)

Scope is T056-T077 only. This review records every corrective-convergence
finding A-K from the Phase 3 handoff. T072/T073 remain package-policy blocked;
T074 remains transitively package-policy blocked. None is a database-access
blocker.

| ID | Severity | Evidence | Resolution | State |
|---|---|---|---|---|
| CORR-A / T061 | High | The earlier RED gate asserted source presence instead of business behavior. | Reproduced eight failing business assertions at parent SHA `fd2cf0d858fc8fce0041e1343b64d966d33d5d46` using only `Phase3BusinessRedEvidenceTests.cs` and a test-only shim. | Closed |
| CORR-B / FR-001..004 | High | Site, Area, Asset, and Point configuration updates were not all authorized command paths. | Added explicit update commands and aggregate `TryUpdate` methods; immutable Code/identity remains unchanged; no-op updates preserve version and emit no event. | Closed |
| CORR-C / concurrency | High | Lifecycle/status/decommission commands lacked a uniform expected-version contract. | Added `ExpectedVersion` to every update/status/decommission command; stale requests return `VERSION_CONFLICT` before mutation/history/event; current requests increment once. | Closed |
| CORR-D / parent lifecycle | High | Child creation did not reject non-configurable parents. | Create Area/Asset/Point now reject Inactive or Decommissioned parents with `PARENT_NOT_CONFIGURABLE`; tests cover all three levels. | Closed |
| CORR-E / T058 events | High | T058 did not execute all five event families or verify exact contracts. | `CompleteEventContractCoverage` executes Site, Area, Asset, Point configuration, and Point status paths and checks exact before/after key sets plus metadata. | Closed |
| CORR-F / trusted ancestry | High | Asset/Point events could report command-supplied ancestry. | Event snapshots derive SiteId/AreaId/AssetId from trusted repository aggregates; tests cover spoofed command ancestry and all event families. | Closed |
| CORR-G / running dependency | High | Optional Simulator query could fail open to `false`. | Handler constructor now requires non-null `IRunningSimulatorQuery`; unavailable state returns `DEPENDENCY_UNAVAILABLE` with no mutation/history/event; every eligible attempt invokes the dependency. | Closed |
| CORR-H / T071 | High | Provider-neutral runner did not cover the complete Organization contract surface. | Runner now covers scoped uniqueness/reservation, Site/Area lifecycle, Phase 5 Point activation guard, running dependency, stale version, commit/rollback/deep rollback, and query scope/order. It uses only public ports. | Closed |
| CORR-I / query scope | High | Area-scoped Site visibility used a fixed first-200 Area page. | Added trusted `GetAreaAncestryAsync` query port and removed the fixed 200-row lookup; unit test authorizes an Area at index 204. | Closed |
| CORR-J / T076 findings | Medium | Review evidence did not enumerate all corrective findings and states. | This artifact enumerates CORR-A through CORR-I plus evidence for the checkpoint and package-policy state. | Closed |
| CORR-K / T077 provenance | High | Checkpoint omitted exact parent/current provenance and command exits. | `phase-03-organization.md` records parent SHA, current `git rev-parse`, worktree, exact files, RED/GREEN exits, build/run and harness evidence, capability state, counts, and stop. | Closed |

## Standards findings

| ID | Severity | Evidence | Resolution | State |
|---|---|---|---|---|
| STD-001 | High | Organization ownership and IAM boundary. | IAM consumes Organization public contracts only; no cross-module internals or command-repository coupling. | Closed |
| STD-002 | High | Authorization and information disclosure ordering. | Authorization and trusted scope resolve before target details; out-of-scope details remain NotFound-equivalent. | Closed |
| STD-003 | High | Provider-neutral persistence verification. | T071 accepts a provider factory and public command/query ports; no fake casts, credentials, `Skip`, or TODO shortcuts. | Closed |
| STD-004 | Medium | Dependency and package restrictions. | No packages, Docker, SQLite, or migration execution were introduced; T072/T073/T074 classifications remain explicit. | Closed |

## Specification findings

| ID | Severity | Evidence | Resolution | State |
|---|---|---|---|---|
| SPEC-001 | High | FR-001..004 and lifecycle/version rules. | Update commands, immutable identity, interval/no-data validation, versioning, and no-op semantics are covered by domain and command tests. | Closed |
| SPEC-002 | High | FR-DC-001..005. | Asset/Point decommission policy remains non-cascading, blocks active children/running Simulator, and records one lifecycle history entry on success. | Closed |
| SPEC-003 | High | Event contracts and trusted scope. | Five exact event families, schema/producer/actor/correlation/causation/UTC metadata, exact snapshots, and trusted ancestry are asserted. | Closed |
| SPEC-004 | High | Scope/filter/paging acceptance. | Filtering occurs before paging/totals; deterministic order, child summaries, Site/Area scope, and >200 ancestry visibility are covered. | Closed |
| SPEC-005 | Medium | IAM fixture idempotency. | Existing public Organization query fixture remains idempotent and is included in RED/GREEN evidence. | Closed |

## Review result

| Severity | Unresolved findings |
|---|---:|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 0 |

## Micro-closure findings

| ID | Severity | Evidence | Resolution | State |
|---|---|---|---|---|
| CORR-L / active config | High | Active Point config update did not return PHASE5_REQUIRED; Decommissioned did not return INVALID_STATE. | Added status guard in `UpdatePointConfigurationCommand`: Active→PHASE5_REQUIRED, Decommissioned→INVALID_STATE; Draft/Inactive succeed. | Closed |
| CORR-M / inactivation history | High | Active→Inactive status transition did not append lifecycle history. | Added `PointLifecycleEntry` in `UpdatePointStatusCommand` for `inactivate` action; stale/no-op inactivation adds no history. | Closed |
| CORR-N / T071 gaps | Medium | Contract-runner did not cover Asset code uniqueness in same Area, Asset lifecycle, Point code reservation after decommission, or stale ExpectedVersion. | Added `AssetCodeDuplicateInSameAreaRejected`, `AssetLifecycleTransitionPersistence`, `PointCodeReservedAfterDecommission`, `StaleApplicationCommandVersion`; 19 tests, 39 assertions, 0 failures. | Closed |
| CORR-O / event aggregate metadata | Medium | Five-family event assertions did not verify AggregateType, AggregateId, AggregateVersion, or trusted AreaId per family. | Added exact AggregateType/Id/Version/AreaId assertions for each of the five event families in `CompleteEventContractCoverage`. | Closed |

T076 status: **PASS**. All corrective findings (A-O) are closed, and the three
package-policy classifications are carried forward without being relabeled as
database-access failures.
