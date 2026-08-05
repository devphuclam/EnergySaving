# Implementation Plan: Industrial Operations UI/UX Redesign

**Branch**: `004-industrial-operations-ui-ux-redesign` | **Date**: 2026-08-04 | **Spec**:
[spec.md](spec.md)

**Input**: Feature specification from `/specs/004-industrial-operations-ui-ux-redesign/spec.md`

**Governing Constitution Version**: 1.1.0 (approved and applied 2026-07-24; DEC-GOV-009)

**Planning Readiness**: **PASS (this checkpoint)** — assigned only after the Planning gate records
PASS. This is a planning-only invocation; it does not authorize green implementation.

**Implementation Readiness**: **NO** — remains NO until the final Phase 0 governance checkpoint
permits progression after a fresh clean `/speckit.analyze` and the recorded remediation gate.

**Release Readiness**: **NO** — remains NO until required release evidence passes.

Planning-ready does not authorize green implementation or release. Implementation-ready and
Release-ready remain independently gated states.

**Constitution Amendment Required**: NO — Feature 004 is presentation-only within the R1/VS-01
Web layer; it respects product boundary, source precedence, PostgreSQL-only verification, the
restricted dependency policy, one-phase implementation, and evidence semantics, so no amendment
is required. Re-evaluated post-design: still NO.

**Active Feature Phase 0 Checkpoint**:
`specs/004-industrial-operations-ui-ux-redesign/checklists/phase-00-planning-checkpoint.md`

## Summary

Feature 004 redesigns the presentation of the existing IUMP Web application into a cohesive
Evidence-First Industrial Operations Console, following DOC-08 (authoritative UI/UX) and the five
locked clarification decisions (light-only; compact operational tables 40–44px; grouped expanded
sidebar with tablet rail/drawer and no persistence; permission-based landing with deep-link
precedence, permitted Dashboard fallback, and a safe no-authorized-capability state; desktop-primary with first-class tablet and mobile
non-regression only). Work is incremental over 6 phases: planning foundation, shell + shared
components, dashboard + measurement visibility, configuration, simulator + audit, hardening. It is
presentation-only: no backend, API, database, contract, package, font, icon, chart, or database
change; no dark theme; no density switch; no new routes.

## Technical Context

**Language/Version**: TypeScript ~6.0.2 (React 19.2.7, react-dom 19.2.7, react-router-dom 7.18.1,
@tanstack/react-query 5.101.4); CSS via existing App.css classes (no CSS-in-JS). Backend unchanged:
ASP.NET Core (API), .NET Worker, PostgreSQL — not modified by this feature.

**Primary Dependencies**: Only the installed dependency tree (`src/Web/package.json` current set).
No new packages, fonts, icon libraries, chart libraries, state-management, or form packages;
missing capabilities are blockers or design constraints, not authorization to install.

**Storage**: N/A — Feature 004 introduces no persistence; reads existing server data only
(`data-model.md` states no new business entities).

**Testing**: `npm run lint`, `npm run build` (type-check + Vite production build); repository
PowerShell verification (`scripts/harness.ps1 -Mode Fast/Full`, `scripts/test.ps1`,
`scripts/build.ps1`, `tests/Verification/architecture.tests.ps1`). Frontend test sources are
type-checked, not executed (no runner; BLOCKED_BY_PACKAGE_POLICY). Manual browser evidence per
phase checkpoint. See `contracts/test-strategy.md`.

**Target Platform**: Web SPA served by the existing Vite build; desktop-first (>=1280px), tablet
first-class (768–1279px), mobile non-regression (<768px), per DOC-08 §10 tiers.

**Project Type**: web application (existing React/TypeScript SPA) — presentation-only changes.

**Performance Goals**: no new interaction-heavy dependencies; POC data volumes (8–20 points,
60-second interval) render in inline SVG charts without libraries or downsampling; compact tables
scan efficiently; no long transactions or polling regressions introduced.

**Constraints**: no package/Docker/public registry/equipment control; no savings/root-cause/AI
claims; No Data distinct from zero; status never color-only; one implementation phase per future
`/speckit.implement` invocation; blocked evidence stays blocked; release readiness separate from
planning readiness (repository harness + constitution).

**Scale/Scope**: 6 existing screen routes (dashboard, telemetry, configuration, simulator, audit,
setup) plus the root `/` landing entry; 7 configuration entities; shared
shell/nav/table/form/dialog/chart/status contracts; 6 incremental phases. No new routes or behavior.

## Constitution and Readiness Gates

Each gate records `PASS`, `FAIL`, `BLOCKED`, or `NOT_RUN` evidence. Task executability
classification and evidence status are separate concepts; blocked evidence is never PASS.

### Planning gate

