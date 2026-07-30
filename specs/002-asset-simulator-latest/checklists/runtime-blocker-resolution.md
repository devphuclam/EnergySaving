# Blocked Runtime Resolution Closure

Date: 2026-07-30  
Baseline SHA: `9d03895a6c82e596223bb1a846f9e8888ecdd9dd`  
Repository: `devphuclam/EnergySaving`

## Capability decision

| Capability | Final state |
|---|---|
| Database connection information | PROVIDED |
| PostgreSQL target | AVAILABLE |
| `pg_isready` absolute path | PASS |
| `psql` absolute path | PASS |
| Authenticated target query | PASS: `iump_dev`, bootstrap role, port 5433, PostgreSQL 18.4 |
| Database-access blocker | RESOLVED |
| Missing PostgreSQL CLI blocker | RESOLVED |
| Npgsql | PASS: 10.0.3 from approved local global-package cache |
| PostgreSQL adapters | PASS: implementation/build/runtime-resolution |
| API/Worker registration | PASS |
| Migrations 0001-0013 | PASS |
| Application roles | PASS |
| API/Worker/Web basic smoke | PASS |
| Complete quickstart | PASS |
| PostgreSQL race/crash E2E | PASS: T226-T229 executable PostgreSQL coverage |
| SC-001/SC-002 | BLOCKED / NOT_EXECUTED |
| T245 company evidence | BLOCKED_BY_COMPANY_APPROVAL |
| Release-ready | NO |

## Tool and package evidence

- `C:\Program Files\PostgreSQL\18\bin\pg_isready.exe`: exists and executed; approved target
  reported accepting connections.
- `C:\Program Files\PostgreSQL\18\bin\psql.exe`: exists and authenticated successfully.
- No command used the prohibited port 5432.
- Npgsql version: `10.0.3`.
- Local source: `%USERPROFILE%\.nuget\packages\npgsql\10.0.3`.
- `lib/net10.0/Npgsql.dll`: present.
- Local nupkg SHA-256:
  `75D0970923A8C9FCBBD37E4EBE72FEE0B10362A1E36723E86777DF1B6728316D`.
- NuGet configured source count: 0.
- Locked offline restore: exit 0; download count 0.

## Implemented runtime

- Strict, redacted local PostgreSQL configuration with `.env.local`/`.env` whitelist loading,
  exact target validation, runtime `iump_app`, pooling, and fail-closed readiness.
- Host PostgreSQL transaction context.
- IAM, Catalog, Organization, Acquisition configuration, Run/attempt, Telemetry ingestion,
  Latest/Health, Operations jobs, Integration idempotency/outbox/inbox, and Audit adapters.
- Module-owned registration descriptors and composition project.
- API endpoint registration, real readiness, server principal, login/session wiring.
- Worker PostgreSQL runtime and delivery/job registrations.
- Local bootstrap/runtime verifier.
- Exact-path PowerShell CLI runtime loader used by migration, seed, and Full harness scripts.

## Migration and role evidence

- Clean 0001-0013: PASS, exit 0.
- N-1 0001-0012 plus 0013: PASS, exit 0.
- `iump_dev` migration level 13: PASS.
- `0013` reconciliation rerun as `iump_migration`: PASS, exit 0.
- `iump_migration`, `iump_app`, and `iump_readonly` authentication: PASS.
- `iump_app` schema CREATE privilege count: 0.
- `iump_app` required DML grants: 124.
- `iump_readonly` non-SELECT grants: 0.
- API and Worker runtime role: `iump_app`.
- Secrets were generated/stored only in ignored or external user-local state and are not recorded
  here.

## Runtime verification

- Bootstrap verifier: Administrator PASS, root Site PASS, post-Site fixture PASS, Engineer scope
  PASS.
- PostgreSQL adapter verifier: scenarios 13, assertions 28, failures 0, exit 0.
- API startup: PASS.
- Worker startup: PASS.
- Web startup: PASS.
- Readiness: PASS.
- Real API login and `/api/v1/me`: PASS.
- Real Web login: PASS.
- Authenticated HTTP command mutation: HTTP 201.
- Complete PostgreSQL functional journey: PASS; Site/scope through Simulator, Accepted
  Measurement, Latest, Health, Audit, and Web data display.
- PostgreSQL recovery/race runner: 6 scenarios, 0 failures, exit 0.
- PostgreSQL integration runner: 4 suites, 0 failures, exit 0.
- T219: PARTIAL. The runner covers repository-level command identity/replay/lease/atomicity,
  but does not yet execute the complete HTTP executor path and both required crash windows.
- T220: PARTIAL. The runner covers Audit and inbox persistence behavior, but not the complete
  owner-to-outbox-to-dispatcher-to-inbox delivery with both crash windows and retry exhaustion.
