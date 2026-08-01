# Tasks: Operational Configuration Workspace

**Input**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

**Tests**: Required. New backend/HTTP/PostgreSQL behavior follows red → green at the public seams
defined in `plan.md`. The frontend behavior runner remains package-policy blocked unless an already
approved runnable dependency exists.

**Execution rule**: Phase 0 and Phase 1 are historical checkpoints. This corrective invocation
reopens only the Phase 2 tasks listed below, records incomplete evidence honestly, commits the
corrective branch, and stops before T049. Do not begin Phase 3 in the same implementation run.

## Checklist format

Every task uses `- [ ] T### [P?] [Story?] [classification] description with exact file path`.

## Phase 0: Specification, contracts, UX state model, gap analysis, and implementation gate

**Goal**: Produce canonical implementation-ready artifacts and red-test definitions without green
application changes.

- [x] T001 [RUNNABLE_NOW] Register Feature 003 purpose, exclusions, source precedence, and authoritative baseline in `specs/003-operational-configuration-workspace/spec.md`
- [x] T002 [RUNNABLE_NOW] Complete the formal clarification coverage scan and encode all resolved role, scope, persistence, failure, localization, and stop-gate decisions in `specs/003-operational-configuration-workspace/spec.md`
- [x] T003 [P] [RUNNABLE_NOW] Record existing endpoint reuse, missing operational status, missing production Engineer assignment, and actual legal activation order in `specs/003-operational-configuration-workspace/research.md`
- [x] T004 [P] [RUNNABLE_NOW] Define derived workspace status, eight-step progress, complete-chain validation, and persistence rationale in `specs/003-operational-configuration-workspace/data-model.md`
- [x] T005 [P] [RUNNABLE_NOW] Define status, Engineer listing/assignment, chain validation, owner-command reuse, idempotency, and safe error contracts in `specs/003-operational-configuration-workspace/contracts/operational-workspace.md`
- [x] T006 [P] [RUNNABLE_NOW] Define landing, wizard, feedback, conflict, partial activation, navigation, accessibility, and frontend evidence states in `specs/003-operational-configuration-workspace/contracts/ui-state-model.md`
- [x] T007 [RUNNABLE_NOW] Complete architecture/module ownership, deep interface seams, TDD seams, six implementation phases, and readiness gates in `specs/003-operational-configuration-workspace/plan.md`
- [x] T008 [P] [RUNNABLE_NOW] Define runnable Phase 1 acceptance and negative journeys without secrets or prohibited dependencies in `specs/003-operational-configuration-workspace/quickstart.md`
- [x] T009 [RUNNABLE_NOW] Run the first read-only `/speckit.analyze` and record every Critical, High, and Medium actionable finding in `specs/003-operational-configuration-workspace/checklists/phase-00-governance.md`
- [x] T010 [RUNNABLE_NOW] Resolve all Critical/High/Medium findings, rerun `/speckit.analyze` to PASS, evaluate constitution impact, and finalize Implementation-ready evidence in `specs/003-operational-configuration-workspace/checklists/phase-00-governance.md`

**Checkpoint**: Analysis PASS; Constitution 1.1.0 impact evaluated; Implementation-ready YES; no
green application code has bypassed Phase 0.

---

## Phase 1: Role-aware Setup Wizard vertical slice and state-aware landing

**User Story**: US1 — Complete and Resume Initial Setup

**Goal**: Deliver a visible PostgreSQL-backed Administrator handoff and Engineer continuation
through one complete chain, with persisted resume, legal activation, navigation to Simulator, and
no automatic Start.

**Independent Test**: On the approved PostgreSQL target, an Administrator creates/activates a Site
and assigns an existing Engineer; the Engineer resumes, creates the remaining chain, refreshes/
restarts without loss, validates and activates legally, reaches Simulator, and zero Run exists.

### Red evidence

