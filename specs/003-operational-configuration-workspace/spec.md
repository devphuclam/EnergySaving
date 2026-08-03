# Feature Specification: Operational Configuration Workspace

**Feature Branch**: `003-operational-configuration-workspace`

**Created**: 2026-07-30

**Status**: Implemented — Release Evidence Blocked

**Input**: Transform the proven Feature 002 runtime into a role-aware, PostgreSQL-backed,
human-operable internal workspace for guided setup, configuration management, Simulator operation,
Latest and Source Health review, and Audit review.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Complete and Resume Initial Setup (Priority: P1)

An Administrator starts the first configuration, activates a Site when it is eligible, assigns an
existing Engineer to that Site, and hands off the remaining setup. The assigned Engineer resumes
from persisted state, creates the Area, Asset, Measurement Point, Data Source, Source Mapping, and
Simulator Configuration, validates the complete chain, and activates only the entities that support
activation in their legal order. Completion navigates to the Simulator page but never starts a Run.

**Why this priority**: The application is not operationally usable until authorized people can
create one complete configuration without direct database work, scripts, or hidden client claims.

**Independent Test**: Starting from an empty approved development database, an Administrator and an
assigned Engineer can complete the handoff and one valid chain through the UI, survive browser and
host restarts, and reach a Simulator page that requires an explicit Start action.

**Acceptance Scenarios**:

1. **Given** no Site exists and an Administrator signs in, **When** the landing state is evaluated,
   **Then** the Setup Wizard opens at editable Site and Engineer assignment.
2. **Given** no Site exists and an Engineer signs in, **When** the landing state is evaluated,
   **Then** an “Administrator setup required” state appears without a Site creation action.
3. **Given** an Administrator created and activated a Site and assigned an Engineer, **When** that
   Engineer signs in, **Then** the Site is read-only and setup continues only inside assigned scope.
4. **Given** any completed setup step, **When** the browser, Web host, API host, or session restarts,
   **Then** the wizard reconstructs the completed and next steps from persisted server state.
5. **Given** the complete chain is valid, **When** the user requests activation, **Then** eligible
   entities activate in legal domain order and the Simulator remains stopped.
6. **Given** activation fails partway, **When** the result is displayed, **Then** committed Draft
   entities remain persisted, no entity is falsely shown Active, the failed step is identified, and
   a retry does not create duplicate entities or events.
7. **Given** a mutation is retried after an uncertain network result, **When** the same operation is
   submitted again, **Then** the operation is idempotent and returns the existing outcome.
8. **Given** another user changed an entity version, **When** stale data is submitted, **Then** the
   user receives a conflict state and cannot silently overwrite the current version.

---

### User Story 2 - Manage Configuration Safely (Priority: P2)

An authorized user can find, inspect, create, edit, duplicate, validate, activate, and where domain
rules allow deactivate, decommission, or delete Draft configuration entities from dedicated
management pages.

**Why this priority**: After initial setup, operators need durable maintenance workflows rather than
repeating the first-run wizard.

**Independent Test**: For each supported configuration entity, an authorized user can complete the
allowed lifecycle actions while forbidden, conflicting, referenced, and historical states fail
safely and visibly.

**Acceptance Scenarios**:

1. **Given** multiple authorized entities, **When** a user searches, filters, or pages a list,
   **Then** results are scope-filtered before paging and contain no out-of-scope counts.
2. **Given** an eligible entity, **When** it is duplicated, **Then** the duplicate has a new identity,
   a unique proposed code/name, Draft state, reviewable relationships, and no operational history,
   version, Run, Measurement, Latest, Source Health, Audit, session, credential, or secret data.
3. **Given** a behavior-changing field is edited, **When** the edit is saved, **Then** a Draft
   transition requiring validation and activation is created without changing historical
   Measurement meaning or a running Simulator’s pinned configuration.
4. **Given** an Active, referenced, or historically used entity, **When** deletion is attempted,
   **Then** existing dependency-protection or lifecycle rules are enforced without unconditional
   destructive deletion.

---

### User Story 3 - Operate an Explicitly Selected Simulator (Priority: P3)

