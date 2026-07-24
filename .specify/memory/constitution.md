<!--
Sync Impact Report
- Version change: 1.0.0 -> 1.1.0
- Decision: DEC-GOV-009, approved and applied 2026-07-24
- Approval: devphuclam - Repository Owner / Acting Product Owner, IUMP Internal POC
- Approval evidence: DEC-GOV-009-APPROVAL-2026-07-24
- Added principles: Product Boundary; Source-of-Truth Traceability; Deep Modules and Ownership;
  Test-First Evidence; Restricted and Secure Execution; Operability from R0
- Added governance: active-feature lifecycle; Planning-ready, Implementation-ready, and
  Release-ready states; one-phase implementation; PASS/FAIL/BLOCKED/NOT_RUN evidence semantics;
  release gate and active-feature provenance
- Source precedence updated through DOC-08; DOC-02 remains supporting feasibility input
- Removed sections: none
- Templates: ✅ .specify/templates/plan-template.md; ✅ .specify/templates/spec-template.md;
  ✅ .specify/templates/tasks-template.md
- Preserved restrictions: PostgreSQL-only verification; no SQLite, EF InMemory, Testcontainers,
  substitute database, Docker, public restore, public actions, unapproved downloads, elevation,
  credentials, keys, tokens, or secrets
- T011 pending: synchronize .specify/templates/plan-template.md,
  .specify/templates/tasks-template.md, and docs/repository-harness.md
- The baseline template inventory above is historical; no T011 synchronization is represented here.
-->
# IUMP Engineering Constitution

## Core Principles

### I. Product Boundary Is Non-Negotiable
IUMP MUST remain an internal, human decision-support product. It MUST NOT introduce equipment
control, command/write-back, AI operational conclusions, SaaS billing, guaranteed-savings claims,
or conditional MVP-2/MVP-3 capability without documented upstream change control and the required
gate. Work MUST stop at the requested release boundary.

### II. Source-of-Truth and Traceability
Decisions MUST follow this precedence:

1. DOC-01 Product Vision and Scope
2. DOC-03 Business Requirements
3. DOC-04 Software Requirements Specification
4. DOC-05 Software Architecture Document
5. DOC-06 Data and Integration Specification
6. DOC-07 MVP Roadmap and Delivery Plan
7. DOC-08 UI/UX Design Specification
8. Repository ADRs
9. Active Spec Kit feature artifacts
10. `CONTEXT.md`
11. Source code and automated tests

DOC-02 remains a supporting feasibility input. Downstream artifacts MUST NOT silently override a
higher-authority source. Requirements, tasks, tests, and verification evidence MUST retain
traceability. Environment constraints MUST be recorded rather than rewriting upstream decisions.

### III. Deep Modules and Explicit Ownership
The central application MUST be a modular monolith with separate API and Worker hosts. Every module
MUST own its business writes, expose a small explicit interface, and avoid internal or cross-schema
writes by other modules. Seams and adapters MUST exist only where behavior genuinely varies.

### IV. Test-First Evidence (NON-NEGOTIABLE)
Executable behavior MUST follow red-green-refactor at the interface that callers use. A test result
MUST be reported only when the exact command ran. PostgreSQL behavior MUST NOT be replaced with
SQLite, EF InMemory, Testcontainers, or any substitute database. Blocked checks MUST be classified
as blocked, never passed.

### V. Restricted and Secure Execution
Work MUST use only preinstalled tools, approved internal/local package sources, approved databases,
and non-administrative commands. It MUST NOT download tools, packages, actions, or images; use a
public package registry or public GitHub Action; weaken security controls; use an administrator-
elevation workaround; or place credentials, keys, tokens, connection strings, or secrets in the
repository. Docker, Testcontainers, and all container artifacts or container dependencies are
prohibited on the current workstation. Missing capabilities MUST be classified and recorded as
blocked.

### VI. Operability Starts in R0
API and Worker foundations MUST provide structured logs, correlation identifiers, configuration
validation, health signals, and explicit failure states. Database jobs and outbox/inbox processing
MUST be observable and idempotent. Applied migrations MUST remain immutable and corrections MUST use
ordered forward-fix migrations. Migration, backup, restore, and CI claims require real evidence.

## R0 Foundation and Historical Constraints

R0 established the engineering foundation and remains reusable infrastructure. These constraints do
not make R0 the only valid lifecycle for later active features.

- R0 MAY create repository structure, documentation, contracts, project skeletons, architecture
  rules, migration sources, local scripts, logging/correlation/health foundations, Worker and
  outbox/inbox persistence skeletons.
- R0 MUST NOT implement R1/VS-01 business workflows, production ingestion, rules, Alerts, reports,
  Modbus, Edge production behavior, or container deployment.
- PostgreSQL is the required database. If no approved instance exists, migration execution,
  integration tests, seed execution, and database health verification remain blocked.
- React/TypeScript and ASP.NET Core on the installed .NET LTS remain the technology direction.
  Dependency versions MUST NOT be changed merely to match an unapproved cache.

