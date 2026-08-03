# Tasks: Operational Configuration Workspace

**Input**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

**Tests**: Required. New backend/HTTP/PostgreSQL behavior follows red → green at the public seams
defined in `plan.md`. The frontend behavior runner remains package-policy blocked unless an already
approved runnable dependency exists.

**Historical execution rule**: Phase 0 through Phase 5 are accepted historical checkpoints. The
previous Phase 6
invocation starts from authoritative merged `main` baseline
`f93c2da8bcd71c0436c38d502ddd7a770c35e621` on branch
`003-operational-configuration-workspace`, executes only T073–T080, records acceptance and final
verification evidence honestly, commits and pushes the feature branch, and stops after T080. It
must not merge automatically, execute work after T080, create Spec 004, or expand Rule/Alert/CSV/
Reporting capability. This paragraph is retained as historical scope and is not the execution gate
for the corrective branch below.

---

## Post-Phase-6 Corrective Closure

This is a narrowly bounded governance, traceability, evidence, regression, and source-precedence
repair on corrective branch `fix/003-final-governance-corrective` from merged `main` baseline
`045f3981f3ba6bb87425009ee8f8cf0e6cf4e56a`. It does not reopen Phase 1–6 implementation, create
Phase 7, create Feature 004, or expand Rule/Alert/CSV/Reporting scope. Historical implementation
and evidence are registered retrospectively where marked; no historical TDD red result is invented.

- [x] T081 [RETROSPECTIVE] [RUNNABLE_NOW] Register the Phase 5 corrective implementation, tests, and evidence in the canonical ledger, mapping commit `986b3dca8673b455710835bc252cd17980f9cac5` and merge `f93c2da8bcd71c0436c38d502ddd7a770c35e621`; disclose that this is retrospective registration and not historical task execution
- [x] T082 [US1] [BLOCKED_BY_MISSING_TOOL] Reconcile AC-005 host-restart evidence by running the approved API/Web restart journey if an approved authenticated browser/process-control surface exists; otherwise change AC-005 to PARTIAL and record the exact missing capability without retaining PASS
- [x] T083 [P] [US1] [RUNNABLE_NOW] Add direct AppShell accessibility regression coverage at the existing static verification seam for visible labels, stable ids, invalid-credential error association/focus contract, and Vietnamese navigation/auth names; record historical RED as `NOT_AVAILABLE` and distinguish the post-merge regression result
- [x] T084 [RUNNABLE_NOW] Synchronize `spec.md`, `plan.md`, acceptance traceability, final verification, and release checkpoint with bounded implementation-complete status and `Release-ready=NO`; use `Implemented — Release Evidence Blocked`, never `Released`
- [x] T085 [RUNNABLE_NOW] Reconcile DOC-05 v0.2/DOC-07 v0.2 against ADR-010 and the harness; supersede stale container-target wording, remove `BLK-ENV-004` emission, and use a concrete non-containerized target-host/service approval blocker without fabricating deployment approval
- [x] T086 [RUNNABLE_NOW] Rerun all approved Unit, PostgreSQL, Web, policy, architecture, Fast, and Full checks, update numeric evidence and blocker classifications, and write `checklists/post-phase-06-corrective-review.md`
- [x] T087 [RUNNABLE_NOW] Obtain independent Standards and Specification reviews of this corrective diff, perform direct artifact comparison because the SpecKit provider is unavailable, prepare a corrective PR without merging, and record the explicit Feature 003 final stop

### Corrective closure dependencies

```text
T073–T080 (accepted historical Phase 6)
  -> T081–T085 corrective provenance, AC-005, accessibility, status, and deployment reconciliation
  -> T086 verification/evidence refresh
  -> T087 independent review, PR preparation, and final stop
```

The corrective ledger is additive. Historical T001–T080 wording and checkboxes remain unchanged;
T081 is explicitly retrospective and cannot be used as proof that the historical Phase 5 work was
test-first. No Phase 7 task is created.

**Historical note (not operative for this run)**: Earlier corrective closure from merged `main`
baseline `2741429fb1a28d403adde69e36810bab16d12af5` addressed T054–T056. That historical work is
superseded by the Phase 6 execution gate below; it does not reopen Phase 3 or Phase 4 and does not
change the current T073–T080 scope. Feature 002 remains untouched.

**Phase 6 execution gate**: The current run is authorized only for T073–T080 from authoritative
merged `main` baseline `f93c2da8bcd71c0436c38d502ddd7a770c35e621`. T001–T072 are accepted
historical work and must not be rewritten. Feature 002, work after T080, Spec 004, and
Rule/Alert/CSV/Reporting capability remain out of scope. T080 is checked only after deterministic
PostgreSQL, hosted HTTP where runnable, honest frontend/browser blocker evidence, final
Standards/Specification review, convergence, and final analysis are recorded.

