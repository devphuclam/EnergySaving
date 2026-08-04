# Feature 003 release-readiness checkpoint (current state)

Date: 2026-08-04
Baseline: `2309cfecdd24538e320dcb70c35fcbd5d42bf9e2` (previous corrective integrated to `main`)
Branch: `fix/003-signed-approval-closure`
Scope: T110–T123 final signed-approval and release-evidence corrective closure; no merge, Phase 7,
or Feature 004.

## Independent readiness states

| State | Result | Evidence / rationale |
|---|---|---|
| Planning-ready | YES | Feature specification, research/data model/contracts/quickstart, source register, ADR review, and Phase 0 requirements checklist are present and accepted. |
| Implementation-ready | YES | Phase 0 governance checkpoint accepted; 123 dependency-ordered tasks exist, including additive T081–T123 corrective closures; no unresolved artifact conflict after direct comparison. |
| Code implementation complete | YES (bounded) | Corrective tasks T098–T123 for governance, documentation, deployment-gate, trusted and signed approval, harness registration, and DOCX package-integrity verification are implemented and their runnable seams pass. |
| Acceptance evidence complete | NO | AC-005 and AC-011 remain `PARTIAL` because no approved authenticated browser/process-control runner is available for host-restart and combined-browser evidence. |
| Release-ready | NO | Mandatory release evidence is not complete while Full has `BLK-ENV-003` and `BLK-ENV-005` company-approval blockers and frontend behavior is `BLOCKED_BY_PACKAGE_POLICY`. |

## Release blockers

1. `BLK-ENV-003` — no approved company CI runner/template context; Full classification
   `BLOCKED_BY_COMPANY_APPROVAL`.
2. `BLK-ENV-005` — approved non-containerized TEST/UAT/PROD target host/service, rollback evidence,
   and a company-managed deployment trust policy with a trusted signer are pending
   Infrastructure/Security approval; Full classification `BLOCKED_BY_COMPANY_APPROVAL`.
3. No approved frontend behavior runner is installed; classification `BLOCKED_BY_PACKAGE_POLICY`
   (no package was installed or downloaded).

## Required release evidence and disposition

- Acceptance traceability: present for AC-001..AC-015; AC-005 and AC-011 are `PARTIAL` at the
  fresh authenticated-browser evidence boundary.
- Signed deployment approval: fail-closed contract implemented and tested — production PASS requires
  an approved company CI context (`CI=true` plus `IUMP_COMPANY_CI_APPROVED=true`),
  `IUMP_DEPLOYMENT_TARGET_APPROVED=true`, a trusted evidence root, manifest path containment,
  reparse-escape rejection, SHA-256 attestation, and a detached CMS/PKCS#7 signature over the exact
  manifest bytes verified against a company-managed machine trust policy with LocalMachine
  certificate-chain validation. The manifest is read exactly once; the same byte buffer is hashed,
  signature-verified, and parsed. Environment-only booleans/digests, self-signed developer signers,
  and synthetic contract fixtures can never reach production PASS; they classify
  `BLOCKED_BY_COMPANY_APPROVAL`/`BLK-ENV-005` or `FAIL`. Missing cryptographic capability is
  `BLOCKED_BY_MISSING_TOOL`.
- DOCX verification: `doc05-architecture` check verifies DOC-05 v0.2 restricted non-containerized
  wording, corrected date, deployment components, ADR AR-11, and Open XML package integrity
  (required entries, relationship XML, office-document target, no traversal) from the locked
  document via a temporary copy without writing into the repository. Visual DOCX QA is a documented
  non-mandatory `NOT_RUN` limitation, never a visual PASS and never an unenforced mandatory gate.
- Harness registration: `deployment-target-contract`, `deployment-signature`, and
  `doc05-architecture` are repository-wide checks registered before Feature-scoped checks; no
  planned Fast/Full check is silently skipped for Features 001/002/003.
- Security/scope: no secret emission, port 5432, substitute database, container, public download,
  fake fallback, savings/AI/control/Modbus behavior found.
