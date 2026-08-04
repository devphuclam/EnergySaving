# Test Strategy

**Feature**: 004 Industrial Operations UI/UX Redesign
**Created**: 2026-08-04

## 1. Verification environment (current facts)

- `src/Web/package.json` scripts: `dev`, `build`, `lint`, `preview`. There is NO test runner
  script; `src/Web/src/test/app-shell.test.tsx` is type-checked by the build (`tsc -b` in
  `npm run build`) but not executed (Feature 003 phase-01-verification evidence, line 78).
- Adding Vitest/Jest/Playwright/axe would require package installs — `BLOCKED_BY_PACKAGE_POLICY`
  (no approved package sources). This blocker is recorded, never reported as PASS.
- Repository-level checks: `tests/Verification/architecture.tests.ps1` verifies policy/module/
  web-test-source contracts; `scripts/harness.ps1 -Mode Fast` (repo-only) and
  `-Mode Full` (mandatory for release claims; may be blocked by environment checks).
- Allowed commands: `npm run lint`, `npm run build` (installed tree), `scripts/build.ps1`,
  `scripts/test.ps1`, `scripts/harness.ps1 -Mode Fast/Full`.

## 2. What each evidence item requires

| Evidence item | Method | Classification |
|---|---|---|
| Route accessibility & shell consistency | Type-checked route/`transitionAppShell` tests; manual browser journey | RUNNABLE_NOW (type-level + manual) |
| Permission-safe navigation / hidden items | Manual review + server-outcome tests; server remains enforcement | RUNNABLE_NOW |
| Deep-link & fallback landing | Type-checked landing resolution logic; manual journeys | RUNNABLE_NOW |
| Keyboard navigation, visible focus, dialog/drawer focus trap & restoration, first-invalid-field focus | Manual reviewer journeys per phase with documented scripts; type-level state tests | RUNNABLE_NOW (manual evidence mandatory) |
| Unsaved-change warning | Manual journey + type-level tests | RUNNABLE_NOW |
| Table density (40–44px rows, 14/12–13px) | DOM/CSS measurement in manual browser evidence; style token review | RUNNABLE_NOW (manual measurement) |
| Desktop/tablet responsive, mobile non-regression | Manual viewport journeys + screenshots (actual rendering evidence) | RUNNABLE_NOW (manual; visual PASS only with approved evidence) |
| Zero vs No Data; Good/Uncertain/Bad; stale/unavailable; color-independent status | Fixture-driven manual review (documented fixtures) + type-level tests | RUNNABLE_NOW |
| Loading/empty/error/forbidden/conflict/blocked/retry states | Fixture/manual review; state model tests | RUNNABLE_NOW |
| Feature 003 regression compatibility | Existing acceptance/regression checks + harness; any blocked evidence reported separately | RUNNABLE_NOW / BLOCKED as evidenced |
| Lint | `npm run lint` | RUNNABLE_NOW |
| Production build (type-check + Vite build) | `npm run build` | RUNNABLE_NOW |
| Automated browser E2E / automated axe | Not installed | BLOCKED_BY_PACKAGE_POLICY — never PASS |
| Visual QA | Requires actual approved rendering evidence (screenshots/review), per phase | Manual; no invented PASS |

## 3. Red-green-refactor adaptation (constitution IV; D-009)

Because no frontend runner exists, behavioral seams are tested at the type level:

- Pure logic (landing resolution, shell transitions, nav-mode transitions, chart gap computation,
  redaction helpers) is extracted into modules with test sources that are type-checked by the
  build; runtime execution is explicitly `NOT_RUN` unless a runner is approved, and is recorded
  honestly.
- Repository PowerShell checks (`architecture.tests.ps1`) remain the executable verification
  surface available today.
- Manual browser evidence is required at each phase checkpoint for interactive/visual claims.

Blocked checks are always reported `BLOCKED` with classification, never PASS (§13; constitution).

## 4. Phase verification plan

| Phase | Fast checks | Full checks | Manual evidence required |
|---|---|---|---|
| P1 Shell/foundations | lint, build, architecture Fast | harness Full when applicable | landing, nav groups, rail/drawer, skip link, tokens |
| P2 Dashboard/telemetry | lint, build | harness Full | zero/Missing/quality/stale fixtures, chart gaps + alt text |
| P3 Configuration | lint, build | harness Full | per-entity tables/forms, validation, conflict, destructive confirm |
| P4 Simulator/audit | lint, build | harness Full | run outcomes, audit redaction/diff, deep-link forbidden |
| P5 Hardening | lint, build | harness Full (mandatory before release claim) | keyboard journeys, contrast, tablet/mobile, regression |

## 5. Honest-limitation register

- Frontend tests type-checked, not executed: NOT_RUN runtime evidence (D-009; Feature 003
  precedent).
- axe/Playwright/accessibility automation: BLOCKED_BY_PACKAGE_POLICY.
- Full harness may record BLOCKED environment checks (e.g., deployment target approval) — reported
  as blocked; Feature 004 does not claim release readiness.
- DOC-08 media (mockup images) not visually inspected by this model: recorded NOT_RUN; design
  decisions rely on DOC-08 normative text, which was read in full.