**Current corrective execution gate**: This invocation executes only T081–T087 on
`fix/003-final-governance-corrective` from baseline `045f3981f3ba6bb87425009ee8f8cf0e6cf4e56a`.
The Phase 6 gate above is historical context; it does not prohibit this explicitly authorized
post-Phase-6 governance closure. No Phase 7 or Feature 004 work is authorized.

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

- [x] T049 [P] [US3] [RUNNABLE_NOW] Add failing selected-Source/configuration, no-first-Source, legacy-route retirement, and retry-key tests in `tests/Unit/Api/SimulatorSelectionTests.cs`
- [x] T050 [P] [US3] [RUNNABLE_NOW] Add failing PostgreSQL control/history/idempotency/conflict, exact pinning, and concurrency tests in `tests/Integration/OperationalWorkspace/SimulatorOperationsTests.cs`
- [x] T051 [US3] [RUNNABLE_NOW] Define selected-start ownership and retry-key contracts in `src/Hosting/Abstractions/SimulatorWorkspacePorts.cs` and Web pure helpers
- [x] T052 [US3] [RUNNABLE_NOW] Implement PostgreSQL selected Simulator queries and atomic exact-configuration Start recheck in `src/Composition/Postgres/PostgresSimulatorWorkspacePorts.cs`
- [x] T053 [US3] [RUNNABLE_NOW] Retire legacy Simulator mutations and enforce the four selected workspace routes in `src/Api/SimulatorEndpoints.cs`
- [x] T054 [US3] [RUNNABLE_NOW] Implement URL-backed dependent Site/Area/Asset/Source/configuration selectors and explicit retry UX in `src/Web/src/features/simulator/SimulatorRoute.tsx`
- [x] T055 [US3] [RUNNABLE_NOW] Implement true Web retry-key state, refresh/logout reconstruction, Run details/history/states in `src/Web/src/gateways/webGateways.ts` and `src/Web/src/features/simulator/SimulatorRoute.tsx`
- [x] T056 [US3] [RUNNABLE_NOW] Complete hosted matrix, authenticated browser journey, reviews, Fast/Full evidence, and checkpoint in `specs/003-operational-configuration-workspace/checklists/phase-03-checkpoint.md`

---

## Phase 4: Explicit Latest/Health selection and refresh

**User Story**: US4 — Observe Selected Latest and Source Health

- [x] T057 [P] [US4] [RUNNABLE_NOW] Add failing selected-Point/no-first-Point/No-Data-not-zero tests in `tests/Unit/Api/LatestSelectionTests.cs`
- [x] T058 [P] [US4] [RUNNABLE_NOW] Add failing PostgreSQL Latest/Health/counter scope tests in `tests/Integration/OperationalWorkspace/LatestHealthTests.cs`
- [x] T059 [US4] [RUNNABLE_NOW] Add authorized hierarchy selector query contract with scope-before-paging in `src/Hosting/Abstractions/TelemetryWorkspacePorts.cs`
- [x] T060 [US4] [RUNNABLE_NOW] Implement PostgreSQL Point selector and selected Latest/Health query adapter in `src/Composition/Postgres/PostgresTelemetryWorkspacePorts.cs`
- [x] T061 [US4] [RUNNABLE_NOW] Expose authorized selector endpoints while preserving safe Point queries in `src/Api/TelemetryQueryEndpoints.cs`
- [x] T062 [US4] [RUNNABLE_NOW] Replace implicit Point lookup with explicit Site/Area/Asset/Point selection and server-authorized Point rehydration in `src/Web/src/features/telemetry/PointCurrentRoute.tsx`
- [x] T063 [US4] [RUNNABLE_NOW] Add ten-second default auto refresh, disable/manual refresh, timestamps, quality, Health, Run/counters, explicit No Data, and same-selection request coordination in `src/Web/src/features/telemetry/PointCurrentRoute.tsx`
- [x] T064 [US4] [RUNNABLE_NOW] Verify US4 rehydration/refresh behavior, review, Fast/Full evidence, and checkpoint in `specs/003-operational-configuration-workspace/checklists/phase-04-checkpoint.md`

---

## Phase 5: Audit usability and Operational Dashboard

**User Story**: US5 — Navigate Operational State and Review Audit

