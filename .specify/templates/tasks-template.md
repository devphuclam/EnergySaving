---

description: "Task list template for feature implementation"
---

# Tasks: [FEATURE NAME]

**Input**: Design documents from `/specs/[###-feature-name]/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Executable behavior requires explicit red-green-refactor tasks. Checks that cannot run
must remain blocked tasks with an evidence command; blocked evidence is never PASS.

**Organization**: Tasks are grouped by governed phase and user story to enable independent
implementation and testing of each story.

## Format: `[ID] [P?] [Story?] [CLASSIFICATION] Description`

- **[P]**: Safely parallel: different files and no unmet dependency.
- **[Story]**: User-story traceability when applicable (e.g., US1, US2, US3).
- **[CLASSIFICATION]**: Exactly one executability classification: `RUNNABLE_NOW`,
  `BLOCKED_BY_DATABASE_ACCESS`, `BLOCKED_BY_PACKAGE_POLICY`, `BLOCKED_BY_MISSING_TOOL`, or
  `BLOCKED_BY_COMPANY_APPROVAL`.
- Every task includes exact paths where applicable, a `Depends:` declaration, and a `Verify:`
  result or expected evidence declaration.
- Classification describes executability; `PASS`, `FAIL`, `BLOCKED`, and `NOT_RUN` describe
  execution evidence. Classification MUST NOT substitute for evidence status.

## Evidence and Blocker Vocabulary

Evidence statuses:

- `PASS`: exact verification executed and succeeded.
- `FAIL`: executable verification ran and failed.
- `BLOCKED`: runnable artifacts were produced where possible, the exact external dependency and
  blocker evidence were recorded, required execution could not occur, and the capability is not
  passing.
- `NOT_RUN`: execution was not attempted.

Allowed task classifications:

- `RUNNABLE_NOW`
- `BLOCKED_BY_DATABASE_ACCESS`
- `BLOCKED_BY_PACKAGE_POLICY`
- `BLOCKED_BY_MISSING_TOOL`
- `BLOCKED_BY_COMPANY_APPROVAL`

Blocked evidence is never PASS. Blocked execution tasks should normally be evidence leaves and must
not unnecessarily block runnable traceability, review, architecture, or checkpoint work. Full and
release tasks cannot pass while mandatory evidence is BLOCKED or NOT_RUN.

## Path Conventions

- **Single project**: `src/`, `tests/` at repository root
- **Web app**: `backend/src/`, `frontend/src/`
- **Mobile**: `api/src/`, `ios/src/` or `android/src/`
- Paths shown below assume single project - adjust based on plan.md structure

## Phase 0: Governance and Environment Evidence

**Purpose**: Establish the source-of-truth baseline and governance gates before green application
behavior.

Generated Phase 0 tasks should cover, when applicable:

- source-register correction;
- environment and blocker evidence;
- initial read-only cross-artifact analysis;
- Critical/High resolution;
- final clean analysis;
- constitution amendment draft when required;
- approval/application when required;
- template and guidance synchronization; and
- the final Phase 0 governance checkpoint.

Red-test source tasks MAY begin after clean analysis only when the generated dependency graph
explicitly permits them. No green application-source task may begin before the final Phase 0
checkpoint permits progression.

**Phase 0 checkpoint**: record PASS count, FAIL count, BLOCKED count by classification, NOT_RUN
count, capability completeness, progression decision, and release decision where relevant; then
stop.

<!--
  ============================================================================
  IMPORTANT: The tasks below are SAMPLE TASKS for illustration purposes only.

  The /speckit-tasks command MUST replace these with actual tasks based on:
  - User stories from spec.md (with their priorities P1, P2, P3...)
  - Feature requirements from plan.md
  - Entities from data-model.md
  - Endpoints from contracts/

  Tasks MUST be organized by user story so each story can be:
  - Implemented independently
  - Tested independently
  - Delivered as an MVP increment

  DO NOT keep these sample tasks in the generated tasks.md file.
  ============================================================================
-->

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 [RUNNABLE_NOW] Create project structure per implementation plan at `[exact path]`; Depends: none; Verify: structure review records PASS/FAIL.
- [ ] T002 [RUNNABLE_NOW] Initialize the [language] project with [framework] dependencies in `[exact path]`; Depends: T001; Verify: permitted environment check records PASS/FAIL/BLOCKED.
- [ ] T003 [P] [RUNNABLE_NOW] Configure linting and formatting tools in `[exact path]`; Depends: T001; Verify: configuration check records PASS/FAIL.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

Examples of foundational tasks (adjust based on your project):

- [ ] T004 [RUNNABLE_NOW] Define and review database schema or migration source in `[exact path]`; Depends: T002; Verify: source review, static validation, or permitted compile checks record PASS/FAIL; no live database is required.
- [ ] T004B [BLOCKED_BY_DATABASE_ACCESS] Execute the reviewed schema/migration source against approved PostgreSQL; Depends: T004; Verify: execution records PASS/FAIL/BLOCKED with blocker ID and evidence.
- [ ] T005 [P] [RUNNABLE_NOW] Implement authentication/authorization framework in `[exact path]`; Depends: T002; Verify: focused checks record PASS/FAIL.
- [ ] T006 [P] [RUNNABLE_NOW] Setup API routing and middleware structure in `[exact path]`; Depends: T002; Verify: route/port checks record PASS/FAIL.
- [ ] T007 [RUNNABLE_NOW] Create base models/entities that all stories depend on in `[exact path]`; Depends: T004; Verify: model checks record PASS/FAIL.
- [ ] T008 [RUNNABLE_NOW] Configure error handling and logging infrastructure in `[exact path]`; Depends: T002; Verify: failure-path checks record PASS/FAIL.
- [ ] T009 [RUNNABLE_NOW] Setup environment configuration management in `[exact path]`; Depends: T002; Verify: configuration checks record PASS/FAIL.
- [ ] T009A [RUNNABLE_NOW] Inspect and record database, package, tool, CI, and approval capability states in `docs/[evidence-file]`, referencing blocked executions such as T004B without depending on them; Depends: T002, T004, T005, T006, T007, T008, T009; Verify: exact blocker IDs and evidence are recorded without requiring blocked execution to complete.
- [ ] T009B [RUNNABLE_NOW] Add structured logging, correlation, health, and explicit failure-state foundations in `[exact path]`; Depends: T008-T009; Verify: observability checks record PASS/FAIL.

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - [Title] (Priority: P1) 🎯 MVP

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 1 (REQUIRED for executable behavior) ⚠️

> **NOTE: Write caller-visible tests FIRST, capture red evidence, then implement the smallest
> behavior that turns the tests green. Documentation-only work still requires explicit evidence.**

- [ ] T010 [P] [US1] [RUNNABLE_NOW] Contract test for [endpoint] in `tests/contract/test_[name].py`; Depends: T006, T009B; Verify: expected red FAIL is recorded before implementation, then PASS after implementation.
- [ ] T011 [P] [US1] [RUNNABLE_NOW] Integration test for [user journey] in `tests/integration/test_[name].py`; Depends: T010; Verify: expected red FAIL is recorded before implementation, then PASS after implementation.

### Implementation for User Story 1

- [ ] T012 [P] [US1] [RUNNABLE_NOW] Create [Entity1] model in `src/models/[entity1].py`; Depends: T010-T011; Verify: model checks record PASS/FAIL.
- [ ] T013 [P] [US1] [RUNNABLE_NOW] Create [Entity2] model in `src/models/[entity2].py`; Depends: T010-T011; Verify: model checks record PASS/FAIL.
- [ ] T014 [US1] [RUNNABLE_NOW] Implement [Service] in `src/services/[service].py`; Depends: T012-T013; Verify: service contract records PASS/FAIL.
- [ ] T015 [US1] [RUNNABLE_NOW] Implement [endpoint/feature] in `src/[location]/[file].py`; Depends: T014; Verify: caller-visible contract turns PASS.
- [ ] T016 [US1] [RUNNABLE_NOW] Add validation and error handling in `src/[location]/[file].py`; Depends: T015; Verify: invalid-input and failure-path checks record PASS/FAIL.
- [ ] T017 [US1] [RUNNABLE_NOW] Add logging for user story 1 operations in `src/[location]/[file].py`; Depends: T016; Verify: structured evidence records PASS/FAIL.

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - [Title] (Priority: P2)

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 2 (REQUIRED for executable behavior) ⚠️

- [ ] T018 [P] [US2] [RUNNABLE_NOW] Contract test for [endpoint] in `tests/contract/test_[name].py`; Depends: T009B; Verify: expected red FAIL is recorded before implementation, then PASS after implementation.
- [ ] T019 [P] [US2] [RUNNABLE_NOW] Integration test for [user journey] in `tests/integration/test_[name].py`; Depends: T018; Verify: expected red FAIL is recorded before implementation, then PASS after implementation.

### Implementation for User Story 2

- [ ] T020 [P] [US2] [RUNNABLE_NOW] Create [Entity] model in `src/models/[entity].py`; Depends: T018-T019; Verify: model checks record PASS/FAIL.
- [ ] T021 [US2] [RUNNABLE_NOW] Implement [Service] in `src/services/[service].py`; Depends: T020; Verify: service contract records PASS/FAIL.
- [ ] T022 [US2] [RUNNABLE_NOW] Implement [endpoint/feature] in `src/[location]/[file].py`; Depends: T021; Verify: caller-visible contract turns PASS.
- [ ] T023 [US2] [RUNNABLE_NOW] Integrate with User Story 1 components in `src/[location]/[file].py`; Depends: T022; Verify: independent-story checks record PASS/FAIL.

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - [Title] (Priority: P3)

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 3 (REQUIRED for executable behavior) ⚠️

- [ ] T024 [P] [US3] [RUNNABLE_NOW] Contract test for [endpoint] in `tests/contract/test_[name].py`; Depends: T009B; Verify: expected red FAIL is recorded before implementation, then PASS after implementation.
- [ ] T025 [P] [US3] [RUNNABLE_NOW] Integration test for [user journey] in `tests/integration/test_[name].py`; Depends: T024; Verify: expected red FAIL is recorded before implementation, then PASS after implementation.

### Implementation for User Story 3

- [ ] T026 [P] [US3] [RUNNABLE_NOW] Create [Entity] model in `src/models/[entity].py`; Depends: T024-T025; Verify: model checks record PASS/FAIL.
- [ ] T027 [US3] [RUNNABLE_NOW] Implement [Service] in `src/services/[service].py`; Depends: T026; Verify: service contract records PASS/FAIL.
- [ ] T028 [US3] [RUNNABLE_NOW] Implement [endpoint/feature] in `src/[location]/[file].py`; Depends: T027; Verify: caller-visible contract turns PASS.

**Checkpoint**: All user stories should now be independently functional

---

## Required Executable Phase Shape

For every executable user-story or cross-cutting implementation phase, generated tasks MUST
model this sequence explicitly: caller-visible red tests or red evidence; contract/port and data
ownership decisions; the smallest green implementation; persistence/adapters where required;
refactor; architecture and repository-policy verification; Standards and Spec-compliance review;
phase checkpoint; and an explicit stop. Tests are required for executable behavior and may not be
replaced with syntax checks, corruption checks, or unconditional placeholder assertions.
Red tests MUST fail because the requested behavior is absent, not because of syntax errors,
project corruption, missing unrelated files, or fabricated assertions; unconditional failure placeholders are prohibited.

Each `/speckit.implement` invocation executes exactly one phase and stops at that phase's
checkpoint. A demonstration-only checkpoint may permit a demo; only a Release-ready checkpoint
may permit release, and no release is permitted while a mandatory blocker remains.

---

[Add more user story phases as needed, following the same pattern]

---

## Phase N: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] TXXX [P] [RUNNABLE_NOW] Documentation updates in `docs/[exact-file]`; Depends: all required story checkpoints; Verify: documentation review records PASS/FAIL.
- [ ] TXXX [RUNNABLE_NOW] Code cleanup and refactoring in `[exact path]`; Depends: all required story checkpoints; Verify: regression checks record PASS/FAIL.
- [ ] TXXX [RUNNABLE_NOW] Performance optimization across all stories in `[exact path]`; Depends: all required story checkpoints; Verify: timed criterion records PASS/FAIL/BLOCKED.
- [ ] TXXX [P] [RUNNABLE_NOW] Additional unit tests in `tests/unit/[exact-file]`; Depends: red/green story evidence; Verify: tests record PASS/FAIL.
- [ ] TXXX [RUNNABLE_NOW] Security hardening in `[exact path]`; Depends: architecture review; Verify: security checks record PASS/FAIL/BLOCKED.
- [ ] TXXX [RUNNABLE_NOW] Run quickstart.md validation; Depends: final implementation checkpoint; Verify: quickstart evidence records PASS/FAIL/BLOCKED.

---

## Final Phase: Acceptance and Release Evidence

The generated final phase MUST include executable evidence for requirements, user stories, and
success criteria; measurable or timed acceptance criteria; Standards and Spec-compliance review;
Fast verification; mandatory Full verification with database access; package-policy, tool, and
company-approval evidence; architecture and repository-policy checks; and a final checkpoint.

- [ ] TXXX [RUNNABLE_NOW] Record requirements traceability in `specs/[###-feature-name]/checklists/[acceptance].md`; Depends: runnable implementation checkpoints; References/Inspect: blocked implementation evidence; Verify: requirements map is complete and records PASS/FAIL.
- [ ] TXXX [RUNNABLE_NOW] Record user-story traceability in `specs/[###-feature-name]/checklists/[acceptance].md`; Depends: requirements traceability; Verify: story map is complete and records PASS/FAIL.
- [ ] TXXX [RUNNABLE_NOW] Record success-criteria traceability in `specs/[###-feature-name]/checklists/[acceptance].md`; Depends: user-story traceability; Verify: criteria map is complete and records PASS/FAIL.
- [ ] TXXX [RUNNABLE_NOW] Define the deterministic timed acceptance journey, timer boundaries, inputs, expected results, and evidence path in `[exact test/evidence path]`; Depends: success-criteria traceability; Verify: source review or permitted compile evidence records PASS/FAIL; no live database is required.
- [ ] TXXX [BLOCKED_BY_DATABASE_ACCESS] Execute the timed acceptance journey and record runtime evidence; Depends: timed acceptance source task; Verify: applicable execution records PASS/FAIL/BLOCKED with blocker ID; never report timing PASS when a prerequisite is blocked.

> **Execution classification rule**: Use the applicable database, package, tool, or
> company-approval classification for each timed or environment-dependent execution task. If a
> prerequisite is unavailable, record BLOCKED with its classification and blocker evidence rather
> than reporting timing PASS.
- [ ] TXXX [RUNNABLE_NOW] Run Standards and Spec-compliance review in `docs/[review-file]`; Depends: success-criteria traceability; References/Inspect: timed and environment evidence; Verify: review records no unresolved Critical/High findings.
- [ ] TXXX [RUNNABLE_NOW] Run Fast verification; Depends: Standards/Spec review task; References/Inspect: timed, Full, package, tool, and approval evidence; Verify: Fast evidence records PASS/FAIL/BLOCKED.
- [ ] TXXX [BLOCKED_BY_DATABASE_ACCESS] Run mandatory Full verification with database access; Depends: Fast task; Verify: Full evidence records PASS/FAIL/BLOCKED with blocker ID when unavailable.
- [ ] TXXX [BLOCKED_BY_PACKAGE_POLICY] Record package-policy execution evidence; Depends: Fast task; Verify: package evidence records PASS/FAIL/BLOCKED.
- [ ] TXXX [BLOCKED_BY_MISSING_TOOL] Record tool-dependent execution evidence; Depends: Fast task; Verify: tool evidence records PASS/FAIL/BLOCKED.
- [ ] TXXX [BLOCKED_BY_COMPANY_APPROVAL] Record company-approval/release execution evidence; Depends: Fast task; Verify: approval evidence records PASS/FAIL/BLOCKED.
- [ ] TXXX [RUNNABLE_NOW] Run architecture and repository-policy checks in `docs/[checkpoint-file]`; Depends: Standards/Spec review task, Fast task; References/Inspect: Full, package, tool, approval, and timed evidence; Verify: checks record PASS/FAIL/BLOCKED.
- [ ] TXXX [RUNNABLE_NOW] Record the final Release-ready checkpoint and stop; Depends: requirements traceability, user-story traceability, success-criteria traceability, Standards/Spec review task, Fast task, architecture/policy verification task; References/Inspect: timed runtime, Full, package, tool, and company-approval evidence; Verify: record each mandatory item as PASS/FAIL/BLOCKED/NOT_RUN with classification and blocker ID, block progression/release on FAIL, and keep Release-ready NO for BLOCKED or NOT_RUN.

The final checkpoint remains runnable when an environment task is blocked, but it MUST NOT report
Release-ready or permit release until every mandatory blocker is resolved and Full verification
passes. Blocked runtime evidence is inspected, never treated as a pass-required dependency.

---

## Dependencies & Execution Order

### Task-Graph Invariants

- Generated task IDs are unique and sequential; dependencies are valid and every `Depends:`
  reference names an existing earlier task. No forward dependency is allowed unless the repository
  explicitly supports and validates it, and the graph must contain no dependency cycle.
- Zero `RUNNABLE_NOW` tasks may transitively depend on a blocked evidence-leaf task. Task
  classification MUST match actual executability; source creation and runtime execution are
  separated whenever their environment requirements differ.
- Red tests or red evidence precede green implementation. Green tasks depend on the final Phase 0
  governance checkpoint and may not bypass it.
- A blocked execution task normally remains an evidence leaf. It must not unnecessarily block
  runnable traceability, review, architecture, or checkpoint tasks.
- Checkpoints inspect blocked evidence and never convert it to PASS. A runnable checkpoint remains
  runnable when an environment task is blocked unless that capability is required for the
  checkpoint decision.
- Checkpoints remain runnable so they can record incomplete capability truthfully; blocked evidence
  is listed under `References/Inspect`, not as a pass-required dependency.
- Full verification and release cannot pass while mandatory evidence is BLOCKED or NOT_RUN.

### Checkpoint Evidence Contract

Every Phase 0, implementation-phase, and final checkpoint task records PASS, FAIL, BLOCKED counts
by classification, NOT_RUN count, capability completeness, progression decision, and release
decision. The checkpoint task is the stop boundary for its phase.

### Phase Dependencies

- **Governance and Environment (Phase 0)**: Must complete the source, analysis, amendment, sync,
  and final governance checkpoint before any green application-source task.
- **Setup (Phase 1)**: Depends on the Phase 0 checkpoint (red evidence may start only when its
  graph explicitly permits it).
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion and the final Phase 0
  checkpoint
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) and the final Phase 0 checkpoint - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) and the final Phase 0 checkpoint - May integrate with US1 but should be independently testable
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) and the final Phase 0 checkpoint - May integrate with US1/US2 but should be independently testable

