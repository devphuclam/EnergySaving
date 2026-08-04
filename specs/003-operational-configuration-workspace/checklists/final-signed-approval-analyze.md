# Feature 003 Final Signed-Approval Analyze

Date: 2026-08-03

Baseline: `2309cfecdd24538e320dcb70c35fcbd5d42bf9e2`

Starting branch: `main`

Corrective branch: `fix/003-signed-approval-closure`

Scope: additive T110-T123 only; no Phase 7, Spec 004, product capability, merge, or release.

## Provider status

| Provider operation | Status | Evidence |
|---|---|---|
| Spec Kit Analyze | NOT_RUN | No provider-native Spec Kit command is available in this runtime. |
| Spec Kit Tasks | NOT_RUN | No provider-native Spec Kit command is available; corrective tasks were appended manually to the canonical ledger as authorized by the supplied closure prompt. |
| Spec Kit Implement | NOT_RUN | No provider-native command is available; bounded implementation follows the appended T110-T123 task graph. |
| Spec Kit Converge | NOT_RUN | No provider-native Spec Kit command is available; direct comparison is recorded after implementation. |

## Capability and trust-anchor analysis (T110)

| Capability | Result | Evidence / disposition |
|---|---|---|
| .NET SDK | AVAILABLE | Preinstalled .NET SDK 10.0.300 was detected. |
| SHA-256 | AVAILABLE | Built-in `System.Security.Cryptography.SHA256` is available. |
| `X509Certificate2` / `X509Chain` | AVAILABLE | Built-in .NET cryptography types are available in the approved runtime. |
| Windows certificate stores | AVAILABLE | `X509Store`/LocalMachine store APIs are available; no production certificate was read or emitted. |
| `System.Security.Cryptography.Pkcs` / `SignedCms` | AVAILABLE | Assembly is present in the preinstalled ASP.NET Core shared runtime and is consumed by the provider-neutral verifier utility; no package was installed or downloaded. |
| Company-managed trust policy | BLOCKED | No repository or workstation company-managed signer policy was supplied. Production verification remains `BLOCKED_BY_COMPANY_APPROVAL`/`BLK-ENV-005`; synthetic fixtures are contract-only. |

## Direct comparison findings before implementation

| ID | Severity | Finding | Corrective task(s) |
|---|---|---|---|
| FINDING-01 | HIGH | Environment booleans, manifest path, and developer-supplied digest could make a local manifest appear approved without a company-managed signer/trust anchor. | T110, T113-T115 |
| FINDING-02 | HIGH | The verifier hashed the file and reopened it with `Get-Content` for parse, leaving a TOCTOU window. | T111, T113-T115 |
| FINDING-03 | HIGH | Current evidence still described no push/no merge and corrective-branch-only state after `2309cfe` had been integrated to `main`. | T118 |
| FINDING-04 | MEDIUM | DOCX structural verification did not validate required package entries and relationship integrity. | T112, T116 |
| FINDING-05 | MEDIUM | Visual DOCX QA was named as a release blocker without a machine-readable Full gate or source-of-truth decision. | T117 |
| FINDING-06 | MEDIUM | Provider-specific OpenCode/DeepSeek instructions remained in `AGENTS.md` and were outside Feature 003 trust-boundary scope. | T119 |

No implementation is claimed by this pre-implementation record. Critical/High conflicts are
understood and are permitted to proceed only through the additive T110-T123 corrective task graph.

## Required fail-closed semantics

- Missing company-managed trust policy: `BLOCKED_BY_COMPANY_APPROVAL` with `BLK-ENV-005`.
- Missing built-in cryptographic capability: `BLOCKED_BY_MISSING_TOOL`.
- Malformed, unsigned, modified, wrong-signer, expired, untrusted, or policy-mismatched evidence:
  `FAIL` when the cryptographic verifier is available.
- Valid synthetic signature: contract PASS only; never company approval or release PASS.
- No secret, certificate private key, signed production manifest, internal hostname, or protected
  policy is written to the repository or evidence.