- [x] T011 [P] [US1] [RUNNABLE_NOW] Add failing IAM application tests for active-Engineer listing, Administrator-only idempotent Site-scope assignment, invalid role/status, and duplicate prevention in `tests/Unit/IAM/EngineerScopeAssignmentTests.cs`, then register the Phase 1 Unit suites in `tests/Unit/Program.cs`
- [x] T012 [P] [US1] [RUNNABLE_NOW] Add failing operational-status tests for all five landing states, eight-step derivation, scope-before-counts, dependency error, validation grouping, and zero implicit Start in `tests/Unit/OperationalWorkspace/OperationalWorkspaceStatusTests.cs`
- [x] T013 [P] [US1] [RUNNABLE_NOW] Add failing HTTP contract tests for status, Engineer list/assignment, chain validation, safe 401/403/404/409/422/503, idempotency, ETag/version, and antiforgery metadata in `tests/Unit/Api/OperationalWorkspaceEndpointTests.cs`
- [x] T014 [P] [US1] [RUNNABLE_NOW] Add failing PostgreSQL vertical-journey tests for Administrator handoff, Engineer continuation, persisted resume, ordered activation, partial retry, and zero Simulator Run in `tests/Integration/OperationalWorkspace/OperationalSetupJourneyTests.cs`, then register the suite in `tests/Integration/Program.cs`
- [x] T015 [US1] [RUNNABLE_NOW] Run the exact new Unit and PostgreSQL Integration seams before green and record numeric non-zero red evidence without exposing secrets in `specs/003-operational-configuration-workspace/checklists/phase-01-red.md`

### Minimal green backend and HTTP

- [x] T016 [P] [US1] [RUNNABLE_NOW] Define typed operational landing, step, validation, Engineer candidate, assignment, and dependency outcomes in `src/Hosting/Abstractions/OperationalWorkspacePorts.cs`
- [x] T017 [P] [US1] [RUNNABLE_NOW] Define the IAM-owned Engineer query/assignment interface and safe result codes in `src/Modules/IAM/Contracts/OperationalWorkspaceIamContracts.cs`
- [x] T018 [US1] [RUNNABLE_NOW] Implement active Engineer discovery and Administrator-authorized duplicate-safe Site scope assignment through `IIamCommandRepository` in `src/Modules/IAM/Application/EngineerScopeAssignment.cs`
- [x] T019 [US1] [RUNNABLE_NOW] Add forward migration `0014_operational_workspace_scope.sql` for scoped pre-Mapping Source resume and atomic root Site-scope uniqueness; extend owner persistence without cross-scope fallback
- [x] T020 [US1] [RUNNABLE_NOW] Implement the deep PostgreSQL operational workspace query/command adapter over public IAM, Organization, Catalog, Acquisition, Integration, and Audit contracts in `src/Composition/Postgres/PostgresOperationalWorkspacePorts.cs`
- [x] T021 [US1] [RUNNABLE_NOW] Register operational workspace ports without cross-module logic in `src/Composition/Postgres/PostgresModuleRegistration.cs`
- [x] T022 [US1] [RUNNABLE_NOW] Expose typed status, Engineer list/assignment, and read-only chain validation with server principal, idempotency, antiforgery, transaction, and safe errors in `src/Api/OperationalWorkspaceEndpoints.cs`
- [x] T023 [US1] [RUNNABLE_NOW] Register Operational Workspace endpoints without business logic in `src/Api/Program.cs`

### Minimal green Web vertical slice

