# Industrial Light P0 Prototype Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Build a self-contained, clickable Industrial Light prototype for the IUMP P0 screens that demonstrates navigation, alert handling, point inspection, rule authoring, and CSV preview without requiring a backend.

**Architecture:** A static single-page prototype uses hash-based routing and in-memory mock state. Shared design tokens, layout primitives, badges, tables, charts, and drawers are implemented once and reused across all screens. Data-changing interactions are simulated locally and reset on page reload.

**Tech Stack:** Semantic HTML5, CSS custom properties, vanilla JavaScript ES2022, inline SVG charts, no runtime dependencies.

## Global Constraints

- Use the Industrial Light visual direction selected from DOC-08 v0.1.
- Desktop-first at 1440 px; remain usable at 1024 px and provide a compact mobile alert view below 768 px.
- Missing data must never be rendered as value `0`; charts must show a visible gap.
- `Recovered` and `Closed` must be visually and behaviorally distinct.
- Role and site/area context must remain visible in the application shell.
- Prototype data is fictional and must not contain real company credentials or production readings.
- The prototype must run by opening `index.html`; no build or package installation may be required.
- Do not implement Modbus, Edge Collector, AI, Improvement Actions, production authentication, or API calls.

---

## File Structure

- Create: `index.html` — application shell and screen mounting point.
- Create: `styles.css` — Industrial Light tokens, responsive layout, component and screen styling.
- Create: `mock-data.js` — fictional site, point, alert, rule and CSV preview data.
- Create: `app.js` — router, render functions, local interaction state and event delegation.
- Create: `README.md` — launch instructions, supported flows and prototype limitations.
- Create: `tests/prototype-smoke.html` — browser-openable smoke checks for route rendering and state rules.

### Task 1: Application Shell and Design Tokens

**Files:**
- Create: `index.html`
- Create: `styles.css`
- Create: `app.js`

**Interfaces:**
- Produces: `navigate(route)`, `renderApp()`, `state.currentRoute`, shared `.app-shell`, `.sidebar`, `.topbar`, `.page` classes.

- [x] Build semantic shell with sidebar, topbar, scope selector, data-cutoff indicator, notification button and user menu.
- [x] Add hash routing with default route `#/overview`.
- [x] Define Industrial Light color, typography, spacing, radius, elevation and status tokens.
- [x] Add responsive sidebar collapse below 1100 px and mobile navigation below 768 px.
- [x] Verify all navigation items update the selected state and page title.

### Task 2: Shared Components and Mock Data

**Files:**
- Create: `mock-data.js`
- Modify: `styles.css`
- Modify: `app.js`

**Interfaces:**
- Produces: `mockData`, `badge()`, `qualityBadge()`, `severityBadge()`, `statusBadge()`, `kpiCard()`, `sparklineSvg()`, `timeSeriesSvg()`.

- [x] Define fictional sites, assets, measurement points, alerts, rule versions and CSV rows.
- [x] Implement reusable status, severity and quality badges using icon + text, not color alone.
- [x] Implement KPI cards with value, context, freshness and drill-down link.
- [x] Implement accessible SVG charts with a Missing-data gap and alert window.
- [x] Verify no mock Missing record is converted to zero.

### Task 3: Operations Overview

**Files:**
- Modify: `app.js`
- Modify: `styles.css`

**Interfaces:**
- Produces: `renderOverview()` route `#/overview`.

- [x] Render four KPI cards: active alerts, High/Critical, missing points and source health.
- [x] Render a 24-hour trend with threshold, alert region and Missing gap.
- [x] Render compact alert queue, data-quality summary, source status and assets requiring attention.
- [x] Make alert rows and point references navigate to details.
- [x] Verify all cards show update or cutoff context.

### Task 4: Alert Queue and Alert Detail Workflow

**Files:**
- Modify: `app.js`
- Modify: `styles.css`

**Interfaces:**
- Produces: `renderAlertQueue()`, `renderAlertDetail(alertId)`, `updateAlertState(alertId, nextState)`; routes `#/alerts`, `#/alerts/:id`.

- [x] Render filter bar, sortable work queue and owner/age/state/severity columns.
- [x] Render alert detail header, trigger summary, point chart, rule snapshot and timeline.
- [x] Implement local interactions: Acknowledge, Assign to me, Start work, Mark recovered, Resolve, Close.
- [x] Enforce state rules so Recovered remains open for review and only Resolve/Close completes the case.
- [x] Append timeline events with fictional current time for each interaction.
- [x] Verify queue values update after returning from detail.

### Task 5: Point Detail

**Files:**
- Modify: `app.js`
- Modify: `styles.css`

**Interfaces:**
- Produces: `renderPointDetail(pointId)` route `#/points/:id`.

- [x] Render point identity, active/source/quality status and latest value with unit and timestamp.
- [x] Render time-range controls and a chart with Missing gap and alert markers.
- [x] Render summary statistics including coverage and missing intervals.
- [x] Render tabs: Overview, History, Data Quality, Rules, Alerts, Source, Audit.
- [x] Make the Rules and Alerts tabs link to relevant prototype screens.

### Task 6: Rule Builder and Review Interaction

**Files:**
- Modify: `app.js`
- Modify: `styles.css`

**Interfaces:**
- Produces: `renderRuleBuilder()`, `runRuleTest()`, `submitRuleReview()` route `#/rules/new`.

- [x] Render five-step rule form: Basic info, Conditions, Settings, Schedule, Review.
- [x] Implement fields for scope, point, type, operator, threshold, duration, cooldown and severity.
- [x] Implement Run Test summary with violations, recoveries, quality exclusions and an evidence chart.
- [x] Require a successful test before enabling Submit for Review.
- [x] Show Submitted state without activating the rule.

### Task 7: CSV Import Preview Wizard

**Files:**
- Modify: `app.js`
- Modify: `styles.css`

**Interfaces:**
- Produces: `renderCsvImport()` route `#/imports/new`.

- [x] Render six-step wizard with Preview selected by default.
- [x] Render summary counts and row-level Valid, Warning, Invalid and Duplicate results.
- [x] Implement row filtering by status and expandable reason detail.
- [x] Implement Confirm & Import Valid interaction that moves to Processing and then Result locally.
- [x] Keep invalid rows excluded and show accepted/rejected totals.

### Task 8: Responsive, Accessibility and Empty/Error States

**Files:**
- Modify: `styles.css`
- Modify: `app.js`

**Interfaces:**
- Produces: compact table/card patterns and reusable `emptyState()`/`errorState()` helpers.

- [x] Add visible keyboard focus states and skip-to-content link.
- [x] Add ARIA labels to icon-only actions and chart descriptions.
- [x] Ensure status is never communicated by color alone.
- [x] Add compact mobile Alert Queue/Detail experience below 768 px.
- [x] Add empty, loading simulation and permission-denied examples accessible from prototype menu.

### Task 9: Smoke Tests, Documentation and Packaging

**Files:**
- Create: `tests/prototype-smoke.html`
- Create: `README.md`

**Interfaces:**
- Consumes: public functions exposed as `window.IumpPrototype`.

- [x] Add browser smoke tests for all P0 routes.
- [x] Assert Missing display text exists and no Missing sample renders `0`.
- [x] Assert Recovered alert is not treated as Closed.
- [x] Document launch, navigation, supported interactions, responsive behavior and limitations.
- [x] Open `index.html` in a browser and manually verify every route and interaction.
- [x] Package the folder as `IUMP_Industrial_Light_P0_Prototype_v0.1.zip`.
