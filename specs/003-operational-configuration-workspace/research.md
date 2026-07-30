# Research: Operational Configuration Workspace

## Decision 1 — Derive workspace state from existing persisted entities

**Decision**: Add a focused server-side Operational Workspace Status query that derives landing,
completed setup steps, next action, and usable-chain state from existing IAM, Organization, Catalog,
and Acquisition data.

**Rationale**: Refresh/restart resilience comes from the existing PostgreSQL state. A derived query
keeps the browser non-authoritative and avoids a second workflow lifecycle that could drift from
real entity states.

**Alternatives considered**:

- Browser localStorage: rejected because it is not authoritative and is lost or stale across hosts.
- New wizard-progress table: rejected because no independent business state exists that cannot be
  derived; it would duplicate lifecycle truth and require an unjustified migration.
- Multiple client-side list calls: rejected for landing because partial failures and client
  combination rules would be harder to secure and reason about.

## Decision 2 — Preserve module write ownership

**Decision**: IAM alone writes Engineer scope; Organization alone writes Site/Area/Asset/Point;
Catalog alone writes Source/Mapping; Acquisition alone writes Simulator Configuration/Run.
Operational workspace composition uses public contracts and performs no controller-level SQL.

**Rationale**: This matches ADR-001, ADR-006, ADR-007, and Constitution III. It keeps owner
invariants, host transactions, outbox, and Audit behavior in one place.

**Alternatives considered**:

- A new Workflow module owning copies of all states: rejected as competing ownership.
- Direct SQL from endpoint handlers: rejected because it bypasses application/domain rules.
- Cross-schema updates in Composition: rejected because composition is coordination, not ownership.

## Decision 3 — Use actual legal activation order

**Decision**:

1. Administrator activates Site.
2. Engineer/Admin activates Area, then Asset.
3. Engineer/Admin activates the Simulator Data Source.
4. Engineer creates a Mapping for the still-Draft Point.
5. Engineer creates immutable Simulator Configuration version; it has no invented Active status.
6. Complete-chain validation rechecks IAM, ancestors, catalog compatibility, intervals, and the
   lifecycle eligibility of Source, Mapping, and configuration before any ordered activation.
7. Engineer/Admin activates Source and Mapping where still Draft, then activates Point; Point
   activation performs the final Active Source/Mapping recheck.
8. Simulator remains stopped until explicit Start on the Simulator page.

**Rationale**: Feature 002 explicitly permits an Active non-producing Mapping for a Draft Point and
requires Source, Mapping, Point, and ancestors Active before Start. Simulator Configuration is
versioned but does not expose a separate activation transition.

**Alternatives considered**:

- Activate Point before Mapping: rejected because Point activation requires exactly one Active
  Mapping. Pre-activation validation may validate an eligible Draft Mapping but cannot activate the
  Point until that Mapping is Active.
- Invent Simulator Configuration activation: rejected because the current domain has immutable
  versions, not that transition.
- Automatically Start after setup: rejected by the approved product design and auditability rule.

## Decision 4 — Ordered activation remains explicit owner commands

**Decision**: The Web invokes existing idempotent owner commands in legal order after one
server-side complete-chain validation. After each committed step it reloads status and version.
Partial failure stops the sequence and identifies the owner step.

**Rationale**: Existing commands already provide authorization, optimistic concurrency,
idempotency, host transaction, and outbox/Audit effects. A distributed all-or-nothing transaction
would contradict the approved partial-failure semantics.

**Alternatives considered**:

- One endpoint writing all schemas: rejected for ownership and transactional coupling.
- Client-only validation: rejected because the browser is non-authoritative.
- Roll back earlier legal transitions after later failure: rejected because committed valid Draft/
  Active states are intentionally retained.

## Decision 5 — Add a production Administrator scope-assignment command

**Decision**: Add an IAM application command that lists eligible existing Engineer accounts and
assigns Site scope after validating server principal, Engineer role/status, Site existence, and
duplicate-safe state. Expose it through the operational workspace command seam.

**Rationale**: Feature 002 has repository support and a POC fixture but no human-operable production
endpoint. Administrator handoff cannot depend on the fixture, SQL, or client-supplied role claims.

**Alternatives considered**:

- Reuse the POC fixture: rejected because it assigns multiple deterministic users and is not a
  general user action.
- Let Engineers self-assign: rejected by IAM and approved responsibilities.
- Store assignment only in the browser: rejected because server scope is authoritative.

## Decision 6 — Use a deep operational workspace interface

**Decision**: Introduce small query and command ports in Hosting Abstractions. The PostgreSQL
adapter hides multi-module reads, landing rules, progress reconstruction, validation, error mapping,
and scope filtering. Web consumes a typed gateway rather than raw lists.

**Rationale**: One focused interface gives callers high leverage and keeps reconstruction logic
local. Separate CQRS-lite query/command ports align with current repository conventions.

**Alternatives considered**:

- Add reconstruction logic to `Program.cs`: rejected because it creates shallow composition and
  cross-module business logic in the host entry point.
- Duplicate landing rules across React pages: rejected because the browser is non-authoritative.
- General-purpose query strings returning `object`: rejected for the new contract because the
  state model needs explicit, testable fields and error modes.

## Decision 7 — Reuse existing frontend dependencies

**Decision**: Build the wizard and landing using existing React, TypeScript, React Router, TanStack
Query, Vite, and CSS. Use a page/stepper layout, Vietnamese text, explicit feedback states, and no
new package.

**Rationale**: DOC-08 selects the Industrial Light direction, progressive disclosure, scope
awareness, long workflow page/stepper, visible conflicts, and Vietnamese default. Existing packages
are adequate.

**Alternatives considered**:

- New component framework: rejected by scope and package policy.
- Chart package: rejected because Phase 1 requires no charts.
- WebSocket/SignalR: rejected because setup state changes only on deliberate commands.

## Decision 8 — Frontend behavior evidence remains honestly blocked

**Decision**: Run TypeScript compile, oxlint, Vite build, HTTP/integration checks, and repository
harness. Add frontend behavior-test source only if already runnable with approved dependencies; do
not install a runner and do not promote Feature 002 T218 to PASS.

**Rationale**: The repository has no approved frontend behavior runner. Build evidence is valuable
but not equivalent to behavior execution.

**Alternatives considered**:

- Public npm install: prohibited.
- Claim production build proves behavior: rejected as false evidence.
- Skip all frontend checks: rejected because compile/lint/build are runnable now.

## Decision 9 — Narrow Phase 1 forward migration

**Decision**: Retain derived progress, but add nullable Site ownership to Data Source and an atomic
root Site-scope uniqueness index in migration `0014`.

**Rationale**: Review proved that pre-Mapping Source resume cannot be scope-safe without one
persisted ownership fact. The new status remains a projection, not a new aggregate.

**Alternatives considered**:

- Add setup-session and setup-step tables preemptively: rejected as speculative generality and
  duplicate truth.

## Decision 10 — Evidence and environment

**Decision**: Use only `127.0.0.1:5433/iump_dev` through existing approved configuration loading.
Run Fast while iterating and a fresh Full after final Phase 1 changes. Report company CI/container
and frontend behavior-runner blockers exactly.

**Rationale**: The database capability is available; packages and company approvals are separate
capabilities. Constitution IV/V and ADR-016 prohibit substitutes.

**Alternatives considered**:

- Port 5432, SQLite, InMemory, Testcontainers, Docker, public restore/download: all prohibited.
