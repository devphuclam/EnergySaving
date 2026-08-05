# Feature 004 Implementation File Map

**Feature**: 004 Industrial Operations UI/UX Redesign
**Status**: Planning-only ownership map; every path marked **planned** is to be created by the
corresponding implementation task. No file is created by this remediation.

This map is a canonical index for the exact ownership repeated in `tasks.md`. Existing source
files were inspected before selecting planned names. The current repository has one existing
frontend test (`src/Web/src/test/app-shell.test.tsx`) and an empty `src/Web/src/components/`
directory; all other component/test paths below are planned files, not claims of current existence.

| Contract | Exact source owner(s) | Exact test/evidence owner(s) | Owning task(s) | Consuming routes |
|---|---|---|---|---|
| C-01 AppShell | Modify existing `src/Web/src/app/AppShell.tsx`; modify existing `src/Web/src/App.tsx` | Extend existing `src/Web/src/test/app-shell.test.tsx`; create planned `src/Web/src/test/route-title-focus.test.tsx`; evidence `specs/004-industrial-operations-ui-ux-redesign/checklists/phase-01-verification.md` | T013, T022, T023 | `/`, `/setup`, `/dashboard`, `/configuration`, `/simulator`, `/telemetry`, `/audit` |
| C-02 Sidebar/Rail/Drawer | Modify existing `src/Web/src/app/AppShell.tsx`; create planned `src/Web/src/components/navigation/NavigationModel.ts`, `src/Web/src/components/navigation/Sidebar.tsx`, `src/Web/src/components/navigation/Rail.tsx`, `src/Web/src/components/navigation/NavigationDrawer.tsx` | Create planned `src/Web/src/test/navigation.test.tsx`; evidence `specs/004-industrial-operations-ui-ux-redesign/checklists/phase-01-verification.md` | T014, T022, T023, T058, T059 | all included routes |
| C-03 Top/Context Bar | Modify existing `src/Web/src/app/AppShell.tsx`; create planned `src/Web/src/components/context/ContextBar.tsx` | Create planned `src/Web/src/test/context-header.test.tsx`; evidence `specs/004-industrial-operations-ui-ux-redesign/checklists/phase-01-verification.md` | T015, T023, T049 | all included routes |
| C-04 Page Header/Breadcrumb | Create planned `src/Web/src/components/context/PageHeader.tsx` and `src/Web/src/components/context/Breadcrumbs.tsx`; modify existing `src/Web/src/app/AppShell.tsx` | Extend planned `src/Web/src/test/context-header.test.tsx`; evidence `specs/004-industrial-operations-ui-ux-redesign/checklists/phase-01-verification.md` | T015, T023, T059 | all included routes |
| C-05 Operational Status Badge | Create planned `src/Web/src/components/status/OperationalStatusBadge.tsx` | Create planned `src/Web/src/test/status-indicators.test.tsx`; evidence `specs/004-industrial-operations-ui-ux-redesign/checklists/phase-01-verification.md` | T016, T029, T030, T049, T050, T060 | `/dashboard`, `/telemetry`, `/configuration`, `/simulator`, `/audit` |
| C-06 Data Quality Indicator | Create planned `src/Web/src/components/status/DataQualityIndicator.tsx` | Extend planned `src/Web/src/test/status-indicators.test.tsx`; evidence `specs/004-industrial-operations-ui-ux-redesign/checklists/phase-01-verification.md` | T016, T029, T030, T060 | `/dashboard`, `/telemetry`, `/configuration` |
| C-07 Freshness Indicator | Create planned `src/Web/src/components/status/FreshnessIndicator.tsx` | Extend planned `src/Web/src/test/status-indicators.test.tsx`; evidence `specs/004-industrial-operations-ui-ux-redesign/checklists/phase-01-verification.md` | T016, T029, T030, T060 | `/dashboard`, `/telemetry`, `/configuration` |
| C-08 Feedback Banner/Notice | Create planned `src/Web/src/components/feedback/FeedbackBanner.tsx` | Create planned `src/Web/src/test/state-presentations.test.tsx`; evidence `specs/004-industrial-operations-ui-ux-redesign/checklists/phase-01-verification.md` | T017, T023, T029, T032, T043, T049, T050 | all included routes |
| C-09 Non-happy-path state set | Create planned `src/Web/src/components/feedback/LoadingState.tsx`, `src/Web/src/components/feedback/EmptyState.tsx`, `src/Web/src/components/feedback/ErrorState.tsx`, `src/Web/src/components/feedback/ForbiddenState.tsx`, `src/Web/src/components/feedback/ConflictState.tsx`, `src/Web/src/components/feedback/BlockedState.tsx`, `src/Web/src/components/feedback/RetryState.tsx` | Extend planned `src/Web/src/test/state-presentations.test.tsx`; create planned `src/Web/src/test/dashboard-telemetry-states.test.tsx`, `src/Web/src/test/configuration-lifecycle-states.test.tsx`, and `src/Web/src/test/simulator-states.test.tsx`; evidence in phase-01 through phase-04 verification checklists | T017, T032, T043, T050, T052 | all included routes |
| C-10 DataTable | Create planned `src/Web/src/components/data/DataTable.tsx` | Create planned `src/Web/src/test/data-table.test.tsx`; extend planned `configuration-tables.test.tsx` and `audit-list.test.tsx`; evidence in phase-01, phase-03, and phase-04 verification checklists | T018, T038, T049, T051, T058 | `/configuration`, `/simulator`, `/telemetry`, `/audit` |
| C-11 FilterBar | Create planned `src/Web/src/components/data/FilterBar.tsx` | Extend planned `src/Web/src/test/data-table.test.tsx`, `configuration-tables.test.tsx`, and `audit-list.test.tsx`; evidence in phase-01, phase-03, and phase-04 verification checklists | T018, T038, T051 | `/configuration`, `/audit` |
| C-12 Pagination | Create planned `src/Web/src/components/data/Pagination.tsx` | Extend planned `src/Web/src/test/data-table.test.tsx`, `configuration-tables.test.tsx`, and `audit-list.test.tsx`; evidence in phase-01, phase-03, and phase-04 verification checklists | T018, T038, T049, T051 | `/configuration`, `/simulator`, `/audit` |
| C-13 FormSection/Field/ErrorSummary | Create planned `src/Web/src/components/forms/FormSection.tsx`, `src/Web/src/components/forms/Field.tsx`, `src/Web/src/components/forms/FieldErrorSummary.tsx`, and `src/Web/src/components/forms/UnsavedChangesGuard.tsx` | Create planned `src/Web/src/test/configuration-forms.test.tsx`; evidence in phase-01 and phase-03 verification checklists | T019, T042, T059 | `/configuration` |
| C-14 ConfirmDialog/ReasonDialog | Create planned `src/Web/src/components/dialogs/ConfirmDialog.tsx` and `src/Web/src/components/dialogs/ReasonDialog.tsx` | Create planned `src/Web/src/test/dialog-focus.test.tsx`; evidence in phase-01 and phase-03 verification checklists | T020, T042, T059 | `/configuration` |
| C-15 Drawer/DetailPanel | Create planned `src/Web/src/components/disclosure/Drawer.tsx` and `src/Web/src/components/disclosure/DetailPanel.tsx` | Create planned `src/Web/src/test/disclosure-focus.test.tsx` and `src/Web/src/test/audit-detail.test.tsx`; evidence in phase-01 and phase-04 verification checklists | T021, T049, T052, T059 | `/simulator`, `/telemetry`, `/audit` |
| C-16 Tabs | Create planned `src/Web/src/components/disclosure/Tabs.tsx` | Extend planned `src/Web/src/test/disclosure-focus.test.tsx`; evidence in phase-01 and phase-04 verification checklists | T021, T052, T059 | `/telemetry`, `/audit` |
| C-17 ChartContainer/text alternative | Create planned `src/Web/src/components/charts/ChartContainer.tsx` and `src/Web/src/components/charts/ChartTextAlternative.tsx` | Create planned `src/Web/src/test/chart-container.test.tsx`; evidence in phase-02 verification checklist | T031, T060 | `/dashboard`, `/telemetry` |

