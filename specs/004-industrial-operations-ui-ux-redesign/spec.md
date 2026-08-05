# Feature Specification: Industrial Operations UI/UX Redesign

**Feature Branch**: `004-industrial-operations-ui-ux-redesign`

**Created**: 2026-08-04

**Status**: Draft

**Input**: User description: "/speckit.specify Feature 004 — Industrial Operations UI/UX Redesign"

## Clarifications

### Session 2026-08-04

- Q: Với MVP-1 của Feature 004, bạn muốn giao diện chỉ có Industrial Light hay bắt buộc có cả công tắc Light/Dark ngay từ đầu? → A: Chỉ triển khai Evidence-First Industrial Light trong MVP-1; Dark theme không thuộc phạm vi Feature 004 và được deferred để xem xét sau pilot và accessibility validation.
- Q: Với các bảng vận hành trên desktop, mật độ mặc định nên là Compact hay Comfortable? → A: Compact mặc định cho các bảng vận hành trên desktop; mục tiêu là tăng khả năng scan queue, configuration, Simulator history và Audit trong thời gian dài mà không giảm accessibility hoặc khả năng đọc. Desktop row height được plan trong khoảng 40–44px, nội dung chính khoảng 14px và metadata 12–13px; tablet giữ touch target phù hợp, có thể tăng interaction target mà không tạo density mode nghiệp vụ khác. Detail panels, forms, dialogs và explanatory content có thể thoáng hơn; không tạo user-facing density switch trong Feature 004 nếu chưa có requirement mới được phê duyệt.
- Q: Sidebar/primary navigation cho Feature 004 trên desktop và tablet nên xử lý như thế nào? → A: Desktop wide sidebar expanded với icons, labels và navigation groups; tại tablet hoặc planned breakpoint, sidebar collapse hoặc chuyển thành accessible drawer/rail phù hợp. Luôn có accessible toggle với name, keyboard support và visible focus; collapsed state giữ navigation item, active section và current location; icon-only items có accessible name và tooltip/flyout; tablet overlay/drawer quản lý focus, Escape đóng drawer và trả focus về opener, đồng thời block background interaction. Không persist preference giữa các session và không tạo user setting mới; sidebar không bao giờ biến mất hoàn toàn ở small breakpoint; exact breakpoint/pattern được defer về /speckit.plan dựa trên current layout và DOC-08.

- Q: Sau khi authentication hoàn tất, landing page nên được chọn theo cơ chế nào? → A: Dựa trên effective authorization/permission của người dùng, không hard-code chỉ theo tên role; chuyển đến capability ưu tiên cao nhất mà người dùng thực sự được phép truy cập. Operational Dashboard là fallback nếu không có capability ưu tiên phù hợp, capability bị disabled, route không còn khả dụng, không xác định được landing, hoặc preference/deep link cũ không hợp lệ. Deep link hợp lệ được ưu tiên hơn landing mặc định; deep link không hợp lệ, hết hạn hoặc không được phép phải dùng safe forbidden/not-found experience với next permitted action. Session-expired chỉ quay lại route cũ khi route vẫn hợp lệ và được phép; không tạo landing setting hoặc preference persistence. Thứ tự capability cụ thể được defer đến /speckit.plan; quyết định này chỉ thay đổi navigation/orientation, không thay đổi backend authorization hay role model.
- Q: Should Feature 004 introduce a first-class mobile target alongside desktop and tablet? → A: Feature 004 commits to desktop as the primary working mode and tablet as a first-class supported mode only. Mobile gets no new navigation model, layout system, backend contract, package, framework, breakpoint library, or acceptance suite; existing mobile routes must keep safe non-regression behavior, and unsupported workflows must clearly direct users to desktop or tablet without implying full support. Exact breakpoints and responsive strategy are deferred to `/speckit.plan` based on DOC-08 and the current code.
## User Scenarios & Testing *(mandatory)*

### User Story 1 - Navigate through a consistent industrial application shell (Priority: P1)

As an internal IUMP user, I want a consistent shell that tells me where I am, what scope I am viewing, and what I can do so that I can move between operational areas without guessing.

**Why this priority**: The shell is the shared orientation and permission-safe entry point for every included capability. Without it, improvements on individual pages remain fragmented.