- [x] T024 [P] [US1] [RUNNABLE_NOW] Add typed operational workspace state, step IDs, form inputs, command outcomes, and error mapping in `src/Web/src/features/setup/setupTypes.ts`
- [x] T025 [US1] [RUNNABLE_NOW] Add the focused workspace gateway for status, Engineer assignment, owner mutations, complete validation, retry-key reuse, and ETag handling in `src/Web/src/features/setup/setupGateway.ts`
- [x] T026 [US1] [RUNNABLE_NOW] Extend gateway composition with the operational workspace interface while preserving existing auth/session behavior in `src/Web/src/gateways/webGateways.ts`
- [x] T027 [US1] [RUNNABLE_NOW] Implement state-aware post-login routing for Wizard, Continue Setup, Dashboard, No Authorized Scope, and Dependency Error in `src/Web/src/app/AppShell.tsx` and integrate the route content in `src/Web/src/App.tsx`
- [x] T028 [P] [US1] [RUNNABLE_NOW] Implement the responsive eight-step progress, summary, validation, Back, Save/Continue, Cancel, loading, submitting, conflict, forbidden, and error shell in `src/Web/src/features/setup/SetupWizard.tsx`
- [x] T029 [US1] [RUNNABLE_NOW] Implement Administrator Site creation/activation and existing Engineer selection/scope handoff, plus read-only assigned Site for Engineers, in `src/Web/src/features/setup/SetupWizard.tsx`
- [x] T030 [US1] [RUNNABLE_NOW] Implement real Area, Asset, Measurement Point, Data Source, Mapping, and Simulator Configuration step forms using authorized persisted selections in `src/Web/src/features/setup/SetupWizard.tsx`
- [x] T031 [US1] [RUNNABLE_NOW] Implement complete-chain validation, legal ordered activation with stop-on-failure/status reload, navigation to Simulator, and an invariant that no Start request occurs in `src/Web/src/features/setup/SetupWizard.tsx`
- [x] T032 [US1] [RUNNABLE_NOW] Add Vietnamese Industrial Light wizard, compact tablet step list, visible focus, badges with text, feedback, and responsive layouts in `src/Web/src/App.css`

### Phase 1 verification, review, and checkpoint

- [x] T033 [US1] [RUNNABLE_NOW] Run new Unit and PostgreSQL Integration seams to green, a manual browser acceptance journey for Administrator handoff/Engineer continuation/persisted resume/no auto-start, existing Fast mode, Web lint/build, and runtime HTTP checks; record exact commands and numeric exit codes in `specs/003-operational-configuration-workspace/checklists/phase-01-verification.md`
- [ ] T034 [US1] [BLOCKED_BY_PACKAGE_POLICY] Execute approved frontend behavior tests for role-aware landing, persisted wizard resume, conflict focus, and no auto-start if a runner is already available; otherwise record the exact separate package-policy blocker in `specs/003-operational-configuration-workspace/checklists/phase-01-verification.md`
- [x] T035 [US1] [RUNNABLE_NOW] Perform separate Standards and Specification reviews against corrective baseline `a08e28eb0e2299d12403af37f275cb9d862421a9`, resolve all Critical/High findings, and record both axes in `specs/003-operational-configuration-workspace/checklists/phase-01-review.md`
- [x] T036 [US1] [RUNNABLE_NOW] Run architecture/repository-policy checks and a fresh Full harness, report company blockers truthfully, update completed task boxes, and create the explicit stop checkpoint in `specs/003-operational-configuration-workspace/checklists/phase-01-checkpoint.md`

**Checkpoint**: Phase 1 functionality visible and usable against PostgreSQL; Administrator handoff,
Engineer continuation, ordered activation, and persisted resume evidenced; Simulator auto-started
NO; explicit stop before T037.

---

## Phase 2: Configuration management, duplication, and version-safe editing

**User Story**: US2 — Manage Configuration Safely

