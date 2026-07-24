# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]

**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

**Governing Constitution Version**: [e.g., 1.1.0]

**Planning Readiness**: **NOT_RUN / NO** — assigned **Planning-ready** only after the Planning
gate records PASS. Creating or populating `plan.md` alone does not establish Planning-ready.

**Implementation Readiness**: **NO** — remains NO until all implementation gates pass.

**Release Readiness**: **NO** — remains NO until required release evidence passes.

Planning-ready does not authorize green implementation or release. Implementation-ready and
Release-ready remain independently gated states.

**Constitution Amendment Required**: [YES/NO]

**Active Feature Phase 0 Checkpoint**: `specs/[###-feature-name]/checklists/phase-00-governance.md`

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: [e.g., Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION]

**Primary Dependencies**: [e.g., FastAPI, UIKit, LLVM or NEEDS CLARIFICATION]

**Storage**: [if applicable, e.g., PostgreSQL, CoreData, files or N/A]

**Testing**: [e.g., pytest, XCTest, cargo test or NEEDS CLARIFICATION]

**Target Platform**: [e.g., Linux server, iOS 15+, WASM or NEEDS CLARIFICATION]

**Project Type**: [e.g., library/cli/web-service/mobile-app/compiler/desktop-app or NEEDS CLARIFICATION]

**Performance Goals**: [domain-specific, e.g., 1000 req/s, 10k lines/sec, 60 fps or NEEDS CLARIFICATION]

**Constraints**: [domain-specific, e.g., <200ms p95, <100MB memory, offline-capable or NEEDS CLARIFICATION]

**Scale/Scope**: [domain-specific, e.g., 10k users, 1M LOC, 50 screens or NEEDS CLARIFICATION]

## Constitution and Readiness Gates

Each gate records `PASS`, `FAIL`, `BLOCKED`, or `NOT_RUN` evidence. Task executability
classification and evidence status are separate concepts; blocked evidence is never PASS.

### Planning gate

The planning gate MUST verify:

- product boundary and requested release are explicit;
- authoritative sources are registered, with DOC-02 treated as supporting feasibility input;
- requirements and exclusions are traceable;
- module ownership and API/Worker composition roots are explicit;
- environment restrictions are classified with evidence;
- required specification, research, data-model, contract, and quickstart artifacts exist; and
- Planning-ready does not authorize green implementation or release.

**Planning gate evidence**: `PASS | FAIL | BLOCKED | NOT_RUN` — [record command, evidence, and blocker ID]

### Implementation gate

The implementation gate MUST require all of the following:

- cross-artifact `/speckit.analyze` is clean;
- zero unresolved Critical or High findings;
- constitution impact has been evaluated;
- every required constitution amendment is approved and applied;
- affected templates and guidance are synchronized;
- the final Phase 0 governance checkpoint permits progression;
- the governing constitution version is recorded; and
- green implementation has not bypassed Phase 0.

**Implementation gate evidence**: `PASS | FAIL | BLOCKED | NOT_RUN` — [record command, evidence, and blocker ID]

### Release gate

The release gate MUST require all of the following:

- required functionality and acceptance evidence exist;
- Fast verification has passed;
- mandatory Full and environment-dependent evidence has passed;
- no mandatory blocker remains;
- the release checkpoint permits release; and
- Planning-ready or Implementation-ready is not represented as Release-ready.

**Release gate evidence**: `PASS | FAIL | BLOCKED | NOT_RUN` — [record command, evidence, and blocker ID]

## Plan Lifecycle and Phase Rules

Use this generic Constitution 1.1.0 lifecycle:

1. Source registration
2. Specification
3. Clarification
4. Plan and design artifacts
5. Dependency-ordered tasks
6. Read-only cross-artifact analysis
7. Critical/High resolution
8. Constitution-impact evaluation
9. Approved amendment when required
10. Template and guidance synchronization
11. Phase 0 governance checkpoint
12. Test-first implementation by phase
13. Standards and Specification review
14. Fast verification
15. Full and environment-dependent verification
16. Acceptance and release evidence

Each implementation phase MUST define red-test work, recorded red evidence, minimal green work,
refactor, architecture/repository-policy verification, Standards and Specification review, a phase
checkpoint, and an explicit stop. Each `/speckit.implement` invocation MUST execute one phase only.

## Evidence Vocabulary

Evidence statuses are `PASS`, `FAIL`, `BLOCKED`, and `NOT_RUN`.

Task classifications are `RUNNABLE_NOW`, `BLOCKED_BY_DATABASE_ACCESS`, `BLOCKED_BY_PACKAGE_POLICY`,
`BLOCKED_BY_MISSING_TOOL`, and `BLOCKED_BY_COMPANY_APPROVAL`. Classification describes
executability; status describes execution evidence. They MUST NOT be conflated. Mandatory blockers
prevent Full and release PASS.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
# [REMOVE IF UNUSED] Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# [REMOVE IF UNUSED] Option 2: Web application (when "frontend" + "backend" detected)
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/

# [REMOVE IF UNUSED] Option 3: Mobile + API (when "iOS/Android" detected)
api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure: feature modules, UI flows, platform tests]
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if a constitution or readiness gate records a violation that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
