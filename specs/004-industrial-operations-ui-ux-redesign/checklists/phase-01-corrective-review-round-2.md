# Feature 004 Phase 1 Corrective Review — Round 2

**Feature**: `004-industrial-operations-ui-ux-redesign`
**Phase**: 1 — Application shell and shared foundations (T011–T027)
**Baseline**: `637b3504d195afa24bc1de938970d5a1cfa97fc6` (merged `main`)
**Branch**: `fix/004-phase-01-corrective-round-2`
**Date**: 2026-08-05
**Scope**: Corrective closure of the four remaining Phase 1 findings (R2-01–R2-04) from the
post-`637b350` review. Frontend-only; no backend/API/Worker/database, package, lockfile, route, or
Docker change. No merge, no T028, no Phase 2.

## Findings and corrections

### R2-01 (High) — ReasonDialog accepts an empty required reason on the first confirm attempt

**Observed**: the confirm handler relied on a render-time error value
(`error = reasonRequiredValidation(reason, required, attempted)`), which is computed before the
first attempt (`attempted === false`), so an empty required reason confirmed on the very first
click and submitted `''`.

**Corrected** in `src/Web/src/components/dialogs/ReasonDialog.tsx`:

- New pure helper `reasonConfirmationDecision(reason, required)` returns
  `{ valid: true, value: reason.trim() }` or `{ valid: false, error: 'Lý do là bắt buộc.' }`.
- The confirm handler computes the decision from the current input **inside the handler at confirm
  time**; an invalid decision marks the attempt (`attempted = true`), refocuses the textarea and
  returns **without** calling `onConfirm`. A valid decision submits the trimmed value.
- Close/reopen resets `reason` and `attempted` on every `open` change, so a reopened dialog never
  shows a stale error. Unique `useId` IDs, trap, Escape, cancel and focus restoration are unchanged.

### R2-02 (High) — Route availability was fail-open for every route except Audit

**Observed**: `isNavigationRouteAvailable`/`visibleNavigationItems`/`firstPermittedNavigationRoute`
treated every non-capability route as available to any authenticated session, and the workspace
status contract was consulted only for Setup gating. Dashboard/Configuration/Simulator/Telemetry
had no explicit availability source, and a workspace-status failure on a non-root entry silently
resolved with session-only availability (`resolve(false)`).

**Contract analysis (server-side, read-only source evidence)**:

| Route | Client availability source (now used) | Server enforcement evidence |
|---|---|---|
| Dashboard | `roleMode` != `ReadOnly` (workspace status) | `PostgresOperationalDashboardPorts`: `NoAuthorizedScope` when `!IsAdministrator && SiteIds == 0 && AreaIds == 0` |
| Configuration | `roleMode` != `ReadOnly` | `PostgresConfigurationManagementPorts`: scope-filtered queries; forbidden mapped to 403 |
| Simulator | `roleMode` != `ReadOnly` | `SimulatorEndpoints`: scope-filtered reads; runtime scope authorization |
| Telemetry | `roleMode` != `ReadOnly` | `TelemetryQueryEndpoints`: `RuntimeScopeDeniedException` -> 404 NotFound |
| Audit | `capabilities` contains `AUDIT_READ` (`/api/v1/me`) | `PostgresApplicationPorts` (line 1194): `!IsAdministrator && !HasCapability("AUDIT_READ")` -> Forbidden |
| Setup | `roleMode` != `ReadOnly` AND landing in `SetupWizard`/`ContinueSetup` | `OperationalWorkspaceStatusBuilder.BuildFromSnapshot`: landing resolution and `hasAuthorizedScope` |

`/api/v1/me` returns site `scopes`, `roles` and `capabilities` but **no area scopes and no per-route
permission list**; that gap is closed by the workspace status contract
(`/api/v1/operational-workspace/status`), whose `roleMode` and `landing` are the server's own
scope-presence/landing computation. Because the contract is sufficient, **no
`BLOCKED_BY_AUTHORIZATION_CONTRACT` is declared**; the missing fields are replaced by the
authoritative `roleMode`/`landing` signal, not by inference.

**Corrected** in `src/Web/src/components/navigation/NavigationModel.ts` and
`src/Web/src/app/AppShell.tsx`:

- New `RouteAccess = Record<NavigationRoute, boolean>` and `deriveRouteAccess({ capabilities,
  roleMode, setupRequired })`: `dashboard/configuration/simulator/telemetry` require confirmed scope
  presence (`roleMode` in `Administrator`/`Engineer` — the server's `hasAuthorizedScope` rule,
  consumed as scope presence, never as a role grant), `audit` requires `AUDIT_READ` in the returned
  capability collection, `setup` requires scope presence **and** `setupRequired`. Absent input fails
  every route closed.
- `canAccessNavigationItem`, `visibleNavigationItems`, `isNavigationRouteAvailable` and
  `firstPermittedNavigationRoute` now take `RouteAccess` and are used by every entry path (deep
  link, root landing, brand, sidebar, rail, drawer, popstate, programmatic callbacks, session
  restoration).
- `AppShellState` gains `routeAccess` (absent until the workspace status confirms scope → navigation
  fails closed); `AppShellTransition` gains `route-access` and `retry-workspace-status`; a new
  session clears `routeAccess` so stale access is never reused.
- Workspace-status failure handling (root **and** non-root): 401 → expired session, 403 → forbidden
  session, dependency/other → `{ kind: 'blocked' }` landing presentation with a retry action
  (`RetryState`, `retry-workspace-status` transition). A failed workspace status never fabricates
  availability and never calls `resolve(false)`.
