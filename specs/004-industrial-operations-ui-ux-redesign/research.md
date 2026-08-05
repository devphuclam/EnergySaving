# Research: Industrial Operations UI/UX Redesign

**Feature**: 004 Industrial Operations UI/UX Redesign
**Created**: 2026-08-04
**Purpose**: Phase 0 research output of `/speckit-plan`. Resolves every NEEDS CLARIFICATION
deferred by the specification and records evidence-backed decisions for the plan artifacts.

## 1. Decisions

### D-001 — Landing capability priority order (FR-028)

- **Decision**: Post-authentication landing resolution order:
  1. Valid permitted deep link (current pathname is a known included route and the user is
     authorized for it).
  2. First permitted capability from the existing effective-permission priority matrix: prefer
     Configuration, Simulator, Telemetry, Audit, or Setup only when its capability is actually
     permitted and enabled for the current environment/workspace.
  3. Operational Dashboard as a fallback only when the Dashboard capability is itself permitted
     and no higher-priority permitted capability is available.
  4. A safe no-authorized-capability state when no included capability is permitted; it must not
     route through Dashboard or any forbidden page and must not disclose capability/object metadata.

  The existing `WorkspaceStatus.landing` (`Dashboard`/`Setup`) is only server-provided context for
  presentation. It cannot bypass the effective-permission check or force Setup ahead of another
  permitted capability. Dashboard is not part of the priority list; it is permission-checked only
  as the documented fallback. The no-authorized-capability result is a safe presentation state, not
  a new backend authorization state or route.

- **Rationale**: FR-028 requires effective-permission-based landing, deep-link precedence, and a
  permitted Dashboard fallback. The priority order follows the daily operational hierarchy in
  DOC-08 while treating Dashboard as an explicit permission-checked fallback rather than an
  assumed destination. Setup remains reachable only when its existing workspace-status and
  permission rules authorize it; no unauthorized route is probed or rendered.

- **Alternatives considered**: hard-coded role-name-based landing (rejected — FR-028 forbids it and
  role is not authorization); a landing preference setting (rejected — FR-028 forbids persistence);
  always landing on Operational Dashboard regardless of deep link (rejected — FR-028 requires deep
  link precedence).

### D-002 — Sidebar breakpoint and collapse pattern (FR-002)

- **Decision**: Follow DOC-08's responsive tiers exactly. At desktop width `>= 1280px` the primary
  navigation is a wide expanded sidebar (DOC-08 sidebar width 236–248px; the current 15rem/240px
  layout already sits within this band). In the tablet range `768–1279px` the sidebar collapses to
  an icon rail on the left (DOC-08 collapsed width 64–72px) that expands to an accessible overlay
  drawer on demand. Below `768px` (mobile, non-regression only) the rail remains available and
  never disappears.
- **Rationale**: DOC-08 section 10 defines the normative tier line: "≥1280 Desktop" full sidebar,
  "768-1279 Tablet" collapsed sidebar, "<768 Mobile" operational subset. The clarification deferred
  the exact breakpoint to `/speckit.plan` "based on the current layout and DOC-08"; following
  DOC-08 verbatim is therefore authoritative and avoids an invented breakpoint. The existing
  `15rem 1fr` content grid in `App.css` remains for desktop; a `@media (max-width: 1279.98px)`
  rule switches to rail + drawer. A hash-based state on a `matchMedia` query decides the default
  mode per session; the open/closed choice is never persisted (no user setting).
- **Alternatives considered**: breakpoint at 1024px (rejected — contradicts DOC-08's 1280 desktop
  tier); a persistent collapsible sidebar with saved preference (rejected — clarification locks
  no-persistence); drawer-only navigation at all sizes (rejected — desktop must show a wide expanded
  sidebar per FR-002).

### D-003 — Drawer interaction contract (FR-002, FR-020)

