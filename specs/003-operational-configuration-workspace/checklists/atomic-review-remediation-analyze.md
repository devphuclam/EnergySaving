# Historical Feature 003 Atomic Signed-Approval Review Remediation Analysis

Date: 2026-08-04

Baseline: `37606adde7ac39476e53d9aaf43ded608e45038e`

Branch: `fix/003-atomic-review-remediation`

Constitution: `1.1.0`

## Spec Kit command status

The repository/runtime contains Spec Kit templates, prerequisite scripts, and a workflow manifest,
but no executable provider-native `/speckit.analyze`, `/speckit.implement`, or convergence command.
Provider status is therefore `NOT_RUN` with reason `SpecKit provider command unavailable in this
runtime`. This direct artifact comparison is not a provider PASS.

## Entry and source gate

- Entry branch was `main`, clean at `37606ad`; that commit is an ancestor of this branch.
- Corrective branch is `fix/003-atomic-review-remediation`; no direct edits were made on `main`.
- Source precedence is DOC-01..DOC-08, ADRs, active Feature 003 artifacts, `CONTEXT.md`, then code.
- The approved PostgreSQL target is `127.0.0.1:5433/iump_dev`; port `5432` and substitute stores are
  prohibited.
- No Phase 7, Spec 004, Rule, Alert, CSV, Reporting, AI, savings, Modbus, or equipment-control work
  is authorized.

## Fresh initial T138 evidence before remediation

| Check | Result | Evidence |
|---|---|---|
| Release build | PASS | `dotnet build .\IUMP.slnx --no-restore --configuration Release`; 0 warnings, 0 errors. |
| Unit | PASS | `dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore`; all registered tests passed. |
| PostgreSQL Integration | PASS | `127.0.0.1:5433/iump_dev`; 15 suites, 0 failures. |
| Web lint/build | PASS | Existing cache only; lint exit 0, build exit 0; warnings are existing fast-refresh warnings. |
| Focused verification | PASS | deployment-signature 30/0, deployment-target 58/0, DOC-05 63/0; repository, policy, architecture, observability PASS. |
| Fast harness | PASS | Feature 003, `PASS=14`, exit 0. |
| Full harness | BLOCKED | Exit 20; `PASS=17`, `BLK-ENV-003` and `BLK-ENV-005` company-approval blockers. |

T138 is not complete because the implementation findings below require remediation and a fresh rerun.

## Findings and severity

| Finding | Severity | Evidence / impact |
|---|---|---|
| F-01 Windows drive-root reparse traversal | High | `ContainsReparsePoint` trims `C:\` to `C:` before `Path.Combine`, allowing drive-relative ancestor checks. This weakens a trust-boundary decision. |
| F-02 Negative tests pass for the wrong reason | High | Several deployment-target cases omit `SignaturePath` and fail before exercising the intended manifest/schema/path fault. Evidence is not single-fault. |
| F-03 ACL threat model is over-broad and incomplete | High | Current logic rejects any applicable Allow create rule on every ancestor without inheritance/propagation/effective applicability, while not separately modelling immediate-directory replacement/delete-child threats. It can block valid policy forever and lacks a deterministic safe contract. |
| F-04 Chain-status precedence | High | Any `RevocationStatusUnknown`/`OfflineRevocation` status currently yields BLOCKED even when mixed with fatal trust, revocation, or validity statuses that must FAIL. |
| F-05 Missing-tool classification | High | Process-start/runtime/capability failures can fall into malformed-evidence FAIL; missing approved runtime must remain `BLOCKED_BY_MISSING_TOOL` with exit 20 and a blocker ID. |
| F-06 Structured result tests are source assertions, not behavioral parser coverage | Medium | JSON mapping is embedded in process invocation and tests mostly inspect source strings; the required result matrix is not directly testable. |
| F-07 Post-main checkpoint truth | Medium | Current main contains `37606ad`, but the current corrective evidence still needs an explicit post-main review/remediation checkpoint, current task ledger, and PR/review/status truth. |

## Implementation gate

The bounded corrective phase is implementation-ready after appending T141 onward for these findings.
It must use red evidence, minimal green changes, refactor, architecture/repository checks, final
review/convergence, fresh verification, and a checkpoint. AC-005 and AC-011 remain `PARTIAL`;
acceptance evidence and Release-ready remain `NO`.
