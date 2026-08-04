# Historical Feature 003 Atomic Signed-Approval Review Remediation Checkpoint

Date: 2026-08-04
Authoritative merged-main baseline: `37606adde7ac39476e53d9aaf43ded608e45038e`
Corrective branch: `fix/003-atomic-review-remediation`
Scope: T141-T156 only; no merge, Phase 7, Spec 004, deployment, or release approval.

## Governance and provider status

- Constitution: `.specify/memory/constitution.md` v1.1.0.
- Entry gate: clean `main` at the baseline above; baseline is an ancestor of this branch.
- Provider-native Spec Kit command detection: **NOT_RUN**. No executable `analyze`, `implement`,
  or `converge` provider command is installed in this checkout. Direct artifact/code/test analysis
  is recorded separately and is never promoted to provider PASS.
- Initial review: Standards and Specification axes both confirmed F-01..F-07 before remediation;
  no Critical findings, High F-01..F-05, and actionable Medium F-06/F-07.

## T138 evidence before remediation (fresh on merged `main`)

| Check | Result |
|---|---|
| Release solution build | PASS, 0 warnings / 0 errors |
| Unit | PASS, all registered suites |
| PostgreSQL Integration | PASS, 15 suites / 0 failures, target `127.0.0.1:5433/iump_dev` |
| Web lint/build | PASS; existing Fast Refresh warnings only |
| Focused deployment-signature | PASS, 30 checks / 0 failures |
| Focused deployment-target | PASS, 58 checks / 0 failures |
| DOC-05 architecture | PASS, 63 checks / 0 failures |
| Repository policy/architecture/observability | PASS |
| Fast Feature 003 | PASS, 14 checks |
| Full Feature 003 | BLOCKED, exit 20; PASS=17, `BLK-ENV-003` and `BLK-ENV-005`; no mandatory FAIL |

## TDD red evidence

The first focused remediation run was intentionally red before the ACL implementation was
completed:

```text
& powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Verification\deployment-signature.tests.ps1
```

Result: non-zero with `FAIL: ACL propagation applicability; missing=PropagationFlags`. This was
the expected contract red for the missing propagation-aware ACL rule evaluation. A transient
missing test-helper failure from an intermediate edit was corrected before the green run and is
not counted as product evidence.

## Bounded implementation result

The single remediation phase applies red -> green -> refactor -> architecture/repository checks:

- `ContainsReparsePoint` preserves Windows drive/UNC roots and canonicalizes every existing
  ancestor; containment uses canonical root boundaries.
- Deployment-target negative cases use valid signed fixtures with one injected SHA/schema/date/
  secret/model/path/reparse fault and assert status, exit code, and safe evidence.
- Policy ACL checks are split across the policy file, immediate directory, and higher ancestors;
  inheritance/propagation, applicability, deny precedence, replacement, and delete-child risks
  are evaluated fail-closed. Unsafe capability maps to `BLOCKED_BY_MISSING_TOOL`/`BLK-ENV-001`.
- Chain classification is extracted and behaviorally tested: fatal statuses remain `Invalid`
  even when mixed with revocation uncertainty; `Blocked` is reserved for revocation-unavailable-only
  statuses.
- Missing dotnet/project/runtime/process-start capability is a structured
  `BLOCKED_BY_MISSING_TOOL`, exit 20, `BLK-ENV-001` result.
- `ConvertFrom-DeploymentVerifierProcessResult` is a fixed protocol parser with required fields,
  exact cardinality, exit/status/blocker/read-count checks, synthetic-production rejection, and
  evidence redaction. Extra stdout is ignored unless it carries the protocol prefix.

## Checkpoint disposition

| State | Result | Rationale |
|---|---|---|
| Planning-ready | YES | Existing Feature 003 artifacts remain authoritative; remediation is additive and bounded. |
| Implementation-ready | YES | T141-T153 are dependency-ordered; the single implementation phase, fresh verification, and independent reviews are complete. |
| Code implementation complete | YES (bounded) | Findings F-01..F-07 have code/test coverage and focused green evidence; Standards/Spec review found no Critical/High/actionable Medium findings. |
| Acceptance evidence complete | NO | AC-005 and AC-011 remain PARTIAL; no fresh approved authenticated browser/process-control trace. |
| Release-ready | NO | Full remains company-approval blocked and frontend behavior remains package-policy blocked. |

## Stop boundary

This checkpoint does not authorize a merge, release, Phase 7, Spec 004, or a product capability
expansion. T154/T155 are complete; only T156 commit/push/PR preparation remains before the
corrective branch is handed off for review.