- [x] T065 [P] [US5] [RUNNABLE_NOW] Add failing Audit filter/paging/redaction and authorized Dashboard summary tests in `tests/Unit/OperationalWorkspace/AuditDashboardTests.cs`, then register and invoke the suite from `tests/Unit/Program.cs`
- [x] T066 [P] [US5] [RUNNABLE_NOW] Add failing PostgreSQL scope-before-paging Audit/Dashboard tests in `tests/Integration/OperationalWorkspace/AuditDashboardTests.cs`, then register and invoke the suite from `tests/Integration/Program.cs`
- [x] T067 [US5] [RUNNABLE_NOW] Extend the Audit-owned query contract for `fromUtc`/`toUtc`, actor, action, entity type, entity ID, Site/Area scope, server-redacted safe before/after diff, Administrator-only correlation permission (implemented by the existing `ServerPrincipal.IsAdministrator` gate), and strict keyset paging in `src/Modules/Audit/Contracts/AuditContracts.cs` and `src/Modules/Audit/Application/AuditQueryService.cs`
- [x] T068 [US5] [RUNNABLE_NOW] Implement the Audit-owned server-side filters/redaction and scope-before-paging path in `src/Modules/Audit/Infrastructure/PostgresAuditRepositories.cs` and `src/Composition/Postgres/PostgresApplicationPorts.cs` (cursor requests are strict keyset pages without OFFSET), then compose authorized Dashboard summaries through public contracts in `src/Composition/Postgres/PostgresOperationalDashboardPorts.cs`
- [x] T069 [US5] [RUNNABLE_NOW] Expose only the typed `/api/v1/operational-dashboard` route in `src/Api/OperationalDashboardEndpoints.cs`, register it in `src/Api/Program.cs`, and keep the existing `/api/v1/audit-events` ownership in `src/Api/AuditEndpoints.cs`
- [x] T070 [US5] [RUNNABLE_NOW] Add the `dashboard.getSnapshot` gateway seam in `src/Web/src/gateways/webGateways.ts`, implement authorized Vietnamese Operational Dashboard navigation in `src/Web/src/features/dashboard/OperationalDashboard.tsx`, and wire it from `src/Web/src/App.tsx`
- [x] T071 [US5] [RUNNABLE_NOW] Extend `audit.getSnapshot` in `src/Web/src/gateways/webGateways.ts` to send server-side date/actor/action/entity/scope filters and implement the resulting pagination, redacted safe diff, Administrator-only correlation display, and explicit states in `src/Web/src/features/audit/AuditRoute.tsx`
- [x] T072 [US5] [RUNNABLE_NOW] Verify US5 behavior, review, Fast/Full evidence, and checkpoint in `specs/003-operational-configuration-workspace/checklists/phase-05-checkpoint.md`

---

## Phase 6: Acceptance hardening, accessibility, traceability, and final evidence

- [x] T073 [P] [RUNNABLE_NOW] Add complete AC-001..AC-015 acceptance traceability to `specs/003-operational-configuration-workspace/checklists/acceptance-traceability.md`
- [x] T074 [P] [RUNNABLE_NOW] Audit Vietnamese content, keyboard focus, responsive desktop/tablet behavior, labels, and non-color status in `specs/003-operational-configuration-workspace/checklists/accessibility.md`
- [x] T075 [P] [RUNNABLE_NOW] Verify no secret, port 5432, fake fallback, public download, container, savings, AI, or control behavior in `specs/003-operational-configuration-workspace/checklists/security-scope.md`
- [x] T076 [RUNNABLE_NOW] Run all runnable Unit and PostgreSQL Integration acceptance journeys and record numeric evidence in `specs/003-operational-configuration-workspace/checklists/final-verification.md`
- [x] T077 [BLOCKED_BY_PACKAGE_POLICY] Run approved frontend behavior suite if available or record the exact package-policy blocker without false PASS in `specs/003-operational-configuration-workspace/checklists/final-verification.md`
- [x] T078 [RUNNABLE_NOW] Perform final Standards and Specification reviews and resolve all Critical/High findings in `specs/003-operational-configuration-workspace/checklists/final-review.md`
- [x] T079 [RUNNABLE_NOW] Run final architecture, repository policy, Web lint/build, Fast, and fresh Full harness evidence in `specs/003-operational-configuration-workspace/checklists/final-verification.md`
- [x] T080 [RUNNABLE_NOW] Assess Planning-ready, Implementation-ready, and Release-ready independently and record explicit remaining blockers in `specs/003-operational-configuration-workspace/checklists/release-checkpoint.md`

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
| FR-023..FR-024; AC-013 | T065-T072 (FR-029 creation is historical; this phase covers only its no-secrets review facet) |
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
| Post-Phase-6 Corrective Closure | 7 | T081-T087 |
| **Total** | **87** | **T001-T087** |

**Historical MVP**: Phase 6 acceptance hardening and final evidence only
(T073–T080). Stop after T080.

**MVP for the current corrective run**: Post-Phase-6 governance, evidence, regression, and
traceability closure only (T081–T087). Stop after T087.