An authorized Engineer explicitly selects Site, optional Area/Asset context, Data Source, and active
Simulator Configuration before manually controlling a Simulator Run and reviewing recent Run
history.

**Why this priority**: Runtime operation must be deliberate, scoped, and traceable; choosing the
first returned Source is not an acceptable business decision.

**Independent Test**: With two authorized Sources and configurations, an Engineer can select either
eligible combination, manually Start/Pause/Resume/Stop it, and observe the correct Run and counters
without any implicit first-item selection.

**Acceptance Scenarios**:

1. **Given** authorized Sources are loaded, **When** the Simulator page opens, **Then** no Source or
   configuration is selected implicitly and no Run starts.
2. **Given** an authorized Source, active Point, valid active Mapping, and eligible configuration,
   **When** the Engineer presses Start, **Then** the server authorizes and creates one idempotent Run.
3. **Given** an ineligible or conflicting selection, **When** Start is requested, **Then** the exact
   validation, authorization, or conflict outcome is shown without creating a Run.
4. **Given** a selected Run, **When** its status changes, **Then** Run ID, status, counters, last
   production time, interval, controls, and recent history reflect that Run only.

---

### User Story 4 - Observe Selected Latest and Source Health (Priority: P4)

An authorized user explicitly selects Site, Area, Asset, and Measurement Point, then observes Latest
and Source Health with automatic or manual refresh.

**Why this priority**: Users must understand current data and source condition without mistaking a
missing Measurement for zero or viewing an arbitrary first Point.

**Independent Test**: With at least two authorized Points, the user selects either Point and sees
only its Latest, quality, timestamps, Source Health, Run state, and counters refreshed every ten
seconds by default.

**Acceptance Scenarios**:

1. **Given** authorized Points are loaded, **When** Latest opens, **Then** no Point is selected
   implicitly.
2. **Given** a selected Point with an Accepted Measurement, **When** data refreshes, **Then** numeric
   value, canonical unit, quality, source timestamp, and received timestamp are shown.
3. **Given** the expected Measurement has not arrived, **When** Latest is rendered, **Then** explicit
   No Data is shown and never represented as numeric zero.
4. **Given** auto refresh is enabled, **When** ten seconds elapse, **Then** the selected Point
   refreshes without a page reload; the user may disable auto refresh or refresh manually.

---

### User Story 5 - Navigate Operational State and Review Audit (Priority: P5)

An authorized user lands on a practical Operational Dashboard when at least one valid chain exists,
can continue incomplete setup, and can review scoped Audit activity with server-side filters and
pagination.

**Why this priority**: A usable internal application must guide the next operational action and make
configuration and Simulator changes reviewable without exposing sensitive information.

**Independent Test**: A user with an operational chain sees only authorized summary data and can
filter recent Audit events; a user without scope sees no global counts; dependency failure shows no
fallback data.

**Acceptance Scenarios**:

1. **Given** at least one valid operational chain, **When** login completes, **Then** the Operational
   Dashboard opens with authorized Sites, Sources, Points, Runs, Latest, Source Health, incomplete
   setup, recent Audit, and available runtime status.
2. **Given** setup is incomplete, **When** the Dashboard is shown, **Then** Continue Setup identifies
   the completed step and next required action.
3. **Given** an Engineer has no assigned scope, **When** login completes, **Then** No Authorized
   Scope appears without global counts.
4. **Given** the API or database is unavailable, **When** workspace state is requested, **Then** a
   dependency error appears and no local fallback or demo data is displayed.
5. **Given** an authorized reviewer supplies Audit filters, **When** results are requested, **Then**
   filtering and pagination occur server-side and safe before/after values exclude credentials and
   secrets.

### Edge Cases

- The server returns entities in a different order between requests; the UI does not infer a
  selection or completed step from list position.
- Scope is revoked between page load and mutation; the server fails closed and the UI clears stale
  privileged actions without exposing resource metadata.
- Two users edit the same Draft; the second stale submission receives an optimistic conflict with a
  reload/compare path.
- A network timeout occurs after the server committed a mutation; retry with the same idempotency key
  resolves to the committed outcome.
- Validation finds failures in multiple wizard steps; every failure is grouped by step and focus
  moves to the first invalid field.
