# Quickstart: Industrial Operations UI/UX Redesign

**Feature**: 004 Industrial Operations UI/UX Redesign
**Created**: 2026-08-04
**Scope**: This feature is presentation-only (no backend/API/database changes). This guide covers
validation of the redesigned Web experience during implementation phases.

## 1. Prerequisites

- Installed: Node 24.16.0 / npm 11.13.0, .NET SDK 10.0.300, existing `src/Web/node_modules`
  (no installs; repository package policy).
- Backend API/Worker runnable via existing scripts for session/workspace data; PostgreSQL at
  `127.0.0.1:5433/iump_dev` when available (never substitute another database).
- Reading: `contracts/design-system.md`, `contracts/information-architecture.md`,
  `contracts/responsive-accessibility.md`, `contracts/component-contracts.md`,
  `contracts/route-and-permission-matrix.md`, `contracts/migration-phases.md`,
  `contracts/test-strategy.md`, `spec.md`, `plan.md`, `data-model.md`.

## 2. Commands

From repository root:

```powershell
# Frontend static checks (installed tree only — no npm install/ci)
Set-Location .\src\Web
npm run lint
npm run build

# Repository verification
& .\scripts\harness.ps1 -Mode Fast            # iteration
& .\scripts\harness.ps1 -Mode Full -Feature 004-industrial-operations-ui-ux-redesign  # before completion claims
& .\scripts\build.ps1
& .\scripts\test.ps1
```

Dev server (Vite proxy `/api` → `http://localhost:5000`):

```powershell
Set-Location .\src\Web
npm run dev
```

## 3. Validation scenarios (manual, per phase)

| # | Scenario | Expected | Contract |
|---|---|---|---|
| 1 | Log in with a permitted user; visit each included area | Active section, scope, timezone, cutoff visible; back navigation preserves context | information-architecture.md §2–3; FR-001/005 |
| 2 | Deep link `/dashboard` and an unauthorized route | Deep link restores; unauthorized shows safe forbidden/not-found with next action; no metadata leak | route-and-permission-matrix.md §3/6; FR-023/028 |
| 3 | Landing after login (no deep link) | First permitted priority capability; Dashboard fallback when none; never via forbidden route | route-and-permission-matrix.md §3; FR-028 |
| 4 | Resize to 1280–1279 and below 768 | Desktop: full sidebar; tablet: rail + drawer with focus trap/Escape/focus-return; mobile: rail preserved, non-regression, unsupported flows direct to desktop/tablet | responsive-accessibility.md §1–2; FR-002/019/020 |
| 5 | Keyboard-only: navigate, open drawer, table row action, dialog, pagination | Visible focus, logical order, accessible names, skip link | responsive-accessibility.md §3; FR-020 |
| 6 | Fixture: valid zero, No Data, Good/Uncertain/Bad, stale, unavailable | Zero ≠ No Data; each quality state has text+icon+reason; chart Missing = gap; text alternative present | design-system.md §3/7; FR-011/012 |
| 7 | Configuration list/edit: invalid field, conflict, destructive action | Field-level error + summary + first-invalid focus; conflict offers reload/compare; confirmation + reason for destructive | component-contracts.md C-10/13/14; FR-008/009/014 |
| 8 | Simulator run outcomes and Audit detail | Success/failure/blocked/conflict/retry visible with reference id; audit redacted diff; pagination context kept | component-contracts.md; FR-010/015/024 |
| 9 | States: loading, empty (no records vs no filter match), error, forbidden, blocked, retry | Context retained; impact + next action; recovery path | component-contracts.md C-09; FR-004 |

## 4. Evidence rules

- Every phase checkpoint records PASS/FAIL/BLOCKED/NOT_RUN with the exact command/manual evidence.
- Visual/manual QA claims require actual approved rendering evidence; never invented.
- Frontend test sources are type-checked by build; runtime execution is NOT_RUN until a runner is
  approved (BLOCKED_BY_PACKAGE_POLICY otherwise) — see contracts/test-strategy.md.
- Feature 004 never claims release readiness; Full harness blocked checks stay blocked.

## 5. Out of scope reminders

No backend/API/database changes, no new packages/fonts/icons/charts, no dark theme, no density
switch, no new routes, no mobile acceptance suite, no equipment control or savings/root-cause
claims (spec.md Scope and Evidence Boundaries; plan.md).