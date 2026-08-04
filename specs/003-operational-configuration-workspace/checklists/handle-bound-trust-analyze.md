# Feature 003 Post-Merge Handle-Bound Trust Closure — Read-only Analyze

Date: 2026-08-04

## Entry gate

| Field | Result |
|---|---|
| Repository | `https://github.com/devphuclam/EnergySaving` |
| Feature | `003-operational-configuration-workspace` |
| Baseline | `4b4713cb42b1a03270a2688b344988d2945bab2c` |
| Branch at analysis | `main` (clean; baseline is an ancestor) |
| Corrective branch | `fix/003-handle-bound-trust-closure` |
| Provider-native Spec Kit command | `NOT_RUN` — no executable provider command is installed |
| Direct analysis | `READY` — findings are bounded to this corrective phase |

## Source precedence and scope

The analysis read `AGENTS.md`, `CONTEXT.md`, Constitution 1.1.0, the repository harness and
source-register, DOC-05/DOC-07 references, the active Feature 003 artifacts, and the current
deployment verifier/PowerShell/test seams. DOC-05/DOC-07 and the Constitution remain authoritative
over implementation artifacts. No product requirement, policy schema v2, CMS/PKCS#7 format,
certificate SHA-256 identity, accepted digest algorithm, revocation policy, or manifest schema is
reopened. No Phase 7 or Spec 004 work is registered.

## Findings

| ID | Severity | Location | Finding | Corrective direction |
|---|---|---|---|---|
| F-08 | High | `Program.cs` policy snapshot/security flow | Policy bytes are read from a `FileStream`, but file identity and security are re-opened through pathname-based `FileInfo`/directory ACL calls. The policy bytes, identity, owner/DACL, and decision are not proven to refer to the same opened filesystem objects. | Open the fixed policy file once with a no-write/no-delete-sharing `SafeFileHandle`; read identity and security from that handle; read bytes from that same stream; verify identity before/after; open immediate and ancestor directories with handles. |
| F-09 | High | `Program.cs`, `PolicyAclEvaluator.cs` | Production trust depends on a custom Allow/Deny/inheritance evaluator instead of Windows effective-access evaluation. | Use Windows built-in `GetSecurityInfo` plus `AccessCheck` against the current process token; fail closed as `BLOCKED_BY_MISSING_TOOL` when the capability cannot be established. Keep the historical evaluator only for isolated static fixture coverage if needed. |
| F-10 | High | `DeploymentTarget.ps1` | A started verifier that exits without a protocol result is labelled `verifier-process-failure` and mapped to missing-tool, conflating implementation/runtime crashes with missing command/runtime capability. | Distinguish pre-start failures from started-process/no-protocol failures. Only command/project/runtime preflight and process-start exceptions map to `BLOCKED_BY_MISSING_TOOL`; a started process without a valid protocol result is `FAIL`. |
| F-11 | Medium | `tasks.md` | T138–T140 remain unchecked even though their equivalent verification/review/convergence work was completed in T141–T156. | Mark T138–T140 complete with explicit fulfillment and evidence references; keep the historical T034 package/company classification unchanged. |
| F-12 | Medium | Current feature checkpoints/review | Current artifacts still describe stale pre-merge branches/commits and use “independent review” for internal two-axis self-review. | Add one current post-merge state section for `main`/`4b4713c`; label internal two-axis Standards/Specification self-review, independent human review `NO`, and GitHub CI/status evidence `NO`; retain historical wording only under historical headings. |

## Implementation gate

`READY`: findings are concrete, bounded, and covered by the corrective task graph T157–T170. The
single implementation phase is **Post-Merge Handle-Bound Trust Closure**. The implementation must
follow red test → recorded red evidence → minimal green implementation → refactor → focused
verification and must stop at its implementation checkpoint. Full/release blockers remain external
evidence and must not be promoted to PASS.

## Provider status

No `speckit.analyze`, `speckit.implement`, or `speckit.converge` executable was found in the
repository/runtime. This direct analysis is recorded as `NOT_RUN` for provider-native execution,
not as provider PASS.