## Active-Feature Lifecycle and Readiness

Every active feature MUST use one governed lifecycle:

1. Product and business source registration
2. Feature specification
3. Clarification
4. Implementation plan and design artifacts
5. Dependency-ordered task generation
6. Read-only cross-artifact analysis
7. Resolution of all Critical and High findings
8. Constitution-impact evaluation
9. Approved constitution amendment when required
10. Template and guidance synchronization
11. Final Phase 0 governance checkpoint
12. Test-first implementation by phase
13. Standards and Specification review
14. Fast verification
15. Full and environment-dependent verification
16. Acceptance and release evidence

The lifecycle has three distinct readiness states:

- **Planning-ready**: specification, clarification, planning, design, and task work may continue;
  this does not authorize green implementation or release.
- **Implementation-ready**: cross-artifact analysis is clean; every required constitution
  amendment is approved and applied; affected templates and guidance are synchronized; and the
  final Phase 0 checkpoint permits progression.
- **Release-ready**: mandatory acceptance evidence and mandatory environment-dependent evidence have
  passed; no mandatory blocker remains; and the release checkpoint permits release.

Planning-ready MUST NOT be represented as implementation-ready or release-ready.

## Development Workflow and Quality Gates

For each active feature, run source registration, specify, clarify, plan, checklist, tasks, analyze,
implement, review, and converge in the governed order. Matt Pocock domain-modeling and codebase-
design enrich the artifacts; TDD governs executable seams; diagnosing-bugs governs unexpected
failures; code-review checks standards and specification. R0 remains the historical foundation and
may be reused by later features.

Each `/speckit.implement` invocation MUST execute one implementation phase only and MUST stop at
that phase's checkpoint. Silent continuation into later phases is prohibited. Within each phase the
required order is:

1. Red tests
2. Recorded red evidence
3. Minimal green implementation
4. Refactor
5. Architecture and repository-policy verification
6. Standards and Specification review
7. Phase checkpoint
8. Explicit stop

Red-test source MAY precede an external approval only where the approved task graph explicitly
permits it. No green application implementation may bypass the final Phase 0 governance gate.

## Evidence and Environment Semantics

- **PASS** means the exact verification executed and succeeded.
- **FAIL** means executable verification ran and failed.
- **BLOCKED** means runnable artifacts were produced where possible, the exact external dependency
  and blocker evidence were recorded, required execution could not occur, and the capability is
  not passing.
- **NOT_RUN** means execution was not attempted.

Allowed task classifications are:

- `RUNNABLE_NOW`
- `BLOCKED_BY_DATABASE_ACCESS`
- `BLOCKED_BY_PACKAGE_POLICY`
- `BLOCKED_BY_MISSING_TOOL`
- `BLOCKED_BY_COMPANY_APPROVAL`

Task classification describes executability. PASS/FAIL/BLOCKED/NOT_RUN describes execution
evidence. These concepts MUST NOT be conflated. Blocked evidence is never PASS. Blocked execution
tasks MAY remain evidence leaves and MUST NOT unnecessarily prevent runnable traceability, review,
architecture, or checkpoint work. Full and release gates MUST NOT pass while a mandatory blocker
remains.

Before restore/install, record sources and cache readiness. Before implementation, analysis MUST
have no unresolved Critical or High conflict. Before completion, verification MUST enumerate PASS,
FAIL, NOT_RUN, and every BLOCKED category. Critical/High review findings and incomplete mandatory
tasks prevent `FULLY_COMPLETE`.

## Governance

This constitution governs repository execution but cannot override the documented source precedence.
Amendments MUST identify affected source requirements, ADRs, templates, tasks, tests, and migration
implications; record semantic-version rationale, approval identity and role, approval date, approval
evidence reference, and a Sync Impact Report; verify synchronized files; and never rewrite
historical evidence retroactively. Semantic versioning applies: MAJOR removes or redefines
governance, MINOR adds or materially expands a rule, and PATCH clarifies without changing
obligations. Every active feature MUST record the constitution version governing planning,
implementation, and release. Every plan, review, and release gate MUST include a constitution
compliance check. Runtime guidance is in `AGENTS.md`.

Risk-based approval applies: a low-risk, governance-only internal POC change MAY be approved by one
accountable Repository Owner acting as Product Owner; medium-risk product or architecture changes
require Product Owner and Tech Lead approval; high-risk production, security, privacy, OT,
integration, deployment, or release changes require Product Owner and Tech Lead plus applicable
company authorities. Governance approval never grants environment, package, database, Data
Protection, CI, deployment, or release authorization unless separately approved.

### Current transition provenance

For the current feature, `specs/002-asset-simulator-latest/checklists/phase-00-governance.md` MUST
record the constitution provenance after T011/T012:

- planning baseline: Constitution 1.0.0
- implementation gate: Constitution 1.1.0
- release gate: Constitution 1.1.0

**Version**: 1.1.0 | **Ratified**: 2026-07-23 | **Last Amended**: 2026-07-24
