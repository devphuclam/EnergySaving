# Tasks: Industrial Operations UI/UX Redesign

**Feature**: 004-industrial-operations-ui-ux-redesign

**Source of truth**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`,
`contracts/`, the constitution, DOC-08, and the existing `src/Web` application.

**Scope**: planning-only task ledger. No task in this file authorizes production code, backend/API/
database changes, package installation, dark mode, a density switch, new routes, or a mobile-first
application. Every future `/speckit.implement` invocation executes exactly one phase, reaches that
phase checkpoint, and stops.

**Task format**: Every task has one executability classification, an exact owner/path, an explicit
dependency list, and verification evidence. `[P]` is used only when the task can safely run in
parallel without editing the same files as another task or depending on unfinished work.

## Phase 0 — Governance and implementation readiness

**Goal**: Reconcile planning truth, validate the graph, and stop before production work.

- [x] T001 [RUNNABLE_NOW] Phase 0 — Correct the attempted Fast harness evidence in `specs/004-industrial-operations-ui-ux-redesign/plan.md` and `checklists/phase-00-planning-checkpoint.md` to record the actual `FAIL=1 (PASS=10)` caused by API/Worker DLL locks, with no process termination; Depends: none; Verify: commit `9d10fb6` and the checkpoint record show `FAIL=1`, the lock explanation, and no reclassification as PASS or NOT_RUN.
- [x] T002 [RUNNABLE_NOW] Phase 0 — Reconcile the permission landing flow in `research.md`, `data-model.md`, `contracts/information-architecture.md`, `contracts/route-and-permission-matrix.md`, `plan.md`, `quickstart.md`, and the Phase 0 checkpoint as valid permitted deep link → first enabled effectively permitted capability → permission-checked Dashboard fallback → safe no-authorized-capability state; Depends: T001; Verify: the changed artifacts use one canonical flow, treat `WorkspaceStatus.landing` as input only, never route through forbidden pages, and never expose capability/object metadata.
- [x] T003 [RUNNABLE_NOW] Phase 0 — Append post-planning integration truth and the historical statement that the planning invocation itself did not merge in `checklists/phase-00-planning-checkpoint.md`; Depends: T001, T002; Verify: both planning commit SHAs `df73c2893fa6b936120ff5742000d0b25e180e4c` and `26b7443f7f88cb84dfbaf2fb3ffddaa6c32fcbd3` are recorded as integrated into `main`.
- [x] T004 [RUNNABLE_NOW] Phase 0 — Record the actual UI UX Pro Max supporting search and rejected landing-page/oversized-typography/package recommendations in `checklists/phase-00-planning-checkpoint.md`; Depends: T003; Verify: the command, output use, and DOC-08/dependency-policy reconciliation are written without creating a competing design source.
- [x] T005 [RUNNABLE_NOW] Phase 0 — Verify constitution impact, Feature 003 preservation, no-backend/no-package/no-Docker boundaries, No Data semantics, color-independent status, and synchronization of `.specify/templates/*`, `docs/repository-harness.md`, `AGENTS.md`, `CONTEXT.md`, and `README.md`; Depends: T003; Verify: inspected paths and the no-drift/actual-drift outcome are recorded in `checklists/phase-00-analysis-remediation.md`; generic guidance is changed only if drift is evidenced.
- [x] T006 [RUNNABLE_NOW] Phase 0 — Maintain the FR-001–FR-028, SC-001–SC-016, C-01–C-17, route, state, and phase-stop traceability tables in `tasks.md` after remediation and task renumbering; Depends: T004, T005; Verify: every listed requirement, criterion, contract, route, and non-happy-path state maps to one or more task IDs.
- [x] T007 [RUNNABLE_NOW] Phase 0 — Validate task IDs, classifications, dependency ordering, parallel markers, phase boundaries, and absence of cycles in `tasks.md`; Depends: T006; Verify: the read-only validation evidence records 71 unique sequential IDs, no forward dependency, no duplicate ID, no invalid `[P]` marker, and no cycle.
- [ ] T008 [RUNNABLE_NOW] Phase 0 — Run a fresh cross-artifact `/speckit.analyze` after T002/T005/T006/T007, record the original baseline and current findings, resolve every Critical/High finding, disposition every Medium/Low finding, and require `Analyze-clean: YES` with no unresolved constitution conflict in `checklists/phase-00-analysis-remediation.md` and `checklists/phase-00-planning-checkpoint.md`; Depends: T007; Verify: fresh analysis evidence is attached, counts are explicit, and implementation remains blocked until clean.
- [ ] T009 [RUNNABLE_NOW] Phase 0 — Perform the post-remediation Standards and Specification reviews against repository guidance, constitution, `spec.md`, `plan.md`, contracts, and locked clarifications in `checklists/phase-00-planning-checkpoint.md`; Depends: T008; Verify: both review sections record no unresolved Critical/High governance contradiction or FR/SC/contract/scope mismatch.
- [ ] T010 [RUNNABLE_NOW] Phase 0 — Record the final governance checkpoint and explicit stop in `checklists/phase-00-planning-checkpoint.md`; Depends: T008, T009; Verify: the checkpoint references fresh `Analyze-clean: YES`, PASS/FAIL/BLOCKED/NOT_RUN counts, capability completeness, Planning-ready/Implementation-ready/Release-ready decisions, and the next command; no production path is changed.

**Phase 0 stop**: Do not begin green production work until T010 is reviewed and the next invocation explicitly selects Phase 1.

## Phase 1 — Application shell and shared foundations

**Goal**: Establish the Evidence-First Industrial Light shell and reusable operational primitives for US1, US3, US6, and US7.

- [ ] T011 [P] [US7] [RUNNABLE_NOW] Phase 1 — Add red caller-visible shell evidence for grouped navigation, permission-safe landing, deep links, keyboard focus, and tablet drawer behavior in `src/Web/src/test/app-shell.test.tsx` and named route fixtures under `src/Web/src/test/`; Depends: T010; Verify: absent behavior fails for the requested reason before implementation and the evidence path is recorded.
- [ ] T012 [P] [US7] [RUNNABLE_NOW] Phase 1 — Define semantic Evidence-First Industrial Light tokens, Vietnamese-first type hierarchy, focus ring, spacing, compact row metrics, reduced-motion hooks, and no-dark/no-density-switch constraints in `src/Web/src/index.css` and `src/Web/src/App.css`; Depends: T010; Verify: token review covers FR-017–FR-021, WCAG 2.2 AA targets, and no raw per-component palette is introduced.
- [ ] T013 [US1] [RUNNABLE_NOW] Phase 1 — Implement C-01 AppShell ownership for product identity, current location, scope context, session states, skip link, `#main-content`, and route-title focus in `src/Web/src/app/AppShell.tsx` and `src/Web/src/App.tsx`; Depends: T011, T012; Verify: shell contract tests and keyboard route evidence record PASS/FAIL without changing backend authorization.
- [ ] T014 [US1] [RUNNABLE_NOW] Phase 1 — Implement C-02 grouped Sidebar/Rail/Drawer with permission-filtered items, `aria-current`, accessible toggle, icon-only names/tooltips, focus trap, Escape, focus restoration, and no persisted preference in `src/Web/src/app/AppShell.tsx` plus the named navigation component files under `src/Web/src/components/`; Depends: T013; Verify: desktop expanded and tablet rail/drawer keyboard evidence covers FR-002, FR-019, FR-020, SC-005, SC-006, and SC-014.
- [ ] T015 [US1] [RUNNABLE_NOW] Phase 1 — Implement C-03 Top/Context Bar and C-04 Page Header/Breadcrumb with Site/Area, timezone, cutoff/freshness, user/role, logout, Vietnamese title, one primary action, and safe scope retention in `src/Web/src/components/` and `src/Web/src/app/AppShell.tsx`; Depends: T013; Verify: context and breadcrumb evidence covers FR-003, FR-005, FR-025, SC-001, and SC-011.
- [ ] T016 [US7] [RUNNABLE_NOW] Phase 1 — Implement C-05 Operational Status Badge, C-06 Data Quality Indicator, and C-07 Freshness Indicator with text, icon/shape, accessible name, reason, zero-versus-Missing semantics, and non-color cues in `src/Web/src/components/`; Depends: T012; Verify: component checks cover Good/Uncertain/Bad/Missing, stale, blocked, forbidden, conflict, pending, processing, and completed-with-errors states.
- [ ] T017 [US7] [RUNNABLE_NOW] Phase 1 — Implement C-08 Feedback Banner/Notice and C-09 Loading/Empty/Error/Forbidden/Conflict/Blocked/Retry states in `src/Web/src/components/`; Depends: T012; Verify: every state retains context, impact, next action, correlation where available, and correct live-region role under FR-004, FR-023, FR-024, and SC-004.
- [ ] T018 [US3] [RUNNABLE_NOW] Phase 1 — Implement C-10 compact DataTable, C-11 FilterBar, and C-12 Pagination with visible sort/filter state, explicit row actions, 40–44px desktop target, accessible tablet scroll/wrap, result count, and no density switch in `src/Web/src/components/`; Depends: T012; Verify: component evidence covers FR-013, FR-015, FR-027, SC-005, SC-006, SC-008, and SC-013.
- [ ] T019 [US3] [RUNNABLE_NOW] Phase 1 — Implement C-13 FormSection/Field/FieldErrorSummary for required marks, grouping, `aria-describedby`, first-invalid focus, preserved input, and unsaved-change warning in `src/Web/src/components/`; Depends: T012; Verify: invalid, retry, conflict, and unsaved-change evidence covers FR-008, FR-014, FR-020, and SC-007.
- [ ] T020 [US3] [RUNNABLE_NOW] Phase 1 — Implement C-14 ConfirmDialog/ReasonDialog with safe cancel, required reason, focus trap/restoration, Escape policy, and server-confirmed outcome language in `src/Web/src/components/`; Depends: T019; Verify: destructive and conflict journeys cannot silently overwrite and record explicit evidence.
- [ ] T021 [US6] [RUNNABLE_NOW] Phase 1 — Implement C-15 Drawer/DetailPanel and C-16 Tabs with quick-detail versus long-workflow rules, focus management, background blocking, Escape, focus return, and keyboard roving tabindex in `src/Web/src/components/`; Depends: T014, T019; Verify: drawer/tab evidence covers FR-020, FR-025, SC-005, SC-008, and SC-014.
- [ ] T022 [US1] [RUNNABLE_NOW] Phase 1 — Integrate permission-based landing in the existing route owner using valid permitted deep link → first permitted capability → permitted Dashboard fallback → safe no-authorized-capability state, without adding routes or probing forbidden metadata in `src/Web/src/App.tsx`, `src/Web/src/app/AppShell.tsx`, and existing gateway types; Depends: T013, T014, T015; Verify: route matrix fixtures cover Dashboard-permitted and Dashboard-forbidden users, unknown/expired links, session-expiry return, and no-authorized-capability recovery.
- [ ] T023 [US1] [RUNNABLE_NOW] Phase 1 — Apply the shared shell contract to `/setup`, `/dashboard`, `/configuration`, `/simulator`, `/telemetry`, `/audit`, and root route composition without changing business behavior in the existing route files under `src/Web/src/features/`; Depends: T015, T016, T017, T018, T021, T022; Verify: route inventory and shell-consistency review show active location, scope, feedback, and safe access on every included route.
- [ ] T024 [US7] [RUNNABLE_NOW] Phase 1 — Run focused shell tests plus installed-tree `npm run lint`, `npm run build`, and the repository Fast command; Depends: T023; Verify: each command records actual PASS/FAIL/BLOCKED/NOT_RUN evidence, including the known DLL-lock failure if it recurs; no package installation is permitted.
- [ ] T025 [RUNNABLE_NOW] Phase 1 — Perform the Standards review for shell and shared foundations in `checklists/phase-01-review.md`; Depends: T024; Verify: no unresolved Critical/High standards finding and all C-01–C-16 ownership boundaries are reviewed.
- [ ] T026 [RUNNABLE_NOW] Phase 1 — Perform the Specification review for shell and shared foundations in `checklists/phase-01-review.md`; Depends: T024; Verify: FR/SC/US1/US3/US6/US7 coverage and locked light/density/sidebar/landing/mobile decisions are confirmed.
- [ ] T027 [RUNNABLE_NOW] Phase 1 — Create the Phase 1 checkpoint and explicit stop in `checklists/phase-01-checkpoint.md`; Depends: T025, T026; Verify: evidence counts, capability completeness, progression decision, release decision, and next-phase boundary are recorded.

**Phase 1 stop**: `/speckit.implement` for Phase 1 stops after T027.

## Phase 2 — Dashboard and Measurement visibility

**Goal**: Deliver exception-first operational visibility for US2 and US5 without unsupported diagnosis or savings claims.

- [ ] T028 [US2] [US5] [RUNNABLE_NOW] Phase 2 — Add red evidence for dashboard exceptions, source health, coverage/freshness, zero-versus-No Data, quality states, missing chart gaps, and textual chart alternatives in `src/Web/src/test/`; Depends: T027; Verify: absent behavior fails for the requested reason before implementation.
- [ ] T029 [US2] [RUNNABLE_NOW] Phase 2 — Redesign `src/Web/src/features/dashboard/OperationalDashboard.tsx` to exception-first attention hierarchy with Missing/No Data, Data Quality, Source Health, coverage, freshness, trend drill-down, and human next actions; Depends: T028, T016, T017; Verify: US2 scenarios and FR-006/018 evidence show no root-cause, savings, automatic decision, or equipment-control claim.
- [ ] T030 [US5] [RUNNABLE_NOW] Phase 2 — Redesign `src/Web/src/features/telemetry/PointCurrentRoute.tsx` and related telemetry presentation for zero, No Data/Missing, Good/Uncertain/Bad, stale/unavailable, source/receipt timestamps, cutoff, coverage, and quality reasons; Depends: T028, T016, T017; Verify: US5 scenarios and FR-011/012 evidence distinguish every fixture without color-only status.
- [ ] T031 [US2] [US5] [RUNNABLE_NOW] Phase 2 — Implement C-17 ChartContainer with self-authored SVG, visible Missing gaps, threshold/marker semantics, metric/unit/timezone/cutoff/quality/coverage metadata, and textual/table alternative in `src/Web/src/components/` plus dashboard/telemetry consumers; Depends: T029, T030; Verify: chart evidence covers FR-012, FR-018, FR-020, SC-003, and SC-011 without a chart dependency.
- [ ] T032 [US2] [US5] [RUNNABLE_NOW] Phase 2 — Apply shared loading, empty, stale/partial, error, forbidden, blocked, and retry states to dashboard and telemetry routes in `src/Web/src/features/dashboard/OperationalDashboard.tsx`, `src/Web/src/features/telemetry/PointCurrentRoute.tsx`, and shared state components; Depends: T029, T030, T031; Verify: SC-004 and FR-004 state coverage distinguishes configuration absence, no received data, and degraded evidence.
- [ ] T033 [US2] [US5] [RUNNABLE_NOW] Phase 2 — Run focused dashboard/measurement tests, installed-tree lint/build, and Fast evidence; Depends: T032; Verify: actual PASS/FAIL/BLOCKED/NOT_RUN results are recorded and no visual PASS is claimed without approved rendering evidence.
- [ ] T034 [RUNNABLE_NOW] Phase 2 — Perform the Standards review for dashboard and measurement in `checklists/phase-02-review.md`; Depends: T033; Verify: accessibility, status semantics, data trust, and no-unsupported-claim findings have no unresolved Critical/High issue.
- [ ] T035 [RUNNABLE_NOW] Phase 2 — Perform the Specification review for dashboard and measurement in `checklists/phase-02-review.md`; Depends: T033; Verify: US2/US5, FR-006, FR-011, FR-012, FR-018, FR-020, SC-002, SC-003, SC-009, and SC-011 are traced.
- [ ] T036 [RUNNABLE_NOW] Phase 2 — Create the Phase 2 checkpoint and explicit stop in `checklists/phase-02-checkpoint.md`; Depends: T034, T035; Verify: evidence counts, capability completeness, progression decision, release decision, and next-phase boundary are recorded.

**Phase 2 stop**: `/speckit.implement` for Phase 2 stops after T036.

## Phase 3 — Configuration management

**Goal**: Migrate all seven configuration entities to the shared compact/table/form/lifecycle contracts for US3.

- [ ] T037 [US3] [RUNNABLE_NOW] Phase 3 — Add red evidence for entity lists, search/filter/sort, lifecycle/dependency states, Draft validation, first-invalid focus, unsaved changes, conflicts, and destructive confirmation in `src/Web/src/test/`; Depends: T036; Verify: absent behavior fails for the requested reason before implementation.
- [ ] T038 [US3] [RUNNABLE_NOW] Phase 3 — Adopt C-10 DataTable, C-11 FilterBar, and C-12 Pagination across `src/Web/src/features/configuration/ConfigurationManagementRoutes.tsx` and `ConfigurationManagementComponents.tsx` with compact-only density and visible sort/filter state; Depends: T037, T018; Verify: SC-006, SC-009, SC-013 and FR-013/015/027 evidence covers all configuration lists.
- [ ] T039 [US3] [RUNNABLE_NOW] Phase 3 — Migrate Sites, Areas, and Assets list/detail/form flows in `src/Web/src/features/configuration/ConfigurationManagementRoutes.tsx` and related components while preserving scope and lifecycle contracts; Depends: T038; Verify: route fixtures cover search, filter, row action, dependency, Draft, forbidden, conflict, and empty states.
- [ ] T040 [US3] [RUNNABLE_NOW] Phase 3 — Migrate Measurement Points, Data Sources, and Source Mappings in the existing configuration route/components with source mapping and dependency presentation; Depends: T038; Verify: entity-specific route and lifecycle evidence covers permission filtering, validation, stale/partial state, and safe actions.
- [ ] T041 [US3] [RUNNABLE_NOW] Phase 3 — Migrate Simulator Configurations in the existing configuration module without creating a new route or simulator business behavior; Depends: T038; Verify: configuration-to-Simulator dependency and lifecycle evidence records the server-confirmed outcome.
- [ ] T042 [US3] [RUNNABLE_NOW] Phase 3 — Apply C-13 FormSection/FieldErrorSummary and C-14 Confirm/ReasonDialog to Draft forms, required fields, first-invalid focus, preserved input, unsaved changes, conflicts, and destructive lifecycle actions in `src/Web/src/features/configuration/`; Depends: T039, T040, T041, T019, T020; Verify: FR-008/009/014, SC-007, and all applicable error/blocked/conflict/retry states are exercised.
- [ ] T043 [US3] [RUNNABLE_NOW] Phase 3 — Add explicit lifecycle, dependency, unavailable-action, loading, empty, stale/partial, forbidden, blocked, conflict, and retry presentation across configuration entities in `src/Web/src/features/configuration/`; Depends: T042, T017; Verify: no action implies completion before the existing server-confirmed outcome.
- [ ] T044 [US3] [RUNNABLE_NOW] Phase 3 — Run focused configuration tests, installed-tree lint/build, and Fast evidence; Depends: T043; Verify: actual PASS/FAIL/BLOCKED/NOT_RUN evidence is recorded without package installation or backend changes.
- [ ] T045 [RUNNABLE_NOW] Phase 3 — Perform the Standards review for configuration in `checklists/phase-03-review.md`; Depends: T044; Verify: table density, form accessibility, lifecycle safety, and component ownership have no unresolved Critical/High finding.
- [ ] T046 [RUNNABLE_NOW] Phase 3 — Perform the Specification review for configuration in `checklists/phase-03-review.md`; Depends: T044; Verify: US3, FR-007/008/009/013/014/022/023/025/027, SC-004/006/007/008/009/013, and all seven entities are traced.
- [ ] T047 [RUNNABLE_NOW] Phase 3 — Create the Phase 3 checkpoint and explicit stop in `checklists/phase-03-checkpoint.md`; Depends: T045, T046; Verify: evidence counts, capability completeness, progression decision, release decision, and next-phase boundary are recorded.

**Phase 3 stop**: `/speckit.implement` for Phase 3 stops after T047.

## Phase 4 — Simulator and Audit

**Goal**: Deliver reproducible Simulator and investigation-oriented Audit workspaces for US4 and US6.

- [ ] T048 [US4] [US6] [RUNNABLE_NOW] Phase 4 — Add red evidence for Simulator context/run state/history/outcomes and Audit filters/detail/redaction/deep links in `src/Web/src/test/`; Depends: T047; Verify: absent behavior fails for the requested reason before implementation.
- [ ] T049 [US4] [RUNNABLE_NOW] Phase 4 — Migrate `src/Web/src/features/simulator/SimulatorRoute.tsx` to context → current run state → permitted controls → counters/diagnostics → compact run history → outcome/next action using C-03, C-05, C-08, C-10, C-12, and C-15; Depends: T048, T021; Verify: US4 scenarios cover scope, configuration, permission, run identifier, history, and no physical-equipment-control implication.
- [ ] T050 [US4] [RUNNABLE_NOW] Phase 4 — Add Simulator success, failure, conflict, blocked, pending, processing, completed-with-errors, retry, inactive-prerequisite, and session/error states in `src/Web/src/features/simulator/SimulatorRoute.tsx`; Depends: T049, T017; Verify: FR-010/024, SC-002/004/010 evidence shows explicit reason and next action.
- [ ] T051 [US6] [RUNNABLE_NOW] Phase 4 — Migrate `src/Web/src/features/audit/AuditRoute.tsx` to C-10 compact table, C-11 filters, C-12 pagination, active filter/result count, actor/action/entity/time/scope/outcome/correlation columns, and safe scope handling; Depends: T048, T018; Verify: US6 scenario 1 and FR-015/027, SC-008/009/013 evidence preserve investigation context.
- [ ] T052 [US6] [RUNNABLE_NOW] Phase 4 — Implement Audit C-15 drawer/split inspection, redacted before/after diff, valid deep link, safe forbidden/not-found, and no-secret/no-metadata disclosure in `src/Web/src/features/audit/AuditRoute.tsx` and shared components; Depends: T051, T021, T022; Verify: US6 scenarios 2–3, FR-015/020/023/025, and SC-008 evidence cover redaction, focus, and safe direct access.
- [ ] T053 [US4] [US6] [RUNNABLE_NOW] Phase 4 — Run Simulator/Audit route regression and failure-path tests across existing APIs/gateways without adding routes or backend behavior in `src/Web/src/test/` and feature files; Depends: T050, T052; Verify: included route inventory, Feature 003 behavior, and all non-happy-path states record actual evidence.
- [ ] T054 [US4] [US6] [RUNNABLE_NOW] Phase 4 — Run focused Simulator/Audit tests, installed-tree lint/build, and Fast evidence; Depends: T053; Verify: actual PASS/FAIL/BLOCKED/NOT_RUN results are recorded and no visual PASS is claimed without rendering evidence.
- [ ] T055 [RUNNABLE_NOW] Phase 4 — Perform the Standards review for Simulator and Audit in `checklists/phase-04-review.md`; Depends: T054; Verify: no unresolved Critical/High finding for control semantics, redaction, status, focus, or investigation density.
- [ ] T056 [RUNNABLE_NOW] Phase 4 — Perform the Specification review for Simulator and Audit in `checklists/phase-04-review.md`; Depends: T054; Verify: US4/US6, FR-010/015/023/024/027, SC-002/004/008/009/010/013 and routes are traced.
- [ ] T057 [RUNNABLE_NOW] Phase 4 — Create the Phase 4 checkpoint and explicit stop in `checklists/phase-04-checkpoint.md`; Depends: T055, T056; Verify: evidence counts, capability completeness, progression decision, release decision, and next-phase boundary are recorded.

**Phase 4 stop**: `/speckit.implement` for Phase 4 stops after T057.

## Phase 5 — Responsive, accessibility, consistency, and regression hardening

**Goal**: Validate desktop/tablet first-class behavior, mobile non-regression, accessibility, consistency, and Feature 003 compatibility.

- [ ] T058 [US7] [RUNNABLE_NOW] Phase 5 — Harden wide desktop (≥1280), standard desktop, tablet (768–1279), and mobile non-regression behavior in `src/Web/src/App.css`, `src/Web/src/index.css`, `src/Web/src/app/AppShell.tsx`, and shared components without adding a breakpoint library; Depends: T057; Verify: responsive evidence records essential tablet data/actions, rail/drawer behavior, table/evidence scroll treatment, and safe mobile unsupported-state messaging.
- [ ] T059 [US7] [RUNNABLE_NOW] Phase 5 — Complete keyboard-only journeys, skip link, route-title focus, dialog/drawer focus trap and restoration, form error association, live regions, and touch-safe targets in `src/Web/src/test/` and shared components; Depends: T057; Verify: WCAG 2.2 AA target checks record actual results without claiming certification.
- [ ] T060 [US7] [RUNNABLE_NOW] Phase 5 — Verify contrast/token use, visible focus, reduced motion, non-color status/quality cues, semantic headings, labels, and chart/table alternatives in `src/Web/src/index.css`, `src/Web/src/App.css`, and `contracts/test-strategy.md`; Depends: T057; Verify: manual/static evidence records PASS/FAIL/BLOCKED and no unsupported automation is called PASS.
- [ ] T061 [US7] [RUNNABLE_NOW] Phase 5 — Conduct the visual consistency review for shell, dashboard, configuration, Simulator, Measurement, Audit, tables, forms, status, feedback, and terminology in `checklists/phase-05-visual-review.md`; Depends: T058, T059, T060; Verify: every high-severity inconsistency has a disposition and no visual PASS is claimed without approved screenshots/rendering evidence.
- [ ] T062 [RUNNABLE_NOW] Phase 5 — Run Feature 003 regression and included-route acceptance checks using `specs/003-operational-configuration-workspace/checklists/acceptance-traceability.md`, `phase-01-verification.md`, `phase-02-verification.md`, `phase-04-checkpoint.md`, `phase-05-checkpoint.md`, and `post-phase-06-corrective-review.md`; execute the existing `npm run lint`/`npm run build` (from `src/Web`) and `scripts/harness.ps1 -Mode Fast/Full -Feature 003-operational-configuration-workspace` commands where permitted, recording PostgreSQL target `127.0.0.1:5433/iump_dev` and any blocked result in `checklists/phase-05-acceptance.md`; Depends: T061, T053; Verify: permitted workflows remain reachable, backend contracts/authorization/PostgreSQL behavior are unchanged, and blocked evidence remains blocked.
- [ ] T063 [RUNNABLE_NOW] Phase 5 — Run Fast verification and record the actual result in `checklists/phase-05-verification.md`; Depends: T062; Verify: Fast evidence records PASS/FAIL/BLOCKED/NOT_RUN with the known DLL-lock condition if present.
- [ ] T064 [US7] [RUNNABLE_NOW] Phase 5 — Execute the SC-002 representative P0 usability evidence protocol for navigate, find a configuration, inspect current data, investigate an exception, and review a run; record `successful_unassisted_attempts`, `valid_attempts`, excluded/invalid attempts, participant role/context (no personal or secret data), and limitations in `checklists/phase-05-usability-evidence.md`; Depends: T063; Verify: `successful_unassisted_attempts / valid_attempts * 100 >= 90%` is required, with the evidence owner, participant set, attempt rules, evidence path, and honest PASS/FAIL/BLOCKED/NOT_RUN status recorded (no facilitator intervention and zero critical misunderstandings of scope/status).
- [ ] T065 [RUNNABLE_NOW] Phase 5 — Run mandatory Full verification against the approved repository environment and `127.0.0.1:5433/iump_dev` where the harness requires database evidence, without port 5432 or substitute stores, and record results in `checklists/phase-05-verification.md`; Depends: T063; Verify: Full evidence records exact PASS/FAIL/BLOCKED/NOT_RUN and never reclassifies a blocked prerequisite as PASS.
- [ ] T066 [BLOCKED_BY_PACKAGE_POLICY] Phase 5 — Record browser/automated accessibility evidence for the unavailable runner or axe/Playwright capability in `checklists/phase-05-verification.md` without installing packages; Depends: T063; Verify: blocker is classified `BLOCKED_BY_PACKAGE_POLICY` and remains an evidence leaf that does not block runnable review or traceability tasks.
- [ ] T067 [RUNNABLE_NOW] Phase 5 — Run architecture, repository-policy, scope, secret, dependency, and no-production-source checks in `checklists/phase-05-checkpoint.md`; Depends: T063; References/Inspect: T064, T065, T066; Verify: no backend/API/database/package/lockfile/secret/generated-binary change and no out-of-scope capability is present.
- [ ] T068 [RUNNABLE_NOW] Phase 5 — Complete final FR/SC, user-story, C-01–C-17, route, state, and acceptance traceability in `checklists/phase-05-acceptance.md`; Depends: T062, T063, T067; References/Inspect: T064, T065, T066; Verify: every FR-001–FR-028, SC-001–SC-016, component contract, included route, and non-happy-path state has evidence or an honest blocker.
- [ ] T069 [RUNNABLE_NOW] Phase 5 — Perform the final Standards review in `checklists/phase-05-review.md`; Depends: T068; Verify: no unresolved Critical/High standards finding and all repository policy constraints are reviewed.
- [ ] T070 [RUNNABLE_NOW] Phase 5 — Perform the final Specification review in `checklists/phase-05-review.md`; Depends: T068; Verify: no unresolved Critical/High spec mismatch and all locked clarifications remain represented.
- [ ] T071 [RUNNABLE_NOW] Phase 5 — Record the final release checkpoint and explicit stop in `checklists/phase-05-checkpoint.md`; Depends: T069, T070; References/Inspect: T064, T065, T066, T067, T068; Verify: PASS/FAIL/BLOCKED/NOT_RUN counts, capability completeness, Planning/Implementation/Release readiness, next command, and explicit stop are recorded; Release-ready remains NO while mandatory evidence is blocked, FAIL, or NOT_RUN.

**Phase 5 stop**: `/speckit.implement` for Phase 5 stops after T071.

## Dependency graph and execution order

```text
T001 → T002 → T003 → T004 → T005 → T006 → T007 → T008 → T009 → T010
T010 → Phase 1 T011–T027 → Phase 2 T028–T036 → Phase 3 T037–T047
      → Phase 4 T048–T057 → Phase 5 T058–T071
```

- No task has a forward dependency; all dependencies name an earlier task ID.
- Phase 0 is the only governance prerequisite for green application work.
- Within each implementation phase, red evidence precedes implementation, reviews precede the
  checkpoint, and the checkpoint is the stop boundary.
- T066 is a blocked evidence leaf; it is referenced by review/checkpoint work but is not a
  pass-required dependency for runnable traceability.
- T064 is the dedicated SC-002 usability evidence task. Its result is recorded honestly and does
  not turn an unavailable participant/evidence channel into a PASS.
- Safe parallel groups are explicitly marked `[P]`; tasks that share `AppShell`, `App.css`, or
  shared component files remain ordered.

## Traceability

### Functional requirements

| Requirements | Tasks |
|---|---|
| FR-001–FR-005, FR-025 | T013–T015, T022–T023 |
| FR-006 | T029, T032, T035 |
| FR-007–FR-009, FR-014 | T038–T043, T046 |
| FR-010 | T049–T050, T056 |
| FR-011–FR-012 | T030–T032, T031, T035 |
| FR-013 | T018, T038, T051 |
| FR-015 | T018, T051–T052 |
| FR-016–FR-018 | T012, T016–T017, T029–T031, T060 |
| FR-019–FR-021 | T014, T022–T023, T058–T060 |
| FR-022–FR-023 | T002, T022, T067–T068 |
| FR-024 | T017, T050, T062, T065, T068 |
| FR-026 | T002, T005, T067 |
| FR-027 | T018, T038, T049, T051, T061 |
| FR-028 | T002, T022, T068 |

### Success criteria

| Criteria | Tasks |
|---|---|
| SC-001 | T013–T015, T023, T025–T027 |
| SC-002 | T029, T049–T050, T062, T064, T068 |
| SC-003 | T030–T031, T060, T068 |
| SC-004 | T017, T032, T043, T050, T068 |
| SC-005 | T011, T014, T018–T021, T059 |
| SC-006 | T014, T018, T023, T058 |
| SC-007 | T019–T020, T042, T059 |
| SC-008 | T002, T022, T051–T052, T067–T068 |
| SC-009 | T025–T026, T034–T035, T045–T046, T055–T056, T061, T069–T070 |
| SC-010 | T002, T022, T050, T067–T068 |
| SC-011 | T015, T029–T032, T068 |
| SC-012 | T053, T062, T065, T068 |
| SC-013 | T018, T038, T049, T051, T061, T068 |
| SC-014 | T014, T021, T058–T059 |
| SC-015 | T002, T022, T052, T068 |
| SC-016 | T058, T062, T064–T068 |

### Component contracts

| Contract | Implementation and verification tasks |
|---|---|
| C-01 AppShell | T013, T023, T025–T027, T059 |
| C-02 Sidebar/Rail/Drawer | T014, T022–T023, T058–T059 |
| C-03 Top/Context Bar | T015, T023, T025–T027 |
| C-04 Page Header/Breadcrumb | T015, T023, T059 |
| C-05 Operational Status Badge | T016, T029–T032, T060 |
| C-06 Data Quality Indicator | T016, T030–T032, T060 |
| C-07 Freshness Indicator | T016, T029–T032, T060 |
| C-08 Feedback Banner/Notice | T017, T023, T032, T050 |
| C-09 Non-happy-path state set | T017, T032, T043, T050, T068 |
| C-10 DataTable | T018, T038, T049, T051, T061 |
| C-11 FilterBar | T018, T038, T051 |
| C-12 Pagination | T018, T038, T049, T051 |
| C-13 FormSection/Field/ErrorSummary | T019, T042, T059 |
| C-14 ConfirmDialog/ReasonDialog | T020, T042, T059 |
| C-15 Drawer/DetailPanel | T021, T049, T052, T059 |
| C-16 Tabs | T021, T030, T052, T059 |
| C-17 ChartContainer/text alternative | T031, T060, T068 |

### Included route coverage

| Route | Migration tasks | Regression/verification |
|---|---|---|
| `/` landing | T022 | T011, T024, T062, T068 |
| `/setup` | T023 | T024, T062, T068 |
| `/dashboard` | T023, T029, T032 | T033, T062, T068 |
| `/telemetry` | T023, T030–T032 | T033, T062, T068 |
| `/configuration` and seven entities | T023, T038–T043 | T044, T062, T068 |
| `/simulator` | T023, T049–T050 | T053–T054, T062, T068 |
| `/audit` | T023, T051–T052 | T053–T054, T062, T068 |

### Non-happy-path coverage

Loading, empty/no-scope/no-filter-match, error, forbidden, expired-session, stale/partial,
conflict, blocked, pending/processing, completed-with-errors, retry, unavailable, invalid/expired
deep link, no-authorized-capability, zero-versus-No Data, and missing chart gaps are implemented or
verified by T017, T022, T032, T041–T043, T050, T052, T058–T060, and T068.

## Review and evidence rules

- Every implementation phase has separate Standards and Specification review tasks followed by an
  explicit checkpoint and stop. Phase 0 T009 contains separate Standards and Specification review
  sections after the fresh T008 analysis gate, followed by T010.
- Blocked evidence is never PASS. `BLOCKED_BY_PACKAGE_POLICY` is used for unavailable browser/
  accessibility automation only; no package installation is authorized.
- The known Fast harness DLL-lock result remains an actual FAIL when it recurs; it is not converted
  to PASS or NOT_RUN.
- Full evidence must use the approved PostgreSQL target when required: `127.0.0.1:5433/iump_dev`;
  port 5432, SQLite, in-memory substitutes, Docker, and secret disclosure are prohibited.
- Next command after this task-generation phase: `/speckit.analyze`.
