# PR preparation: `fix(feature-003): reconcile DOC-05 and deployment approval gate`

This file is a prepared PR body because no approved GitHub PR connector/API is available in this
runtime. It is not a merge authorization and must be used from the corrective branch only.

## Summary

- Starting merged-main SHA: `6dbfaf3bcbc95f2d262ddeacf174232d9d746bd7`
- Corrective branch: `fix/003-doc05-deployment-gate`
- Corrective tasks: T088-T097; historical T001-T087 are not rewritten.
- Scope: canonical documentation reconciliation, fail-closed deployment evidence, verification,
  readiness wording, independent review boundary, and final Feature 003 stop.

## Documentation and architecture

- DOC-05 remains v0.2 and records the corrected date `2026-08-03` in version history.
- The remaining Architecture Summary/technology-choice contradiction was changed from a
  containerized reference deployment to restricted non-containerized host/service deployment,
  consistent with DOC-05 section 19, the deployment diagram, ADR catalogue, and AR-11.
- ADR-010 status is `Accepted for MVP-1 architecture; deployment approval pending`; it is the
  current architecture decision and is not marked superseded by DOC-05.

## Deployment approval contract

- `scripts/common/DeploymentTarget.ps1` requires both
  `IUMP_DEPLOYMENT_TARGET_APPROVED=true` and `IUMP_DEPLOYMENT_EVIDENCE_PATH`.
- The sanitized manifest requires the restricted non-containerized model, hosting/service,
  lifecycle/rollback runbook, approval reference, approver, and valid UTC approval date fields.
- A missing flag/path/file is `BLOCKED_BY_COMPANY_APPROVAL` with `BLK-ENV-005`.
- Malformed JSON, missing fields, wrong model, invalid date, or secret-like keys is `FAIL`.
- Evidence logs report only presence/schema/status; manifest contents and secret-like values are not
  emitted.
- Blocked path: `deployment-target=BLOCKED_BY_COMPANY_APPROVAL [BLK-ENV-005]`.
- Temporary approved-path contract fixture (created in the process temp/scratch area and removed
  after the run): `deployment-target=PASS`; Full remained exit 20 because CI was still
  `BLOCKED_BY_COMPANY_APPROVAL [BLK-ENV-003]`. This is synthetic contract evidence, not company
  release approval or a persisted deployment manifest.

## Verification

- TDD red: deployment contract test initially stopped with missing verifier (`RED`).
- TDD green: deployment contract test PASS, 25 checks.
- PostgreSQL Integration: PASS, 15 suites, 0 failures, target `127.0.0.1:5433/iump_dev`.
- Fast harness: PASS=12, exit 0.
- Full blocked path: PASS=15, `BLOCKED_BY_COMPANY_APPROVAL=2` (`BLK-ENV-003`, `BLK-ENV-005`),
  no mandatory FAIL, contract exit 20.
- Full approved-manifest path: PASS=16, `BLOCKED_BY_COMPANY_APPROVAL=1` (`BLK-ENV-003`),
  `deployment-target=PASS`, contract exit 20.
- DOC-05 structural reconciliation: PASS; render QA could not run because LibreOffice/soffice is
  unavailable in the approved runtime.

## Acceptance and readiness

- AC-005: PARTIAL; approved authenticated API/Web host restart runner is unavailable.
- AC-011: PARTIAL; fresh combined authenticated browser journey is unavailable.
- Code implementation complete: YES, bounded to the Feature 003 implementation and governance
  seams.
- Acceptance evidence complete: NO.
- Release-ready: NO.
- Remaining blockers: `BLK-ENV-003`, `BLK-ENV-005`, frontend behavior package-policy blocker,
  and authenticated browser/process-control capability for AC-005/AC-011.
- Spec Kit Analyze/Converge: `NOT_RUN` because provider commands are unavailable; direct artifact
  comparison is recorded and never promoted to provider PASS.

## Review and boundary

- Independent Standards and Specification reviews are required on this branch; do not self-claim a
  review as human approval.
- No merge, Phase 7, Spec 004, Rule/Alert/CSV/Reporting scope, database substitution, container,
  package download, port 5432 access, or secret emission is included.

## Current trusted-approval corrective PR preparation (T109)

This section supersedes the historical T088-T097 PR preparation above for the current branch. It
is a prepared body only; no PR was created, no reviewer was requested, and no merge was performed.

### Title

`fix(feature-003): enforce trusted deployment approval`

### Baseline and scope

- Starting merged-main SHA: `6b77256f29775bb2a777ddcb555d868d7e671243`.
- Corrective branch: `fix/003-trusted-deployment-approval`.
- Corrective tasks: T098-T109; historical T001-T097 remain unchanged and labeled by their own closure.
- Scope: trusted approval boundary, release-checkpoint normalization, repository-wide harness
  registration, DOCX structural verification, verification, review, and final analysis only.
- No Phase 7, Spec 004, Rule/Alert/CSV/Reporting capability, merge, or product-scope expansion.

### Trust and verification contract

- A deployment-target PASS requires approved company context (`CI=true` and
  `IUMP_COMPANY_CI_APPROVED=true`), `IUMP_DEPLOYMENT_TARGET_APPROVED=true`,
  company-provided `IUMP_DEPLOYMENT_TRUSTED_ROOT`, an evidence path inside that root, and the
  protected `IUMP_DEPLOYMENT_EVIDENCE_SHA256` digest matching the manifest.
- Missing/untrusted approval is `BLOCKED_BY_COMPANY_APPROVAL` with `BLK-ENV-005`.
- Outside-root, traversal, reparse escapes, malformed JSON, non-scalar fields, invalid UTC/future
  dates, digest mismatch, wrong deployment model, and secret-like fields are `FAIL`.
- Synthetic fixtures prove the verifier contract only; they are never company approval or
  Release-ready evidence.
- DOC-05 structural verification is a text-level ZIP/XML PASS only; visual rendering remains
  `BLOCKED_BY_MISSING_TOOL`.

### Evidence and readiness

- Deployment contract: PASS, `47 checks`, `0 failures`.
- DOC-05 structural contract: PASS, `10 checks`, `0 failures` (text-level only).
- Repository-harness contract: PASS; repository-wide checks execute for Features 001/002/003.
- Fast Feature 003: PASS=13; Fast Feature 002: PASS=10; Fast Feature 001: PASS=9, NOT_RUN=1.
- Full Feature 003: exit 20, PASS=16, `BLOCKED_BY_COMPANY_APPROVAL=2` (`BLK-ENV-003`,
  `BLK-ENV-005`), no mandatory FAIL.
- Unit: PASS; PostgreSQL integration: PASS, 15 suites/0 failures against
  `127.0.0.1:5433/iump_dev`; no port 5432 access.
- AC-005: PARTIAL; AC-011: PARTIAL; code implementation complete: YES (bounded); acceptance
  evidence complete: NO; Release-ready: NO.
- Spec Kit Analyze/Converge: `NOT_RUN` because provider-native commands are unavailable;
  direct artifact comparison is recorded and never promoted to provider PASS.

### Review and Git boundary

- Standards review: 0 Critical / 0 High / 0 actionable Medium after the path and contract fixes.
- Specification review: 0 Critical / 0 High / 0 actionable Medium after the scalar/UTC/env-name/
  task-count fixes.
- Human review completed: NO; reviewer request: NO; CI/status check: NO.
- Push branch: NOT RUN in this closure; merge performed: NO.
