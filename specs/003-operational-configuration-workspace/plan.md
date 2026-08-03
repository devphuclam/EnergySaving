# Implementation Plan: Operational Configuration Workspace

**Branch**: `003-operational-configuration-workspace` | **Date**: 2026-07-30 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from
`specs/003-operational-configuration-workspace/spec.md`

**Governing Constitution Version**: 1.1.0

**Planning Readiness**: **PASS / YES** — the source register, specification, clarification scan,
research, data model, contracts, quickstart, module ownership, and environment constraints are
recorded. Planning-ready does not authorize green implementation or release.

**Implementation Readiness**: **PASS / YES** — task generation and both analysis passes completed;
all three High and three Medium first-pass findings were resolved; final analysis has zero
Critical/High/Medium findings; the Phase 0 checkpoint permits one implementation phase at a time,
and the current Phase 5 continuation is explicitly authorized after accepted T001–T064 evidence.

**Release Readiness**: **NO** — remains NO until all Feature 003 phases and mandatory release
evidence pass with no mandatory blocker.

**Constitution Amendment Required**: NO. Feature 003 stays inside the internal decision-support
boundary, reuses the modular monolith, and strengthens test-first, secure, and operable behavior.

**Active Feature Phase 0 Checkpoint**:
`specs/003-operational-configuration-workspace/checklists/phase-00-governance.md`

## Summary

Feature 003 converts the Feature 002 verification shell into an operational workspace. A focused
server-side operational status query derives landing and wizard progress from existing PostgreSQL
entities and authorized scopes. The existing module commands remain the only write owners. A new
IAM application command exposes Administrator-to-Engineer Site scope assignment, while the Web
drives idempotent configuration commands and legal activation in the established order:

1. Administrator creates and activates Site, then assigns Engineer Site scope.
2. Engineer creates Area, Asset, and Draft Measurement Point.
3. Engineer activates Area and Asset after their parents are Active.
4. Engineer creates and activates the Simulator Data Source.
5. Engineer creates a Source Mapping for the Draft Point.
6. Engineer creates an immutable Simulator Configuration version.
7. The server validates the complete chain; Engineer activates the Measurement Point.
8. The UI navigates to Simulator with no implicit selection and no automatic Start.

No separate wizard-progress table is planned. Progress is a derived read model over existing IAM,
Organization, Catalog, and Acquisition contracts; migration `0014` only supplies the missing
pre-Mapping Source-to-Site ownership fact. Later phases add management,
duplication/versioning, Simulator selection/control, Latest polling, and Audit/dashboard usability.

## Technical Context

**Language/Version**: C# / .NET 10 for API, Worker, modules, composition, Unit and Integration
executables; TypeScript ~6 with React 19 for Web

**Primary Dependencies**: Existing ASP.NET Core, Npgsql 10 lock files, React Router, TanStack Query,
Vite 8, and repository BuildingBlocks; no new packages

**Storage**: Existing PostgreSQL 18 target at `127.0.0.1:5433/iump_dev`; no SQLite, InMemory,
Testcontainers, alternate database, or new Phase 1 schema

**Testing**: Existing custom Unit executable, PostgreSQL Integration executable, PowerShell
verification contracts, Web TypeScript compile, oxlint, Vite production build, Fast and Full harness

**Target Platform**: Restricted local Windows development environment; separate API and Worker
processes; desktop/tablet Web application

**Project Type**: Modular-monolith Web application with separate API and Worker composition roots

**Performance Goals**: Operational status and one wizard-step read should complete within two
seconds in the POC dataset; scope filtering occurs before paging; Latest refresh is ten seconds in
Phase 4

**Constraints**: Server-authoritative IAM/scope; idempotency and expected version on mutations; no
secret output; no port 5432; no install/restore/download; no Docker/container; no fake fallback data;
Vietnamese UI; no new chart or frontend test-runner package

**Scale/Scope**: Eight setup steps, seven configuration entity types, five user journeys, six
implementation phases, existing internal POC users and dataset sizes

## Constitution and Readiness Gates

Each gate records `PASS`, `FAIL`, `BLOCKED`, or `NOT_RUN`. Executability classification and evidence
status remain separate; blocked evidence is never PASS.

### Planning gate

- Product boundary: internal configuration and operations only; no savings analytics, AI,
  equipment control, real meter integration, or SaaS.
