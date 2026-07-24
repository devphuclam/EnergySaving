# Decision Log

| ID | Date | Decision/status | Basis |
|---|---|---|---|
| DEC-R0-001 | 2026-07-23 | Execute only R0 Engineering Foundation; stop before R1/VS-01. | User request; DOC-07 R0 |
| DEC-R0-002 | 2026-07-23 | Use Spec Kit artifacts as the canonical feature lifecycle and Matt Pocock skills as engineering methods. | User request; `AGENTS.md` |
| DEC-R0-003 | 2026-07-23 | Use database execution Mode C and classify database checks as `DATABASE_EXECUTION_BLOCKED`. | `psql` missing; no approved endpoint/credentials |
| DEC-R0-004 | 2026-07-23 | Do not run package restore/install while only public registries are configured. | Company dependency policy |
| DEC-R0-005 | 2026-07-23 | Current workstation execution is non-containerized; DOC-05 container target remains deferred for infrastructure review. | Company policy; ADR-010 |
| DEC-R0-006 | 2026-07-23 | Do not create a public-action CI pipeline; provide local equivalent scripts and runner requirements. | Company policy; ADR-016 |
| DEC-R0-007 | 2026-07-23 | Do not substitute SQLite/InMemory for PostgreSQL and do not report unexecuted checks as passing. | Constitution; user request |
| DEC-R0-008 | 2026-07-23 | Treat `CONTEXT.md` as a glossary only; technical choices remain in ADRs and Spec Kit plans. | Matt Pocock domain-modeling |

## DEC-GOV-009 — Proposed Active-Feature and Release Lifecycle Amendment

**Date**: 2026-07-24
**Repository baseline**: `devphuclam/EnergySaving` at `f2a1cc65251e348172af2fd6ae7f3d90890ad2b9`
**Status at proposal**: **PROPOSED / AWAITING COMPANY APPROVAL**
**Scope**: Generic repository governance; current-feature impact is recorded below for
`specs/002-asset-simulator-latest/` only.

### Context

The current constitution establishes an R0 Engineering Foundation lifecycle and strong execution
constraints. The repository now needs a reusable governance model for active features that move from
specification through implementation, verification, acceptance, and release. The final
`/speckit.analyze` baseline for the current feature is clean: 68/68 functional requirements, 5/5
user stories, 9/9 success criteria, 247 tasks, zero invalid dependencies, and zero dependency
cycles.

This entry performs governance task T009 only. It drafts the amendment and records its impact; it
does not amend the constitution, claim company approval, synchronize templates, or implement any
application task.

### Problem with the current R0-only wording

The existing R0 constraints correctly prevent premature R1/VS-01 implementation, but they do not
define a reusable active-feature lifecycle, a distinct implementation gate, a release gate, or a
single vocabulary for execution evidence and environment blockers. Without those distinctions,
planning readiness can be mistaken for implementation or release readiness, and blocked evidence
can be interpreted as a passing result.

### Proposed constitutional amendment (draft for T010)

The following generic language is proposed for the constitution. It is non-operative until the
required approval is recorded and T010 applies the approved semantic-versioned amendment.

#### Active-feature lifecycle

Every active feature MUST use one governed lifecycle:

1. Register product and business sources.
2. Produce the feature specification.
3. Resolve material clarification questions.
4. Produce the implementation plan and design artifacts.
5. Generate the dependency-ordered task graph.
6. Run read-only cross-artifact analysis.
7. Resolve all Critical and High analysis findings.
8. Evaluate constitution impact.
9. Obtain and apply an approved constitution amendment when required.
10. Synchronize affected templates and repository guidance.
11. Record the final Phase 0 governance checkpoint.
12. Perform test-first implementation one phase at a time.
13. Run Standards and Specification review.
14. Run Fast verification.
15. Run Full and other environment-dependent verification.
16. Record acceptance and release evidence.

The lifecycle has three distinct readiness states:

- **Planning-ready**: specification, design, and task work may continue. This state MUST NOT be
  treated as permission to implement or release.
- **Implementation-ready**: analysis is clean; any required constitution amendment is approved and
  applied; affected guidance is synchronized; and the final governance checkpoint permits
  progression.
