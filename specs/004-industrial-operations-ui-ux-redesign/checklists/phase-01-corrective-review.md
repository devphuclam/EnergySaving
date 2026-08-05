# Feature 004 Phase 1 Corrective Review

**Feature**: `004-industrial-operations-ui-ux-redesign`
**Phase**: 1 — Application shell and shared foundations (T011–T027)
**Baseline**: `1cc45e3e64636e093aa0e714f0b2ecc08968ecbb`
**Branch**: `feat/004-phase-01-shell-foundations`
**Date**: 2026-08-05
**Scope**: Corrective closure of the four High findings (C1–C4) from the Phase 1 review. Frontend-only;
no backend/API/Worker/database, package, lockfile, route, or Docker change.

## Findings and corrections

### C1 (High) — Navigation permission contract not enforced on every entry path

**Observed**: `canAccessNavigationItem` opened `audit` when the capability collection was absent
(legacy shape), contradicting the strictly-permission-filtered invariant; programmatic `navigate`
had no route validation; root workspace-status failure had no handling; the transition contract
lacked `setup-required` and `navigation-denied` events; deep-link `popstate` could revive a denied
route; the brand link sent a raw `navigate`.

**Corrected** in `src/Web/src/components/navigation/NavigationModel.ts` and
`src/Web/src/app/AppShell.tsx`:

- `canAccessNavigationItem` now fails closed: a capability-protected item is visible only when the
  session exposes a capability collection containing it.
- New canonical predicate `isNavigationRouteAvailable(route, session, setupRequired)` is used by
  every entry path (deep link, root landing, brand, sidebar, rail, drawer, popstate, programmatic
  callbacks, session restoration). Setup is reachable only when the workspace status requires it.
- New `firstPermittedNavigationRoute(session, setupRequired)` returns the safe home destination
  (Dashboard first, then the shared priority order).
- `AppShell.requestNavigation` validates the route before any navigation; blocked attempts emit the
  `navigation-denied` transition with a safe recovery route instead of rendering the denied content.
- The `popstate` handler validates the popped route through the same predicate before resolving.
- The brand link navigates to `firstPermittedNavigationRoute` through `requestNavigation`.
- Root-only workspace-status failures map through `workspaceStatusFailureSession` (401 → expired,
  403 → forbidden, other → error); non-root deep-link failures resolve with `setupRequired=false`
  so Setup is never fabricated (fail closed).
- `AppShellTransition` gains `setup-required` and `navigation-denied`; `AppShellState` gains
  `setupRequired` and `landingPresentation`.

### C2 (High) — Tablet rail/CSS breakpoint consistency

**Observed**: duplicate `.content` rules with conflicting `max-width` (1280px vs 1200px); an
obsolete `@media (max-width: 820px)` block that switched `.layout` to a single column and the
sidebar to a horizontal row, contradicting the rail/drawer contract in the 768–1279px tier; mixed
layout dimensions.

**Corrected** in `src/Web/src/App.css`:

- Consolidated the duplicate `.content` rules into one `max-width: 1280px` rule.
- Removed the obsolete `max-width: 820px` media block (legacy horizontal sidebar rules and the
  `grid-template-columns: 1fr` layout override).
- The tablet tier is now governed by a single `@media (max-width: 1279px)` block: layout column
  `4.25rem` matches the `.sidebar-rail` width; expanded sidebar (15rem column) applies at ≥1280px;
  the `@media (max-width: 767px)` block retains the `4.25rem` rail column (mobile non-regression).
- The JS side is exposed as the pure function `viewportNavigationMode(width)` with the contract
  ≥1280 expanded, below 1280 rail; it is exercised by compile-visible checks.

### C3 (High) — Unsaved-change guard covered only `beforeunload`; error summary auto-focused

**Observed**: `UnsavedChangesGuard` registered only a `beforeunload` handler; shell navigation could
drop unsaved work. `FieldErrorSummary` focused mounted errors without an explicit activation.

**Corrected**:

- `src/Web/src/components/forms/UnsavedChangesGuard.tsx` now maintains a module-level registry
  (`registerUnsavedChange`/`clearUnsavedChange`/`hasUnsavedChanges`/`unsavedChangesMessage`) plus
  the existing component form. `AppShell.requestNavigation` consults `hasUnsavedChanges()` and shows
  a ConfirmDialog (`pendingNavigation`) before leaving; confirming performs the navigation, cancel
  stays and restores the previous history entry for `popstate` cases.
- `src/Web/src/components/forms/FieldErrorSummary.tsx` now exposes `firstErrorFieldId(errors)` and
  an `activate` prop (default `false`); focus moves only on explicit activation.

### C4 (High) — Dialog accessibility and required-reason behavior

**Observed**: `ConfirmDialog`/`ReasonDialog` used a hard-coded `id="reason-dialog-input"` (duplicate
ID risk) and no unique title/description wiring; an empty required reason submitted silently with
no error or focus.

**Corrected**:

- `ConfirmDialog.tsx` and `ReasonDialog.tsx` generate unique `useId`-based title/description/input
  IDs wired to `aria-labelledby`/`aria-describedby`.
- `ReasonDialog` exports the pure `reasonRequiredValidation(reason, required, attempted)`; an
  attempted empty required reason produces a field error with `aria-invalid`, an `aria-describedby`
  error text (`role="alert"`), and focus returns to the input. Empty reason is accepted when the
  reason is not required.

## Compile-visible evidence (T011/T024 extension)

Runtime behavior execution remains **BLOCKED_BY_PACKAGE_POLICY** (no test runner script, no
approved executor, no installation). The corrected invariants are therefore recorded as
compile-visible checks in the exact planned test sources and are type-checked by `tsc -b`:

| Check | Owner | Invariant |
|---|---|---|
| Navigation fail-closed | `src/Web/src/test/navigation.test.tsx` | `audit` hidden without capabilities or with `[]`; legacy session shape keeps `dashboard`; `isNavigationRouteAvailable` setup gating; `firstPermittedNavigationRoute` prefers Dashboard |
| Landing/denied/setup/rail | `src/Web/src/test/app-shell.test.tsx` | `navigation-denied` transition surfaces safe-forbidden with recovery route; `setup-required` transition; `workspaceStatusFailureSession` 401/403/other mapping; `viewportNavigationMode` 1280/1279/768/767; `resolveLanding` never selects Setup unless required |
| Required-reason validation | `src/Web/src/test/dialog-focus.test.tsx` | `reasonRequiredValidation` empty+required+attempted → error; provided reason passes; not-required accepts empty; unsubmitted is not invalid |
| Form/unsaved registry | `src/Web/src/test/configuration-forms.test.tsx` | `firstErrorFieldId` returns first invalid; `registerUnsavedChange` blocks and `clearUnsavedChange` releases shell navigation |

## Verification commands

| Command/check | Result | Notes |
|---|---|---|
| `npm run lint` (from `src/Web`) | **PASS** | Exit 0; only the pre-existing Fast Refresh/deps warnings. |
| `npm run build` (from `src/Web`) | **PASS** | `tsc -b` + Vite build exit 0; all extended test sources type-check. |
| `scripts/harness.ps1 -Mode Fast -Feature 004-industrial-operations-ui-ux-redesign` | **PASS** | Actual completed result: `Harness Fast summary: PASS=11`; no process was terminated. |
| Runtime behavior execution | **BLOCKED_BY_PACKAGE_POLICY** | No runner/executor; no installation permitted; not claimed as executed. |
| Visual/browser rendering | **NOT_RUN** | No approved rendering evidence; no visual PASS claimed. |

## Scope and safety checks

- Changed files are limited to `src/Web/src/app/AppShell.tsx`,
  `src/Web/src/components/{navigation/NavigationModel.ts,forms/UnsavedChangesGuard.tsx,
  forms/FieldErrorSummary.tsx,dialogs/ConfirmDialog.tsx,dialogs/ReasonDialog.tsx}`,
  `src/Web/src/App.css`, and the compile-visible sources under `src/Web/src/test/`.
- No backend/API/Worker/database/migration, package/lockfile, secret, Docker, route, or PostgreSQL
  target change. Port `5432` was not used.
- Server authorization remains the authority; capability filtering is presentation-only and fails
  closed.