**Independent Test**: From an authenticated session, a tester can visit each included area, identify the active section and scope, reach another permitted area, and return without losing context.

**Acceptance Scenarios**:

1. **Given** a user with access to multiple operational areas, **When** the user opens IUMP and selects a permitted navigation item, **Then** the shell keeps product identity, active section, user/role context, and the selected Site/Area context visible while the destination loads.
2. **Given** a user without permission for a capability or object, **When** the user views navigation or follows a direct link, **Then** unavailable navigation is not presented as an available action and the direct access response is safe without revealing out-of-scope metadata.
3. **Given** a page with a nested object, **When** the user follows a breadcrumb or back action, **Then** the stable hierarchy and scope remain understandable and the user returns to the expected parent context.

4. **Given** an authenticated user with a valid deep link or no deep link, **When** the session completes authentication, **Then** a valid permitted deep link takes precedence; otherwise the user is routed to the highest-priority capability allowed by effective permission, or to Operational Dashboard as a safe fallback without first visiting a forbidden route.

### User Story 2 - Understand operational state from the redesigned dashboard (Priority: P1)

As an Operator or Manager, I want an overview of attention items, freshness, coverage, source health, and trends so that I can decide what to inspect next without mistaking the interface for automatic diagnosis or control.

**Why this priority**: The overview is the primary daily entry point and must establish trust before showing insight.

**Independent Test**: With representative alert, missing, stale, quality, and coverage fixtures, a tester can identify the highest-priority attention item and drill into its supporting detail.

**Acceptance Scenarios**:

1. **Given** a selected Site and optional Area, **When** the overview is displayed, **Then** the page shows the current scope, timezone, cutoff/freshness, operational exceptions, and the next available human action in a stable hierarchy.
2. **Given** low coverage, stale sources, or missing points, **When** the overview is displayed, **Then** the condition is labeled with its impact and a next step, and no unsupported root-cause, savings, or equipment-control claim is made.
3. **Given** no configured points or no received data, **When** the overview is displayed, **Then** configuration absence is distinguished from a configured source that is not receiving data, and the user is given the appropriate permitted path.

### User Story 3 - Manage configuration entities through consistent tables, forms, and lifecycle actions (Priority: P1)

As an Engineer or Administrator, I want Sites, Areas, Assets, Measurement Points, Data Sources, Source Mappings, and Simulator Configurations to use predictable list/detail/form patterns so that I can maintain configuration without losing scope or lifecycle meaning.

**Why this priority**: Configuration is the foundation of trustworthy monitoring and is the largest group of included pages.

**Independent Test**: For each included configuration entity, a tester can find a record, inspect its status and dependencies, edit an allowed Draft, submit validation, and understand why a lifecycle action is available or blocked.

**Acceptance Scenarios**:

1. **Given** a configuration list, **When** the user searches, filters, sorts, or opens a record, **Then** the table preserves readable column hierarchy, active filter/sort state, scope, and a clear row action affordance.
2. **Given** a user editing a Draft configuration, **When** a required field is missing or invalid, **Then** the form identifies the field, summarizes the problem when needed, focuses the first invalid field, and preserves entered values.
3. **Given** a lifecycle action with an unmet dependency, insufficient permission, conflict, or destructive effect, **When** the user requests the action, **Then** the UI explains the blocking reason and required next step, confirms destructive actions with the applicable reason, and never changes server state silently.

### User Story 4 - Operate and review the Simulator through a focused operational workspace (Priority: P1)

As an Engineer, I want a Simulator workspace that keeps scope, selected configuration, run state, controls, history, and outcomes together so that I can reproduce and diagnose a run without confusing it with a physical device.

**Why this priority**: Simulator operations are a primary existing capability and require unusually clear state and result feedback.

**Independent Test**: A tester can select a valid scope and configuration, start or stop a run where permitted, inspect the run history, and distinguish success, failure, conflict, blocked, and retry outcomes.

**Acceptance Scenarios**:

1. **Given** a permitted Simulator workspace, **When** the user opens it, **Then** Site, Area, Asset, Data Source, configuration, current run state, and available operations are visible together.
2. **Given** an inactive or decommissioned prerequisite, **When** the user attempts a run, **Then** the operation is blocked with a specific explanation and no misleading success state is shown.
3. **Given** a run request that completes, fails, conflicts, or can be retried, **When** the result is returned, **Then** the workspace shows the outcome, run identifier/history, relevant counters or reason, and an explicit next action without implying real equipment control.

### User Story 5 - Inspect current Measurements, freshness, and Data Quality without confusing No Data with zero (Priority: P1)

As an Operator, Engineer, Manager, or scoped Viewer, I want current Measurement and Source Health views to show value meaning, timestamps, freshness, source, and Data Quality together so that I can judge whether an observation is usable.

**Why this priority**: Trustworthy interpretation of telemetry is a core product boundary and a documented DOC-08 normative rule.

**Independent Test**: Using fixtures for a valid zero, No Data, Good, Uncertain, Bad, stale, and unavailable observation, a tester can correctly name each condition and its evidence.

**Acceptance Scenarios**:

1. **Given** a received Measurement whose numeric value is zero, **When** the current view renders it, **Then** it displays zero with unit, observation timestamp, source timestamp, source, and quality; it does not label the value as No Data.
2. **Given** an expected observation that has not arrived, **When** the current view renders it, **Then** it displays No Data/Missing separately from zero and includes last-seen (when available), expected interval, elapsed duration, and the source status.
3. **Given** Good, Uncertain, or Bad Data Quality, **When** the view renders it, **Then** each state has text, an icon or shape, a reason/detail, and a consistent treatment that does not rely on color alone.
4. **Given** a time-series view with missing intervals, **When** it renders the series, **Then** missing periods remain visible as gaps and the view exposes unit, timezone, cutoff, quality, and coverage context.

### User Story 6 - Review Audit activity through a readable investigation-oriented workspace (Priority: P2)

As an Auditor, Administrator, Manager, or scoped reviewer, I want to scan and investigate audit events without exposing secrets so that I can establish who did what, when, where, and with what outcome.

**Why this priority**: Audit is an existing trust and accountability surface, but it follows the daily operational workflows after shell and telemetry clarity.

**Independent Test**: With a representative event set, a tester can filter and paginate results, identify actor/action/entity/time/scope/outcome, and inspect a redacted before/after difference.

**Acceptance Scenarios**:

1. **Given** an audit event list, **When** the user filters by actor, action, target, time, outcome, scope, or correlation, **Then** the active filters and result count remain visible and pagination preserves the investigation context.
2. **Given** an event with before/after data, **When** the user opens its detail, **Then** the difference is readable, safe redaction is applied, and no secret is rendered or recoverable from the presentation.
3. **Given** an out-of-scope target, **When** the user follows a direct audit link, **Then** the UI returns the contract-appropriate safe forbidden/not-found experience without leaking target metadata.

### User Story 7 - Use the application effectively with keyboard navigation, desktop/tablet layouts, and explicit feedback (Priority: P1)

As an internal user working for long periods, I want readable, keyboard-accessible, responsive screens and explicit feedback for every important state so that I can complete work reliably without depending on color, pointer precision, or hidden conventions.

**Why this priority**: Accessibility, operational resilience, and tablet support are cross-cutting acceptance requirements for every redesigned surface.

**Independent Test**: A tester can complete representative navigation, list, form, and status tasks using keyboard-only input at desktop and tablet widths, including an error/retry path.

**Acceptance Scenarios**:

1. **Given** a supported desktop or tablet viewport, **When** the user navigates through included screens, **Then** the hierarchy, essential data, filters, actions, and state remain readable without accidental horizontal overflow or hidden essential information.
2. **Given** keyboard-only input, **When** the user moves through navigation, tables, forms, dialogs, drawers, and pagination, **Then** focus is visible, tab order follows visual order, controls have names, and the primary flows are completable without a pointer.
3. **Given** loading, empty, stale, partial, error, forbidden, conflict, blocked, or retry conditions, **When** the state appears, **Then** context is retained, the message states impact and next action, and recovery does not require guessing.

### Edge Cases