- [x] T037 [P] [US2] [RUNNABLE_NOW] Add failing duplicate-to-Draft and exclusion tests for all eligible entity types in `tests/Unit/OperationalWorkspace/ConfigurationDuplicationTests.cs`
- [x] T038 [P] [US2] [RUNNABLE_NOW] **Corrective closure complete**: PostgreSQL scoped search/filter/paging, explicit target-Source Simulator duplication, activation/mutation malformed-JSON handling, public command-seam lifecycle coverage, hosted HTTP matrix, and the authenticated browser journey are green in `tests/Integration/OperationalWorkspace/ConfigurationManagementTests.cs`
- [x] T039 [US2] [RUNNABLE_NOW] **Corrective closure**: Define typed paged management and create/edit/validate/lifecycle/delete command contracts in `src/Hosting/Abstractions/ConfigurationManagementPorts.cs`
- [x] T040 [US2] [RUNNABLE_NOW] **Corrective closure**: Implement scope-before-paging management queries, including Simulator ID/source/version search before paging, through owner contracts in `src/Composition/Postgres/PostgresConfigurationManagementPorts.cs`
- [x] T041 [US2] [RUNNABLE_NOW] Implement owner-domain duplicate-to-Draft behavior without history/secrets in `src/Modules/Organization/Application/ConfigurationDuplication.cs`
- [x] T042 [US2] [RUNNABLE_NOW] Implement Source/Mapping duplicate-to-Draft and dependency-safe lifecycle behavior in `src/Modules/Catalog/Application/ConfigurationDuplication.cs`
- [x] T043 [US2] [RUNNABLE_NOW] **Corrective closure complete**: Persisted Acquisition receipts and activation authority remain green; server-derived management detail/list state, stale receipt flags, and invalidation coverage after the hosted edit path are verified in `src/Modules/Acquisition/Application/SimulatorConfiguration.cs`, `src/Modules/Acquisition/Infrastructure/PostgresConfigurationRepository.cs`, `src/Composition/Postgres/PostgresApplicationPorts.cs`, and management UI
- [x] T044 [US2] [RUNNABLE_NOW] **Corrective closure reevaluated — no regression**: Management paging, details, create, version-safe edit, validate, legal lifecycle, duplicate, and safe Draft deletion endpoints remain exposed in `src/Api/ConfigurationManagementEndpoints.cs`; detail responses now include authorized Simulator Draft review metadata
- [x] T045 [P] [US2] [RUNNABLE_NOW] **Corrective closure**: Build reusable Vietnamese table/filter/pagination/detail/editor/action/feedback primitives with loading, validation, conflict, forbidden, not-found, dependency, and runtime states in `src/Web/src/features/configuration/ConfigurationManagementComponents.tsx`
- [x] T046 [US2] [RUNNABLE_NOW] **Corrective closure complete**: Implement server-resumable Simulator relationship review, selector loading/empty/forbidden/dependency/runtime states with Retry, exact field validation messages, and first-invalid focus across the Sites, Areas, Assets, Points, Sources, Mappings, and Simulator Configuration pages in `src/Web/src/features/configuration/ConfigurationManagementRoutes.tsx`
- [x] T047 [US2] [RUNNABLE_NOW] Remove or replace decorative Open Hierarchy and Review Mapping actions in `src/Web/src/features/configuration/ConfigurationRoutes.tsx`
- [x] T048 [US2] [RUNNABLE_NOW] **Corrective closure complete**: Verification and review artifacts record receipt, selector, malformed-JSON, hosted health, authenticated hosted HTTP lifecycle/replay/receipt/Audit-outbox evidence, fresh harness evidence, and the completed authenticated browser journey in `specs/003-operational-configuration-workspace/checklists/phase-02-checkpoint.md`

---

## Phase 3: Explicit Simulator selection, controls, and Run history

**User Story**: US3 — Operate an Explicitly Selected Simulator

- [ ] T049 [P] [US3] [RUNNABLE_NOW] Add failing selected-Source/configuration and no-first-Source tests in `tests/Unit/Api/SimulatorSelectionTests.cs`
- [ ] T050 [P] [US3] [RUNNABLE_NOW] Add failing PostgreSQL control/history/idempotency/conflict tests in `tests/Integration/OperationalWorkspace/SimulatorOperationsTests.cs`
- [ ] T051 [US3] [RUNNABLE_NOW] Add authorized Source/configuration eligibility and recent Run history query contracts in `src/Hosting/Abstractions/SimulatorWorkspacePorts.cs`
- [ ] T052 [US3] [RUNNABLE_NOW] Implement PostgreSQL selected Simulator workspace queries in `src/Composition/Postgres/PostgresSimulatorWorkspacePorts.cs`
- [ ] T053 [US3] [RUNNABLE_NOW] Extend Simulator endpoints with selected configuration eligibility and paged Run history in `src/Api/SimulatorEndpoints.cs`
- [ ] T054 [US3] [RUNNABLE_NOW] Replace implicit Source lookup with explicit Site/Area/Asset/Source/configuration selection and Run controls in `src/Web/src/features/simulator/SimulatorRoute.tsx`
- [ ] T055 [US3] [RUNNABLE_NOW] Add Run ID/version/status/counters/last production/interval/history and complete feedback states in `src/Web/src/features/simulator/SimulatorRoute.tsx`
- [ ] T056 [US3] [RUNNABLE_NOW] Verify US3 behavior, review, Fast/Full evidence, and checkpoint in `specs/003-operational-configuration-workspace/checklists/phase-03-checkpoint.md`