- T236: PARTIAL. Mapping and Latest races plus the recovery probe pass, but the complete
  T226-T229 configuration-race and health/restart matrix has not run.
- API idempotency smoke: original HTTP 201, byte-exact replay, conflict HTTP 409.
- Debug build/unit: exit 0 / 0.
- Release build/unit: exit 0 / 0.
- Web lint/build: exit 0 / 0; three non-failing fast-refresh warnings.
- Architecture: exit 0.
- Repository policy: exit 0.
- Observability: 12 checks, 0 failures, exit 0.
- Fast harness: PASS=8, exit 0.
- Full harness: PASS=11, BLOCKED_BY_COMPANY_APPROVAL=2, exit 20.
- Full database, backend, and frontend checks: PASS.
- Full remains non-passing because company CI and deployment approval are mandatory blockers.

## Reopened task ledger

| Tasks | State | Evidence |
|---|---|---|
| T029,T030,T050,T051,T072,T073,T089,T125,T126,T146,T147,T164,T165,T192,T193,T202,T205 | PASS | adapters/registrations build and resolve |
| T031,T052,T074,T090,T104,T127,T148,T166,T206 | NOT_RUN / runnable | broad PostgreSQL runtime coverage passes, but the exhaustive task-specific suites remain distinct and were not falsely promoted |
| T219,T220 | NOT_RUN / runnable | executable runner passes its covered cases, but exact task coverage remains incomplete |
| T218 | BLOCKED_BY_PACKAGE_POLICY | approved frontend behavior runner remains absent |
| T233 | EXECUTION PASS / task unchecked | clean/N-1/forward/reconciliation passed, but declared T031/T052/T074/T090/T127/T148/T166/T206 dependencies remain incomplete |
| T234 | PASS | API/Worker/Web and complete accepted PostgreSQL quickstart |
| T235 | BLOCKED_BY_COMPANY_APPROVAL | T034 blocked; no timing started |
| T236 | NOT_RUN / runnable | partial PostgreSQL race/recovery evidence does not execute the complete T226-T229 matrix |
| T242 | NOT_RUN / dependency incomplete | Full database check passes, but T236 is not complete |
| T243 | PASS | locked local package restore/build |
| T244 | PASS | required local tools and basic host smoke |
| T245 | BLOCKED_BY_COMPANY_APPROVAL | no approved company lane |

## Exact changed files

The closure changes the following repository paths:

```text
Directory.Build.props
Directory.Packages.props
IUMP.slnx
docs/blocker-report.md
scripts/build.ps1
scripts/common/PostgresRuntime.ps1
scripts/db-migrate.ps1
scripts/db-seed.ps1
scripts/harness.ps1
scripts/start-api.ps1
scripts/start-worker.ps1
specs/002-asset-simulator-latest/tasks.md
specs/002-asset-simulator-latest/checklists/migrations-full.md
specs/002-asset-simulator-latest/checklists/phase-01-iam.md
specs/002-asset-simulator-latest/checklists/phase-02-catalog.md
specs/002-asset-simulator-latest/checklists/phase-03-organization.md
specs/002-asset-simulator-latest/checklists/phase-04-configuration.md
specs/002-asset-simulator-latest/checklists/phase-05-postgresql.md
specs/002-asset-simulator-latest/checklists/phase-06-simulator.md
specs/002-asset-simulator-latest/checklists/phase-07-telemetry.md
specs/002-asset-simulator-latest/checklists/phase-08-latest-health.md
specs/002-asset-simulator-latest/checklists/phase-09-api-audit-web.md
specs/002-asset-simulator-latest/checklists/phase-10-acceptance.md
specs/002-asset-simulator-latest/checklists/phase-10-full.md
specs/002-asset-simulator-latest/checklists/quickstart-evidence.md
specs/002-asset-simulator-latest/checklists/runtime-blocker-resolution.md
specs/002-asset-simulator-latest/checklists/sc-001-sc-002-timed-journeys.md
src/Api/AuthEndpoints.cs
src/Api/AuthSecurityOptions.cs
src/Api/IUMP.Api.csproj
src/Api/Infrastructure/ApplicationPorts.cs (moved)
src/Api/Infrastructure/HttpServerPrincipalAccessor.cs
src/Api/Infrastructure/IdempotentCommandExecutor.cs
src/Api/Program.cs
src/Api/TelemetryQueryEndpoints.cs
src/Api/packages.lock.json
src/BuildingBlocks/packages.lock.json
src/Composition/Postgres/IUMP.Composition.Postgres.csproj
src/Composition/Postgres/PostgresApplicationPorts.cs
src/Composition/Postgres/PostgresModuleRegistration.cs
src/Composition/Postgres/packages.lock.json
src/Hosting/Abstractions/ApplicationPorts.cs
src/Hosting/Abstractions/IUMP.Hosting.Abstractions.csproj
src/Hosting/Abstractions/packages.lock.json
src/Infrastructure/Postgres/IUMP.Infrastructure.Postgres.csproj
src/Infrastructure/Postgres/PostgresRuntimeConfiguration.cs
src/Infrastructure/Postgres/PostgresServiceBinding.cs
src/Infrastructure/Postgres/packages.lock.json
src/Modules/Acquisition/Contracts/PostgresServices.cs
src/Modules/Acquisition/Infrastructure/PostgresConfigurationRepository.cs
src/Modules/Acquisition/Infrastructure/PostgresRunRepositories.cs
src/Modules/Acquisition/IUMP.Modules.Acquisition.csproj
src/Modules/Acquisition/packages.lock.json
src/Modules/Alerts/packages.lock.json
src/Modules/Audit/Contracts/PostgresServices.cs
src/Modules/Audit/Infrastructure/PostgresAuditRepositories.cs
src/Modules/Audit/IUMP.Modules.Audit.csproj
src/Modules/Audit/packages.lock.json
src/Modules/Catalog/Contracts/CatalogRuntimeGateway.cs
src/Modules/Catalog/Contracts/PostgresServices.cs
src/Modules/Catalog/Infrastructure/PostgresCatalogRepositories.cs
src/Modules/Catalog/IUMP.Modules.Catalog.csproj
src/Modules/Catalog/packages.lock.json
src/Modules/Files/packages.lock.json
src/Modules/IAM/Contracts/PostgresServices.cs
src/Modules/IAM/Infrastructure/PostgresIamRepositories.cs
src/Modules/IAM/IUMP.Modules.IAM.csproj
src/Modules/IAM/packages.lock.json
src/Modules/Integration/Contracts/PostgresServices.cs
src/Modules/Integration/Infrastructure/PostgresIntegrationRepositories.cs
src/Modules/Integration/IUMP.Modules.Integration.csproj
src/Modules/Integration/packages.lock.json
src/Modules/Notifications/packages.lock.json
src/Modules/Operations/Contracts/PostgresServices.cs
src/Modules/Operations/Infrastructure/PostgresJobRepositories.cs
src/Modules/Operations/IUMP.Modules.Operations.csproj
src/Modules/Operations/packages.lock.json
src/Modules/Organization/Contracts/PostgresServices.cs
src/Modules/Organization/Infrastructure/PostgresOrganizationRepositories.cs
src/Modules/Organization/IUMP.Modules.Organization.csproj
src/Modules/Organization/packages.lock.json
src/Modules/Reporting/packages.lock.json
src/Modules/Rules/packages.lock.json
src/Modules/Telemetry/Contracts/PostgresServices.cs
src/Modules/Telemetry/Infrastructure/PostgresTelemetryRepositories.cs
src/Modules/Telemetry/IUMP.Modules.Telemetry.csproj
src/Modules/Telemetry/packages.lock.json
src/Web/vite.config.ts
src/Worker/IUMP.Worker.csproj
src/Worker/Integration/RequiredConsumerRegistry.cs
src/Worker/PostgresRuntimeWorker.cs
src/Worker/Program.cs
src/Worker/packages.lock.json
tests/Unit/Telemetry/Phase7ReviewCheck.cs
tests/Unit/packages.lock.json
tests/Verification/architecture.tests.ps1
tests/Verification/observability.tests.ps1
tests/Verification/repository-policy.tests.ps1
tools/IumpLocalRuntime/IumpLocalRuntime.csproj
tools/IumpLocalRuntime/Program.cs
tools/IumpLocalRuntime/packages.lock.json
```

Final functional/recovery convergence additionally changed these exact paths:

```text
src/Api/ConfigurationEndpoints.cs
src/BuildingBlocks/Persistence/HostTransactionCoordinator.cs
src/BuildingBlocks/Persistence/IHostTransaction.cs
src/Composition/Postgres/PostgresRuntimeProviders.cs
src/Modules/Acquisition/Contracts/ConfigurationPersistenceContracts.cs
src/Modules/IAM/Contracts/IdentityRuntimeGateway.cs
src/Modules/Telemetry/Application/TelemetryPersistenceService.cs
src/Web/src/gateways/webGateways.ts
tests/Unit/Api/ConfigurationEndpointTests.cs
tests/Unit/Fakes/FakeAcquisitionConfigurationRepository.cs
tests/Unit/Fakes/FakePhase9Ports.cs
```

Checkpoint/report files receive append-only resolution addenda. `.env`, `.env.local`, and the
local connection-information Markdown file remain ignored and untracked.

## Final decision

Database/package/CLI/adapter/registration/migration/runtime-smoke and complete quickstart blockers
are resolved. The required T226-T229 PostgreSQL E2E coverage passes. The closure is not
release-ready because the remaining task-specific PostgreSQL suites,
approved Data Protection provisioning, the frontend behavior runner, and company approval remain
incomplete or blocked.

