# Feature 003 Atomic Signed-Approval Analyze

Date: 2026-08-04

Baseline: `90bafced98f80b3bbbe80bf86f81ef1c28b694ef`

Branch: `fix/003-atomic-signed-approval`

## Provider status

| Provider operation | Status | Evidence |
|---|---|---|
| Spec Kit Analyze | NOT_RUN | No provider-native Spec Kit command was available in this runtime; direct artifact comparison is used. |
| Spec Kit Implement | NOT_RUN | No provider-native command was available; bounded T124-T140 implementation is executed directly from the canonical task ledger. |
| Spec Kit Converge | NOT_RUN | No provider-native command was available; final direct comparison is recorded after verification. |

## Entry gate

- Worktree was clean before corrective branch creation.
- `origin/main` resolved to `90bafce` and is an ancestor of this branch.
- Corrective branch is `fix/003-atomic-signed-approval`; no direct implementation was made on `main`.
- PR #7 is historical and already merged at `90bafce`; no pending merge is claimed.

## Direct findings

| Finding | Severity | Current risk | Required disposition |
|---|---|---|---|
| FINDING-01 | High | PowerShell hashes/parses the manifest separately from the .NET signature verifier. | One .NET verifier owns hash, expected-SHA comparison, signature verification, UTF-8 decode, and JSON parse over one byte snapshot. |
| FINDING-02 | High | Trust policy ACL/path checks and policy reads can use separate pathname operations. | Open policy once with write/delete sharing denied; inspect security while the handle is open; parse bytes from that handle. |
| FINDING-03 | High | Verifier JSON is discarded and classification is inferred from process exit code. | Capture exactly one machine-readable JSON result and validate/propagate its contract. |
| FINDING-04 | High | Production trust identity uses SHA-1 thumbprints and revocation is disabled. | Use policy v2 SHA-256 certificate raw-byte fingerprints, strong algorithms, and explicit Online/Offline revocation. |
| FINDING-05 | Medium | Signature path is not independently constrained to the trusted evidence root. | Treat manifest/signature as one evidence pair; reject traversal, reparse, repository, and regular-file violations. |
| FINDING-06 | Medium | The console verifier uses a UI framework reference only to obtain PKCS support. | Prefer the installed non-UI ASP.NET Core framework; record debt if unavailable. |
| FINDING-07 | Medium | Prior release evidence was stale after PR #7 merge. | Synchronize current checkpoint and report reviewers/CI honestly; Release-ready remains NO. |

## Required fail-closed semantics

- Missing company policy, trust root, or approval evidence: `BLOCKED_BY_COMPANY_APPROVAL`/`BLK-ENV-005`.
- Missing verifier capability: `BLOCKED_BY_MISSING_TOOL` with the repository blocker ID.
- Malformed, unsigned, modified, wrong-signer, weak, revoked, untrusted, path-unsafe, or policy-mismatched evidence: `FAIL` when the verifier can execute.
- Revocation unknown/unavailable: `BLOCKED`, never PASS.
- Synthetic signatures are contract-only and can never establish production approval.
- No real certificate, private key, company policy, signed production manifest, credential, or secret value is written to the repository.