---

## Phase 4: Explicit Latest/Health selection and refresh

**User Story**: US4 — Observe Selected Latest and Source Health

- [ ] T057 [P] [US4] [RUNNABLE_NOW] Add failing selected-Point/no-first-Point/No-Data-not-zero tests in `tests/Unit/Api/LatestSelectionTests.cs`
- [ ] T058 [P] [US4] [RUNNABLE_NOW] Add failing PostgreSQL Latest/Health/counter scope tests in `tests/Integration/OperationalWorkspace/LatestHealthTests.cs`
- [ ] T059 [US4] [RUNNABLE_NOW] Add authorized hierarchy selector query contract with scope-before-paging in `src/Hosting/Abstractions/TelemetryWorkspacePorts.cs`
- [ ] T060 [US4] [RUNNABLE_NOW] Implement PostgreSQL Point selector and selected Latest/Health query adapter in `src/Composition/Postgres/PostgresTelemetryWorkspacePorts.cs`
- [ ] T061 [US4] [RUNNABLE_NOW] Expose authorized selector endpoints while preserving safe Point queries in `src/Api/TelemetryQueryEndpoints.cs`
- [ ] T062 [US4] [RUNNABLE_NOW] Replace implicit Point lookup with explicit Site/Area/Asset/Point selection in `src/Web/src/features/telemetry/PointCurrentRoute.tsx`
- [ ] T063 [US4] [RUNNABLE_NOW] Add ten-second default auto refresh, disable/manual refresh, timestamps, quality, Health, Run/counters, and explicit No Data in `src/Web/src/features/telemetry/PointCurrentRoute.tsx`
- [ ] T064 [US4] [RUNNABLE_NOW] Verify US4 behavior, review, Fast/Full evidence, and checkpoint in `specs/003-operational-configuration-workspace/checklists/phase-04-checkpoint.md`

---

## Phase 5: Audit usability and Operational Dashboard

**User Story**: US5 — Navigate Operational State and Review Audit

- [ ] T065 [P] [US5] [RUNNABLE_NOW] Add failing Audit filter/paging/redaction and authorized Dashboard summary tests in `tests/Unit/OperationalWorkspace/AuditDashboardTests.cs`
- [ ] T066 [P] [US5] [RUNNABLE_NOW] Add failing PostgreSQL scope-before-paging Audit/Dashboard tests in `tests/Integration/OperationalWorkspace/AuditDashboardTests.cs`
- [ ] T067 [US5] [RUNNABLE_NOW] Extend Audit query contract for date range, entity ID, Site/Area, safe diff, and keyset paging in `src/Modules/Audit/Contracts/AuditContracts.cs`
- [ ] T068 [US5] [RUNNABLE_NOW] Implement remaining Audit filters/redaction and Dashboard composition in `src/Composition/Postgres/PostgresOperationalDashboardPorts.cs`
- [ ] T069 [US5] [RUNNABLE_NOW] Expose typed Operational Dashboard and complete Audit query contracts in `src/Api/OperationalDashboardEndpoints.cs`
- [ ] T070 [US5] [RUNNABLE_NOW] Implement authorized Vietnamese Operational Dashboard navigation in `src/Web/src/features/dashboard/OperationalDashboard.tsx`
- [ ] T071 [US5] [RUNNABLE_NOW] Implement Audit date/actor/action/entity/scope filters, pagination, safe diff, and correlation display in `src/Web/src/features/audit/AuditRoute.tsx`
- [ ] T072 [US5] [RUNNABLE_NOW] Verify US5 behavior, review, Fast/Full evidence, and checkpoint in `specs/003-operational-configuration-workspace/checklists/phase-05-checkpoint.md`

---

## Phase 6: Acceptance hardening, accessibility, traceability, and final evidence