### Within Each User Story

- Caller-visible tests or red evidence MUST be written and FAIL before implementation; tests are
  required for executable behavior.
- Models before services
- Services before endpoints
- Core implementation before integration
- Refactor, architecture/repository-policy verification, and Standards/Spec review precede the
  story checkpoint.
- Story checkpoint is an explicit stop; story completion is required before moving to the next
  priority unless the graph explicitly allows parallel stories.

### Parallel Opportunities

- Tasks marked [P] can run in parallel only when they modify different files and have no unmet dependency
- All Foundational tasks marked [P] can run in parallel when their dependencies are satisfied
- Once Foundational phase completes, all user stories can start in parallel (if team capacity allows)
- Independent red tests for a user story marked [P] can run in parallel
- Models within a story marked [P] can run in parallel when their dependencies are satisfied
- Different user stories can be worked on in parallel only when their graph has no shared-file or unmet-dependency conflict

---

## Parallel Example: User Story 1

```bash
# Launch independent red tests for User Story 1 together:
Task: "[RUNNABLE_NOW] Contract test for [endpoint] in tests/contract/test_[name].py; Depends: T006, T009B"
Task: "[RUNNABLE_NOW] Integration test for [user journey] in tests/integration/test_[name].py; Depends: T010"

# Launch all models for User Story 1 together:
Task: "[RUNNABLE_NOW] Create [Entity1] model in src/models/[entity1].py; Depends: T010-T011"
Task: "[RUNNABLE_NOW] Create [Entity2] model in src/models/[entity2].py; Depends: T010-T011"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 0: Governance and Environment Evidence
2. Complete Phase 1: Setup
3. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
4. Complete Phase 3: User Story 1
5. **STOP and VALIDATE**: Test User Story 1 independently
6. Demonstrate only when the relevant checkpoint permits; release only when Release-ready

### Incremental Delivery

Always complete Phase 0 governance evidence before Setup/Foundational green work. Each story
stops at its checkpoint; demonstration is permitted only when that checkpoint says so, and release
is permitted only from Release-ready evidence with no mandatory blocker.

1. Complete Phase 0 + Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → demonstrate only if its checkpoint permits
3. Add User Story 2 → Test independently → demonstrate only if its checkpoint permits
4. Add User Story 3 → Test independently → demonstrate only if its checkpoint permits
5. Each story adds value without breaking previous stories
6. Release only from a Release-ready checkpoint; never release while mandatory evidence is blocked

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1
   - Developer B: User Story 2
   - Developer C: User Story 3
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files and no unmet dependency
- [Story] label maps task to specific user story for traceability
- Every task has exactly one executability classification plus explicit `Depends:` and `Verify:` evidence
- Each executable user story is independently completable and testable through caller-visible red-green-refactor evidence
- Verify tests or red evidence fail before implementing
- Commit after each task or logical group
- Stop at every checkpoint; a checkpoint records evidence counts and does not silently continue
- Blocked evidence is never PASS; mandatory blockers keep Full and Release-ready NO
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
