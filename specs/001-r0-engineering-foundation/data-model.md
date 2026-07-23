# Design Model: R0 Engineering Foundation

This is an engineering-evidence model, not an IUMP business schema.

## Environment Capability

Fields: name, version, executable path, configured source, Internet dependency, approval status,
evidence command, blocker reference. Approval status is one of `AVAILABLE_AND_APPROVED`,
`AVAILABLE_BUT_SOURCE_UNVERIFIED`, `MISSING`, or `BLOCKED_BY_POLICY`.

Validation: a capability cannot be used for install/restore unless its source is approved. Presence
of an executable does not imply source approval.

## Verification Check

Fields: identifier, description, command, prerequisite capability identifiers, mandatory flag,
result classification, exit code, evidence summary, affected acceptance criteria.

States: `NOT_RUN` → `PASS` or `FAIL`; or `NOT_RUN` → one of
`BLOCKED_BY_MISSING_TOOL`, `BLOCKED_BY_PACKAGE_POLICY`, `BLOCKED_BY_DATABASE_ACCESS`,
`BLOCKED_BY_COMPANY_APPROVAL`. A blocked state cannot transition to PASS without a new execution.

## Blocker

Fields: identifier, evidence commands, affected components/criteria, completed safe work,
unavailable work, requested capability/approval, lowest-risk next step, owner status.

Relationship: one Blocker may block many Verification Checks. A check references exactly one
primary classification but may cite several related blockers.

## Architecture Decision

Fields: ADR identifier, title, status, source references, decision, rationale, consequences.

Validation: an ADR cannot override a higher-priority source. Deployment decisions requiring company
review cannot be marked Accepted by the software team alone.

## Module Contract

Fields: owning module, interface responsibility, invariants/error modes, callers, adapters, owned
schema, verification surface.

Validation: one module owns each business write. Host composition roots may select adapters;
modules cannot reference other modules' implementations or expose command/write-back to OT.

## Dependency relationships

Environment Capability → enables Verification Check. Blocker → prevents Verification Check.
Architecture Decision → constrains Module Contract. Feature Requirement → maps to Task → maps to
Verification Check or Blocker evidence.