- A user has one permitted Site or Area; the context control remains understandable and may be non-editable rather than disappearing.
- A list has no records, no matching filters, or records that are all outside the current scope; the empty state distinguishes each case and offers only permitted next actions.
- A long table or wide evidence difference cannot fit the viewport; the responsive treatment preserves column meaning and provides an explicit, accessible way to inspect remaining content.
- A request finishes after the user has navigated away, the session expires, or another user changes the record; the UI shows the current safe outcome and offers reload/retry without overwriting silently.
- A background operation is queued, processing, completed with errors, failed, or expired; the user sees the state, reason/correlation where applicable, and a valid next step.
- A chart has an insufficient or mixed-quality period; the UI identifies coverage/quality limitations and does not imply a complete comparison.
- A conditional capability is disabled for the environment; it is not presented as a production-ready navigation or action.
- An existing mobile route reaches a workflow outside the Feature 004 mobile boundary; the route remains safe, identifies the unsupported or limited state, preserves authorization and essential context, and directs the user to continue on desktop or tablet without implying full mobile support.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The application MUST provide one consistent shell for all included areas with product identity, active section, navigation hierarchy, current user and role, logout/account actions, and operational context when available. After authentication, landing MUST be resolved from effective authorization/permission rather than hard-coded role names; a valid permitted deep link takes precedence, and Operational Dashboard is the safe fallback when no permitted priority capability is available.
- **FR-002**: Navigation MUST group existing capabilities by understandable operational areas, preserve permission-safe visibility, and omit conditional capabilities that are not enabled for the environment. On desktop the primary navigation MUST be a wide expanded sidebar with icons, labels, and navigation groups. At the tablet or planned breakpoint the sidebar MUST collapse or become an accessible drawer/rail, MUST offer an accessible toggle with an accessible name, keyboard support, and visible focus, MUST preserve the navigation item, active section, and current location when collapsed, MUST give icon-only items an accessible name and tooltip/flyout, and MUST never disappear completely at a small breakpoint. The collapsed preference MUST NOT be persisted between sessions and MUST NOT introduce a new user setting; the exact breakpoint and pattern are deferred to /speckit.plan against the current layout and DOC-08.
- **FR-003**: Every included page MUST present a clear Vietnamese title, a concise purpose or scope description, at most one visually primary action, predictable secondary actions, and stable placement for filters, content, details, and status.
- **FR-004**: Every included page MUST define and present applicable loading, empty, error, forbidden, conflict, blocked, stale/partial, and retry states without removing the user's context.
- **FR-005**: The shell and data-bearing pages MUST keep the selected Site/Area, Site timezone, data cutoff, refresh/freshness, and live/stale/degraded context visible when applicable.
- **FR-006**: The Operational Dashboard MUST summarize current operational state, exceptions, attention items, source health, coverage, and useful trends with drill-down paths, while making no unsupported root-cause, savings, automatic decision, or equipment-control claim.
- **FR-007**: Configuration pages for Sites, Areas, Assets, Measurement Points, Data Sources, Source Mappings, and Simulator Configurations MUST use consistent list, detail, search/filter, status, dependency, and lifecycle presentation.
- **FR-008**: Configuration forms MUST distinguish editable Draft information from other lifecycle states, show required fields and field-level validation, focus the first invalid field, preserve user input on validation failure, and explain unavailable actions.
- **FR-009**: Lifecycle actions MUST respect existing authorization, transition, dependency, validation, version, and conflict behavior; the UI MUST not imply completion before the existing server-confirmed outcome is available.
- **FR-010**: Simulator screens MUST show selected scope and configuration, current run state, permitted operations, operation history, run identifier, and success/failure/conflict/blocked/retry outcomes, and MUST NOT represent the Simulator as physical equipment control.
- **FR-011**: Current Measurement and Source Health views MUST distinguish a valid zero from No Data/Missing, and MUST show value meaning with unit, observation timestamp, source timestamp, source, freshness, and availability when present.
- **FR-012**: Good, Uncertain, Bad, and Missing Data Quality states MUST have consistent text, non-color cues, and a reason or detail; missing periods in time-series views MUST remain visible as gaps rather than being rendered as zero or silently interpolated.
- **FR-013**: Tables MUST provide readable column hierarchy, stable alignment, sensible density, visible sort/filter state, explicit row actions, meaningful empty states, pagination where needed, and responsive behavior that preserves essential information. The compact-density rule in FR-027 applies only to operational data tables and lists, not to detail panels, forms, dialogs, or explanatory content.
- **FR-014**: Forms and dialogs MUST group related fields, expose required fields, communicate save/cancel/retry/conflict outcomes, warn about unsaved changes when loss is possible, and require safe confirmation and reason for destructive actions where policy requires it.
- **FR-015**: Audit pages MUST support investigation by actor, action, target/entity, time, outcome, scope, and correlation; show readable event summaries and pagination; and present safe redacted before/after differences without secrets.
- **FR-016**: Status MUST never be conveyed by color alone. Status, severity, quality, source, job, and lifecycle states MUST also have text and an icon, shape, pattern, or equivalent non-color cue.
- **FR-017**: The included experience MUST use a restrained Evidence-First Industrial Light visual direction with a coherent typography hierarchy, spacing, surfaces, borders, elevation, icons, controls, badges, notifications, dialogs, drawers, tabs, breadcrumbs, pagination, loading placeholders, and purposeful charts. A dark theme is not part of the Feature 004 MVP-1 release.
- **FR-018**: Visual hierarchy MUST prioritize operational state, exceptions, selected scope, timestamps/freshness, Data Quality, and available human actions ahead of decoration or unsupported analytics.
- **FR-019**: The experience MUST treat desktop as the primary working mode and tablet as a first-class supported mode; essential data and actions MUST remain available at supported tablet widths, including the accessible collapsed sidebar/drawer, responsive table or evidence treatment, keyboard operation, and touch-safe targets. Feature 004 MUST NOT introduce a new mobile navigation model, layout system, mobile-first experience, mobile-specific backend contract, package, framework, breakpoint library, or mobile acceptance suite. Existing mobile routes MUST not be intentionally broken: they must load safely, preserve authentication/authorization, avoid out-of-scope metadata, must not lose essential content entirely, present clear errors or unsupported states, and avoid crashes, blank screens, or unintended destructive actions. A mobile-unsupported workflow MUST clearly direct the user to desktop or tablet and MUST NOT imply full mobile support.
- **FR-020**: Included screens MUST support keyboard navigation with visible focus, logical tab order, semantic labels, accessible names for icon-only actions, field-to-error association, and a textual/table alternative for important chart information. A tablet navigation drawer/overlay used for the collapsed sidebar MUST manage focus (trap within the drawer), close on Escape, return focus to the opener on close, and block background interaction while open.
- **FR-021**: Vietnamese MUST be the primary interface language. Technical identifiers, codes, and reasons MAY retain their original form when translation would reduce accuracy, but terminology MUST remain consistent across screens.
- **FR-022**: The redesign MUST preserve existing API contracts, domain rules, authorization enforcement, audit behavior, PostgreSQL behavior, and Feature 003 functionality; a UI change MUST NOT introduce a substitute data store or new backend capability.
- **FR-023**: Direct access, forbidden, expired-session, and unavailable-resource responses MUST be safe, explain the user's next permitted action, and MUST NOT disclose out-of-scope object metadata or secrets. An invalid, expired, or unauthorized deep link MUST use the safe forbidden/not-found experience; an expired-session return MUST restore the prior route only when it remains valid and permitted, otherwise it MUST use the landing fallback without probing or revealing unauthorized capability/object metadata.
- **FR-024**: Feedback for important operations MUST distinguish succeeded, failed, blocked, conflicted, pending/processing, completed-with-errors, and retryable outcomes and retain a correlation/reference identifier when the existing behavior provides one.
- **FR-025**: Scope selectors, filters, breadcrumbs, drill-downs, and back navigation MUST preserve scope and terminology so a user can understand where a result came from and return to the expected parent context.
- **FR-026**: The feature MUST keep new or conditional capabilities (including Trusted Telemetry Ingestion, CSV import, Rule Version lifecycle, Alert creation/workflow, Notifications, reports, savings calculations, AI/ML, Modbus, Edge Collector, equipment control/write-back, and deployment approval) out of the redesign release unless they already exist as an explicitly included, contract-compatible surface; no new backend behavior is authorized by this specification.
- **FR-027**: Operational data tables and operational lists MUST use compact density by default to support prolonged scanning of queues, configuration, Simulator history, and Audit. On desktop, the target row height is 40–44px with primary content around 14px and metadata around 12–13px. The compact treatment MUST preserve readable contrast, keyboard focus, semantic labels, wrapping or an accessible expansion path, and non-color status cues. Tablet interaction targets MAY be taller for touch usability without introducing a separate business density mode, and Feature 004 MUST NOT add a user-facing density switch without an approved requirement.

