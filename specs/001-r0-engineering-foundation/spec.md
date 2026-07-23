# Feature Specification: R0 Engineering Foundation

**Feature Branch**: `001-r0-engineering-foundation`

**Created**: 2026-07-23

**Status**: Draft — environment-blocked implementation

**Input**: Establish only the R0 engineering foundation for IUMP using the approved document set,
Spec Kit lifecycle, Matt Pocock engineering methods, and the restricted company workstation policy.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Understand and verify the foundation (Priority: P1)

As an IUMP engineer, I can start from a clean repository checkout, understand the product boundary,
identify the approved tools and blockers, and invoke one documented verification entry point without
installing software or weakening workstation controls.

**Why this priority**: Every later release depends on a reproducible and truthful engineering base.

**Independent Test**: On the restricted workstation, inspect the source register and environment
inventory, invoke verification, and confirm each check reports a precise result or blocker without
network download, administration, or container use.

**Acceptance Scenarios**:

1. **Given** only approved preinstalled tools, **When** an engineer runs local verification, **Then**
   each check reports PASS, FAIL, NOT_RUN, or a specific BLOCKED classification and exits non-zero
   when required work cannot be verified.
2. **Given** a missing database or approved package source, **When** verification reaches the
   affected check, **Then** it stops that check, cites the evidence command, and continues only safe,
   independent checks.

---

### User Story 2 - Preserve architecture and scope (Priority: P2)

As a Tech Lead or reviewer, I can inspect a single canonical specification, plan, tasks, glossary,
and decision set showing that R0 preserves IUMP's product boundary and module/data ownership without
silently substituting technology or implementing later business capability.

**Why this priority**: A runnable but architecturally misleading skeleton creates expensive rework
and can compromise the read-only OT boundary.

**Independent Test**: Review traceability from DOC-01..DOC-07 through the constitution, ADRs, plan,
and tasks; confirm R1, Modbus, Edge production behavior, control, AI, and business workflows are
explicitly absent.

**Acceptance Scenarios**:

1. **Given** the R0 artifacts, **When** a reviewer follows the source hierarchy, **Then** every hard
   decision is supported by a higher-priority source or explicitly marked as an environment
   constraint/proposal.
2. **Given** source or task content, **When** prohibited or later-release terms are scanned, **Then**
   no executable container/control/Modbus/AI artifact or completed R1 workflow is present.

---

### User Story 3 - Request missing company capabilities (Priority: P3)

As an Infrastructure, Security, Database, or CI owner, I receive actionable requests stating the
minimum approved capability needed to unblock R0 and the exact verification that will follow.

**Why this priority**: R0 cannot be fully complete without approved dependencies, PostgreSQL, and a
company runner, but approvals must not be bypassed.

**Independent Test**: Review the database access request, CI readiness document, and blocker report;
confirm they contain no real credential, no network scan instruction, and no request for elevated
rights.

**Acceptance Scenarios**:

1. **Given** no approved PostgreSQL, **When** the database owner reviews the request, **Then** the
   version decision, least privileges, schemas, prohibited data, secret delivery, and post-access
   verification are explicit.
2. **Given** no approved CI template, **When** the CI owner reviews readiness, **Then** required tools,
   package mirrors, database access, checks, redaction, and public-action prohibition are explicit.

### Edge Cases

- A tool exists but its configured dependency source is public or unverified.
- A package appears in cache but the lockfile tree is incomplete or its source is not approved.
- A verification command would implicitly restore packages or access the Internet.
- PostgreSQL client exists but no approved endpoint, credential, or TLS policy is supplied.
- A source document describes container deployment while workstation policy prohibits containers.
- Pre-existing source exceeds R0 scope or claims verification that cannot be reproduced.
- A check is technically runnable but would reveal a secret in console output.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The foundation MUST identify DOC-01 through DOC-07, ADRs, feature artifacts, glossary,
  source, and tests in their authoritative order.
- **FR-002**: The foundation MUST provide one canonical feature named
  `001-r0-engineering-foundation` through the full Spec Kit workflow.
- **FR-003**: The repository MUST contain a domain-only glossary and an auditable decision log.
- **FR-004**: The repository MUST contain the R0 ADR set defined by DOC-07 and the task, including
  restricted workstation and offline verification decisions.
- **FR-005**: The environment inventory MUST record tool version, executable path, configured source,
  Internet dependency, permission classification, blocker, and evidence command.
- **FR-006**: Package restore or installation MUST NOT run until an approved source and complete
  locked dependency set are verified.