## Route and regression file owners

| Route/workflow | Exact route owner(s) | Exact red/regression test owner(s) |
|---|---|---|
| Root landing and shell | Existing `src/Web/src/App.tsx`, existing `src/Web/src/app/AppShell.tsx` | Existing `src/Web/src/test/app-shell.test.tsx`; planned `src/Web/src/test/route-fixtures.ts` and `src/Web/src/test/landing-routing.test.tsx` |
| Setup | Existing `src/Web/src/features/setup/SetupWizard.tsx` | Phase-01 evidence `specs/004-industrial-operations-ui-ux-redesign/checklists/phase-01-verification.md` |
| Dashboard | Existing `src/Web/src/features/dashboard/OperationalDashboard.tsx` | Planned `src/Web/src/test/dashboard-telemetry-red-evidence.test.tsx` and `dashboard-telemetry-states.test.tsx` |
| Telemetry | Existing `src/Web/src/features/telemetry/PointCurrentRoute.tsx`; existing `src/Web/src/features/telemetry/telemetryRefreshCoordinator.ts` | Planned `src/Web/src/test/dashboard-telemetry-red-evidence.test.tsx` and `dashboard-telemetry-states.test.tsx` |
| Configuration | Existing `src/Web/src/features/configuration/ConfigurationRoutes.tsx`, `src/Web/src/features/configuration/ConfigurationManagementRoutes.tsx`, and `src/Web/src/features/configuration/ConfigurationManagementComponents.tsx` | Planned `src/Web/src/test/configuration-red-evidence.test.tsx`, `src/Web/src/test/configuration-tables.test.tsx`, `src/Web/src/test/configuration-entity-flows.test.tsx`, `src/Web/src/test/configuration-source-mapping.test.tsx`, `src/Web/src/test/configuration-forms.test.tsx`, and `src/Web/src/test/configuration-lifecycle-states.test.tsx` |
| Simulator | Existing `src/Web/src/features/simulator/SimulatorRoute.tsx`; existing `src/Web/src/gateways/simulatorRetry.ts` | Planned `src/Web/src/test/simulator-red-evidence.test.tsx`, `simulator-states.test.tsx`, and `simulator-regression.test.tsx` |
| Audit | Existing `src/Web/src/features/audit/AuditRoute.tsx`; existing `src/Web/src/gateways/webGateways.ts` | Planned `src/Web/src/test/audit-red-evidence.test.tsx`, `audit-list.test.tsx`, `audit-detail.test.tsx`, and `audit-regression.test.tsx` |

