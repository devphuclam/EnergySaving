# Feature 004 Phase 1 Verification

**Feature**: `004-industrial-operations-ui-ux-redesign`
**Phase**: 1 — Application shell and shared foundations (`T011`–`T027`)
**Baseline**: `1cc45e3e64636e093aa0e714f0b2ecc08968ecbb` (authoritative merged `main`)
**Branch**: `feat/004-phase-01-shell-foundations`
**Date**: 2026-08-05
**Scope**: frontend shell and reusable presentation primitives only. No backend/API/Worker/database,
package, lockfile, route, Docker, dark-theme, density-switch, or mobile-first changes.

## Entry and red evidence (T011)

- Entry gate: **PASS**. Baseline is in ancestry; T001–T010 are complete; Phase 0 checkpoint records
  `Analyze-clean: YES` and `Implementation-ready: YES`.
- The planned frontend behavior sources were added under `src/Web/src/test/` and compile as part of
  the existing Web build. `src/Web/package.json` has no test runner script and no approved runtime
  executor is available. Therefore the requested red/runtime behavior evidence is
  **BLOCKED_BY_PACKAGE_POLICY**; no package was installed and no test claim is inferred from
  type-checking.

## UI UX Pro Max evidence (T012 and T013–T023)

The installed skill was invoked with the bundled workspace Python runtime (no installation):

```text
& $py C:\Users\TD-999\Research\EnergySaving\Codespace\.agents\skills\ui-ux-pro-max\scripts\search.py "internal industrial utility monitoring operations console evidence-first light compact tables accessibility navigation shell desktop tablet" --design-system --variance 2 --motion 2 --density 8 -p "IUMP Feature 004 Phase 1" -f markdown
& $py ...\search.py "industrial operations console accessibility focus drawer compact table status feedback reduced motion" --domain ux -n 20
& $py ...\search.py "React web application shell navigation keyboard accessibility drawer focus" --domain web -n 20
& $py ...\search.py "React application shell navigation state focus management" --stack react
```

Applied guidance: visible focus, keyboard names and `aria-current`, skip-link and route-title focus,
focus trap/Escape/restoration for overlays, live-region feedback, semantic non-color status, compact
table overflow/scroll treatment, reduced-motion hooks, and explicit loading/empty/error next actions.
Rejected recommendations: the generated `Video-First Hero`, `Exaggerated Minimalism`, dark/video
direction, oversized marketing typography, external Google fonts, and new icon/chart packages. They
conflict with DOC-08 Evidence-First Industrial Light, the desktop/tablet operational scope, and the
repository no-install/dependency policy. The skill is supporting intelligence only; DOC-08 and the
feature artifacts remain authoritative.

## Implementation evidence by task

| Task | Evidence | Result |
|---|---|---|
| T011 | Extended `app-shell.test.tsx`; added route fixtures and landing-routing sources covering grouped navigation, permission-safe landing, deep links, focus, and drawer contracts. Runtime execution unavailable. | Recorded; runtime **BLOCKED_BY_PACKAGE_POLICY** |
| T012 | `index.css` and `App.css` now use semantic light tokens, Vietnamese-first system stack, visible focus, reduced motion, compact row metrics, and no dark/density switch. | PASS (static/source review) |
| T013 | `AppShell.tsx`/`App.tsx`: identity, current route, context, session states, skip link, `#main-content`, and route-title focus. | PASS (static/source review) |
| T014 | Grouped Sidebar/Rail/NavigationDrawer, capability filtering, `aria-current`, accessible labels/tooltips, Escape, focus trap and restoration; no persisted preference. | PASS (static/source review) |
| T015 | ContextBar, Breadcrumbs, PageHeader contracts provide site/area, timezone/freshness, user/session and Vietnamese page orientation. | PASS (static/source review) |
| T016 | Operational status, data-quality, and freshness indicators expose text/reason/icon semantics, including zero vs missing. | PASS (static/source review) |
| T017 | Feedback banner plus loading/empty/error/forbidden/conflict/blocked/retry states expose impact and next action with live-region semantics. | PASS (static/source review) |
| T018 | DataTable, FilterBar, Pagination provide explicit state, result count, actions, compact 40–44px desktop rows and tablet overflow treatment. | PASS (static/source review) |
| T019 | FormSection, Field, FieldErrorSummary, UnsavedChangesGuard provide grouping, required/error association and unsaved-change contract. | PASS (static/source review) |
| T020 | ConfirmDialog and ReasonDialog provide safe cancel, required reason path, Escape, focus trap and focus restoration. | PASS (static/source review) |
| T021 | Drawer, DetailPanel and Tabs provide quick-detail disclosure, modal background blocking, Escape/restoration and roving tab semantics. | PASS (static/source review) |
| T022 | Landing resolution is permission/capability-based: permitted deep link → first permitted capability → permitted Dashboard fallback → safe no-authorized state. No forbidden route probing or metadata disclosure. | PASS (static/source review) |
| T023 | `App.tsx` composes the common shell around the existing setup, dashboard, configuration, simulator, telemetry and audit route owners; feature behavior and route inventory are preserved. | PASS (static/source review) |

