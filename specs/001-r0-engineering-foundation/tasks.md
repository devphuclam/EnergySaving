# Tasks: R0 Engineering Foundation

**Input**: Design documents in `specs/001-r0-engineering-foundation/`  
**Tests**: TDD is mandatory for every executable seam. Each red/green task records the command and
actual result. A blocked task remains unchecked and cites `docs/blocker-report.md`.

## Phase 1: Setup and Canonical Artifacts

**Purpose**: Establish the source hierarchy and single canonical feature before source changes.

- [x] T001 Create the R0 constitution and synchronized Spec Kit templates in `.specify/memory/constitution.md` and `.specify/templates/`
- [x] T002 [P] Create the domain-only glossary in `CONTEXT.md`
- [x] T003 [P] Create source register and decision log in `docs/source-register.md` and `docs/decision-log.md`
- [x] T004 [P] Record tool/source/cache classifications in `docs/environment-inventory.md`
- [x] T005 Create specification, plan, research, data model, contracts, and quickstart in `specs/001-r0-engineering-foundation/`
- [x] T006 [P] Create requirement-quality checklists in `specs/001-r0-engineering-foundation/checklists/`

---

## Phase 2: Foundational Scope and Safety Controls

**Purpose**: Remove misleading/prohibited pre-existing surfaces and establish a compile-neutral R0
shape. These tasks block all three user stories.

- [x] T007 Remove `docker-compose.yml`, `src/Api/Dockerfile`, and `src/Worker/Dockerfile`
- [x] T008 Remove the public-action/container workflow `.github/workflows/ci.yml` and retain CI requirements in `docs/ci-readiness.md`
- [x] T009 Remove hard-coded database credentials and unsafe setup SQL from `src/Api/appsettings.json`, `src/Worker/appsettings.json`, host source, design-time factories, and `scripts/setup-database.sql`
- [x] T010 Remove or exclude pre-existing R1 business models, migrations, API wiring, Worker placeholders, and Web business pages under `src/Modules/`, `src/Api/`, `src/Worker/`, and `src/Web/src/pages/`
- [x] T011 Resolve the duplicate non-solution project `src/BuildingBlocks/BuildingBlocks.csproj` and keep a framework-light foundation project
- [x] T012 Reconcile ADR-001, ADR-002, ADR-005, ADR-007, ADR-010, and ADR-016 with DOC-05/06 and restricted policy in `docs/adr/`
- [x] T013 Pin the installed SDK and central package metadata in `global.json`, `Directory.Build.props`, and `Directory.Packages.props` without changing locked dependency versions to fit cache
- [x] T014 Create the R0 solution/project reference graph in `IUMP.slnx` and `src/` with separate API/Worker composition roots and module contract skeletons
- [x] T015 Create ordered PostgreSQL migration-source and synthetic-seed structure in `database/migrations/` and `database/seeds/` without executing it

**Checkpoint**: No prohibited artifact, real credential, fake pass, or completed R1 workflow remains
in the canonical source tree.

---

## Phase 3: User Story 1 — Understand and Verify the Foundation (P1)

**Goal**: One safe local entry point reports truthful classifications without installation/network.

**Independent Test**: Run `scripts/verify.ps1` and inspect one result for every registered check;
mandatory blockers produce a non-zero aggregate exit without suppressing safe checks.

### Tests first

- [x] T016 [US1] Write and run a failing contract test for prerequisite/result classification in `tests/Verification/verification-contract.tests.ps1`
- [x] T017 [P] [US1] Write and run a failing secret-redaction/static policy test in `tests/Verification/repository-policy.tests.ps1`

### Minimal implementation

- [x] T018 [US1] Implement prerequisite detection and result classification in `scripts/common/Verification.ps1`
- [x] T019 [P] [US1] Implement non-restoring backend build entry point in `scripts/build.ps1`
- [x] T020 [P] [US1] Implement test entry point with package/database blocker preservation in `scripts/test.ps1`
- [x] T021 [P] [US1] Implement guarded PostgreSQL migration and seed entry points in `scripts/db-migrate.ps1` and `scripts/db-seed.ps1`
- [x] T022 [P] [US1] Implement guarded host entry points in `scripts/start-api.ps1`, `scripts/start-worker.ps1`, and `scripts/start-web.ps1`
- [x] T023 [US1] Implement aggregate local verification and dependency inventory in `scripts/verify.ps1`
- [x] T024 [US1] Re-run red/green/refactor verification contract suites and record actual evidence in `docs/verification-report.md`