- **Release-ready**: mandatory acceptance and environment-dependent evidence has passed and no
  mandatory blocker remains.

Planning-ready MUST never be represented as implementation-ready or release-ready.

#### Implementation invocation and phase gates

Each `/speckit.implement` invocation MUST execute one implementation phase only and MUST stop at
that phase's checkpoint. Silent continuation into a later phase is prohibited. Within a phase,
the execution order MUST be red tests, minimal green behavior, refactor, architecture and
repository-policy verification, Standards/Specification review, and checkpoint evidence.

Red-test source preparation MAY occur before an external approval only when the active task graph
explicitly permits it. Green application implementation MUST remain behind the final governance
gate. A checkpoint MUST record PASS, FAIL, NOT_RUN, and each applicable BLOCKED classification,
plus capability and progression decisions.

#### Evidence and blocker semantics

- **PASS** means the exact verification executed and succeeded.
- **FAIL** means executable verification ran and failed.
- **BLOCKED** means runnable artifacts were produced where possible, the exact external dependency
  and blocker evidence were recorded, required execution could not occur, and the capability is
  not passing.
- **NOT_RUN** means no execution attempt has occurred.

Task classification describes executability and is independent from execution evidence. The
allowed environment classifications are:

- `RUNNABLE_NOW`
- `BLOCKED_BY_DATABASE_ACCESS`
- `BLOCKED_BY_PACKAGE_POLICY`
- `BLOCKED_BY_MISSING_TOOL`
- `BLOCKED_BY_COMPANY_APPROVAL`

A blocked execution task MAY be an evidence leaf and MUST NOT unnecessarily prevent runnable
traceability, review, architecture, or checkpoint work. A Full or release gate MUST NOT pass while
any mandatory blocker remains. Blocked evidence MUST never be represented as PASS.

#### Change control and active-feature provenance

Constitution amendments MUST use semantic versioning and document the rationale for the selected
version change. Where required, the amendment MUST identify company approval identity, approval
date, and approval evidence reference. Each amendment MUST include a Sync Impact Report naming
affected templates, guidance, tasks, tests, and migration implications, and the synchronized files
MUST be verified after approval.

Historical feature evidence MUST NOT be rewritten retroactively. Every active feature MUST record
which constitution version governed its planning, implementation, and release gates. A proposed
amendment is not an approval, and a planning artifact is not evidence that an amendment was applied.

### Principles preserved

This proposal preserves, without weakening, the current principles and restrictions:

- IUMP remains an internal human decision-support product with no equipment control, command/write-
  back, unsupported AI operational conclusions, SaaS expansion, or guaranteed-savings claims.
- Modular-monolith ownership boundaries remain explicit; API and Worker remain separate composition
  roots; consumer modules do not write across schemas.
- DOC-01..DOC-07, ADRs, Spec Kit artifacts, `CONTEXT.md`, code, and tests retain the documented
  source-of-truth precedence, with requirement-to-task-to-evidence traceability.
- Red-green-refactor and exact-command evidence remain mandatory.
- PostgreSQL remains the only persistence-verification database. SQLite, EF InMemory, Testcontainers,
  substitute databases, Docker, and container dependencies remain prohibited.
- Public package restore, unapproved Internet downloads, unapproved tools, credentials, keys, and
  secrets in the repository remain prohibited. Approved internal or offline sources remain required.
- Applied migrations remain immutable and corrections use ordered forward-fix migrations.
- Structured logs, correlation identifiers, configuration validation, health signals, explicit
  failure states, and observable/idempotent jobs and outbox/inbox processing remain required.
- Architecture and repository-policy verification remain explicit gates.

### Evidence, implementation, and release gates

The proposed lifecycle does not make an external approval look like a test result. A task may be
classified as blocked before execution, while its checkpoint records the resulting evidence as
BLOCKED or NOT_RUN. A runnable traceability or review task may record the blocked state without
declaring the underlying capability complete. Implementation may proceed only after the clean
analysis, approved amendment (when required), synchronized guidance, and final governance
checkpoint all permit it. Release may proceed only when mandatory acceptance and environment-
dependent evidence is PASS and no mandatory blocker remains.

### Semantic-version recommendation