## Evidence artifact owners

All phase verification tasks write to their named artifact, never to an unnamed “tests” or
“manual evidence” bucket:

- Phase 0 governance: `specs/004-industrial-operations-ui-ux-redesign/checklists/phase-00-analysis-remediation.md` and `phase-00-planning-checkpoint.md`.
- Phase 1 shell/foundations: `specs/004-industrial-operations-ui-ux-redesign/checklists/phase-01-verification.md`, `phase-01-review.md`, and `phase-01-checkpoint.md`.
- Phase 2 dashboard/telemetry: `specs/004-industrial-operations-ui-ux-redesign/checklists/phase-02-verification.md`, `phase-02-review.md`, and `phase-02-checkpoint.md`.
- Phase 3 configuration: `specs/004-industrial-operations-ui-ux-redesign/checklists/phase-03-verification.md`, `phase-03-review.md`, and `phase-03-checkpoint.md`.
- Phase 4 Simulator/Audit: `specs/004-industrial-operations-ui-ux-redesign/checklists/phase-04-verification.md`, `phase-04-review.md`, and `phase-04-checkpoint.md`.
- Phase 5 hardening/release: `specs/004-industrial-operations-ui-ux-redesign/checklists/phase-05-verification.md`, `phase-05-usability-evidence.md`, `phase-05-acceptance.md`, `phase-05-visual-review.md`, `phase-05-review.md`, and `phase-05-checkpoint.md`.

No implementation file, test file, package, route, or backend artifact is created by this
documentation remediation.
