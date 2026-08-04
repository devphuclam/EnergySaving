# Feature 003 Atomic Signed-Approval Review Remediation Review

Date: 2026-08-04

Baseline reviewed: `37606adde7ac39476e53d9aaf43ded608e45038e`

Branch under review: `fix/003-atomic-review-remediation`

Provider-native review status: `NOT_RUN`; no executable provider command is available. The review
below is a direct two-axis comparison against the repository constitution, DOC-05/DOC-07 deployment
constraints, Feature 003 artifacts, and the current verifier/test implementation.

## Initial pre-remediation review (T141/T139 evidence)

The findings below are the intentionally preserved pre-remediation gate; they are not the final
verdict for the corrected branch.

## Standards axis

| Finding | Severity | Review conclusion |
|---|---|---|
| F-01 | High | `Program.cs:288-302` must preserve rooted drive/UNC semantics while walking every ancestor. Current `TrimEnd` can turn `C:\` into `C:` and invalidate reparse checks. |
| F-03 | High | `Program.cs:572-620` treats broad inherited Allow rules as immediately unsafe without evaluating inheritance/propagation, deny precedence, target type, or immediate-parent versus higher-ancestor threat. This conflicts with fail-closed but viable ACL verification. |
| F-04 | High | `BuildCompanyChain` returns BLOCKED for any revocation-unavailable flag even when fatal chain flags coexist. Fatal invalidity must take precedence over unavailable revocation evidence. |
| F-05 | High | `DeploymentTarget.ps1:25-149` does not distinguish missing command/project/runtime/process-start capability from invalid verifier evidence. Missing capability must map to `BLOCKED_BY_MISSING_TOOL`, not malformed-evidence FAIL. |
| F-06 | Medium | `DeploymentTarget.ps1:76-132` contains a parser mixed with process invocation, while tests assert source strings rather than exercising a behavioral result parser matrix. |
| F-02 | High | Deployment-target negative cases at `tests/Verification/deployment-target.tests.ps1:199-292` omit the signature evidence pair, so they can pass on an earlier missing-signature failure instead of their named fault. |
| F-07 | Medium | The post-main checkpoint and review/status evidence must describe `37606ad` as integrated on `main`, with no new PR/reviewer/workflow/status checks. |

No additional product-scope violation was found in the signed-approval implementation. The WPF
framework reference remains documented architecture debt rather than a desired UI dependency.

## Specification axis

The implementation satisfies the broad atomic-manifest, policy-v2, expected-SHA, certificate,
signature-path, and fail-closed product boundary, but is partial against the corrective task
requirements because:

1. The required rooted reparse algorithm and drive/UNC tests are missing (T134 contract is not
   fully satisfied).
2. The required single-fault signed fixtures and exact evidence assertions are missing; several
   current negative tests omit `SignaturePath` (T125/T127 evidence is insufficient).
3. ACL effective applicability and deterministic locked-policy acceptance are not modelled as the
   threat model requires (T131 remains partial).
4. Chain fatal-status precedence and missing-tool capability mapping are not implemented (T133/T132
   remain partial).
5. Structured result handling has no independent behavioral parser seam and does not cover the full
   required matrix (T132 remains partial).
6. Current post-main evidence still needs a new remediation checkpoint and task ledger (T137/T140
   synchronization is incomplete for this continuation).

The remediation is in scope; no Phase 7, Spec 004, Rule/Alert/CSV/Reporting/AI/savings, Modbus, or
equipment-control behavior is requested or introduced.

## Review gate

Critical findings: 0 identified.

High findings: F-01, F-02, F-03, F-04, F-05 — must be resolved before T139 completion.

Actionable Medium findings: F-06, F-07 — must be resolved or explicitly dispositioned before
T139/T140 completion.

## Historical post-remediation internal two-axis self-review rerun (T152/T153)

Date: 2026-08-04

The separate internal Standards and Specification axes were rerun against the latest working tree after
the remediation changes. No Critical or High finding remains. The following evidence closes the
initial findings:

| Finding | Final disposition | Evidence |
|---|---|---|
| F-01 rooted path/reparse handling | RESOLVED | `CanonicalPathPolicy` preserves drive/UNC roots; focused behavior covers drive and UNC root boundaries plus reparse escape. |
| F-02 single-fault negative tests | RESOLVED | Signed fixture variants inject one SHA/schema/date/secret/model fault; stale missing-signature duplicates were removed. |
| F-03 ACL threat model | RESOLVED | Parent/ancestor owner checks, inheritance/propagation applicability, deny precedence, replacement/delete-child rights, and real inherited ACL fixture scenarios are covered. |
| F-04 chain precedence | RESOLVED | Fatal/mixed/revocation-only/empty statuses plus crypto/platform exception scenarios are behaviorally classified. |
| F-05 missing-tool mapping | RESOLVED | Runtime/project/process-start failures without protocol output map to `BLOCKED_BY_MISSING_TOOL`/20/`BLK-ENV-001`. |
| F-06 structured parser | RESOLVED | Required fields/types, status/exit/blocker/read counts, cardinality, extra output, malformed/no protocol, synthetic, redaction, and process failures are tested. |
| F-07 post-main truth | RESOLVED | Current baseline/branch/checkpoint/review/verification truth is synchronized; historical evidence remains labelled. |

Remaining judgement-call smell: the existing WPF framework reference is documented architecture
debt for built-in PKCS support and was not expanded or replaced with an unapproved package. No
product-scope creep was found. Release verdict remains **NO**: Full is blocked by
`BLK-ENV-003`/`BLK-ENV-005`, frontend behavior is `BLOCKED_BY_PACKAGE_POLICY`, AC-005/AC-011 remain
PARTIAL, and provider-native Spec Kit commands remain `NOT_RUN`.

Review gate after remediation: Critical=0, High=0, actionable Medium=0; T152/T153 may be marked
complete after the fresh T151 ledger is synchronized.

## Current review terminology

The historical rerun above is internal agent evidence, not independent human review. For the current
phase, Internal two-axis Standards/Specification self-review is the available evidence; independent
human review is `NO`; GitHub CI/status evidence is `NO`.
