# Feature 003 Atomic Signed-Approval Implementation Checkpoint

Date: 2026-08-04

Baseline: `90bafced98f80b3bbbe80bf86f81ef1c28b694ef`

Branch: `fix/003-atomic-signed-approval`

Constitution: `1.1.0`

## Scope and phase boundary

This is one bounded Atomic Signed-Approval corrective implementation phase. It does not create
Phase 7, Spec 004, Rule, Alert, CSV, Reporting, AI, savings analytics, Modbus, equipment control,
or release approval. Constitution 1.1.0 requires an explicit stop at this implementation
checkpoint; Standards Review, Specification Review, Converge, Final Analyze, push, and PR
preparation remain subsequent tasks and are not silently executed in this phase.

## Spec Kit status

| Step | Actual command | Status | Evidence |
|---|---|---|---|
| Analyze | provider-native command check; no repository Spec Kit command available | NOT_RUN | Direct analysis is recorded in `atomic-signed-approval-analyze.md`; this is not provider PASS. |
| Task append | append-only edit to `tasks.md` | PASS | New T124-T140 ledger appended after T001-T123; prior task boxes were not rewritten. |
| Implement | bounded direct implementation from T124-T140 | PASS | Production verifier, PowerShell adapter, fixtures, and focused tests changed only in scope. |
| Standards Review | not run by this phase | NOT_RUN | Constitution checkpoint stop. |
| Specification Review | not run by this phase | NOT_RUN | Constitution checkpoint stop. |
| Convergence/Final Analyze | not run by this phase | NOT_RUN | No provider-native command and checkpoint stop. |

## TDD evidence

- RED evidence captured before green implementation: `deployment-signature.tests.ps1` exited non-zero
  because the policy-v2 source contract was missing `allowedSignerCertificateSha256`.
- RED evidence was not reconstructed for any other seam after implementation; no historical failure
  is claimed.
- GREEN focused evidence after implementation:
  - `deployment-signature.tests.ps1`: `30 checks, 0 failures`, exit 0.
  - `deployment-target.tests.ps1`: `58 checks, 0 failures`, exit 0.
  - `doc05-architecture.tests.ps1`: `63 checks, 0 failures`, exit 0.
  - `repository-harness.tests.ps1`: PASS, exit 0.
  - `repository-policy.tests.ps1`: PASS, exit 0.
  - `architecture.tests.ps1`: PASS, exit 0.
  - `observability.tests.ps1`: `12 checks, 0 failures`, exit 0.
  - `dotnet build .\IUMP.slnx --no-restore --configuration Release`: 0 warnings, 0 errors.
  - Unit suite: all registered tests passed, exit 0.
  - PostgreSQL Integration: `127.0.0.1:5433/iump_dev`, 15 suites, 0 failures, exit 0.

## Trust-critical implementation contract

- PowerShell no longer performs `Get-FileHash`, `Get-Content`, `ConvertFrom-Json` manifest
  processing, schema validation, secret-key validation, date validation, or signature conclusions.
- PowerShell passes `--expected-sha256`, `--manifest`, `--signature`, `--trusted-root`, and the
  repository root to the .NET verifier, then validates and propagates exactly one JSON result.
- Manifest bytes are opened once with `FileShare.Read`, hashed, expected-SHA checked, CMS verified,
  strict UTF-8 decoded, and parsed from the same byte array. Successful manifest processing reports
  `manifestReadCount=1`; no manifest reopen exists.
- Production policy uses the fixed machine path `%ProgramData%\IUMP\DeploymentTrustPolicy.json`.
  It is opened once with `FileShare.Read`, ACL/owner and parent-directory security are checked while
  the handle remains open, and policy bytes are parsed from that handle. Successful policy reads
  report `policyReadCount=1`; no policy reopen exists.
- Policy schema is version 2 with SHA-256 fingerprints of `certificate.RawData`, required EKUs, and
  `Online`/`Offline` revocation modes. `X509RevocationMode.NoCheck` is absent.
- CMS SHA-1/MD5 digests and weak/unknown public keys are rejected; RSA requires 2048 bits and ECDSA
  requires NIST P-256 when supported.
- Manifest/signature are a trusted evidence pair: same root, no traversal, no reparse escape, no
  repository path, and regular files only. Evidence text contains no detailed sensitive paths.
- Synthetic fixtures are contract-only and never establish production approval.

## Framework and lockfile disposition

- The installed ASP.NET Core shared runtime contains `System.Security.Cryptography.Pkcs`, but its
  reference pack does not expose the forwarded PKCS types for this console target without the
  Windows Desktop reference. The approved no-download fallback is to keep the existing
  `Microsoft.WindowsDesktop.App.WPF` framework reference temporarily, record this architecture debt,
  and avoid claiming WPF is a desired UI dependency.
- `tests/Integration/packages.lock.json` differs from the earlier baseline only by removing the
  stale `iump.tests.unit` project entry. `IUMP.Tests.Integration.csproj` has no Unit project
  reference, while the current lock graph matches its project references. No restore or Internet
  mutation was used; the deterministic correction is retained.

## Readiness and blockers

- AC-005: PARTIAL.
- AC-011: PARTIAL.
- Acceptance evidence complete: NO.
- Release-ready: NO.
- Company-managed trust policy and approved deployment target remain unavailable; production approval
  must remain `BLOCKED_BY_COMPANY_APPROVAL`/`BLK-ENV-005`.
- No Phase 7 or Spec 004 was created.

## Explicit stop

This implementation checkpoint is recorded under Constitution 1.1.0. Stop here. Do not run the
remaining review, convergence, final Analyze, push, PR, merge, or release steps until a subsequent
explicit continuation is authorized.