- **FR-028**: Post-authentication landing MUST choose the highest-priority capability the user is actually authorized to access, using effective permission as the source of truth and role only as optional orientation within the existing permission model. It MUST prefer a valid permitted deep link, fall back to Operational Dashboard when the priority route is unavailable, disabled, unknown, or no longer permitted, and MUST never redirect through a forbidden route. Feature 004 MUST NOT add a landing-page setting or persisted preference, disclose unauthorized capability/object metadata, or change backend authorization or role behavior; the concrete capability order is deferred to `/speckit.plan`.

### Key Entities

- **Application Context**: The selected Site/Area, timezone, environment, user/role, permission scope, cutoff, and freshness context shown across screens.
- **Operational Overview**: A read-only presentation of current exceptions, attention items, source health, coverage, quality, and trends with links to supporting detail.
- **Configuration Record**: An existing Site, Area, Asset, Measurement Point, Data Source, Source Mapping, or Simulator Configuration with status, scope, dependencies, and permitted lifecycle actions.
- **Simulator Run**: An existing run's selected context, configuration, state, identifier, history, counters or diagnostic reason, and outcome.
- **Measurement Presentation**: A current or historical observation shown with value, unit, timestamps, source, freshness, availability, Data Quality, coverage, and Missing semantics.
- **Audit Event**: An existing append-only activity record with actor, action, target/entity, time, outcome, scope, correlation, and redacted before/after information.
- **Interaction State**: A screen or component state such as loading, empty, stale, partial, error, forbidden, conflict, blocked, retryable, or completed-with-errors that must retain context and next action.

