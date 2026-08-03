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

## Phase 9 corrective functional closure evidence — 2026-07-29

Parent baseline: `6e7ff79942188517c644eb43ae541d6eddc23d06`. A native temporary worktree at that
commit produced the required true RED before green correction: Debug build exit `0`, focused unit
runner exit `1`, `Phase9FunctionalCoverageRed` cases `15`, failures `15`, with Phase 0–8 suites green
and no process crash. The temporary worktree was removed after capture.

Green evidence is provider-neutral and did not connect to PostgreSQL or execute migrations:

- Debug build/unit: exit `0` / `0`.
- Release build/unit: exit `0` / `0`.
- Fast harness: exit `0` (`PASS=8`).
- Full harness: exit `20` (`PASS=10`, `BLOCKED_BY_MISSING_TOOL=1` for missing `psql`,
  `BLOCKED_BY_COMPANY_APPROVAL=2` for CI/container target). Full is explicitly non-passing.
- Web lint/build: exit `0` / `0`; no install/download.
- Architecture, repository policy/scope, and diff checks: exit `0`.

Phase 9 task classifications remain: T192/T193/T202/T205/T218 are
`BLOCKED_BY_PACKAGE_POLICY`; T206/T219/T220 are `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE`. The
approved database capability is still `AVAILABLE` at `127.0.0.1:5433/iump_dev`, but migration and
E2E execution are `NOT_RUN`; `127.0.0.1:5432` was not contacted. No password or other secret is
recorded here.

## Phase 9 final contract-alignment closure evidence - 2026-07-29

Frozen corrective baseline: `bd513d25f07c1034398419b068fae88ad0136b0e`.

Green provider-neutral evidence: Debug and Release build/unit exit `0`; architecture verification
exit `0`; Web lint/build exit `0`; Fast harness exit `0` (`PASS=8`); Full harness exit `20` with
`PASS=10`, `BLOCKED_BY_MISSING_TOOL=1` (`psql`) and `BLOCKED_BY_COMPANY_APPROVAL=2` (CI/container
target). The Full result is explicitly non-passing because its blockers are real and are not
promoted to PASS.

Measured T170-T181 evidence is recorded in the Phase 9 checkpoint. Ledger remains
`PASS=46`, `BLOCKED=8` (`T192/T193/T202/T205/T218` package-policy and `T206/T219/T220`
transitive), `FAIL=0`, runnable `NOT_RUN=0`. Browser source/build is `YES`; Ready for Phase 10 is
`YES` only for the runnable provider-neutral contracts; Live runtime, PostgreSQL E2E/migrations,
and Release are `NO`. T218 remains unchecked and T224+ were not executed.

No database connection, migration, package installation/download, Docker command, port `5432`, or
secret value was used or recorded during this closure.

## Blocked-runtime resolution addendum — 2026-07-30

The prior database, PostgreSQL CLI, and Npgsql package-policy findings are superseded for the
approved local development target by
`specs/002-asset-simulator-latest/checklists/runtime-blocker-resolution.md`.

- Database target `127.0.0.1:5433/iump_dev`: **AVAILABLE / PASS**.
- Absolute PostgreSQL 18 `pg_isready` and `psql`: **PASS**.
- Npgsql 10.0.3 from the approved local cache: **PASS**; offline locked restore exit 0,
  download count 0.
- PostgreSQL adapters and API/Worker registration: **PASS**.
- Ordered 0001-0013 clean, N-1, `iump_dev`, and 0013 reconciliation: **PASS**.
- API/Worker/Web basic startup, readiness, login, and Web login: **PASS**.
- T218 remains `BLOCKED_BY_PACKAGE_POLICY`.
- T234 is **FAIL / incomplete** because the complete business quickstart still reaches
  fail-closed reduced-payload and Simulator Start paths.
- T236 and its task-specific PostgreSQL race/crash/E2E prerequisites are **NOT_RUN / runnable**;
  provider-neutral source contracts are not PostgreSQL execution.
- T235 remains **BLOCKED / NOT_EXECUTED** because T034 lacks company-approved Data Protection
  provisioning and T234 is not PASS.
- T245 remains `BLOCKED_BY_COMPANY_APPROVAL`.
- Release readiness remains **NO**.

No stale database-access or missing-PostgreSQL-CLI blocker is retained. No secret is recorded and
the prohibited port 5432 was not contacted.

## Functional and recovery resolution addendum - 2026-07-30

The runnable acceptance gaps identified in the preceding addendum are now resolved:

- T234: **PASS** with API/Worker/Web smoke, authenticated HTTP mutation, Administrator-created
  Site and Engineer scope, Engineer configuration journey, Simulator production, Accepted
  Measurement, Latest, Health, Audit, and Web data display against PostgreSQL.
- T236: **PASS** with six PostgreSQL recovery/race scenarios and zero failures.
- T242: **PASS**; the final Full database check reported `database target=PASS`.

T034 and T245 remain `BLOCKED_BY_COMPANY_APPROVAL`; T218 remains
`BLOCKED_BY_PACKAGE_POLICY`. T235 is therefore still `BLOCKED / NOT_EXECUTED`, and release
readiness remains **NO**. Port 5432 was not contacted and no secret is recorded.

The final Full summary is `PASS=11`, `BLOCKED_BY_COMPANY_APPROVAL=2`, captured process exit 20.

## Post-review evidence correction - 2026-07-30

The targeted six-scenario PostgreSQL recovery probe remains PASS, but it is not the complete
T226-T229 suite required by T236. T236 is therefore `NOT_RUN / runnable`, and dependency-bound
T242 remains unchecked even though the Full database check itself is PASS. The earlier T236/T242
PASS statements are superseded.

## Executable PostgreSQL acceptance closure - 2026-07-30

The post-review `NOT_RUN` state for T219, T220, and T236 is superseded by executable PostgreSQL
evidence:

- `tests/Integration/IUMP.Tests.Integration.csproj`: 4 suites, 0 failures, exit 0.
- T219 covers concurrent command identity registration, live/expired Pending behavior, exact
  status/body/Location/ETag/correlation replay, fingerprint conflict, and atomic
  completion/outbox commit and rollback.
- T220 covers Audit replay IDs, source hash conflict, transaction rollback, query visibility under
  five seconds, and inbox lease/dedup/hash-conflict/Failed behavior.
- The acceptance suite adds real Mapping optimistic-concurrency and Latest ordering races.
- The recovery runner adds 6/6 real Simulator/Telemetry crash, retry, Latest no-regression,
  start-race, and Audit dedup scenarios.

T219, T220, and T236 are PASS. T242 is runnable and awaits the fresh final Full harness. T034 and
T245 remain `BLOCKED_BY_COMPANY_APPROVAL`; T218 remains `BLOCKED_BY_PACKAGE_POLICY`. Release
readiness remains NO.

## Exact-coverage correction - 2026-07-30

The preceding T219/T220/T236 PASS claim is superseded. The executable PostgreSQL runner passes
all implemented cases, but it does not yet cover the exact full HTTP/crash/delivery/race matrices
required by those task descriptions. T219, T220, and T236 are therefore `NOT_RUN / runnable`.
T242 remains unchecked because T236 is incomplete, although the fresh Full database target check
passes. Database capability remains AVAILABLE; this is not a database-access or package-policy
blocker.

T233's migration execution evidence passes, but the task is unchecked because its declared
T031/T052/T074/T090/T127/T148/T166/T206 dependencies remain incomplete.

## Final executable leaf-suite closure - 2026-07-30

The preceding incomplete runnable-task state is superseded:

- PostgreSQL integration runner: **13 suites, zero failures, exit 0**.
- Functional runner with Point-activation/configuration and Start/Mapping races:
  **PASS, exit 0**.
- Recovery runner: **6 scenarios, zero failures, exit 0**.
- T031/T052/T074/T090/T104/T127/T148/T166/T206/T219/T220/T233/T236/T242:
  **PASS**.
- Database target, CLI, package, migrations, adapters, API/Worker/Web smoke, and functional
  quickstart: **PASS**.
- Fresh Full: numeric exit **20**, `PASS=11`, `BLOCKED_BY_COMPANY_APPROVAL=2`, `FAIL=0`.
- T218 remains `BLOCKED_BY_PACKAGE_POLICY`.
- T034/T235/T245 remain `BLOCKED_BY_COMPANY_APPROVAL`.
- Release readiness remains **NO**.

No secret is recorded, `.env` remains untracked, and port 5432 was not contacted.

## Deployment reconciliation — 2026-08-03

The historical container-target record above is superseded by DOC-05 v0.2 and DOC-07 v0.2. The
current target is restricted non-containerized host/service deployment. The current harness does
not emit `BLK-ENV-004`; it emits `BLK-ENV-005` when Infrastructure/Security approval for a concrete
TEST/UAT/PROD host, service manager, lifecycle, and rollback evidence is unavailable.

`BLK-ENV-005` is a company-approval blocker for release deployment, not a database-access or
package-policy blocker. No deployment or service mutation was performed during this reconciliation.