- Activation fails after some legal transitions committed; persisted states are re-read before retry
  and no duplicate outbox or Audit event is produced.
- A safe Draft has dependencies or historical use by the time deletion is confirmed; deletion is
  rejected using the current dependency contract.
- A Simulator Run remains pinned to its original configuration while a replacement Draft is edited
  or activated.
- Latest has no Measurement, a Bad Measurement, late data, or stale Source Health; each is displayed
  distinctly and No Data remains non-numeric.
- Audit before/after values contain fields classified as sensitive; sensitive values are redacted or
  omitted before reaching the browser.
- A narrow screen is used; the wizard exposes an accessible compact step list and all primary
  actions remain keyboard reachable.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST derive post-login workspace state from persisted, server-authorized,
  scope-filtered information and route to Setup Wizard, Continue Setup, Operational Dashboard, No
  Authorized Scope, or Dependency Error.
- **FR-002**: The system MUST provide an eight-step resumable setup flow covering Site and Engineer
  assignment, Area, Asset, Measurement Point, Data Source, Source Mapping, Simulator Configuration,
  and Validate/Activate.
- **FR-003**: Only an Administrator MUST be able to create a root Site, activate it according to
  existing domain rules, select an existing Engineer, and grant appropriate Site scope.
- **FR-004**: An Engineer MUST be able to continue setup only in an assigned Site/Area; the Site step
  MUST be read-only and an Engineer MUST NOT create a root Site through client data or claims.
- **FR-005**: Wizard completion and progress MUST be reconstructed from committed server state and
  survive refresh, logout/login, API restart, and Web restart without browser storage as authority.
- **FR-006**: Every mutation MUST use server-side principal resolution, authorization, an
  idempotency key, and the current entity version where concurrency applies.
- **FR-007**: Complete-chain validation MUST identify failures by wizard step before activation.
- **FR-008**: Activation MUST use only existing legal domain transitions in their established order;
  it MUST NOT invent activation for unsupported entities or start the Simulator automatically.
- **FR-009**: Partial activation failure MUST preserve committed valid Draft data, show the exact
  failed step, never falsely display Active state, and permit duplicate-safe retry.
- **FR-010**: Every query MUST filter authorized scope before paging; out-of-scope access MUST fail
  closed with established safe unauthorized, forbidden, or not-found behavior.
- **FR-011**: The wizard MUST support Back, Save and Continue, Continue Setup, Cancel, retry, field
  validation, focus of the first invalid field, and safe Draft discard only when domain rules allow.
- **FR-012**: Dedicated pages MUST manage Sites, Areas, Assets, Measurement Points, Data Sources,
  Source Mappings, and Simulator Configurations with applicable scoped list, search, status filter,
  pagination, details, create, edit, duplicate, validate, lifecycle, and safe Draft deletion actions.
- **FR-013**: Existing decorative actions such as Open Hierarchy and Review Mapping MUST become real
  navigation/actions or be removed.
- **FR-014**: Duplication MUST create a new uniquely identified Draft, require relationship review,
  and exclude Active state, optimistic version, Runs, Measurements, Latest, Source Health history,
  Audit, sessions, tokens, credentials, and secrets.
- **FR-015**: Direct editing MUST be limited to non-behavioral fields already supported by the
  domain; changes to measurement meaning, lineage, eligibility, mapping, or runtime behavior MUST
  use a Draft/versioned validation and activation transition.
- **FR-016**: Historical Measurement meaning and a running Simulator’s pinned configuration MUST
  remain unchanged by later edits.
- **FR-017**: Simulator operation MUST require explicit authorized Site, optional Area/Asset, Source,
  and active Simulator Configuration selection; no first Source may be an implicit choice.
- **FR-018**: Start MUST require an authorized Source, valid active Mapping, active Point, eligible
  configuration, no invalid conflicting Run, server authorization, and an idempotency key.
- **FR-019**: The Simulator page MUST provide Start, Pause, Resume, Stop, recent Run history, and the
  selected Source/configuration version, Run ID/status/counters, last production time, and interval.