### Requirement Traceability

| Requirement(s) | Acceptance coverage |
|---|---|
| FR-001–FR-005, FR-025 | US1 scenarios 1–3; US7 scenarios 1–3; SC-001, SC-004, SC-011 |
| FR-002, FR-019–FR-020 | US1 scenario 1; US7 scenarios 1–2; SC-005, SC-006, SC-014 |
| FR-006 | US2 scenarios 1–3; SC-002, SC-009, SC-011 |
| FR-007–FR-009, FR-014 | US3 scenarios 1–3; US7 scenario 3; SC-004, SC-007, SC-009 |
| FR-010 | US4 scenarios 1–3; SC-002, SC-004, SC-010 |
| FR-011–FR-012 | US5 scenarios 1–4; SC-003, SC-011 |
| FR-013 | US3 scenario 1; US6 scenario 1; US7 scenario 1; SC-006, SC-009 |
| FR-015 | US6 scenarios 1–3; SC-008, SC-009 |
| FR-016–FR-018 | US2 scenario 1; US5 scenario 3; US7 scenarios 1–3; SC-001, SC-003, SC-009 |
| FR-019–FR-021 | US7 scenarios 1–2; SC-005, SC-006, SC-009, SC-014 |
| FR-022–FR-023 | US1 scenario 2; US3 scenario 3; US7 scenario 3; SC-008, SC-012 |
| FR-024 | US4 scenario 3; US7 scenario 3; SC-004, SC-008 |
| FR-028 | US1 scenarios 1–4; US7 scenarios 1–3; SC-001, SC-008, SC-010, SC-015 |
| FR-019 mobile non-regression boundary | US7 scenarios 1–3; Edge case 8; SC-005, SC-006, SC-016 |
| FR-026 | Edge case 7; SC-010; Scope and Evidence Boundaries |
| FR-027 | US3 scenario 1; US4 scenario 3; US6 scenario 1; US7 scenarios 1–2; SC-005, SC-006, SC-009 |

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of included top-level areas use the same application shell and page contract, and a usability reviewer can identify current section, scope, and primary action on every representative screen.
- **SC-002**: At least 90% of representative P0 tasks (navigate, find a configuration, inspect current data, investigate an exception, and review a run) are completed without facilitator intervention, with zero critical misunderstandings of scope or status.
- **SC-003**: In a fixture-based review, 100% of valid-zero, No Data/Missing, Good, Uncertain, Bad, stale, and unavailable cases are correctly distinguishable from the rendered text and non-color cues alone.
- **SC-004**: 100% of included screens document and render each applicable non-happy-path state (loading, empty, error, forbidden, conflict, blocked, stale/partial, and retry), with an observable recovery or next-action path for every recoverable state.
- **SC-005**: A keyboard-only reviewer can complete the representative navigation, list/filter, detail, form-validation, and feedback-recovery flows on desktop and tablet, with visible focus and no unnamed interactive control.
- **SC-006**: At supported desktop and tablet widths, 100% of essential page content and actions remain available without accidental horizontal overflow; any intentionally scrollable table or evidence region has an explicit accessible treatment.
- **SC-007**: 100% of destructive configuration or lifecycle actions tested require the applicable confirmation/reason and provide an unambiguous cancel path; no tested action silently overwrites a conflict.
- **SC-008**: 100% of authorization and forbidden-state tests preserve the existing server-enforced outcome and avoid disclosure of out-of-scope metadata, while permitted Feature 003 workflows remain reachable.
- **SC-009**: A visual consistency review of the shell, dashboard, configuration, simulator, measurement, audit, table, form, status, and feedback patterns records no unresolved high-severity inconsistency in typography, spacing, status semantics, action placement, or terminology.
- **SC-010**: 100% of included navigation and route checks hide disabled conditional capabilities and do not expose new backend, control, savings, AI/ML, or other out-of-scope claims.
- **SC-011**: Operational screenshots and task review show that scope, timezone, cutoff/freshness, timestamps, Data Quality, source status, and coverage appear before or alongside the data interpretation they qualify on every applicable screen.
- **SC-012**: Existing Feature 003 acceptance/regression checks remain green for the redesigned experience, with any unrelated or blocked evidence reported separately rather than reclassified as a UI success.
- **SC-013**: Desktop review of each operational table/list confirms the compact default target (40–44px row height, approximately 14px primary content and 12–13px metadata) while preserving readable text, visible focus, keyboard operation, touch-safe tablet targets, and an accessible way to inspect wrapped or additional content; no user-facing density switch is present.
- **SC-014**: At desktop width the primary navigation is a wide expanded sidebar with icons, labels, and navigation groups; at the tablet or planned breakpoint it collapses or becomes an accessible drawer/rail with a named, keyboard-operable toggle and visible focus, preserves the navigation item, active section, and current location, gives icon-only items an accessible name and tooltip/flyout, and never disappears entirely. The drawer/overlay traps focus, closes on Escape, returns focus to the opener, and blocks background interaction; the preference is not persisted between sessions and no new user setting is introduced.
- **SC-015**: 100% of tested post-authentication and session-expiry flows route only to capabilities allowed by effective permission, prioritize valid permitted deep links, use Operational Dashboard as the documented fallback when needed, and present safe forbidden/not-found recovery for invalid or unauthorized links without exposing out-of-scope capability or object metadata.
- **SC-016**: At supported tablet widths, 100% of representative flows retain essential data and actions with accessible sidebar/drawer behavior, explicit table/evidence overflow treatment, keyboard operation, and touch-safe targets. Existing mobile routes tested for non-regression load without crash or blank screen, preserve authentication/authorization and essential content, avoid unintended destructive actions and out-of-scope metadata, and clearly identify unsupported workflows with a desktop/tablet next step.