- Unit, PostgreSQL Integration, architecture, repository policy, observability, Web lint/build:
  PASS (see `final-verification.md`).
- Fast harness: PASS for the approved local seams; see `final-verification.md`.
- Full harness: exit 20 by the repository contract with PASS plus mandatory company blockers;
  therefore not PASS and not release approval.
- SpecKit provider analysis/convergence: recorded honestly when the provider command is available,
  otherwise `NOT_RUN` with direct artifact comparison, never promoted to PASS.

## Current task ledger state

- `task_count=123`, `unique_task_count=123`, no duplicate task IDs.
- Unchecked tasks: historical T034 (`BLOCKED_BY_PACKAGE_POLICY`) only; T098–T123 are complete for
  this bounded closure.
- T080/T087/T097 and the T098–T109 trusted-approval closure are historical corrective closures and
  remain labeled historical; they do not describe the current branch state.

## Historical entries (superseded)

The following describe earlier corrective closures on earlier branches and are retained for the
audit trail only; they are not the current state:

- T080/T087 closure on `045f3981f3ba6bb87425009ee8f8cf0e6cf4e56a` (branch
  `fix/003-final-governance-corrective`).
- T088–T097 closure on `6dbfaf3bcbc95f2d262ddeacf174232d9d746bd7` (branch
  `fix/003-doc05-deployment-gate`).
- T098–T109 closure on `6b77256f29775bb2a777ddcb555d868d7e671243` (branch
  `fix/003-trusted-deployment-approval`), merged to `main` at `2309cfec`.

This checkpoint permits the bounded Feature 003 implementation to remain implementation-complete,
but explicitly withholds Release-ready approval until the listed external capabilities are available
and rerun. Stop here; do not merge, create Spec 004, or start Rule, Alert, CSV, Reporting, or any
other Phase 7 work.

## Atomic signed-approval implementation checkpoint (current phase)

Date: 2026-08-04
Baseline: `90bafced98f80b3bbbe80bf86f81ef1c28b694ef` (authoritative merged `main`, PR #7)
Branch: `fix/003-atomic-signed-approval`
Scope: T124-T137 implementation checkpoint only; no merge, Phase 7, or Spec 004.

| State | Result | Evidence / rationale |
|---|---|---|
| Planning-ready | YES | Existing Feature 003 artifacts remain the governing scope; direct atomic analysis is recorded and appended T124-T140 without rewriting prior history. |
| Implementation-ready | YES | Constitution 1.1.0 checkpoint gate is satisfied for the bounded implementation; T124-T137 are complete and T138-T140 remain subsequent work. |
| Code implementation complete | YES (bounded) | Single-read verifier, expected-SHA attestation, policy-v2 trust checks, structured result propagation, path hardening, fixtures, and focused regression suites are implemented. |
| Acceptance evidence complete | NO | AC-005 and AC-011 remain PARTIAL; no fresh approved browser/process-control trace exists. |
| Release-ready | NO | Company-managed trust policy/target and approved CI remain unavailable; Full/review/convergence evidence was not run in this checkpoint. |

Focused green evidence is recorded in `atomic-signed-approval-implementation-checkpoint.md`:
deployment-signature 30/0, deployment-target 58/0, DOC-05 63/0, repository/architecture/
observability checks PASS, Release build 0 warnings/0 errors, Unit PASS, and sequential PostgreSQL
Integration PASS against `127.0.0.1:5433/iump_dev` (15 suites, 0 failures). A fresh Fast/Full run,
Standards/Specification review, Converge, push, and merge are intentionally NOT_RUN because the
Constitution requires stopping at this implementation checkpoint.

The signed-approval contract remains fail-closed: missing company policy is
`BLOCKED_BY_COMPANY_APPROVAL`/`BLK-ENV-005`; missing cryptographic capability is
`BLOCKED_BY_MISSING_TOOL`; synthetic fixtures are contract-only and never establish production
approval. No port 5432, substitute database, package install, container, secret, Phase 7, or Spec
004 work was used.