- [ ] T073 [P] [RUNNABLE_NOW] Add complete AC-001..AC-015 acceptance traceability to `specs/003-operational-configuration-workspace/checklists/acceptance-traceability.md`
- [ ] T074 [P] [RUNNABLE_NOW] Audit Vietnamese content, keyboard focus, responsive desktop/tablet behavior, labels, and non-color status in `specs/003-operational-configuration-workspace/checklists/accessibility.md`
- [ ] T075 [P] [RUNNABLE_NOW] Verify no secret, port 5432, fake fallback, public download, container, savings, AI, or control behavior in `specs/003-operational-configuration-workspace/checklists/security-scope.md`
- [ ] T076 [RUNNABLE_NOW] Run all runnable Unit and PostgreSQL Integration acceptance journeys and record numeric evidence in `specs/003-operational-configuration-workspace/checklists/final-verification.md`
- [ ] T077 [BLOCKED_BY_PACKAGE_POLICY] Run approved frontend behavior suite if available or record the exact package-policy blocker without false PASS in `specs/003-operational-configuration-workspace/checklists/final-verification.md`
- [ ] T078 [RUNNABLE_NOW] Perform final Standards and Specification reviews and resolve all Critical/High findings in `specs/003-operational-configuration-workspace/checklists/final-review.md`
- [ ] T079 [RUNNABLE_NOW] Run final architecture, repository policy, Web lint/build, Fast, and fresh Full harness evidence in `specs/003-operational-configuration-workspace/checklists/final-verification.md`
- [ ] T080 [RUNNABLE_NOW] Assess Planning-ready, Implementation-ready, and Release-ready independently and record explicit remaining blockers in `specs/003-operational-configuration-workspace/checklists/release-checkpoint.md`

## Dependencies

```text
T001-T008
  -> T009 first analysis
  -> remediation
  -> T010 final analysis + Phase 0 checkpoint
  -> T011-T015 red
  -> T016-T023 backend/HTTP green
  -> T024-T032 Web vertical slice
  -> T033-T036 Phase 1 verification/review/checkpoint
  -> STOP

T037-T048 Phase 2
  -> T049-T056 Phase 3
  -> T057-T064 Phase 4
  -> T065-T072 Phase 5
  -> T073-T080 Phase 6
```

- T011-T014 are parallel red-test sources at different public seams.
- T016 and T017 may proceed in parallel after recorded red evidence.
- T024 may proceed with backend contracts while T018-T023 remain green work because it touches
  separate files, but T025-T032 depend on the final contract shape.
- Later phases depend on the Phase 1 checkpoint and require separate `/speckit.implement`
  invocations.

## Requirement coverage

| Requirement group | Task coverage |
|---|---|
| FR-001..FR-011, FR-025..FR-030; AC-001..AC-005, AC-014, AC-015 | T011-T036 |
| FR-012..FR-016; AC-006..AC-008 | T037-T048 |
| FR-017..FR-019; AC-009 | T049-T056 |
| FR-020..FR-022; AC-010..AC-012 | T057-T064 |
| FR-023..FR-024, FR-029; AC-013 | T065-T072 |
| Cross-cutting SC-001..SC-012 and AC-001..AC-015 | T073-T080 |

## Parallel example: Phase 1

```text
T011 IAM red       ┐
T012 status red    ├─> T015 red evidence
T013 HTTP red      ┤
T014 PostgreSQL red┘

T016 host contracts ─┐
T017 IAM contracts   ├─> T018-T023 backend/API green
T024 Web types       ┘           └─> T025-T032 Web green
```

## Implementation strategy

1. Phase 0 proves implementation readiness without green code.
2. Phase 1 delivers the smallest operationally usable vertical slice against PostgreSQL.
3. Stop and review before management breadth.
4. Add later stories one phase at a time so each remains independently demonstrable.

## Task count and phase breakdown

| Phase | Tasks | Range |
|---|---:|---|
| Phase 0 | 10 | T001-T010 |
| Phase 1 / US1 | 26 | T011-T036 |
| Phase 2 / US2 | 12 | T037-T048 |
| Phase 3 / US3 | 8 | T049-T056 |
| Phase 4 / US4 | 8 | T057-T064 |
| Phase 5 / US5 | 8 | T065-T072 |
| Phase 6 | 8 | T073-T080 |
| **Total** | **80** | **T001-T080** |

**MVP for the corrective run**: Phase 2 / US2 only. Do not execute T049 or later.