- Authoritative sources: DOC-01, DOC-03, DOC-04, DOC-05, DOC-06, DOC-07, DOC-08, ADRs, Feature 003
  artifacts, `CONTEXT.md`, then code/tests.
- Module ownership: IAM owns scope writes; Organization owns hierarchy writes; Catalog owns Source
  and Mapping writes; Acquisition owns configuration and Run writes; Audit remains append-only.
- Composition: API exposes HTTP only; Worker continues background execution; cross-module status
  composition uses public query contracts in `Composition/Postgres`.
- Environment: approved local PostgreSQL is runnable; frontend behavior runner is
  `BLOCKED_BY_PACKAGE_POLICY`; company CI/container evidence remains
  `BLOCKED_BY_COMPANY_APPROVAL`.
- Artifacts: `spec.md`, `research.md`, `data-model.md`, `contracts/`, and `quickstart.md` exist.

**Planning gate evidence**: `PASS` — `setup-plan.ps1 -Json`, source/ADR inspection, artifact
placeholder scan, and requirements checklist 16/16.

### Implementation gate

Requires generated `tasks.md`, final `/speckit.analyze` PASS, zero unresolved Critical/High/Medium
actionable findings, constitution impact NO, synchronized guidance where affected, and Phase 0
checkpoint PASS.

**Implementation gate evidence**: `PASS` — 80 valid tasks; requirement coverage 45/45; final
read-only analysis reports Critical=0, High=0, Medium=0; Phase 0 governance checkpoint PASS.

### Release gate

Requires all six implementation phases, acceptance evidence, Fast PASS, mandatory Full/environment
PASS, no blocker, and release checkpoint approval.

**Release gate evidence**: `NOT_RUN` — Phase 1 is not release completion; known company approval and
frontend behavior-runner blockers must not be represented as PASS.

## Design and Ownership

### Deep interfaces and seams

1. **Operational workspace query seam**
   - A small `IOperationalWorkspaceQueryPort` returns the authorized landing/status document,
     eligible Engineer choices for Administrators, and complete-chain validation.
   - Its PostgreSQL adapter composes public IAM, Organization, Catalog, and Acquisition query
     contracts. It performs no writes and does not expose module repositories to Web callers.
   - The status interface hides reconstruction rules, scope filtering, setup-step derivation, and
     dependency classification behind one caller-facing contract.

2. **Engineer scope assignment seam**
   - IAM application behavior validates Administrator authority, active Engineer identity, Site
     existence, and idempotent duplicate assignment before IAM writes its own scope.
   - The API command uses `Idempotency-Key`, server principal, host transaction, outbox/Audit path,
     and safe errors. Browser-supplied roles or claims are ignored.

3. **Existing configuration command seams**
   - Organization, Catalog, and Acquisition remain the write owners. The wizard calls the existing
     configuration endpoints with typed fields, idempotency key, and `If-Match` where required.
   - Complete-chain validation is read-only, validates lifecycle eligibility before activation, and
     must not use the command-idempotency registry.
   - Ordered activation is intentionally a visible sequence of existing owner commands. Each step
     commits independently, is re-read after completion, and supports safe partial-failure retry.
     Point activation performs the final Active Source/Mapping recheck.

4. **Web gateway seam**
   - A focused workspace gateway owns transport, antiforgery, idempotency-key creation/reuse,
     ETag/version parsing, and safe error mapping.
   - Wizard and landing components consume typed outcomes rather than raw `fetch` responses.
   - Persisted server data, never localStorage, determines progress.

### Phase 1 database decision

Implementation review disproved one part of the original no-migration decision: a Draft Source has
no Site relationship before Mapping, so server-derived resume cannot be both deterministic and
scope-safe. Forward migration `0014_operational_workspace_scope.sql` adds nullable
`catalog.data_sources.site_id` for new setup-created Sources and a partial unique index for root
Site scopes. Legacy Sources remain nullable and are never adopted by the workspace as an unmapped
scoped Source. No workflow/progress table is added.

### Phase 1 TDD seams

The user-approved seams are fixed by this plan and spec:

- IAM application command: list eligible Engineers and assign Site scope.
- Operational workspace query: landing decision, completed/next step, no-scope, dependency failure,
  and complete-chain validation.
- HTTP contract: safe authorization, scope filtering, idempotent assignment, and no auto-start.
- PostgreSQL integration journey: Administrator handoff, Engineer continuation, persisted resume,
  legal activation, and zero Simulator Run after completion.
