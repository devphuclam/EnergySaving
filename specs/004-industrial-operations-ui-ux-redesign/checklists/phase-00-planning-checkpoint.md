# Phase 0 Planning Checkpoint

**Feature**: 004 Industrial Operations UI/UX Redesign
**Constitution**: 1.1.0
**Baseline**: `48c439535d95437c63c7cb07f5e70ab64ee5db34` (in HEAD ancestry; verified)
**Branch**: `004-industrial-operations-ui-ux-redesign`
**Date**: 2026-08-04
**Status**: PLANNING — no implementation performed.

## 1. Actual commands executed

- `git status --short` / `git branch --show-current` / `git rev-parse HEAD` / `git log -8 --oneline` — PASS (clean tracked tree; on `004-industrial-operations-ui-ux-redesign` at `863f339fccaa82840c98b622ca61b62b726abf4c`).
- `git merge-base --is-ancestor 48c439535d95437c63c7cb07f5e70ab64ee5db34 HEAD` — PASS (baseline in ancestry).
- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -PathsOnly` — PASS (FEATURE_DIR/SPEC/PLAN/TASKS under `specs/004-industrial-operations-ui-ux-redesign`).
- `.specify/scripts/powershell/setup-plan.ps1 -Json` — PASS (plan template copied to `specs/004-industrial-operations-ui-ux-redesign/plan.md`; parsed FEATURE_SPEC, IMPL_PLAN, SPECS_DIR, BRANCH).
- `.specify/extensions.yml` — does not exist; no before/after plan hooks (skipped per skill rules).
- `scripts/harness.ps1 -Mode Fast -Feature 004-industrial-operations-ui-ux-redesign` — NOT_RUN as
  valid evidence: attempted, FAIL=1 (PASS=10) because running `IUMP.Api` (PID 22416) and two
  `IUMP.Worker` instances (PIDs 27560, 31104) hold locks on `src/Modules/*/bin/Debug` DLLs, so the
  harness build cannot copy outputs (MSB3027/MSB3021). Environment condition unrelated to this
  planning-only change (no production source touched; running processes were not killed). Fast
  checks are not required for documentation-only planning changes.

## 2. UI UX Pro Max invocation

- **Skill mechanism**: Native skill tool invocation (`skill(name: "ui-ux-pro-max")`) twice — once at
  feature start (shell/UI planning), once before Phase 1 design. Not simulated or paraphrased;
  invocation recorded here as evidence.
- **Datasets read directly (Python CLI unavailable — no installed Python; skill policy forbids
  installation)**: `styles.csv` (67 styles), `typography.csv` (57 pairings), `colors.csv`
  (161 palettes), `ux-guidelines.csv` (99 guidelines), `motion.csv` (16 motion specs), `icons.csv`
  (106 icon records), `charts.csv` (25 chart types), `stacks/react.csv` (54 React guidelines).
- **Subagent dispatch for mining**: attempted, FAILED — `Model not found:
  opencode/deepseek-v4-flash-free#max` (provider policy). Recorded as limitation; all mining done
  inline.
- **Recommendations reconciled** against DOC-08 (authoritative), Feature 004 spec, repository
  dependency policy, and existing code. Skill output never overrides DOC-08; pairings requiring
  Google Fonts and icon/chart packages rejected (dependency policy). Details in
  `research.md` §3.

## 3. Files read (authoritative inputs)

`specs/004-industrial-operations-ui-ux-redesign/spec.md`, `checklists/requirements.md`,
`.specify/feature.json`, `.specify/memory/constitution.md`, `AGENTS.md`, `CONTEXT.md`, `README.md`,
`docs/source-register.md`, `docs/repository-harness.md`, `docs/decision-log.md`,
`Business Docs/DOC-08_UI_UX_Design_Specification_v0.1.docx` (full text, 44,696 chars,
`hasMoreText=false`; embedded images NOT inspected — model lacks image input, recorded NOT_RUN),
`specs/003-*/plan.md`/`specs/002-*/plan.md` (structure precedent), Feature 003 phase-00-governance
checkpoint, and the full existing Web source:
`src/Web/package.json`, `src/Web/src/{App.tsx, index.css, App.css}`,
`src/Web/src/app/AppShell.tsx`, `src/Web/src/features/{configuration/*,dashboard/OperationalDashboard.tsx,simulator/SimulatorRoute.tsx,telemetry/PointCurrentRoute.tsx,audit/AuditRoute.tsx,setup/*}`,
`src/Web/src/test/app-shell.test.tsx`, `src/Web/vite.config.ts`, `src/Web/index.html`.

## 4. Current UI audit (summary)

| Area | Finding | Classification |
|---|---|---|
| Routes | `setup/dashboard/configuration/simulator/telemetry/audit`; unknown path → `configuration` fallback | redesign requirement (FR-023 safe fallback) |
| Shell | Topbar + flat sidebar, Vietnamese labels, `transitionAppShell` pure contract | redesign requirement (groups, rail/drawer, context bar) |
| CSS | ad-hoc hex tokens in `App.css`; `prefers-color-scheme: dark` in `index.css`; no breakpoints | redesign requirement (DOC-08 tokens; light-only; tiers) |
| Tables | shared state machine + filter bar + pagination (pageSize 20) in configuration | promote to shared contracts (not unrelated refactor) |
| Icons/charts | none | redesign requirement (inline SVG) |
| Language | Vietnamese UI; `index.html lang="en"` | redesign requirement (lang="vi", FR-021) |
| Tests | type-check-only test source; no runner script | environment constraint (BLOCKED_BY_PACKAGE_POLICY) |
| Backend | unchanged | untouched |

## 5. Design direction

Evidence-First Industrial Light (DOC-08 UX-D01/§6); light-only; compact operational tables
(40–44px rows; ~14px primary, 12–13px metadata); expanded sidebar ≥1280px with tablet rail/drawer
(64–72px) below; permission-based landing (deep link → priority capability → dashboard fallback);
desktop-primary, tablet first-class, mobile non-regression. Rejected: marketing/bento layouts,
glassmorphism, neon, giant KPI cards, emoji, decorative charts, color-only status, unsupported
packages/fonts, dark theme (deferred).

## 6. Clarified decisions (locked)

1. Light-only MVP-1; dark deferred.
2. Compact default for operational tables; no density switch.
3. Expanded sidebar desktop; rail/drawer tablet with focus mgmt; no persisted preference/setting.
4. Permission-based landing; deep-link precedence; Operational Dashboard fallback; no forbidden
   routing; no metadata disclosure; no landing preference.
5. Desktop primary; tablet first-class; mobile non-regression only.

## 7. Artifacts created

- `plan.md` (this invocation; planning gate PASS; Implementation/Release gates NOT_RUN/NO)
- `research.md` (decisions D-001..D-012, UI audit, skill evidence)
- `data-model.md` (presentation models PM-001..PM-010; explicitly no business entities)
- `contracts/design-system.md`
- `contracts/information-architecture.md`
- `contracts/responsive-accessibility.md`
- `contracts/component-contracts.md`
- `contracts/route-and-permission-matrix.md`
- `contracts/migration-phases.md`
- `contracts/test-strategy.md`
- `quickstart.md`
- `checklists/phase-00-planning-checkpoint.md` (this file)

## 8. Frontend architecture

Single existing Web project; new `src/Web/src/components/` for shared contracts; token migration
in `App.css`; `AppShell` extended (grouped nav, rail/drawer, context bar, landing, skip link);
feature folders keep domain logic; no framework/package/state/form/CSS-in-JS changes
(`contracts/component-contracts.md`, `plan.md` Project Structure).

## 9. Component contracts

17 shared contracts (C-01..C-17): AppShell, grouped sidebar/rail/drawer, top/context bar, page
header/breadcrumb, status badge, Data Quality indicator, freshness indicator, feedback banner,
state components (loading/empty/error/forbidden/conflict/blocked/retry), DataTable, FilterBar,
Pagination, form section/field/error summary, confirm/reason dialog, drawer/detail panel, tabs,
chart container + text alternative.

## 10. Responsive strategy

DOC-08 §10 tiers verbatim: desktop ≥1280 (full sidebar, 12-col grid, full tables); tablet
768–1279 (rail + drawer, 1–2 cols, filter drawer, table wrap/scroll); mobile <768 non-regression
(rail preserved; unsupported flows direct to desktop/tablet). No invented breakpoints, no mobile
navigation model, no breakpoint library.

## 11. Accessibility strategy

WCAG 2.2 AA target without certification claim: skip link, visible focus-visible, logical tab
order, accessible names, form error association + first-invalid focus, dialog/drawer focus trap +
Escape + focus restore, aria-current/status/alert semantics, status never color-only, reduced
motion, chart text/table alternatives, Vietnamese-first. Verification uses only available tools
(lint/build/manual journeys); axe/browser automation BLOCKED_BY_PACKAGE_POLICY; visual PASS only
with actual approved rendering evidence.

## 12. Migration phases

Phase 0 planning (this invocation) → Phase 1 shell + foundations → Phase 2 dashboard/telemetry →
Phase 3 configuration → Phase 4 simulator/audit → Phase 5 hardening. Each future
`/speckit.implement` executes exactly one phase with checkpoint and stop.

## 13. Testing strategy

See `contracts/test-strategy.md`. Available: `npm run lint`, `npm run build` (type-check),
`scripts/{build,test}.ps1`, `scripts/harness.ps1 -Mode Fast/Full`,
`tests/Verification/architecture.tests.ps1`, manual browser evidence. Frontend runtime test
execution NOT_RUN (no runner); automation BLOCKED_BY_PACKAGE_POLICY; never PASS.

## 14. Package-policy compliance

No package installed; no package.json/lockfile change; no font/icon/chart library; no CDN; no
Docker/public registry/public action; no backend/API/database change; no secrets.

## 15. Constitution result

PASS — source precedence respected (DOC-08 authoritative for UI; DOC-02 supporting); Feature 003
behavior preserved; no backend capability added; No Data distinct from zero; status not
color-only; one phase per future invocation; blocked evidence remains blocked; release readiness
separate from planning; constitution amendment NOT required (1.1.0 governs).

## 16. Unresolved assumptions

- DOC-08 v0.1 is draft; visual baseline requires pilot-user/PO validation (external dependency,
  not a planning blocker).
- Manual rendering/a11y evidence will be produced per phase; until then no visual PASS claim.

## 17. External validation dependencies

- PO/Operator/Engineer/Manager/Reviewer terminology and priority confirmation (DOC-08 §30).
- Accessibility review and pilot-user feedback before production visual baseline.
- Approved package sources (if a frontend runner is ever requested — not required by this
  feature).

## 18. Findings

| Severity | Count | Detail |
|---|---|---|
| Critical | 0 | — |
| High | 0 | — |
| Medium | 0 | — |

Planning quality review (prompt §16) completed: all FR-001..FR-028 and SC-001..SC-016 mapped to
artifacts; every included surface has a migration destination; every clarified decision appears in
plan; no out-of-scope backend feature; no unsupported package assumed; accessibility and
responsive behavior concrete; component ownership clear; phase boundaries independently
verifiable; no TODO/TBD/placeholder/contradiction/scope-creep found.

## 19. Readiness

| State | Result |
|---|---|
| Planning-ready | YES |
| Implementation-ready | NO (requires `/speckit.tasks` + `/speckit.analyze` + final governance checkpoint) |
| Release-ready | NO |

## 20. Next command

`/speckit.tasks`

## 21. Explicit stop

- Production source changed: NO.
- Package installed: NO.
- tasks.md created: NO.
- `/speckit.implement` executed: NO.
- Feature 003 reopened: NO.
- Merge/PR/release performed: NO (commit + push branch only).
