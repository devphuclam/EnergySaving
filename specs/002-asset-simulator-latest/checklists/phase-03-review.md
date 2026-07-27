# Phase 3 Standards/Specification review

This review is split into independent Standards and Specification findings. The
review scope is T056–T075 and the corrected Phase 3 artifacts. T072/T073 remain
package-policy blocked and T074 is transitively package-policy blocked; none is
represented as a database-access blocker.

## Standards findings

| ID | Severity | Evidence | Resolution | State |
|---|---|---|---|---|
| STD-001 | High | Organization owns hierarchy domain, repository ports, query service, and migration; IAM adapter imports only `Organization.Contracts`. | Removed IAM Organization Domain/Application/Infrastructure and command-repository coupling. | Closed |
| STD-002 | High | `HierarchyCommands.cs` resolves trusted scope before target details and resolves caller username for events/history. | Reordered authorization and added caller snapshot resolution. | Closed |
| STD-003 | High | `CreateAsset`/`CreatePoint` construct IDs from trusted parent ancestry; fakes and migration enforce composite ancestry. | Added parent-scope ports, ancestry validation, and composite foreign keys. | Closed |
| STD-004 | High | T071 runner accepts `IOrganizationRepositoryTestProviderFactory` and has no fake casts. | Replaced concrete fake casts with provider-neutral command/query ports. | Closed |
| STD-005 | High | Point decommission evaluates `IRunningSimulatorQuery` on every attempt and appends explicit history once. | Added `DecommissionPolicy.cs`, running-state query, actual old status, and no fake auto-history. | Closed |
| STD-006 | Medium | Query service applies IAM scope and immutable DTOs; repository filters before paging/totals and orders by code plus ID. | Added query application service, scope DTOs, child summaries, and deterministic fake query adapter. | Closed |
| STD-007 | Medium | Migration status checks, positive versions, append-only trigger, UTC history, and indexes are static-reviewable. | Repaired `0004_organization_hierarchy.sql`; execution remains outside this invocation. | Closed |
| STD-008 | Medium | Architecture verification rejects cross-module internals, unsafe Point activation command paths, and fake casts. | Extended `tests/Verification/architecture.tests.ps1`. | Closed |

## Specification findings

| ID | Severity | Evidence | Resolution | State |
|---|---|---|---|---|
| SPEC-001 | High | FR-001..007, FR-AP-001/002; T056/T065. | Site/Area/Asset/Point aggregates retain lifecycle, code, interval, and optimistic-version rules. | Closed |
| SPEC-002 | High | FR-DC-001..005, SC-009; T057/T066. | Asset no-cascade policy blocks active children; Point policy blocks running Simulator and terminal retries. | Closed |
| SPEC-003 | High | FR-028..035, P-021; T058/T067. | Administrator/scoped Engineer authorization, five-role denial, trusted ancestry, actor username, exact owner events, and no-op silence are covered. | Closed |
| SPEC-004 | High | FR-029..034, SC-005; T059/T068. | Site/Area scope filtering, out-of-scope NotFound details, filter-before-paging/total counts, child summaries, and stable order are covered. | Closed |
| SPEC-005 | High | FR-IAM-006, P-019; T060/T069. | Real Post-Site IAM fixture uses public Site query, creates four scoped roles plus Manager `AUDIT_READ`, and is idempotent. | Closed |
| SPEC-006 | High | Persistence adapters contract, migration 0004; T062/T063/T070/T071. | Public command/query ports, provider-neutral contract runner, uniqueness/reservation, rollback, and ancestry/history invariants are present. | Closed |
| SPEC-007 | High | T061 RED gate. | Post-hoc RED was reproduced at the accepted Phase 2 SHA with build exit 0 and focused exit 1; no production fix was used in the temporary worktree. | Closed |
| SPEC-008 | Medium | T072/T073 package policy. | PostgreSQL adapter and host registration remain unchecked `BLOCKED_BY_PACKAGE_POLICY`; no packages were added. | Closed |
| SPEC-009 | Medium | T074 dependency graph and approved database capability. | Migration execution is unchecked `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE` because T072/T073 are unavailable; DB is available and 0004 was not executed. | Closed |
| SPEC-010 | High | SC-001/005/009 and T075. | Architecture and scope verification are green; no Phase 4 implementation was introduced. | Closed |

## Review result

| Severity | Unresolved findings |
|---|---:|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 0 |

T076 review status: **PASS**. The Phase 3 checkpoint may proceed to T077 with
the three explicit package-policy blockers recorded.