- **Decision**: In the tablet range (below 1280px) the icon rail is always visible; activating the
  rail toggle (named "Mở điều hướng" / "Đóng điều hướng") opens an overlay drawer that traps
  focus, closes on Escape, returns focus to the opener, and blocks background interaction while
  open. Icon-only rail items have accessible names and a tooltip/flyout on hover/focus. The
  preference is not persisted and no user setting is introduced.
- **Rationale**: This is the exact contract FR-002/FR-020 and SC-014 require; it also follows the
  interaction-guideline evidence (focus management, Escape-to-close, background block) from the
  UI/UX skill dataset (ux-guidelines rows on Focus States, Keyboard Navigation, and ARIA Labels).
- **Alternatives considered**: CSS-only hover rail (rejected — not keyboard operable and does not
  satisfy focus/escape requirements); removing navigation entirely on small widths (rejected —
  FR-002 forbids the sidebar disappearing completely).

### D-004 — Responsive strategy (FR-019)

- **Decision**: Desktop-first implementation with explicit tablet first-class support, using the
  DOC-08 tier lines (section 10):
  - **Desktop `>= 1280px`**: wide expanded sidebar, 12-column content grid with 12–16px gutters,
    24px page padding, full tables.
  - **Tablet `768–1279px`**: collapsed sidebar rail (64–72px) + accessible drawer, 1–2 column
    layout, filter drawer, tables wrap to per-row card blocks or gain an explicit horizontal-scroll
    region with accessible announcement, touch targets >= 44px, forms single-column.
  - **Mobile `< 768px`** (non-regression only): existing routes must load safely, preserve
    auth/scope, avoid horizontal overflow of essential content, present clear unsupported states,
    and direct users to desktop/tablet. No new mobile navigation model, mobile-first experience,
    layout system, package, framework, breakpoint library, or acceptance suite.
- **Rationale**: FR-019/SC-006/SC-016, the locked clarifications, and DOC-08 section 10 define
  this exact boundary; the tiers are taken verbatim from DOC-08. The current app has a single
  media query (a `prefers-color-scheme: dark` block in `index.css` that Feature 004 removes for
  light-only) and no viewport breakpoints, so breakpoints are new work contained in the Web layer.
- **Alternatives considered**: mobile-first development (rejected — FR-019 explicitly forbids a
  mobile-first experience); a responsive library or breakpoint framework (rejected — no package
  installs authorized); inventing an 1024px tier line (rejected — contradicts DOC-08).

### D-005 — Font stack and typography (FR-017)

- **Decision**: Keep the existing system font stack (Inter, ui-sans-serif, system-ui, Segoe UI)
  and extend it with an explicit type scale and numeric treatment. Do not add webfont downloads.
  Vietnamese text uses the same stack; Segoe UI provides strong Vietnamese coverage on Windows.
- **Rationale**: The UI/UX skill typography dataset recommends pairings that all reference Google
  Fonts (Playfair Display + Inter, Poppins + Open Sans, Space Grotesk + DM Sans), which are
  rejected under the repository dependency policy (no unapproved downloads, no public registries).
  The skill's "Minimal Swiss" guidance (Inter for dashboards/enterprise) aligns with the existing
  stack. `font-variant-numeric: tabular-nums` for numeric data cells is adopted from the
  data-density style evidence to keep numbers aligned and readable during prolonged scanning.
- **Alternatives considered**: Google Fonts download (rejected — dependency policy); a dedicated
  numeric font (rejected — same policy); larger display type for operational pages (rejected —
  FR-027 wants 14px primary / 12–13px metadata in data tables).

### D-006 — Icons (FR-016, FR-017)

- **Decision**: Use a small set of inline SVG icons in outline style at 16px (inline/metadata) and
  20px (action/navigation) with `aria-hidden="true"` on decorative icons and accessible names on
  interactive icon-only controls. Icons follow a restrained single-stroke outline style consistent
  with the Industrial Light direction. No icon package is installed.
- **Rationale**: The skill icon dataset recommends Phosphor outline icons (20px regular weight),
  but the package is not in the approved dependency set. The same visual style (outline, stroke
  based, 20px) is achievable with hand-authored inline SVGs already present in the codebase
  (none currently exist; the current UI is text-only in `AppShell.tsx`). FR-016 requires
  non-color cues; icons + text satisfy it without a package.
