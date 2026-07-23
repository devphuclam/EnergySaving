<!--
Sync Impact Report
- Version change: template -> 1.0.0
- Added principles: Product Boundary; Source-of-Truth Traceability; Deep Modules and Ownership;
  Test-First Evidence; Restricted and Secure Execution; Operability from R0
- Added sections: R0 Delivery Constraints; Development Workflow and Quality Gates
- Removed sections: none
- Templates: ✅ .specify/templates/plan-template.md; ✅ .specify/templates/spec-template.md;
  ✅ .specify/templates/tasks-template.md
- Follow-up: company owners must approve PostgreSQL, dependency sources, CI runner, and target
  deployment before the affected gates can pass.
-->
# IUMP Engineering Constitution

## Core Principles

### I. Product Boundary Is Non-Negotiable
IUMP MUST remain an internal, human decision-support product. It MUST NOT introduce equipment
control, command/write-back, AI conclusions, SaaS billing, or conditional MVP-2/MVP-3 capability
without the documented gate and upstream change control. Work MUST stop at the requested release.

### II. Source-of-Truth and Traceability
Decisions MUST follow DOC-01, DOC-03, DOC-04, DOC-05, DOC-06, DOC-07, ADRs, Spec Kit artifacts,
`CONTEXT.md`, then code and tests. Requirements, tasks, tests, and verification evidence MUST retain
traceability. Environment constraints MUST be recorded rather than rewriting upstream decisions.

### III. Deep Modules and Explicit Ownership
The central application MUST be a modular monolith with separate API and Worker hosts. Every module
MUST own its business writes, expose a small explicit interface, and avoid internal or cross-schema
writes by other modules. Seams and adapters MUST exist only where behavior genuinely varies.

### IV. Test-First Evidence (NON-NEGOTIABLE)
Executable behavior MUST follow red-green-refactor at the interface that callers use. A test result
MUST be reported only when the exact command ran. PostgreSQL behavior MUST NOT be replaced with
SQLite or an in-memory fake. Blocked checks MUST be classified as blocked, never passed.

### V. Restricted and Secure Execution
Work MUST use only preinstalled tools, approved internal/local package sources, approved databases,
and non-administrative commands. It MUST NOT download tools, packages, actions, or images; use a
public registry; weaken security controls; or place real credentials in the repository. Docker and
all container artifacts are prohibited on the current workstation.

### VI. Operability Starts in R0
API and Worker foundations MUST provide structured logs, correlation identifiers, configuration
validation, health signals, and explicit failure states. Database jobs and outbox/inbox processing
MUST be observable and idempotent. Migration, backup, restore, and CI claims require real evidence.

## R0 Delivery Constraints

- R0 MAY create repository structure, documentation, contracts, project skeletons, architecture
  rules, migration sources, local scripts, logging/correlation/health foundations, Worker and
  outbox/inbox persistence skeletons.
- R0 MUST NOT implement R1/VS-01 business workflows, production ingestion, rules, Alerts, reports,
  Modbus, Edge production behavior, or container deployment.
- PostgreSQL is the required database. If no approved instance exists, migration execution,
  integration tests, seed execution, and database health verification remain blocked.
- React/TypeScript and ASP.NET Core on the installed .NET LTS remain the technology direction.
  Dependency versions MUST NOT be changed merely to match an unapproved cache.

## Development Workflow and Quality Gates

Use one canonical feature, `001-r0-engineering-foundation`, and run: constitution, specify,
clarify, plan, checklist, tasks, analyze, implement, review, then converge. Matt Pocock
domain-modeling and codebase-design enrich the artifacts; TDD governs executable seams;
diagnosing-bugs governs unexpected failures; code-review checks standards and specification.

Before restore/install, record sources and cache readiness. Before implementation, analysis MUST
have no unresolved Critical conflict. Before completion, verification MUST enumerate PASS, FAIL,
NOT_RUN, and every BLOCKED category. Critical/High review findings and incomplete R0 tasks prevent
`FULLY_COMPLETE`.

## Governance

This constitution governs repository execution but cannot override DOC-01..DOC-07. Amendments MUST
identify affected source requirements, ADRs, templates, tasks, tests, and migration implications;
Product Owner and Tech Lead approval is required for product or architecture changes. Semantic
versioning applies: MAJOR removes/redefines governance, MINOR adds or materially expands a rule,
and PATCH clarifies without changing obligations. Every plan, review, and release gate MUST include
a constitution compliance check. Runtime guidance is in `AGENTS.md`.

**Version**: 1.0.0 | **Ratified**: 2026-07-23 | **Last Amended**: 2026-07-23