## Assumptions

- DOC-08 v0.1 is the authoritative UI/UX direction for this feature but remains a draft; Evidence-First Industrial Light is the selected MVP-1 direction, not a final corporate brand sign-off.
- Feature 004 MVP-1 is light-only. Dark theme is an explicitly deferred option that may be reconsidered only after pilot feedback and accessibility validation; no dark-theme implementation or acceptance evidence is required in this feature.
- Operational data tables and lists use compact density by default for prolonged scanning. The compact target applies only to those surfaces; detail panels, forms, dialogs, and explanatory content may use more generous spacing. Tablet may increase interaction target height without adding a separate business density mode.
- The primary navigation sidebar is expanded on wide desktop and collapses or becomes an accessible rail/drawer at a tablet or planned breakpoint. It always has a named, keyboard-operable toggle with visible focus; tablet overlays manage focus, close on Escape, return focus to the opener, and block background interaction. The sidebar never disappears completely, no new user setting is created, and the collapsed state is not persisted between sessions. Exact breakpoint and implementation pattern are deferred to `/speckit.plan` based on the current layout and DOC-08.
- Post-authentication landing is permission-driven: effective authorization is authoritative, role names are only optional orientation, valid permitted deep links take precedence, and Operational Dashboard is the fallback for unavailable or unknown priority routes. No landing setting or persisted preference is introduced; the capability priority order and exact route checks are deferred to `/speckit.plan`, while backend authorization and the role model remain unchanged.
- Vietnamese is the default interface language; technical identifiers and reason codes may remain in English when that preserves operational accuracy.
- Desktop is the primary working mode and tablet is a first-class supported mode. Mobile is not a first-class target: no new mobile navigation, layout, mobile-first experience, or acceptance suite is required. Existing mobile routes receive safe non-regression coverage only, and unsupported workflows must explain how to continue on desktop or tablet.
- No mobile-specific backend contract, package, framework, or breakpoint library is authorized by Feature 004; exact breakpoint values and responsive implementation strategy are deferred to `/speckit.plan` using DOC-08 and the current code.
- Feature 003 is the complete functional baseline. Existing APIs, authorization, data semantics, audit behavior, and PostgreSQL configuration are reused; no security corrective work is reopened here.
- Existing permission assignments, lifecycle policies, quality semantics, source timestamps, coverage calculations, and server outcomes are authoritative even when the redesign changes their presentation.
- Mockup and illustrative values in DOC-08 are examples for layout and content, not production thresholds, measurements, savings, or operational conclusions.
- Formal brand assets, pilot-user validation, and final terminology sign-off remain product/UX validation inputs before a production visual baseline. Dark-theme need is deferred beyond Feature 004 and is not an MVP-1 decision; compact operational-table density is the selected Feature 004 default.