The planning gate MUST verify:

- product boundary and requested release are explicit: YES — presentation-only, MVP-1, DOC-08
  direction, locked clarifications recorded in spec and research.md.
- authoritative sources are registered, with DOC-02 treated as supporting feasibility input:
  YES — `docs/source-register.md` lists DOC-01..DOC-08 + ADRs + active feature; DOC-02
  supporting.
- requirements and exclusions are traceable: YES — FR-001..FR-028 and SC-001..SC-016 traced in
  spec.md; each included surface has a migration destination (migration-phases.md).
- module ownership and API/Worker composition roots are explicit: YES — Web application module
  owns presentation; API/Worker and all module internals unchanged (FR-022/026; zero backend
  changes by this feature).
- environment restrictions are classified with evidence: YES — frontend runner/automation and
  package policy recorded as BLOCKED_BY_PACKAGE_POLICY/NOT_RUN in contracts/test-strategy.md.
- required specification, research, data-model, contract, and quickstart artifacts exist:
  YES — spec.md, research.md, data-model.md, contracts/* (7 artifacts), quickstart.md, plan.md.
- Planning-ready does not authorize green implementation or release: YES — recorded above.

**Planning gate evidence**: `PASS` — historical planning artifacts were committed at baseline
`48c439535d95437c63c7cb07f5e70ab64ee5db34` and are now integrated into the authoritative `main`
baseline `9d10fb6510863418f7871c4bdc05d1cf0a7ade4c`; the remediation checkpoint records the
current neutral branch and entry ancestry check.

### Implementation gate

The implementation gate MUST require all of the following:

- cross-artifact `/speckit.analyze` is clean: **NOT_RUN** for the fresh remediation run; the
  original analysis baseline found Critical/High governance findings that are recorded in
  `checklists/phase-00-analysis-remediation.md` and must be rechecked before implementation.
- zero unresolved Critical or High findings: pending fresh analysis; no implementation work may
  begin while any Critical/High finding remains.
- constitution impact has been evaluated: PASS — source precedence respected, Feature 003
  preserved, no backend capability added, no package/Docker/public source, no equipment control,
  No Data distinct from zero, status not color-only, one phase per invocation, blocked evidence
  stays blocked, release readiness separate from planning.
- every required constitution amendment is approved and applied: none required (NO).
- affected templates and guidance are synchronized: pending the explicit path-by-path verification
  in Phase 0 T005; generic templates/guidance are changed only if actual drift is evidenced.
- the final Phase 0 governance checkpoint permits progression: pending (checkpoint requires fresh
  clean analysis, post-remediation reviews, and T010).
- the governing constitution version is recorded: 1.1.0.
- green implementation has not bypassed Phase 0: N/A — no implementation performed.

**Implementation gate evidence**: `NOT_RUN` — requires the fresh `/speckit.analyze`, the
remediation evidence, the template/guidance synchronization check, and the final Phase 0 checkpoint.
Implementation-ready stays NO.

### Release gate

The release gate MUST require all of the following:

- required functionality and acceptance evidence exist: NO (no implementation).
- Fast verification evidence is an attempted `scripts/harness.ps1 -Mode Fast` result of FAIL=1
  (PASS=10) caused by running API/Worker processes holding module DLL locks; this is unrelated to
  planning-only documentation, no processes were killed, and the result is not reclassified as
  PASS or NOT_RUN.
- mandatory Full and environment-dependent evidence has passed: NOT_RUN / BLOCKED as evidenced
  later.
- no mandatory blocker remains: unknown until implementation.
- the release checkpoint permits release: NO.
- Planning-ready or Implementation-ready is not represented as Release-ready: YES — Release-ready
  is NO.

**Release gate evidence**: `NOT_RUN` — Release-ready stays NO.

## Plan Lifecycle and Phase Rules

This planning invocation covers lifecycle steps 1–4 (source registration → specification →
clarification → plan and design artifacts). Steps 5–16 (tasks, analyze, Critical/High resolution,
constitution-impact, amendment, guidance sync, Phase 0 checkpoint, one-phase implementation,
review, Fast, Full, acceptance/release) are future invocations. Each implementation phase MUST
define red-test work, recorded red evidence, minimal green work, refactor,
architecture/repository-policy verification, Standards and Specification review, a phase
checkpoint, and an explicit stop. Each `/speckit.implement` invocation MUST execute one phase only
(migration-phases.md §2).

## Evidence Vocabulary

Evidence statuses are `PASS`, `FAIL`, `BLOCKED`, `NOT_RUN`. Classifications:
`RUNNABLE_NOW`, `BLOCKED_BY_DATABASE_ACCESS`, `BLOCKED_BY_PACKAGE_POLICY`,
`BLOCKED_BY_MISSING_TOOL`, `BLOCKED_BY_COMPANY_APPROVAL`. Blocked evidence is never PASS.

## Project Structure

### Documentation (this feature)

```text
specs/004-industrial-operations-ui-ux-redesign/
├── plan.md                       # this file
├── research.md                   # Phase 0 output: decisions D-001..D-012 + UI audit + evidence
├── data-model.md                 # presentation/UI state models only; no business entities
├── quickstart.md                 # manual validation guide + commands
├── contracts/                    # design artifacts
│   ├── design-system.md          # tokens, typography, density, status semantics, motion, charts
│   ├── information-architecture.md   # nav groups, shell, scope, landing, permission
│   ├── responsive-accessibility.md   # tiers, sidebar flow, keyboard/focus/contrast, a11y verification
│   ├── component-contracts.md    # 17 shared component contracts
│   ├── route-and-permission-matrix.md # routes, visibility, landing priority, deep-link rules
│   ├── migration-phases.md       # Phase 1..5 + stop gates
│   └── test-strategy.md          # available-tools evidence plan + honest limitations
├── checklists/
│   ├── requirements.md           # specification quality checklist (all pass)
│   ├── phase-00-planning-checkpoint.md  # planning gate evidence and readiness
│   └── phase-00-analysis-remediation.md # original findings and remediation evidence
└── tasks.md                      # canonical dependency-ordered task ledger
```

### Source Code (repository root)

```text
src/Web/
├── index.html                    # lang="vi" (FR-021)
├── vite.config.ts                # unchanged
├── package.json                  # UNCHANGED dependency set
├── node_modules/                 # existing installed tree only
└── src/
    ├── index.css                 # remove prefers-color-scheme dark block (light-only)
    ├── App.css                   # migrate tokens to DOC-08 semantic set; compact table spacing
    ├── App.tsx                   # existing route switch; composition/landing presentation only
    ├── app/
    │   └── AppShell.tsx          # grouped nav, rail/drawer, context bar, landing, skip link
    ├── components/               # NEW: shared contracts (DataTable, FormSection, StatusBadge,
    │                             #       QualityIndicator, FeedbackBanner, states, Dialog, Drawer,
    │                             #       ChartContainer, FilterBar, Pagination, ...)
    ├── features/
    │   ├── dashboard/            # OperationalDashboard: exception-first + chart + text alt
    │   ├── telemetry/            # PointCurrentRoute: zero/Missing/quality/freshness + chart
    │   ├── configuration/        # 7 entities via shared table/form/lifecycle patterns
    │   ├── simulator/            # workspace hierarchy + run history
    │   ├── audit/                # filters, compact table, redacted diff, detail drawer
    │   └── setup/                # shell-consistent pass
    ├── gateways/                 # unchanged typed clients
    └── test/
        └── app-shell.test.tsx    # extended pure state tests (type-checked; not executed)

backend/  API + Worker + modules   # UNCHANGED by Feature 004
tests/Verification/architecture.tests.ps1   # extended only if it references new web test sources
```

**Structure Decision**: Single existing Web project; presentation changes are additive inside
`src/Web/src` with a new `components/` directory for shared contracts and CSS-class token
migration in `App.css`. No framework, package, or project-structure change (FR-022; prompt §11).

## Complexity Tracking

> No constitution or readiness-gate violation requires justification. No additional project,
> repository pattern, or dependency is introduced. Table left empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| _(none)_ | — | — |

## Attachments / Design artifacts

- `research.md` — decisions D-001..D-012 with rationale and rejected alternatives; UI technical
  audit; skill evidence consulted (UI UX Pro Max datasets) and reconciliation.
- `data-model.md` — presentation/UI state models (PM-001..PM-010); explicit statement that no
  business entities or persistence model are introduced.
- `contracts/design-system.md`, `contracts/information-architecture.md`,
  `contracts/responsive-accessibility.md`, `contracts/component-contracts.md`,
  `contracts/route-and-permission-matrix.md`, `contracts/migration-phases.md`,
  `contracts/test-strategy.md`.
- `quickstart.md`.
- `tasks.md` and `checklists/phase-00-analysis-remediation.md` — the governed task ledger and
  targeted remediation evidence; fresh analysis remains pending.
- `checklists/phase-05-usability-evidence.md` — SC-002 protocol template; execution remains NOT_RUN.
- Mermaid diagrams are embedded in the artifacts: application-shell structure
  (information-architecture.md), navigation/permission + landing flow
  (route-and-permission-matrix.md), responsive sidebar state flow
  (responsive-accessibility.md), component hierarchy (component-contracts.md), incremental
  page-migration sequence (migration-phases.md), UI feedback-state model
  (responsive-accessibility.md).