**Proposed constitution version: 1.1.0 (MINOR).** The current version is 1.0.0. This proposal adds
materially new governance obligations—active-feature lifecycle states, one-phase implementation
invocation, explicit evidence/blocker vocabulary, release gates, and amendment provenance—while
preserving the existing principles and restrictions. It therefore expands governance without
removing or redefining a principle, which is a MINOR change rather than PATCH or MAJOR.

### Approval requirements (proposal state)

The proposal originally assumed Product Owner and Tech Lead approval (or the repository's formally
designated company approvers). The approved risk-based model below supersedes that assumption for
this low-risk governance-only internal POC change. The approval record MUST include approver
identity, date, decision, and an evidence reference. No approval was claimed by the proposal state.

### Sync Impact Report for T010/T011

T011 MUST wait for T010 approval and application. The expected impact is:

| File | Why affected | Concepts to synchronize | Waits for T010 approval? | Verification after synchronization |
|---|---|---|---|---|
| `.specify/memory/constitution.md` | The operative constitution must define active-feature and release governance. | Generic lifecycle and readiness states; one-phase implementation gate; PASS/FAIL/BLOCKED/NOT_RUN semantics; environment classifications; semantic-version and provenance rules; preserved security and product boundaries; updated Sync Impact Report and version. | Yes. | Approved diff, semantic-version rationale, preserved principles, approval identity/date/evidence, and constitution compliance review. |
| `.specify/templates/plan-template.md` | Plans must distinguish planning-ready, implementation-ready, and release-ready states. | Lifecycle checkpoints; constitution-impact gate; one-phase implementation rule; Fast/Full/release evidence vocabulary; no blocked PASS. | Yes. | Template terminology review and a generated-plan consistency check against the approved constitution. |
| `.specify/templates/tasks-template.md` | Task graphs must express executability separately from evidence outcomes. | Task classifications; dependency-ordered phase checkpoints; red-before-green ordering; blocked evidence leaves; final acceptance/release gate semantics. | Yes. | Template review, dependency/ordering validation, and a generated-task consistency check. |
| `docs/repository-harness.md` | Harness guidance must report exact commands and environment-dependent outcomes consistently. | Fast versus Full responsibilities; exact-command evidence; PASS/FAIL/BLOCKED/NOT_RUN; approved PostgreSQL/package/tool/CI constraints; no substitute database or container. | Yes. | Documentation terminology review, `git diff --check`, and a fresh harness-mode verification when applicable. |

The following artifacts are explicitly expected to remain unchanged by T009/T010/T011 unless a
separate approved change identifies a new impact:

| Artifact | Expected impact |
|---|---|
| `specs/002-asset-simulator-latest/spec.md` | No change expected. |
| `specs/002-asset-simulator-latest/plan.md` | No change expected. |
| `specs/002-asset-simulator-latest/tasks.md` | No task-graph change expected. |
| `specs/002-asset-simulator-latest/contracts/` | No change expected. |
| Source code, migrations, and tests | No change expected. |

### Rollback and non-application rule

This entry is a proposal only. If approval is withheld, T010 MUST remain BLOCKED and no constitution,
template, guidance, source, migration, test, or task artifact may be changed under this decision. If
an approved amendment is later rejected, superseded, or found inconsistent with source authority,
the repository MUST stop at the last approved constitution version and record a new governed decision;
historical evidence MUST NOT be rewritten and no partial synchronization may be represented as
complete.

### Current-feature impact (proposal state)

`specs/002-asset-simulator-latest/` is the current feature used to validate the proposed governance
model, but this proposal does not hardcode that feature into permanent principles. Its clean analysis
result remains evidence under the current constitution version 1.0.0 until T010 is approved and
applied. The feature is planning-ready, not implementation-ready or release-ready; no application
task, migration, test, or runtime approval is authorized by this entry.

### T009 final readiness decision (proposal state)

- T009: **COMPLETE** — governance amendment drafted in this decision log.
- Decision status: **PROPOSED / AWAITING COMPANY APPROVAL**.
- Constitution modified: **NO**.
- Templates modified: **NO**.
- Repository guidance modified: **NO**.
- Company approval: **OUTSTANDING; not claimed**.
- Ready for T010 approval/application: **YES**.
- Ready for `/speckit.implement`: **NO**.

### T010 approval model clarification