- **FR-020**: Latest and Source Health MUST require explicit authorized Site, Area, Asset, and
  Measurement Point selection; no first Point may be an implicit choice.
- **FR-021**: Latest MUST show value, canonical unit, quality, source/received timestamps, Source
  Health, Run status, counters, and explicit No Data that is never numeric zero.
- **FR-022**: Latest MUST refresh every ten seconds by default using existing browser capabilities
  and support disabling auto refresh and manual refresh.
- **FR-023**: The Operational Dashboard MUST show only authorized operational navigation summaries,
  incomplete setup, recent Audit, and available runtime status; it MUST NOT claim energy savings.
- **FR-024**: Audit review MUST provide server-side date/time, actor, action, entity type, entity ID,
  and applicable Site/Area filters plus pagination, correlation ID where permitted (the
  `AUDIT_CORRELATION` capability is Administrator-only), and a server-redacted safe before/after
  diff while preserving append-only behavior.
- **FR-025**: Every interactive page and mutation MUST expose explicit loading, empty, submitting,
  success, validation, conflict, forbidden, not-found, dependency-conflict, and runtime/network error
  states without local fallback/demo data.
- **FR-026**: User-facing text MUST be Vietnamese while technical identifiers, status codes, paths,
  and code symbols remain English.
- **FR-027**: The interface MUST be keyboard accessible, responsive at common desktop/tablet widths,
  provide visible focus, and never communicate state by color alone.
- **FR-028**: Destructive or deactivating actions MUST require confirmation and follow existing
  dependency, deactivation, decommission, and Audit rules.
- **FR-029**: Configuration and Simulator actions MUST create reviewable Audit records through the
  established host transaction/outbox/Audit path where required and MUST NOT expose secrets.
- **FR-030**: The feature MUST reuse the existing IAM, scope, domain ownership, PostgreSQL-backed
  persistence, and API/Worker runtime without controller-level database bypasses or a competing
  workflow datastore unless a plan-approved projection is proven necessary.

### Acceptance Criteria

- **AC-001**: After login, a user is routed to Wizard, Continue Setup, Dashboard, No Scope, or
  Dependency Error according to server-derived state.
- **AC-002**: An Administrator can create a Site, assign Engineer scope, and hand off remaining setup
  without direct SQL or scripts.
- **AC-003**: An Engineer can resume and complete Area, Asset, Point, Source, Mapping, and Simulator
  Configuration only inside assigned scope.
- **AC-004**: The complete chain can be validated and legally activated without starting the
  Simulator automatically.
- **AC-005**: Browser or host restart during setup does not lose persisted progress.
- **AC-006**: Management pages use real PostgreSQL-backed data and working actions rather than
  decorative buttons.
- **AC-007**: A duplicate is a new Draft with no operational history or secret material.
- **AC-008**: Behavior-changing edits use a Draft/versioned transition and preserve historical
  Measurement meaning.
- **AC-009**: Simulator operation uses an explicitly selected Source/configuration and never an
  implicit first Source.
- **AC-010**: Latest and Health use an explicitly selected Point and never an implicit first Point.
- **AC-011**: After manual eligible Simulator start and Worker execution, an Accepted Measurement is
  visible through Latest using the supported UI journey.
- **AC-012**: Latest refreshes at the configured interval without page reload and No Data is never
  displayed as zero.
- **AC-013**: Audit records from configuration and Simulator actions are visible to authorized
  reviewers with filtering and redaction.
- **AC-014**: Engineers cannot access or infer resources outside assigned scope.
- **AC-015**: Delivery uses no Docker, public download, substitute database, PostgreSQL port 5432, or
  secret emission.

### Key Entities

- **Operational Workspace Status**: Server-derived view of authorized scope, completed setup steps,
  next required action, usable chains, dependency health, and landing destination; not a browser
  authority or independent business lifecycle.
- **Configuration Chain**: The related Site, Area, Asset, Measurement Point, Data Source, Source
  Mapping, and Simulator Configuration whose persisted states determine setup progress and
  operational eligibility.
- **Setup Step State**: Derived completion, validation failures, and next action for one logical
  wizard step.
- **Site Scope Assignment**: Existing relationship granting an Engineer authorized responsibility
  within a Site or narrower Area.
