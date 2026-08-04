# PR body — Feature 003 Atomic Signed-Approval Review Remediation

## Summary

This corrective branch remediates the seven Standards/Specification findings against merged-main
baseline `37606adde7ac39476e53d9aaf43ded608e45038e` without widening Feature 003.

## Changes

- Preserve Windows drive/UNC roots during reparse and containment checks.
- Exercise deployment-target negative cases with valid signed evidence and one injected fault.
- Make policy ACL evaluation inheritance/propagation-aware with deny precedence and fail-closed
  missing-tool mapping.
- Extract and test fatal-versus-revocation-unavailable chain classification.
- Return structured `BLOCKED_BY_MISSING_TOOL`/exit 20/`BLK-ENV-001` for missing runtime/process
  capability.
- Extract the protocol parser with required-field, cardinality, mismatch, read-count, synthetic,
  redaction, and process-failure checks.
- Synchronize current main/branch/checkpoint/review/verification truth while preserving history.

## Verification

- Release build: PASS, 0 warnings/0 errors.
- Unit: PASS.
- PostgreSQL Integration: PASS, 15 suites/0 failures at `127.0.0.1:5433/iump_dev`.
- Web lint/build: PASS (existing Fast Refresh warnings only).
- Focused deployment-signature: PASS, 65 checks/0 failures.
- Focused deployment-target: PASS, 95 checks/0 failures.
- Fast Feature 003: PASS=14.
- Full Feature 003: exit 20, PASS=17, blocked by `BLK-ENV-003` and `BLK-ENV-005`; no mandatory FAIL.

## Explicit limits

AC-005 and AC-011 remain PARTIAL. Frontend behavior is `BLOCKED_BY_PACKAGE_POLICY`; provider-native
Spec Kit commands are `NOT_RUN` because unavailable. This PR is corrective evidence work only and
does not authorize merge, release, Phase 7, Spec 004, or deployment.