The approved risk-based model distinguishes governance risk from product, architecture, security,
environment, and release risk. A low-risk, governance-only change for an internal non-production POC
may be approved by one accountable Repository Owner acting as Product Owner. Medium-risk product or
architecture changes require Product Owner and Tech Lead approval. High-risk production, security,
privacy, OT, external-integration, deployment, or release changes require Product Owner and Tech
Lead plus applicable company authorities.

`DEC-GOV-009` is classified as **LOW-RISK INTERNAL GOVERNANCE CHANGE**: internal POC, governance-only,
no production impact, no product or architecture change, no security weakening, no environment
authorization, and no release authorization. The Repository Owner approval below is therefore
sufficient for T010 and does not authorize any separately governed capability.

### T010 approval/application outcome

**Status**: **APPROVED / APPLIED — T011 SYNCHRONIZATION PENDING**

- **Approved by**: devphuclam - Repository Owner / Acting Product Owner, IUMP Internal POC
- **Approval date**: 2026-07-24
- **Approval evidence**: `DEC-GOV-009-APPROVAL-2026-07-24`
- **Approval scope**: Constitution governance only.
- **Approved version**: Constitution 1.1.0, applied from previous version 1.0.0.
- **Constitution application date**: 2026-07-24.
- **Semantic-version rationale**: MINOR; governance obligations were materially expanded without
  removing or weakening an existing principle.
- **Files changed by T010**: `.specify/memory/constitution.md` and `docs/decision-log.md` only.
- **No change**: templates, `docs/repository-harness.md`, source-register, spec, plan, tasks,
  contracts, source code, migrations, tests, scripts, package files, CI, and deployment files.
- **Explicit exclusions**: no environment, package, database, security exception, CI, deployment,
  release, or application-implementation authorization.
- **T010**: **COMPLETE**.
- **T011**: **NOT_RUN**; pending synchronization of `.specify/templates/plan-template.md`,
  `.specify/templates/tasks-template.md`, and `docs/repository-harness.md`.
- **T012**: **NOT_RUN**.
- **Implementation**: **NO**.
- **Release**: **NO**.

T011 remains the next governance task. The current feature's planning, implementation-gate, and
release-gate constitution provenance is recorded by the applied constitution for
`specs/002-asset-simulator-latest/checklists/phase-00-governance.md`; T010 does not create or modify
that checklist.

### Post-application synchronization and Phase 0 outcome

**Date**: 2026-07-24
**Repository HEAD**: `d0f50b5a25e63b7f317c2228f69aed70fcc0567e`
**Feature**: `specs/002-asset-simulator-latest/`

This is a later outcome record. It preserves the historical T010 snapshot above and does not
rewrite prior NOT_RUN evidence.

- **T011**: **COMPLETE**.
- **T011 commit**: `d0f50b5a25e63b7f317c2228f69aed70fcc0567e`.
- **T011 evidence**: `.specify/templates/plan-template.md`,
  `.specify/templates/tasks-template.md`, and `docs/repository-harness.md` at the T011 commit.
- **T012**: **COMPLETE**.
- **T012 evidence**: `specs/002-asset-simulator-latest/checklists/analysis.md`,
  `docs/blocker-report.md`, and `specs/002-asset-simulator-latest/checklists/phase-00-governance.md`.
- **Governing Constitution**: 1.1.0; planning baseline 1.0.0, implementation gate 1.1.0,
  release gate 1.1.0.
- **Phase 0 progression**: **YES** for classification-permitted Phase 1 tasks after the
  checkpoint; unavailable database, package, tool, and company capabilities remain blocked and
  must not be bypassed.
- **Implementation readiness**: **YES — governance gate only**; this does not mean all environment
  capabilities are available.
- **Release readiness**: **NO**.
- **Release decision**: **NO**; mandatory Full and acceptance evidence has not been performed.
- **Remaining environment blockers**: `BLK-T012-DB-001`, `BLK-T012-PKG-001`,
  `BLK-T012-TOOL-001`, `BLK-T012-APP-001` through `BLK-T012-APP-004`, and existing R0 blockers
  documented in `docs/blocker-report.md`.
- **Application implementation**: none performed; no application source, migration, test, package,
  CI, deployment, or release authorization was created.