- No capability code was invented, no role name is used as authorization, no forbidden route is
  probed; the server remains the authorization authority.

### R2-03 (High) — beforeunload lost; popstate cancel restored the already-popped URL

**Observed**: `UnsavedChangesGuard` no longer registered any `beforeunload` handler; the popstate
cancel path captured `window.location.href` **inside** the popstate handler (i.e., after the URL
had already changed), so cancelling pushed the popped URL back — a restore trap with possible
Back/Forward loops.

**Corrected**:

- `src/Web/src/components/forms/UnsavedChangesGuard.tsx`: a single module-level `beforeunload`
  listener is registered once (`typeof window` guarded); it reads the current registry, calls
  `preventDefault()` and sets `returnValue = message` only while at least one guard is dirty, and
  does nothing when the registry is empty — multiple mounted guards are safe and the message tracks
  the current registry.
- `src/Web/src/app/AppShell.tsx`: `lastCommittedHrefRef` keeps the URL of the last **committed**
  navigation (updated only in `performNavigation`/root landing `replaceState`, initialized to the
  page URL), i.e., the URL captured **before** any popstate fires. `requestNavigation` stores
  `previousHref` via the pure `navigationCancellationRestore(fromHistory, lastCommittedHref)`;
  cancel pushes back to the last committed URL and confirms the history destination without loops.

### R2-04 (Medium) — FieldErrorSummary first-invalid focus worked only once

**Observed**: `FieldErrorSummary` used a boolean `activate` plus a `handledActivation` ref, so the
second submit attempt with remaining invalid fields never moved focus again.

**Corrected** in `src/Web/src/components/forms/FieldErrorSummary.tsx`:

- `activate` is replaced by a numeric `activationKey` the consumer increments on **every** submit
  attempt; `activationKey <= 0` never forces focus (mounting with pre-existing server errors stays
  passive).
- Every new key re-evaluates the current errors: the pure `fieldErrorSummaryFocusTarget(errors)`
  returns the first invalid field id, `'summary'` when no field id exists, or `undefined` when there
  is nothing to report; focus goes to the first invalid field, else to the summary.

## Compile-visible evidence (round-2 extension)

Runtime behavior execution remains **BLOCKED_BY_PACKAGE_POLICY** (no test runner script, no
approved executor, no installation). The corrected invariants are recorded as compile-visible
checks in the exact planned test sources and type-checked by `tsc -b`:

| Check | Owner | Invariant |
|---|---|---|
| Route-access model | `src/Web/src/test/navigation.test.tsx` | `deriveRouteAccess` fail-closed (unconfirmed/ReadOnly → nothing), scope confirms operational routes but never Audit, `AUDIT_READ` gates Audit, Setup needs scope + requirement, `isNavigationRouteAvailable`/`firstPermittedNavigationRoute`/`visibleNavigationItems` consume `RouteAccess` |
| Shell transitions | `src/Web/src/test/app-shell.test.tsx` | `route-access` publishes confirmed access; new session clears stale access; blocked landing + retry-workspace-status; `navigationCancellationRestore` restores the last committed URL for popstate and nothing for programmatic cancel; 503 never masquerades as an auth outcome |
| Required reason | `src/Web/src/test/dialog-focus.test.tsx` | `reasonConfirmationDecision('', true)` rejected with `'Lý do là bắt buộc.'`; whitespace-only rejected; valid input trimmed; not-required accepts empty |
| Error focus | `src/Web/src/test/configuration-forms.test.tsx` | `fieldErrorSummaryFocusTarget` → first invalid field, summary fallback, no target without errors |

## Verification commands

| Command/check | Result | Notes |
|---|---|---|
| `npm run lint` (from `src/Web`) | **PASS** | Exit 0; only the pre-existing Fast Refresh/deps warnings. |
| `npm run build` (from `src/Web`) | **PASS** | `tsc -b` + Vite build exit 0 (one TS narrowing error found and fixed; all extended test sources type-check). |
| `scripts/harness.ps1 -Mode Fast -Feature 004-industrial-operations-ui-ux-redesign` | **PASS** | Actual completed result: `Harness Fast summary: PASS=11`. First attempt returned `FAIL=1` only because a stale `IUMP.Api.exe` (PID 25388, started 2026-08-05 10:21) from the earlier session locked its own build outputs (MSB3021/MSB3027); the leftover dev process was stopped and the harness re-ran clean: `PASS=11`, all unit checks green. |
| Runtime behavior execution | **BLOCKED_BY_PACKAGE_POLICY** | No runner/executor; no installation permitted; not claimed as executed. |
| Visual/browser rendering | **NOT_RUN** | No approved rendering evidence; no visual PASS claimed. |

## Scope and safety checks

- Changed files are limited to `src/Web/src/app/AppShell.tsx`,
  `src/Web/src/components/{navigation/NavigationModel.ts,forms/UnsavedChangesGuard.tsx,
  forms/FieldErrorSummary.tsx,dialogs/ReasonDialog.tsx}` and the compile-visible sources under
  `src/Web/src/test/`. `webGateways.ts` was not modified (the gateway already returns and retains
  `capabilities`); `App.css` was not modified (tablet 1279px rail and 4.25rem contract verified
  intact).
- No backend/API/Worker/database/migration, package/lockfile, secret, Docker, route, or PostgreSQL
  target change. Port `5432` was not used.
- No new capability code, no role-name authorization, no forbidden-route probing; server
  authorization remains the authority and presentation fails closed.
