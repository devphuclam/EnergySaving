# Phase 10 Functional-Requirement Traceability

Validation rule: this register contains exactly one row for each of the 68 canonical functional
requirements in `spec.md`. `PASS` means the named provider-neutral implementation and verification
evidence passed. Where execution needs the unregistered PostgreSQL adapter/runtime, the execution
capability column remains `BLOCKED`; source/compile evidence is never represented as PostgreSQL E2E.

| Requirement | Implementation task/file | Verification/evidence task/file | Execution capability | State |
|---|---|---|---|---|
| FR-001 | T052–T068; `Organization/Domain/Hierarchy.cs` | T224; hierarchy/unit/API suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-002 | T052–T068; `Organization/Domain/Hierarchy.cs` | T224; hierarchy/unit/API suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-003 | T052–T068; `Organization/Domain/Hierarchy.cs` | T225; hierarchy/decommission suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-004 | T052–T068; `Organization/Domain/Hierarchy.cs` | T225; hierarchy/activation suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-005 | T094–T103; `ActivateMeasurementPoint.cs` | Point activation transaction suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-006 | T053,T060,T225; lifecycle policies | `LifecycleAcceptanceTests.cs` | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-007 | T054–T068; hierarchy repositories/contracts | hierarchy repository contract suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-AP-001 | T052–T068; hierarchy commands | hierarchy command suites | Provider-neutral PASS | PASS |
| FR-AP-002 | T052–T068; activation policy | hierarchy/activation suites | Provider-neutral PASS | PASS |
| FR-AP-003 | T075–T103; readiness contracts | mapping/point activation suites | Provider-neutral PASS | PASS |
| FR-AP-004 | T075–T103; compatibility/readiness | mapping/point activation suites | Provider-neutral PASS | PASS |
| FR-AP-005 | T094–T103; activation outcomes | point activation transaction suites | Provider-neutral PASS | PASS |
| FR-008 | T075–T093; configuration aggregate | configuration command/repository suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-009 | T108–T130; run commands | run-control and endpoint suites | Provider-neutral PASS; registered runtime BLOCKED | PASS |
| FR-010 | T110–T130; durable run/worker contracts | run recovery/worker suites | Provider-neutral PASS; registered runtime BLOCKED | PASS |
| FR-011 | T108; deterministic generator | deterministic vector suite | Provider-neutral PASS | PASS |
| FR-012 | T110–T130; production gating | run-control/dispatch suites | Provider-neutral PASS | PASS |
| FR-013 | T110–T130; run counters/status | run-control/attempt suites | Provider-neutral PASS | PASS |
| FR-014 | T075–T093; mapping aggregate | source/mapping suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-015 | T075–T103; half-open mapping policy | T226 source plus mapping suites | Source/compile PASS; PostgreSQL race E2E BLOCKED | PASS |
| FR-016 | T094–T130; start/readiness policy | T226 source plus run-control suites | Source/compile PASS; registered runtime BLOCKED | PASS |
| FR-017 | T131–T151; Telemetry contracts | ingestion persistence suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-018 | T131–T151; identity registry | T227 source plus idempotency suites | Source/compile PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-019 | T131–T151; ingestion orchestration | Telemetry orchestration suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-020 | T131–T151; quality classification | Telemetry/Latest suites | Provider-neutral PASS | PASS |
| FR-021 | T131–T151; timestamp policy | Telemetry ingestion suites | Provider-neutral PASS | PASS |
| FR-022 | T152–T169; `PointLatestService.cs` | T228 source plus Latest suites | Source/compile PASS; PostgreSQL race E2E BLOCKED | PASS |
| FR-023 | T152–T169,T180,T215; Latest query/UI | endpoint/unit and Web build evidence | Source/build PASS; live runtime BLOCKED | PASS |
| FR-024 | T153–T169; `SourceHealthService.cs` | T228 source plus health suites | Source/compile PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-025 | T153; health threshold policy | Source Health suite | Provider-neutral PASS | PASS |
| FR-026 | T153,T180,T215; No Data semantics | Latest/Health endpoint and Web source/build | Source/build PASS; live runtime BLOCKED | PASS |
| FR-027 | T153–T169,T180,T215; health projection | health/endpoint and Web build evidence | Source/build PASS; live runtime BLOCKED | PASS |
| FR-028 | T023,T033,T178–T181 | T224 authorization-negative suite | Provider-neutral PASS | PASS |
| FR-029 | T023,T176,T178–T181 | T224 scope-before-page/lookup evidence | Provider-neutral PASS | PASS |
| FR-030 | T023,T178–T181 | T224 Administrator global case | Provider-neutral PASS | PASS |
| FR-031 | T023,T178,T212–T213 | T224 Engineer scope cases; Web build | Source/build PASS; live runtime BLOCKED | PASS |
| FR-032 | T023,T180,T212 | authorization/query suites | Provider-neutral PASS | PASS |
| FR-033 | T023,T178–T181,T212 | T224 Manager scoped/read-only case | Provider-neutral PASS | PASS |
| FR-034 | T023,T178–T181,T212 | T224 Viewer denial/read case | Provider-neutral PASS | PASS |
| FR-DC-001 | T053,T060; decommission policy | T225 active-child/no-cascade case | Provider-neutral PASS | PASS |
| FR-DC-002 | T060; Point decommission handler | T225 running dependency case | Provider-neutral PASS | PASS |
| FR-DC-003 | T053,T060; terminal lifecycle | T225 terminal/superseded cases | Provider-neutral PASS | PASS |
| FR-DC-004 | T060; atomic command handler | T225 safe conflict/no-child-mutation cases | Provider-neutral PASS | PASS |
| FR-DC-005 | T060,T175; owner event/Audit | lifecycle and Audit consumer suites | Provider-neutral PASS; live delivery BLOCKED | PASS |
| FR-035 | T170–T181; owner events/Audit | Audit consumer/query/endpoint suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-036 | T170–T181; mapping events/Audit | Audit delivery suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-037 | T170–T181; Simulator events/Audit | T229 source plus Audit delivery suites | Source/compile PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-038 | T170–T181; `AuditContracts.cs` | T229 and Audit consumer suites | Provider-neutral PASS | PASS |
| FR-039 | T175,T191; append-only Audit | Audit consumer/repository and migration review | Source/compile PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-IAM-001 | T013–T037; IAM model/session | IAM/session/auth suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-IAM-002 | T013–T037; role model | IAM domain/repository suites | Provider-neutral PASS | PASS |
| FR-IAM-003 | T013–T037; scope model | IAM authorization/repository suites | Provider-neutral PASS | PASS |
| FR-IAM-004 | T023,T033,T178–T181 | T224 server-principal/header cases | Provider-neutral PASS | PASS |
| FR-IAM-005 | T023,T178–T181 | T224 401/403/404/scope cases | Provider-neutral PASS | PASS |
| FR-IAM-006 | T026,T231; deterministic bootstrap source | fixture tests and migration static review | Source/compile PASS; PostgreSQL seed execution BLOCKED | PASS |
| FR-IAM-007 | T175–T181; authorization Audit | Audit delivery/query suites | Provider-neutral PASS; live delivery BLOCKED | PASS |
| FR-IAM-008 | T001–T012 governance boundary | architecture/repository-policy checks | Runnable policy PASS | PASS |
| FR-DS-001 | T040–T051; Source lifecycle | T225 suspend/decommission suite | Provider-neutral PASS | PASS |
| FR-DS-002 | T040–T051; Mapping lifecycle | T225 superseded terminal suite | Provider-neutral PASS | PASS |
| FR-DS-003 | T040–T051; deletion policy | T225 protected/audit-only delete cases | Provider-neutral PASS | PASS |
| FR-DS-004 | T040–T051; dependency result | T225 safe `DEPENDENT_HISTORY` case | Provider-neutral PASS | PASS |
| FR-CAT-001 | T038–T051; Metric aggregate | Catalog unit/repository suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-CAT-002 | T038–T051; Unit aggregate | Catalog unit/repository suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-CAT-003 | T038–T051; compatibility contracts | Catalog readiness suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-CAT-004 | T047,T231; deterministic catalog seeds | Catalog seed/unit plus migration static review | Source/compile PASS; PostgreSQL seed execution BLOCKED | PASS |
| FR-DO-001 | T052–T103; Point owner identity | hierarchy/activation suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-DO-002 | T022,T094–T103; owner eligibility | IAM/activation suites | Provider-neutral PASS; PostgreSQL E2E BLOCKED | PASS |
| FR-DO-003 | T023; explicit authorization roles/capabilities | IAM and T224 authorization suites | Provider-neutral PASS | PASS |

## Validation summary

- Unique canonical FR mappings: **68/68**
- Duplicate FR mappings: **0**
- Missing FR mappings: **0**
- Malformed rows: **0**
- PostgreSQL execution evidence claimed: **NO**
- T235 timed runtime evidence: **BLOCKED / NOT_EXECUTED**