## Scope and Evidence Boundaries *(mandatory)*

- **Included release/capability**: Feature 004 specification for a cohesive Industrial Operations Console presentation of the existing application shell, navigation, Operational Dashboard, configuration pages, Simulator workspace/run history, current Measurement and Source Health views, Audit, and shared tables/forms/filters/feedback/dialog/status patterns; desktop and tablet usability, keyboard/accessibility, and explicit state presentation are acceptance concerns.
- **Explicitly excluded**: New backend/business capabilities; Trusted Telemetry Ingestion; CSV import; Rule Version lifecycle; Alert creation or workflow; Notifications; reports; savings calculations; AI/ML; Modbus; Edge Collector; equipment control or write-back; deployment approval; changes to existing business rules; replacing PostgreSQL; and API-contract changes unless a genuine UI blocker is separately documented and approved.
- **Responsive/mobile boundary**: Desktop is primary and tablet is first-class for Feature 004. A new mobile navigation/layout system, mobile-first experience, mobile application, mobile-specific backend contract, mobile acceptance suite, package, framework, or breakpoint library is out of scope. Existing mobile routes must retain safe non-regression behavior only; workflows that are not suitable for mobile must state the limitation and direct the user to desktop or tablet.
- **Deferred options**: Dark theme is deferred until after pilot feedback and accessibility validation; it is not part of Feature 004 MVP-1 and must not create an implementation dependency or acceptance requirement here.
- **External approvals/dependencies**: DOC-08 UI/UX review; terminology and priority confirmation by Product Owner and representative Operator/Engineer/Manager/Reviewer users; accessibility review; existing Feature 003 contracts and authorization; repository-restricted PostgreSQL and package policy; no new package, service, or registry access is authorized by this specification.
- **Evidence classification**: NOT_RUN — this `/speckit.specify` deliverable creates only the feature specification, quality checklist, active-feature pointer, and feature branch. No production code, technical plan, tasks, implementation, database mutation, or harness execution is included.
