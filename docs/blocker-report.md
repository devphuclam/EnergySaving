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

## Active Phase 2 capability evidence — verified local `.env` runtime target

The historical R0/T012 database-unavailable entries above are retained as history and are
superseded by the successful read-only connection verification performed against the approved
local `.env` target. `IUMP_Local_Database_Connection_Info.md` remains environment evidence only
and is not a project change.

- **PostgreSQL capability**: `AVAILABLE`.
- **Engine**: PostgreSQL 18.
- **Host**: `127.0.0.1`.
- **Port**: `5433`.
- **Database**: `iump_dev`.
- **Bootstrap user**: `postgres`.
- **Credential source**: existing repository-local `.env` loaded into the runtime environment.
- **Password**: `REDACTED` (never recorded here).
- **Connection verification**: `PASS` (read-only target/version query).
- **Database mutation/migration execution**: `NOT_RUN`.
- **Old cluster `127.0.0.1:5432`**: `PROHIBITED`; it was not contacted.

Phase 2 package classifications remain separate from database capability:

- T050: `BLOCKED_BY_PACKAGE_POLICY` (locked PostgreSQL adapter packages unavailable).
- T051: `BLOCKED_BY_PACKAGE_POLICY` (host registration depends on T050).
- T052: `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE` (depends on T050/T051; not a database-access
  classification).
- `BLOCKED_BY_DATABASE_ACCESS` count for this invocation: `0`.

## Phase 6 Simulator Run/Worker capability evidence — 2026-07-28

The approved PostgreSQL runtime target remains available. Phase 6 did not connect to either local
cluster and did not execute migration `0007_acquisition_run.sql`; database capability and adapter
package capability remain separate.

- **PostgreSQL capability**: `AVAILABLE` at `127.0.0.1:5433/iump_dev`.
- **Credential source**: existing ignored repository-local `.env`; secret values were not read into
  evidence, printed, serialized, copied, or committed.
- **Database mutation/migration execution**: `NOT_RUN`.
- **Old cluster `127.0.0.1:5432`**: `PROHIBITED`; not contacted.
- **`psql` tool**: `BLOCKED_BY_MISSING_TOOL`.
- **Database-access blocker count**: `0`.

Phase 6 task classifications:

- T125: `BLOCKED_BY_PACKAGE_POLICY` because no approved PostgreSQL adapter package is available.
- T126: `BLOCKED_BY_PACKAGE_POLICY` because runtime registration depends on the absent adapter and
  no package/host registration was authorized.
- T127: `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE` because T125/T126 are incomplete. This is not
  `BLOCKED_BY_DATABASE_ACCESS`.

Provider-neutral work remains runnable and is not blocked by the package classification. Migration
source review and fake-backed Run/attempt transaction tests do not claim PostgreSQL execution.

## Phase 7 Telemetry capability evidence — 2026-07-28

The approved PostgreSQL runtime target remains `AVAILABLE` at `127.0.0.1:5433/iump_dev`.
Phase 7 did not connect to either local cluster, did not run `psql`, and did not execute migration
`0008_telemetry_measurement.sql`. The ignored local `.env` remained untracked and no secret value
was read into evidence, printed, serialized, copied, or committed.

- T146: `BLOCKED_BY_PACKAGE_POLICY`; the PostgreSQL Telemetry adapter and required approved package
  dependencies are unavailable, so `PostgresTelemetryRepositories.cs` was not created.
- T147: `BLOCKED_BY_PACKAGE_POLICY`; runtime registration depends on T146, so API/Worker
  composition roots were not modified.
- T148: `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE`; migration execution and PostgreSQL Telemetry
  transaction/concurrency tests depend on T146/T147. PostgreSQL capability itself is available.
- `psql`: `BLOCKED_BY_MISSING_TOOL`, a separate tool capability.
- Database-access blocker count: `0`.
- Database mutation: `NOT_RUN`.
- Prohibited cluster `127.0.0.1:5432`: not contacted.

## Phase 8 Latest/Source Health capability evidence — 2026-07-29

Phase 8 provider-neutral Latest, Source Health, and Operations contract work
did not connect to PostgreSQL, run `psql`, execute migration `0009`, or mutate
database data. The approved runtime target remains available at
`127.0.0.1:5433/iump_dev`; credentials remain in the ignored local `.env` and
are not recorded here.

- T164: `BLOCKED_BY_PACKAGE_POLICY`; the PostgreSQL Operations adapter and its
  approved package dependencies are unavailable. `PostgresJobRepositories.cs`
  was not created.
- T165: `BLOCKED_BY_PACKAGE_POLICY`; Worker registration depends on T164 and
  `src/Worker/Program.cs` was not modified.
- T166: `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE`; it depends on T164/T165 and the
  blocked T146 adapter path. PostgreSQL itself is available, so this is not a
  database-access classification.
- `psql`: `BLOCKED_BY_MISSING_TOOL`, a separate capability; no installation or
  download was attempted.
- Database-access blocker count for this invocation: `0`.
- Database mutation/migration execution: `NOT_RUN`.
- Prohibited cluster `127.0.0.1:5432`: not contacted.

## Phase 9 API/Audit/Web capability evidence — 2026-07-29

Phase 9 completed provider-neutral API, Integration, Worker, Audit and Web seams only. The approved
PostgreSQL target remains `AVAILABLE` at `127.0.0.1:5433/iump_dev`; Phase 9 did not connect, run
`psql`, execute migrations `0010`/`0011`, or mutate database data. The ignored local `.env` stayed
untracked and no secret value was read into evidence, printed, serialized, copied, or committed.

- T192, T193, T202, T205: `BLOCKED_BY_PACKAGE_POLICY`; approved locked PostgreSQL adapter packages
  are unavailable, so adapter source and composition-root registration remain absent.
- T206, T219, T220: `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE`; each depends on the blocked adapter and
  registration chain. PostgreSQL capability itself is available; this is not database-access
  blocking.
- T218: `BLOCKED_BY_PACKAGE_POLICY`; the locked Web package has no approved behavior-test runner
  command. `npm run lint` and `npm run build` were run from the existing cache without install or
  download.
- `psql`: `BLOCKED_BY_MISSING_TOOL`, a separate tool capability; no installation or download was
  attempted.
- Database-access blocker count for this invocation: `0`.
- Database mutation/migration execution: `NOT_RUN`.
- Prohibited cluster `127.0.0.1:5432`: not contacted.