No backend authorization or API contract was changed; server authorization remains authoritative.

## Verification commands (T024)

| Command/check | Result | Notes |
|---|---|---|
| `npm run lint` (from `src/Web`) | **PASS** | Exit 0. Existing Fast Refresh warnings remain in GatewayContext/AppShell/ConfigurationManagementComponents; no new error. |
| `npm run build` (from `src/Web`) | **PASS** | Vite build completed successfully. |
| Exact Phase 1 frontend runtime test sources | **BLOCKED_BY_PACKAGE_POLICY** | No runner script/executor; no packages installed. Sources are compile-visible only. |
| `scripts/harness.ps1 -Mode Fast -Feature 004-industrial-operations-ui-ux-redesign` | **PASS** | Actual completed result: `Harness Fast summary: PASS=11`; deployment, architecture, repository policy/scope, feature-artifact and unit checks passed; no process was terminated. |
| Visual/browser rendering | **NOT_RUN** | No approved rendering/screenshot evidence was available in this execution; no visual PASS is claimed. |
| Automated browser/axe accessibility | **BLOCKED_BY_PACKAGE_POLICY** | No approved runner; no installation permitted. |
| Manual keyboard/touch acceptance | **NOT_RUN** | Requires an approved interactive browser/evidence channel; source contracts are recorded, not certified. |

## Scope and safety checks

- `git diff --check`: PASS.
- Changed production files are limited to `src/Web/src/` shell, gateway response typing, styles, shared
  presentation components, and compile-visible test sources.
- No `package.json`, lockfile, API, Worker, database, migration, secret, generated binary, Docker, or
  PostgreSQL target change. Port `5432` was not used.
- No new route was added; no equipment-control/write-back behavior was introduced.

## Corrective review (C1–C4)

Post-review corrections are recorded in `phase-01-corrective-review.md`: navigation permission
fail-closed on every entry path (including `popstate` and brand), `navigation-denied` and
`setup-required` transitions, tablet rail/CSS breakpoint consolidation in `App.css`,
`viewportNavigationMode` contract, unsaved-change shell guard via the registry + ConfirmDialog,
`FieldErrorSummary` explicit `activate`, dialog `useId` accessibility wiring, and
`reasonRequiredValidation`. The extended compile-visible checks type-check under `tsc -b`; lint,
build, and the Fast harness (`PASS=11`) remain PASS. Runtime execution and visual rendering
classifications are unchanged.

## Corrective review round 2 (R2-01–R2-04) — supersedes the closure statements above

**Baseline**: `637b3504d195afa24bc1de938970d5a1cfa97fc6`; **Branch**:
`fix/004-phase-01-corrective-round-2`; evidence: `phase-01-corrective-review-round-2.md`.

- R2-01: `ReasonDialog` validates inside the confirm handler via `reasonConfirmationDecision`;
  an empty required reason is rejected on the very first attempt, focus returns to the textarea,
  close/reopen resets state.
- R2-02: route availability is now derived from server data only — `deriveRouteAccess` consumes
  `roleMode` (workspace status, as server `hasAuthorizedScope` scope presence) for
  Dashboard/Configuration/Simulator/Telemetry/Setup and `AUDIT_READ` from the returned capability
  collection for Audit; every entry path fails closed until the workspace status confirms scope;
  workspace-status failures map to expired (401), forbidden (403) or a blocked/retry presentation
  (dependency/other) on root and non-root entries. No new capability code, no role-name
  authorization, no probing; the server contract was sufficient (no blocker declared).
- R2-03: a single registry-driven `beforeunload` listener restores the unload guard
  (`preventDefault` + `returnValue` while any guard is dirty); popstate cancel restores the last
  committed URL via `lastCommittedHrefRef` captured before popstate, with no Back/Forward loops.
- R2-04: `FieldErrorSummary` re-focuses the first invalid field (else the summary) on every submit
  attempt via a numeric `activationKey`; mount-time server errors never force focus.
- Verification: `npm run lint` PASS, `npm run build` PASS, Fast harness **PASS=11** (first attempt
  `FAIL=1` solely because a stale `IUMP.Api.exe` from the earlier session locked its own build
  outputs; the leftover dev process was stopped and the harness re-ran clean). Runtime behavior
  execution remains **BLOCKED_BY_PACKAGE_POLICY**; visual/browser rendering remains **NOT_RUN**.
