# Phase 4 Configuration Checkpoint

## 1. Parent baseline

- Repository: `devphuclam/EnergySaving`
- Feature: `specs/002-asset-simulator-latest/`
- Parent baseline HEAD: `7d7069cd8e9e6e6dfdd0feb42cb47b5a730bc402`
- Constitution: `1.1.0`
- Accepted predecessor: T077 / Phase 3

## 2. Result-commit identity semantics

No commit was created by this invocation. The result is the working-tree diff relative to the parent baseline above; the baseline SHA remains the provenance anchor.

## 3. Exact changed files

- `database/migrations/0005_acquisition_configuration.sql`
- `database/migrations/0006_catalog_source_mapping.sql`
- `src/Modules/Acquisition/Application/SimulatorConfiguration.cs`
- `src/Modules/Acquisition/Contracts/ConfigurationPersistenceContracts.cs`
- `src/Modules/Acquisition/IUMP.Modules.Acquisition.csproj`
- `src/Modules/Catalog/Application/OrganizationPointReadinessAdapter.cs`
- `src/Modules/Catalog/Contracts/CatalogEligibilityContracts.cs`
- `src/Modules/Catalog/IUMP.Modules.Catalog.csproj`
- `tests/Integration/Acquisition/ConfigurationRepositoryTests.cs`
- `tests/Unit/Acquisition/ConfigurationCommandTests.cs`
- `tests/Unit/Acquisition/ConfigurationTests.cs`
- `tests/Unit/Catalog/MappingReadinessTests.cs`
- `tests/Unit/Fakes/FakeAcquisitionConfigurationRepository.cs`
- `tests/Unit/IUMP.Tests.Unit.csproj`
- `tests/Unit/Program.cs`
- `tests/Verification/architecture.tests.ps1`
- `specs/002-asset-simulator-latest/checklists/phase-04-red.md`
- `specs/002-asset-simulator-latest/checklists/phase-04-review.md`
- `specs/002-asset-simulator-latest/checklists/phase-04-configuration.md`
- `specs/002-asset-simulator-latest/tasks.md`

No prohibited file was changed.

## 4. RED commands/exits/failures

See `phase-04-red.md`. The focused Debug executable built with exit 0 and then exited 1 on the missing accepted configuration-create behavior: `T079: Administrator can create globally and source identity is resolved server-side.` No syntax, package, restore or project failure caused RED.

## 5. GREEN commands/exits/counts

```text
dotnet build tests/Unit/IUMP.Tests.Unit.csproj --no-restore --configuration Debug: exit 0, 0 warnings, 0 errors
dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj --no-build --configuration Debug: exit 0
T071: tests=19; assertions=39; failures=0
T088: tests=8; assertions=8; failures=0
PASS: all tests

dotnet build tests/Unit/IUMP.Tests.Unit.csproj --no-restore --configuration Release: exit 0, 0 warnings, 0 errors
dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj --no-build --configuration Release: exit 0
T071: tests=19; assertions=39; failures=0
T088: tests=8; assertions=8; failures=0
PASS: all tests
```

## 6. Configuration-domain result

Create persists one head and version 1. Edit appends exactly one immutable next version, increments aggregate version once, and rejects stale or no-op edits without a new version/event. Interval, finite bounds, scenario, seed, and fixed algorithm constraints are enforced.

## 7. Immutability result

The provider-neutral port has exact lookup/list/create/append/transaction operations and no version update/delete. The deterministic fake deep-copies snapshots, enforces one head per Source, monotonic versions, optimistic aggregate version, stable ordering and deep rollback.

## 8. Authorization result

Administrator is global. Engineer requires the server-resolved Catalog Source Site scope. Unscoped/out-of-scope and Operator/Manager/Viewer callers are denied; client SiteId is not authority and out-of-scope failures are non-enumerating.

## 9. Event result

The owner event is exactly `SimulatorConfigurationChanged.v1` with SchemaVersion 1, Producer `IUMP.Acquisition`, actor username snapshot, trusted SiteId, UTC time, distinct correlation/causation and an explicit safe before/after allowlist. It is not claimed as Audit persistence.

## 10. Organization readiness result

The real Catalog adapter consumes only Organization public snapshots, validates Point → Asset → Area → Site ancestry, returns trusted IDs/provider version, and never mutates Organization.

