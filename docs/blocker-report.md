# R0 Blocker Report

## BLK-R0-001 — Approved dependency source unavailable

- Evidence: `dotnet nuget list source`, NuGet configuration inspection, `npm config get registry`,
  lockfile/cache inventory.
- Impact: restore, reproducible backend build/test, certified offline frontend install.
- Completed safely: tool/source/cache inventory and dependency manifest inspection.
- Required: company-approved internal/local NuGet and npm sources containing locked versions.
- Lowest-risk path: IT provides read-only source endpoints and trust configuration; then run restore
  in locked/offline mode with network access constrained to approved endpoints.

## BLK-R0-002 — PostgreSQL execution unavailable

- Evidence: `where.exe psql`, `Get-Command psql`, and `psql --version` found no client; no internal
  endpoint or credential was supplied.
- Impact: migration execution, seed idempotency, database health, outbox/inbox integration tests.
- Completed safely: schema/contract planning and access request.
- Required: approved local PostgreSQL or internal development database per
  `docs/database-access-request.md`.
- Lowest-risk path: company-provisioned least-privilege development database and redacted secret
  delivery; no local installation by the project.

## BLK-R0-003 — Approved CI runner/template unavailable

- Evidence: existing workflow depends on public GitHub actions and a PostgreSQL container.
- Impact: hosted build/test/migration evidence and immutable release artifacts.
- Completed safely: local-equivalent design and runner requirements.
- Required: approved internal runner, templates/actions, tools, package mirrors, and database.
- Lowest-risk path: execute `scripts/verify.ps1` locally now; port identical checks to the supplied
  company template later.

## BLK-R0-004 — Container verification prohibited

- Evidence: explicit company policy in the task; prior Dockerfiles/Compose are prohibited artifacts.
- Impact: DOC-05 reference deployment validation and image promotion.
- Completed safely: non-container workstation decision and target-deployment deferment.
- Required: separate Infrastructure/Security decision for TEST/UAT/PROD if containers remain the
  target architecture.
- Lowest-risk path: use approved executables/services locally and review the deployment topology in
  a controlled infrastructure environment.

## T012 Phase 0 evidence closure — 2026-07-24

The documentation tasks below are PASS because the exact read-only inspections were completed and
recorded. Capability outcomes remain separate and are never promoted to PASS by documentation
completion.

### T002 — PostgreSQL capability

- **Documentation task**: PASS.
- **Capability**: `BLOCKED_BY_DATABASE_ACCESS`.
- **Blocker ID**: `BLK-T012-DB-001`.
- **Inspection**: read `docs/database-access-request.md`; ran `Get-Command psql`, `where.exe psql`,
  and `psql --version` without supplying credentials or connecting.
- **Redacted result**: no approved endpoint/profile or approved credential-delivery evidence was
  supplied; `psql` is unavailable. `dotnet ef` is installed but cannot replace an approved
  PostgreSQL execution target.
- **Impact**: migration execution, seed/idempotency checks, and PostgreSQL integration evidence
  remain unavailable.
- **Required authority/action**: IT/company owner supplies an approved least-privilege development
  PostgreSQL endpoint and redacted secret-delivery evidence.
- **Lowest-risk resolution**: use the company-provisioned development database with synthetic data
  and approved TLS/credential delivery; do not install a local server or substitute a database.

### T003 — Package capability

- **Documentation task**: PASS.
- **Capability**: `BLOCKED_BY_PACKAGE_POLICY`.
- **Blocker ID**: `BLK-T012-PKG-001`.
- **Inspection**: ran `dotnet nuget list source`, `dotnet nuget locals global-packages --list`,
  `npm config get registry`, and inspected `Directory.Packages.props`, `global.json`, and the
  locked manifest `src/Web/package-lock.json`.
- **Redacted result**: NuGet reports no configured sources; npm resolves to the public
  `https://registry.npmjs.org/`; central package versions are intentionally inactive. A local
  NuGet cache exists, but its approved provenance is not established.
- **Impact**: approved reproducible restore/build/test evidence is unavailable.
- **Required authority/action**: company dependency owner provides approved internal/local mirrors
  and trust configuration for the locked package set.
- **Lowest-risk resolution**: restore only from the approved offline/internal cache or mirror in
  locked mode after provenance is recorded; no public restore or download.

### T004 — Tool capabilities

- **Documentation task**: PASS.
- **Capabilities**:
  - `dotnet` 10.0.300: PASS.
  - PowerShell 5.1.19041.6456: PASS.
  - `dotnet ef` 10.0.10: PASS.
  - repository harness and compatibility wrapper: PASS (`scripts/harness.ps1`,
    `scripts/verify.ps1`).
  - `curl.exe`/PowerShell web request surface: PASS for local smoke-tool availability only.
  - `psql`: `BLOCKED_BY_MISSING_TOOL`.
- **Blocker ID**: `BLK-T012-TOOL-001` for `psql`.
- **Inspection**: read-only version/path checks for `dotnet`, `dotnet ef`, PowerShell, `curl.exe`,
  `psql`, and the repository harness scripts.
- **Redacted result**: `psql` was not found; no tool was installed or downloaded.
- **Impact**: approved PostgreSQL execution cannot start from this workstation.
- **Required authority/action**: provide the approved PostgreSQL client through company tooling or
  an approved execution host.
- **Lowest-risk resolution**: use an already approved client on the provisioned database host;
  do not install a client locally under the restricted policy.

### T005 — Company approvals

- **Documentation task**: PASS.
- **Capabilities**: each unavailable approval is `BLOCKED_BY_COMPANY_APPROVAL`.
- **Blocker IDs**:
  - `BLK-T012-APP-001`: Data Protection provisioning approval.
  - `BLK-T012-APP-002`: company CI runner/template approval.
  - `BLK-T012-APP-003`: target-host approval.
  - `BLK-T012-APP-004`: separate operational/security approval evidence.
- **Inspection**: reviewed `docs/decision-log.md`, `docs/repository-harness.md`, ADR references,
  and the existing CI/deployment guidance; no concrete capability approval was found for these
  environments.
- **Redacted result**: DEC-GOV-009 is governance-only and does not authorize Data Protection, CI,
  database, package, target-host, deployment, or release capability.
- **Impact**: protected-data configuration, hosted CI, target-host execution, and operational
  release evidence remain unavailable.
- **Required authority/action**: the relevant company owners provide separate written approvals and
  evidence for each capability.
- **Lowest-risk resolution**: continue with local source/evidence review only, then use approved
  company templates, hosts, and security provisioning when supplied; no keys or sensitive approval
  content are recorded here.