- **Simulator Run**: Existing deliberate execution pinned to an eligible Simulator Configuration
  version and Data Source.
- **Audit Event**: Existing append-only record of an authorized configuration or operational action
  with safe, reviewable context.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In acceptance testing, 100% of the five defined landing states route correctly from
  server-derived conditions without local fallback data.
- **SC-002**: An Administrator and assigned Engineer can complete one initial configuration chain
  and handoff in no more than eight visible logical steps without SQL or scripts.
- **SC-003**: Refresh, logout/login, and separate Web/API restarts preserve 100% of committed wizard
  progress in the defined resume scenarios.
- **SC-004**: Repeating each defined mutation retry scenario produces exactly one business outcome
  and no duplicate entity, outbox event, or Audit event.
- **SC-005**: All defined stale-version scenarios reject silent overwrite and provide a recoverable
  conflict state.
- **SC-006**: All out-of-scope scenarios expose zero unauthorized resource records, metadata, or
  global counts.
- **SC-007**: Successful setup completes with zero automatically started Simulator Runs.
- **SC-008**: In the two-Source and two-Point acceptance fixtures, 100% of operations and reads use
  the user’s explicit selection rather than response order.
- **SC-009**: Latest updates within one configured refresh interval in acceptance testing and all No
  Data scenarios render a non-numeric state.
- **SC-010**: Every reviewed page demonstrates explicit loading, empty, error, forbidden, and
  conflict behavior applicable to that page, with no decorative action remaining.
- **SC-011**: Keyboard-only review can complete the setup flow and primary page actions with visible
  focus at desktop and tablet widths.
- **SC-012**: Automated and manual evidence finds zero emitted passwords, tokens, credentials,
  connection secrets, port 5432 access, public downloads, container use, or substitute databases.

## Assumptions

- Feature 002’s existing IAM, scope, domain transitions, idempotency, optimistic concurrency,
  transaction, outbox, Audit, API, Worker, Simulator, Telemetry, Latest, and Source Health behavior
  remain authoritative and reusable.
- Wizard progress can be derived from persisted existing entities unless planning proves a focused
  server-side status projection is necessary; no separate workflow lifecycle is assumed.
- Site creation and Site-scope assignment are Administrator responsibilities; Engineers continue
  only within assigned scope.
- The existing user directory contains an Engineer account that an Administrator can select.
- Desktop and tablet are the required complete experiences; narrow screens receive an accessible
  compact wizard and operational subset consistent with the authoritative UX direction.
- Existing approved dependencies are sufficient; frontend behavior-runner execution remains
  separately blocked unless an already approved runner is present.

## Scope and Evidence Boundaries *(mandatory)*

- **Current corrective closure**: The historical Phase 6 acceptance run covered T073–T080. This
  corrective branch adds only governance, traceability, accessibility regression, deployment-source
  reconciliation, and evidence tasks T081–T087. It stops after T087 and does not authorize Phase 7,
  Feature 004, or product-scope expansion.

- **Included release/capability**: One Feature 003 operational workspace delivered in six reviewable
  implementation phases; the historical Phase 6 execution implemented acceptance hardening, accessibility,
  traceability, and final evidence (T073–T080) after the accepted T001–T072 history and a clean
  Phase 6 authorization analysis.
- **Explicitly excluded**: Energy baselines, period comparison, anomaly detection, savings
  calculation or verification, savings claims, AI recommendations, equipment control/writeback,
  real meter integration, external customer/SaaS behavior, new charts, new real-time transport, and
  work after T087, Phase 7, Spec 004, Rule/Alert/CSV/Reporting capability, and any other capability
  outside the Feature 003 operational workspace in the current corrective run.
- **External approvals/dependencies**: Approved local PostgreSQL at `127.0.0.1:5433/iump_dev`;
  existing approved package caches; company CI and restricted non-containerized target-host/service
  approvals remain separate blockers and do not authorize substitutes.
- **Evidence classification**: Every check is reported as PASS, FAIL, NOT_RUN, or BLOCKED with its
  actual classification; a blocked frontend behavior runner or company environment check is never
  promoted to PASS.