**Checkpoint**: US1 is independently usable even when database/CI/container checks remain blocked.

---

## Phase 4: User Story 2 — Preserve Architecture and Scope (P2)

**Goal**: Reviewers can prove R0 boundaries and absence of later/prohibited capability.

**Independent Test**: Run architecture/static policy checks against the project graph and deliberate
forbidden fixtures; follow source-to-requirement-to-task-to-evidence traceability.

### Tests first

- [x] T025 [US2] Write and run failing architecture fixtures for host/module/foundation references in `tests/Architecture/fixtures/`
- [x] T026 [P] [US2] Write and run a failing prohibited-surface scan for containers, credentials, Modbus, control, AI, and R1 UI in `tests/Verification/repository-scope.tests.ps1`

### Minimal implementation

- [x] T027 [US2] Implement static module-reference and public-surface enforcement in `tests/Verification/architecture.tests.ps1`
- [x] T028 [P] [US2] Implement minimal API correlation/configuration/health foundation in `src/Api/`
- [x] T029 [P] [US2] Implement minimal Worker lifecycle/configuration/health foundation in `src/Worker/`
- [x] T030 [US2] Define job/outbox/inbox identifiers and persistence contracts without fake storage behavior in `src/Modules/Operations/Contracts/`
- [x] T031 [P] [US2] Reduce Web to an accessible foundation shell with environment/health state only in `src/Web/src/`
- [x] T032 [US2] Re-run architecture and scope red/green/refactor suites and record evidence in `docs/verification-report.md`

**Checkpoint**: US2 shows one product/release boundary, separate host processes, owned module
interfaces, and no R1/conditional behavior.

---

## Phase 5: User Story 3 — Request Missing Company Capabilities (P3)

**Goal**: Company owners receive complete, least-privilege, non-secret unblock requests.

**Independent Test**: Review documents against the Blocker entity/contract and confirm every blocked
verification maps to an actionable request and post-approval command.

- [x] T033 [P] [US3] Complete PostgreSQL least-privilege request in `docs/database-access-request.md`
- [x] T034 [P] [US3] Complete approved-runner/package-mirror requirements in `docs/ci-readiness.md`
- [x] T035 [US3] Map every blocker to affected criteria and lowest-risk next step in `docs/blocker-report.md`
- [x] T036 [US3] Add Gate G1 evidence and named-approval gaps in `docs/gate-g1-status.md`

**Checkpoint**: US3 is complete without obtaining or using credentials/approval itself.

---

## Phase 6: Review, Convergence, and Handoff

- [x] T037 Validate all requirement-quality checklist items and record unresolved gaps in `specs/001-r0-engineering-foundation/checklists/`
- [x] T038 Run Spec Kit cross-artifact analysis and resolve all Critical conflicts in `specs/001-r0-engineering-foundation/analysis.md`
- [x] T039 Run separate Standards and Spec code reviews and record Critical/High findings in `docs/code-review.md`
- [x] T040 Run local verification and classify every R0 acceptance criterion in `docs/verification-report.md`
- [x] T041 Run Spec Kit convergence and append any genuinely missing work to `specs/001-r0-engineering-foundation/tasks.md`
- [x] T042 Update `README.md` with VERIFIED, NOT VERIFIED, BLOCKED BY ENVIRONMENT, and REQUIRES COMPANY APPROVAL sections
- [x] T043 Produce final R0 report and git status in `docs/r0-final-report.md`

## Dependencies and Execution Order

- Phase 1 precedes planning and implementation evidence.
- Phase 2 blocks all user-story implementation because prohibited/fake/R1 surfaces invalidate tests.
- US1 is first: it supplies the truthful verification interface used by US2 and final handoff.
- US2 depends on US1 result semantics but is otherwise reviewable without PostgreSQL.
- US3 depends only on inventory/blocker evidence and can proceed independently after Phase 2.
- Review/convergence depends on the desired R0 source tasks; database/package/CI blocked tasks stay
  unchecked and prevent `FULLY_COMPLETE`.

## Parallel Opportunities

- T002–T004 and T006 touch independent documentation files.
- T019–T022 implement independent guarded entry points after T018 defines their shared interface.
- T025 and T026 create independent red fixtures.
- T028, T029, and T031 touch separate host/UI trees after architecture constraints are defined.
- T033 and T034 are independent company-owner requests.

## Implementation Strategy

Complete safe documentation and static/offline seams first. Never widen scope to make a blocked check
green. Stop the affected task at its blocker, preserve the unchecked box and evidence, then continue
only independent R0 work. Stop after R0.