- **Alternatives considered**: Phosphor package install (rejected — package policy); emoji icons
  (rejected — inconsistent, non-restrained, and noisy for industrial consoles); Unicode glyphs
  (rejected — font-dependent rendering and poor accessibility names).

### D-007 — Charts (FR-006, FR-011, FR-012, FR-018)

- **Decision**: Implement time-series/overview charts as small self-contained SVG components in
  `src/Web` with explicit chart metadata (metric, unit, timezone, cutoff, grain, quality,
  coverage). Missing intervals render as visible gaps (never zero, never interpolated). Thresholds
  and quality markers render as distinct line styles and markers, not color alone. A data-table
  fallback or summary text alternative is provided for important chart information.
- **Rationale**: The skill chart dataset (trend/line evidence) requires series differentiation by
  line style and an accessible table alternative; DOC-08 chart rules (Missing = gap, no default
  interpolation, no misleading dual axes) are normative. No chart package exists or is authorized,
  so hand-authored SVG is the only permitted path. Data volumes in the POC (8–20 points, 60-second
  interval) fit SVG rendering without downsampling.
- **Alternatives considered**: Chart.js/Recharts/ApexCharts (rejected — no package installs);
  Canvas-based charts (rejected — unnecessary for POC volumes and harder to keep accessible);
  server-rendered chart images (rejected — no new backend capability is authorized).

### D-008 — Component architecture (FR-013, FR-014, FR-024)

- **Decision**: Extract shared presentational primitives into a small `src/Web/src/components`
  directory (DataTable, FormField, StatusBadge, FeedbackBanner, EmptyState, ErrorPanel,
  ConfirmDialog, Drawer, Breadcrumbs, Pagination) driven by CSS classes already in `App.css` where
  possible. The existing management state machine (`ManagementState` with loading/ready/forbidden/
  expired/no-data/validation/conflict/not-found/dependency/runtime/error) is preserved and promoted
  as the shared Interaction State contract for all included pages.
- **Rationale**: The existing configuration module already implements the required states with
  Vietnamese labels; Feature 003 verified this pattern. Reusing it keeps cross-page consistency
  (FR-013/FR-018) without new packages. The React stack dataset (colocate related code, small
  components, error boundaries) supports this extraction.
- **Alternatives considered**: a third-party component library (rejected — package policy);
  page-local implementations for each surface (rejected — violates FR-001/FR-013 consistency and
  SC-009).

### D-009 — Testing and verification strategy

- **Decision**: Frontend behavior remains verified by the existing contract: `tsc`-typechecked
  test sources (`src/Web/src/test/app-shell.test.tsx`) and the repository `architecture.tests.ps1`
  checks that reference the web test source. No frontend test runner exists in the dependency
  set; adding Vitest/Jest is BLOCKED_BY_PACKAGE_POLICY and is recorded as blocked, never PASS.
  Red-green phases therefore use type-level contracts, the shared pure transition functions
  (`transitionAppShell`), and repository-level PowerShell verification, plus explicit manual
  browser evidence checkpoints for visual/responsive/accessibility checks.
- **Rationale**: Feature 003 recorded exactly this constraint (phase-01-verification.md line 78:
  tests are type-checked but not executed; no test runner script in package.json). Changing the
  package graph to add a runner is forbidden without approved package sources. The plan must not
  weaken the requirement by claiming execution that did not happen.
- **Alternatives considered**: adding Vitest (rejected — BLOCKED_BY_PACKAGE_POLICY); Playwright
  browser tests (rejected — same policy); treating type-check as runtime execution (rejected —
  violates evidence semantics).

### D-010 — Navigation grouping (FR-002, FR-026)