- **FR-007**: Local scripts MUST detect prerequisites, validate required environment variables,
  avoid printing secrets, avoid system changes/downloads, and return non-zero for failed or blocked
  mandatory checks.
- **FR-008**: Verification MUST classify every check as PASS, FAIL, NOT_RUN,
  BLOCKED_BY_MISSING_TOOL, BLOCKED_BY_PACKAGE_POLICY, BLOCKED_BY_DATABASE_ACCESS, or
  BLOCKED_BY_COMPANY_APPROVAL.
- **FR-009**: The source foundation MUST preserve separate central HTTP and background-process hosts,
  explicit module ownership, and a minimal shared technical kernel.
- **FR-010**: The source foundation MUST provide structured logging, request correlation, health
  reporting, background-process lifecycle, and persistence contracts for jobs and outbox/inbox
  without implementing business workflows.
- **FR-011**: Architecture verification MUST reject forbidden module implementation dependencies,
  cross-module internal access, and command/write-back contracts.
- **FR-012**: Database artifacts MUST target PostgreSQL and MUST remain blocked rather than using a
  substitute when approved PostgreSQL access is unavailable.
- **FR-013**: The repository MUST include company-actionable database and CI readiness requests when
  those capabilities are unavailable.
- **FR-014**: The repository MUST contain no Dockerfile, Compose file, container script/image
  configuration, real credential, Modbus implementation, production Edge behavior, AI behavior, or
  R1/VS-01 business feature.
- **FR-015**: Test-first evidence MUST be recorded only for seams that can execute with approved
  tools and dependencies; unexecuted tests MUST NOT be described as passing.
- **FR-016**: Completion reporting MUST list every changed file, executed command, verification
  result, deviation, Gate G1 item, required approval, and final git status.

### Key Entities

- **Environment Capability**: A tool, source, cache, database, or runner with evidence and an
  approval classification.
- **Verification Check**: A deterministic local check with command, result classification, evidence,
  and affected acceptance criterion.
- **Blocker**: A missing or prohibited capability with impact, completed safe work, requested
  approval, and lowest-risk next step.
- **Architecture Decision**: A hard-to-reverse implementation choice supported by source documents
  and status.
- **Module Contract**: The small interface through which a module exposes behavior while retaining
  ownership of its implementation and data writes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new engineer locates the authoritative R0 scope, glossary, decisions, and verification
  entry point in under 10 minutes without external assistance.
- **SC-002**: 100% of mandatory verification checks have one explicit result classification and an
  evidence command; no blocked/not-run check is reported as passed.
- **SC-003**: 100% of dependency, database, CI, credential, and container constraints have a named
  blocker or approved evidence before the affected command can run.
- **SC-004**: Automated/source inspection finds zero real credentials and zero container, Modbus,
  command/write-back, AI, or completed R1 artifacts.
- **SC-005**: Every R0 requirement maps to at least one task and one verification or documented
  blocker, with no unresolved Critical consistency issue.
- **SC-006**: A company owner can act on each access/readiness request without requesting missing
  privilege, prohibited-data, validation, or security-boundary information.

## Assumptions

- DOC-01..DOC-07 remain draft/in-review sources but are explicitly authorized for R0 preparation.
- The installed .NET and Node toolchains may execute; their public registries are not approved.
- The current package trees may be inspected and used directly only where execution does not restore,
  install, or access a registry.
- No approved PostgreSQL endpoint, CI runner, internal action/template, or internal package registry
  has been supplied in this session.
- Target deployment remains the DOC-05 on-premise proposal; only current workstation execution is
  changed to non-containerized and target verification is deferred.

## Scope and Evidence Boundaries *(mandatory)*

- **Included release/capability**: R0 documentation, repository and host/module skeletons,
  architecture/migration/test structure, local scripts, logging, correlation, health, Worker,
  job/outbox/inbox foundations, and truthful verification.
- **Explicitly excluded**: R1/VS-01 and all later business capability; Modbus; production Edge;
  containers; Kubernetes; business dashboards/CRUD/ingestion/rules/Alerts/email/reports; AI and
  enterprise integrations; database substitutes and fabricated results.
- **External approvals/dependencies**: Approved NuGet/npm sources and caches, PostgreSQL development
  access, company CI runner/templates, named environment/security/operations owners.
- **Evidence classification**: PASS / FAIL / NOT_RUN / BLOCKED_BY_MISSING_TOOL /
  BLOCKED_BY_PACKAGE_POLICY / BLOCKED_BY_DATABASE_ACCESS / BLOCKED_BY_COMPANY_APPROVAL.