## 11. Draft non-producing Mapping result

Configuration-ready Draft Points are accepted for Mapping activation while `producingReady=false`; Active Points with Active ancestors are producing-ready. Inactive/decommissioned hierarchy and invalid interval cases remain non-producing/not-ready.

## 12. Migration 0005 static result

Reviewed source defines Acquisition head/version tables, positive and finite constraints, Constant/Normal rules, fixed algorithm, indexes, same-schema FK, and append-only trigger. It was not executed.

## 13. Migration 0006 static result

Reviewed source defines Catalog Source/Point mapping, lifecycle, half-open period validation, positive version and indexes. The PostgreSQL `btree_gist` exclusion strategy is documented and execution remains blocked because the extension is not approved; no `CREATE EXTENSION` or migration execution was run.

## 14. T088 contract runner result

Provider/factory runner uses only the public Acquisition repository port and executes against the deterministic fake: 8 tests, 8 assertions, 0 failures. It contains no fake casts, concrete fake references, TODO/Skip, credentials or fallback connection strings.

## 15. Architecture result

`tests/Verification/architecture.tests.ps1` and Fast harness pass. Approved public-contract references are Acquisition → Catalog and Catalog → Organization; no cross-schema FK exists in 0005/0006, no PostgreSQL adapter or Phase 5/6 behavior was added, and API/Worker composition roots are unchanged.

## 16. Standards review

Phase 4 review has 0 unresolved Critical and 0 unresolved High findings; see `phase-04-review.md`.

## 17. Specification review

FR-008, FR-014..016, FR-028, FR-031, FR-037/038, P-004, P-008, P-010, P-011, P-016 applicable portion, P-021, SC-007, SC-008, simulator/catalog contracts, data model and T078–T091 are covered; review result is PASS.

## 18. T078–T093 task ledger

| Task | Result |
|---|---|
| T078–T088 | PASS |
| T089 | BLOCKED_BY_PACKAGE_POLICY (unchecked; no adapter/packages) |
| T090 | BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE (unchecked; T089 and prior PostgreSQL adapters incomplete) |
| T091–T093 | PASS |

## 19. Counts

- PASS: 14
- BLOCKED: 2
- FAIL: 0
- Runnable NOT_RUN: 0

Blocked tasks are not counted as PASS.

## 20. Database capability

PostgreSQL 18 is AVAILABLE / VERIFIED at `127.0.0.1:5433/iump_dev` through the approved ignored local `.env`. No mutation was run, migrations 0005/0006 were not executed, `psql` was not run, port 5432 was not contacted, and the database-access blocker count is 0.

## 21. Package blockers

No package was installed, downloaded, restored from a public source or added. T089 remains `BLOCKED_BY_PACKAGE_POLICY`; T090 is `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE`, not a database-access blocker.

## 22. Phase 5 progression decision

YES. All runnable Phase 4 tasks pass.

## 23. Release readiness

NO. This checkpoint is not release approval.

## 24. Demo-readiness statement

The configuration backend capability exists. The Simulator does not run yet; no Telemetry exists; no Web demo exists. Configuration-only Demo 0.1 becomes feasible after Phase 5. A live monitoring demo requires Phases 6–8 plus a thin API/Web slice.

## 25. Explicit stop

Stop after T093. Do not execute T094+, Point activation orchestration, Simulator Run controls, Worker production, Telemetry ingestion, API/Web endpoints, or Phase 5 files in this invocation.

## Final verification evidence

- Fresh Debug and Release build/run: PASS (0 warnings/errors; T071 19/39; T088 8/8; 0 failures).
- Fresh architecture, verification-contract, repository-harness, repository-policy, repository-scope and `git diff --check`: PASS.
- SQL static checks for 0005/0006, changed-file scope review, `.env` ignore/tracking check and prohibited-port scan: PASS; no secret values were printed.
- Fresh Full harness: mandatory repository/build/unit checks PASS; environment checks are BLOCKED by missing `psql` (`BLK-ENV-002`) and company approval for CI/container targets (`BLK-ENV-003`, `BLK-ENV-004`). These are environment blockers, not Phase 4 task failures, and are not counted as runnable Phase 4 PASS.