- **Decision**: Navigation groups:
  - **Vận hành (Operations)**: Vận hành (dashboard), Dữ liệu & tình trạng (telemetry).
  - **Cấu hình (Configuration)**: Cấu hình (configuration), Mô phỏng (simulator).
  - **Quản trị (Governance)**: Nhật ký (audit).
  - **Thiết lập (Setup)**: Thiết lập (setup), shown as a lower group with a boundary note.
  Conditional capabilities (Trusted Telemetry Ingestion, CSV import, Rules, Alerts,
  Notifications, Reports, Edge, Modbus, savings, AI/ML, equipment control) are NOT presented.
- **Rationale**: FR-002 requires grouping by operational areas with permission-safe visibility and
  no conditional capability presentation; DOC-08's task-oriented navigation supports this split.
  The current flat six-item sidebar is regrouped without renaming destinations (Vietnamese labels
  already in use).
- **Alternatives considered**: single flat list (current state; rejected — FR-002 requires
  groups); role-specific navigation (rejected — permission-safe visibility must come from server
  data, not role names).

### D-011 — Light-only palette enforcement

- **Decision**: Adopt the DOC-08 token set as the single source for colors (Primary #1F4E78 /
  #2F75B5, App Background #F4F7FA, Surface #FFFFFF, Success #2E7D32, Warning #B26A00, Danger
  #C62828, Critical #7F1D1D, Missing #667085). Replace the current ad-hoc `--ink/--muted/--line/
  --panel/--canvas/--blue/--green/--amber/--red` tokens in `App.css` with semantic tokens derived
  from DOC-08, and remove the `prefers-color-scheme: dark` block in `index.css` (light-only).
- **Rationale**: The skill color dataset recommends professional navy/blue palettes for
  B2B/enterprise/financial dashboards with light `#F8FAFC`-style backgrounds — consistent with
  DOC-08's mandated token set, which is authoritative (source precedence). FR-017 locks light-only
  for MVP-1; the existing dark-mode media query must not survive, or it becomes a silent dark
  implementation.
- **Alternatives considered**: keeping current hex tokens (rejected — SC-009 requires a coherent
  token set and DOC-08 is authoritative); implementing dark theme (rejected — explicitly deferred);
  keeping the dark media query for future use (rejected — dead code that contradicts FR-017 and
  could be mistaken for dark support).

### D-012 — Compact density implementation (FR-027, SC-013)

- **Decision**: Apply compact density only to operational data tables/lists via CSS classes
  (`data-table` rows 40–44px, primary 14px, metadata 12–13px) and a shared table component
  contract. Detail panels, forms, dialogs, and explanatory content keep normal spacing. Tablet
  interaction targets may be taller (>= 44px touch target) without a second business density mode.
  No user-facing density switch.
- **Rationale**: FR-027/SC-013 define the exact targets and forbid the switch. The skill
  data-density style evidence (table row height ~36px, 12–14px font) corroborates the range.
- **Alternatives considered**: a density toggle (rejected — explicitly forbidden);
  compact everywhere (rejected — FR-027 limits compactness to operational tables/lists).

## 2. Technical audit of the current Web layer (2026-08-04)

Read sources: `src/Web/package.json`, `src/Web/src/App.tsx`, `src/Web/src/app/AppShell.tsx`,
`src/Web/src/App.css`, `src/Web/src/index.css`, `src/Web/index.html`,
`src/Web/vite.config.ts`, `src/Web/src/features/configuration/*`,
`src/Web/src/features/dashboard/OperationalDashboard.tsx`,
`src/Web/src/features/simulator/SimulatorRoute.tsx`,
`src/Web/src/features/telemetry/PointCurrentRoute.tsx`,
`src/Web/src/features/audit/AuditRoute.tsx`, `src/Web/src/test/app-shell.test.tsx`.

| Area | Current state | Feature 004 change |
|---|---|---|
| Routes | `setup/dashboard/configuration/simulator/telemetry/audit`; unknown path falls back to `configuration` | Reuse route set; unknown-path fallback becomes safe forbidden/not-found experience (FR-023); landing per D-001 |
| Shell | Topbar brand + session controls + scope pill; flat sidebar with Vietnamese labels; `transitionAppShell` pure state | Grouped navigation (D-010), icons, rail/drawer below 1280px (D-002/D-003), persistent context bar |
| Styling | `App.css` with ad-hoc tokens, `15rem 1fr` grid, 4.5rem topbar, dark sidebar `#132236`; `index.css` has dark media query | DOC-08 semantic tokens, light-only (D-011), compact tables (D-012) |
| Data tables | `ConfigurationManagementComponents.tsx` — shared state machine, filter bar, pagination (pageSize 20) | Promote to shared `components/` primitives (D-008) |
| Feedback | `FeedbackBanner`, `notice` blocks, `role="status"`/`role="alert"` usage | Standardize via components contract (D-008) |
| Telemetry | `PointCurrentRoute.tsx` — current value + health; no charts | Add SVG chart component with Missing gaps (D-007); keep zero-vs-missing semantics |
| Audit | `AuditRoute.tsx` — filters fromUtc/toUtc, redaction regex | Reuse redaction contract; add before/after readable diff presentation |
| Language | Vietnamese labels everywhere; `index.html` `lang="en"` | Set `lang="vi"`; keep technical identifiers/reason codes (FR-021) |
| Icons/charts | none | Inline SVG only (D-006/D-007) |
| Testing | type-check-only test source; no runner script | D-009 strategy; blocked runner recorded as BLOCKED_BY_PACKAGE_POLICY |

## 3. Skill evidence consulted

- `ui-ux-pro-max` `styles.csv`: Minimalism & Swiss (enterprise/dashboards, grid-based, subtle
  hover 200–250ms) selected as base; Data-Dense Dashboard style corroborates compact tables
  (row ~36–44px, 12–14px fonts); Real-Time Monitoring style informs live status indicators;
  rejected styles: Glassmorphism, Neumorphism, Brutalism, Dark/OLED, Executive Dashboard
  (large consumer-style KPI cards), Motion-driven.
- `typography.csv`: all recommended pairings require Google Fonts downloads — rejected under
  dependency policy; "Minimal Swiss" Inter-based guidance aligns with the system stack (D-005).
- `colors.csv`: enterprise/B2B/financial-analytics palettes use navy-blue primaries on light
  backgrounds with muted slate text — consistent with DOC-08 tokens (D-011).
- `ux-guidelines.csv`: adopted — visible focus rings, contrast >= 4.5:1, never color-only,
  keyboard navigation, aria-live for errors, skip link, loading/empty/error recovery states,
  touch targets >= 44px (mobile), no `100vh` traps, tables overflow-x with accessible treatment,
  reduced-motion respect, 150–300ms micro-interactions, transform/opacity only.
- `charts.csv`: line chart for trend with line-style differentiation + data-table alternative;
  bullet/compact KPI display with values always visible; Missing as gap (DOC-08 normative).
- `motion.csv`: micro-interactions 150–300ms, hover displacement < 2px, no layout-affecting
  animation, respect `prefers-reduced-motion`, exit faster than entrance.
- `icons.csv`: Phosphor outline style at 20px regular weight — style reference only; icons are
  hand-authored inline SVG (D-006).
- `stacks/react.csv`: colocate related code, small focused components, error boundaries, stable
  keys, memoize context values, lazy-load heavy views — adopted as implementation guidance.

## 4. Resolved NEEDS CLARIFICATION items

All deferred items from the spec clarifications are resolved in this document:

| Deferred item | Resolution |
|---|---|
| Landing capability priority order (FR-028) | D-001 |
| Sidebar breakpoint + pattern (FR-002) | D-002/D-003 |
| Responsive strategy/breakpoints (FR-019) | D-004 |
| Icon strategy | D-006 |
| Chart strategy | D-007 |
| Compact-density implementation approach (FR-027) | D-012 |
| Font/typography approach (FR-017) | D-005 |
| Testing approach | D-009 |

No unresolved unknowns remain; the plan can proceed to Phase 1 design artifacts.