- Web gateway/type seam: compile-time contract and existing package-policy evidence. No unapproved
  behavior runner is installed or falsely passed.

## Implementation Phases

### Phase 0 — Specification, contracts, UX state model, API gap analysis, and red tests

Generate and analyze canonical artifacts, record actual endpoint reuse/gaps, create Phase 0
governance evidence, and add the first runnable red tests at the agreed backend/HTTP seams.
Checkpoint: Implementation-ready YES only after clean final analysis and governance PASS.

### Phase 1 — Role-aware Setup Wizard vertical slice and state-aware landing

Implement operational status, role-aware landing, Administrator Site/Engineer handoff, Engineer
continuation, one complete persisted chain, validation, legal ordered activation, navigation to
Simulator, and explicit no-auto-start. Use real PostgreSQL evidence. Checkpoint records runnable
PASS/FAIL and frontend/company blockers truthfully, then stops.

### Phase 2 — Configuration management, duplication, and version-safe editing

Add dedicated scoped management pages, real action navigation, duplicate-to-Draft behavior, direct
metadata edits, behavior-changing Draft versions, dependency-safe lifecycle, and paging/search.

### Phase 3 — Explicit Simulator selection, controls, and Run history

Remove implicit first Source behavior; add explicit context/configuration selection, Start/Pause/
Resume/Stop, counters, pinned version, and recent Run history.

### Phase 4 — Explicit Latest/Health selection and refresh

Remove implicit first Point behavior; add hierarchy selectors, ten-second default polling, disable/
manual refresh, complete Latest/Health state, and non-numeric No Data.

### Phase 5 — Audit usability and Operational Dashboard completion

Add server-side Audit filters/pagination/redaction and the authorized operational navigation
dashboard with incomplete setup and recent activity.

### Phase 6 — Acceptance hardening, accessibility, traceability, and final evidence

Complete state/accessibility review, full acceptance traceability, standards/spec review,
repository-policy verification, Fast/Full evidence, and release readiness assessment.

## Plan Lifecycle and Phase Rules

The Constitution 1.1.0 lifecycle applies: source registration → specification → clarification →
plan/design → tasks → read-only analysis → remediation → constitution impact → Phase 0 checkpoint →
one implementation phase → Standards/Spec review → Fast → Full → acceptance/release evidence.

Every implementation phase executes red test, recorded red evidence, minimal green, review/refactor,
architecture and repository-policy verification, checkpoint, and explicit stop. This run ends after
Phase 5 only (T065–T072) and stops before Phase 6/T073; later phases are not implemented in this run.

## Evidence Vocabulary

Statuses: `PASS`, `FAIL`, `BLOCKED`, `NOT_RUN`.

Classifications: `RUNNABLE_NOW`, `BLOCKED_BY_DATABASE_ACCESS`,
`BLOCKED_BY_PACKAGE_POLICY`, `BLOCKED_BY_MISSING_TOOL`,
`BLOCKED_BY_COMPANY_APPROVAL`.

The approved database is available and must not be classified as
`BLOCKED_BY_DATABASE_ACCESS`. Frontend behavior execution remains a separate package-policy
capability. Full/release cannot pass while mandatory company approval blockers remain.

## Project Structure

### Documentation

```text
specs/003-operational-configuration-workspace/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── operational-workspace.md
│   └── ui-state-model.md
├── checklists/
│   ├── requirements.md
│   ├── phase-00-governance.md
│   └── phase-01-checkpoint.md
└── tasks.md
```

### Source Code

```text
src/
├── Api/
│   └── OperationalWorkspaceEndpoints.cs
├── Hosting/Abstractions/
│   └── OperationalWorkspacePorts.cs
├── Modules/IAM/
│   ├── Application/EngineerScopeAssignment.cs
│   └── Contracts/OperationalWorkspaceIamContracts.cs
├── Composition/Postgres/
│   └── PostgresOperationalWorkspacePorts.cs
└── Web/src/
    ├── app/
    ├── gateways/
    └── features/setup/

tests/
├── Unit/IAM/
├── Unit/OperationalWorkspace/
├── Integration/OperationalWorkspace/
└── Verification/
```

**Structure Decision**: Extend existing module/application/host/Web locations. Do not create a new
business module, workflow datastore, UI framework, or composition root. The operational workspace
is a deep host-facing query/command facade over existing owner contracts.

## Complexity Tracking

No constitutional violation or extra architectural layer requires justification.