## 2026-07-30 exact leaf-suite closure addendum

This addendum supersedes the earlier `PARTIAL`/`NOT_RUN` states for the now-executed runnable
PostgreSQL tasks.

- PostgreSQL integration runner: **13 suites, zero failures, exit 0**.
- Functional runner with Point-activation/configuration and Start/Mapping races:
  **PASS, exit 0**.
- Recovery runner: **6 scenarios, zero failures, exit 0**.
- T031, T052, T074, T090, T104, T127, T148, T166, T206, T219, T220, T233, and
  T236: **PASS**.
- Fixed defects found by the executable leaf suites:
  - IAM/Catalog/Organization/Configuration local transactions now retain their transaction context
    across asynchronous calls, so rollback no longer publishes staged rows.
  - Operations completion replay is idempotent.
  - outbox payload mapping accepts owner-event envelopes that do not carry the internal document
    hash field.
- Exact new evidence paths:
  `migration-0002.md`, `migration-0003.md`, `migration-0004.md`,
  `migrations-0005-0006.md`, `migration-0007.md`, `migration-0008.md`,
  `migration-0009.md`, and `migrations-0010-0011.md`.
- T218 remains `BLOCKED_BY_PACKAGE_POLICY`.
- T034, T235, and T245 remain `BLOCKED_BY_COMPANY_APPROVAL`.
- Release-ready remains **NO**.
- Secret emitted: **NO**. Port 5432 contacted: **NO**.

### Exact changed-file reconciliation

The earlier changed-file inventory plus the following paths is the exact closure working-tree
inventory. These paths were omitted from the earlier inventory and are recorded here without
rewriting historical evidence:

```text
specs/002-asset-simulator-latest/checklists/migration-0002.md
specs/002-asset-simulator-latest/checklists/migration-0003.md
specs/002-asset-simulator-latest/checklists/migration-0004.md
specs/002-asset-simulator-latest/checklists/migration-0007.md
specs/002-asset-simulator-latest/checklists/migration-0008.md
specs/002-asset-simulator-latest/checklists/migration-0009.md
specs/002-asset-simulator-latest/checklists/migrations-0005-0006.md
specs/002-asset-simulator-latest/checklists/migrations-0010-0011.md
src/Api/SimulatorEndpoints.cs
src/Modules/Acquisition/Application/RunCommands.cs
src/Modules/Acquisition/Application/SimulatorConfiguration.cs
src/Modules/Acquisition/Contracts/RunPersistenceContracts.cs
src/Modules/Catalog/Domain/MetricUnitModel.cs
src/Modules/Catalog/Domain/SourceMappingModel.cs
src/Modules/IAM/Application/SessionManager.cs
src/Modules/IAM/Contracts/IamSessionContracts.cs
src/Modules/Organization/Application/ActivateMeasurementPoint.cs
src/Modules/Organization/Application/HierarchyCommands.cs
src/Modules/Organization/Contracts/OrganizationPersistenceContracts.cs
src/Modules/Organization/Contracts/OrganizationQueryContracts.cs
src/Modules/Organization/Contracts/OrganizationRuntimeGateway.cs
src/Worker/Integration/OutboxDispatcherWorker.cs
tests/Integration/Acceptance/AcceptancePostgresTests.cs
tests/Integration/Acquisition/ConfigurationRepositoryTests.cs
tests/Integration/Audit/AuditDeliveryTests.cs
tests/Integration/Integration/CommandIdempotencyApiTests.cs
tests/Integration/IUMP.Tests.Integration.csproj
tests/Integration/Operations/OperationsJobRepositoryTests.cs
tests/Integration/packages.lock.json
tests/Integration/Program.cs
tests/Integration/Runtime/PostgresRuntimeLeafTests.cs
tests/Unit/Acquisition/ProductionAttemptTests.cs
tests/Unit/Api/SimulatorEndpointTests.cs
tests/Unit/Fakes/FakeOperationsJobRepositoryTestProviderFactory.cs
tests/Unit/Organization/HierarchyCommandTests.cs
tests/Unit/Worker/ProductionDispatchTests.cs
```

## 2026-07-30 final Full checkpoint addendum

- T242: **PASS** for the approved PostgreSQL database check.
- Fresh Full numeric exit: **20**.
- Fresh Full summary: **PASS=11**, **BLOCKED_BY_COMPANY_APPROVAL=2**, **FAIL=0**.
- Fast: **PASS=8**, exit **0**.
- Architecture, repository policy, and observability: **PASS**, exit **0**.
- Remaining mandatory blockers: T034/T235/T245 company approval and T218 approved frontend
  behavior-runner package policy.
- Release-ready: **NO**.
