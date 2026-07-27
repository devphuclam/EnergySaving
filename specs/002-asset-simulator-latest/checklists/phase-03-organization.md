# Phase 3 checkpoint: Organization hierarchy

**Stories**: US1, US4, US5
**Verification command**: `dotnet build .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore; dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-build`

## Result counts

| Category | Count |
|----------|-------|
| PASS     | 16    |
| FAIL     | 0     |
| BLOCKED_BY_PACKAGE_POLICY | 2 (T072, T073) |
| BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE | 0 |
| BLOCKED_BY_DATABASE_ACCESS | 1 (T074) |
| BLOCKED_BY_MISSING_TOOL | 0 |
| BLOCKED_BY_COMPANY_APPROVAL | 0 |
| NOT_RUN  | 0     |

**Runnable tasks**: all 16 pass (T056–T071, T075–T076)
**Blocked tasks**: T072 (PostgreSQL adapters), T073 (host registration), T074 (migration execution)

## Artifacts produced

- `tests/Unit/Organization/HierarchyDomainTests.cs` — domain lifecycle/code/interval tests
- `tests/Unit/Organization/DecommissionTests.cs` — decommission/no-cascade/terminal tests
- `tests/Unit/Organization/HierarchyCommandTests.cs` — authorized command/event tests
- `tests/Unit/Organization/HierarchyQueryTests.cs` — scope-filtered query tests
- `tests/Unit/Organization/PostSiteFixtureTests.cs` — post-Site fixture wiring tests
- `tests/Unit/Fakes/FakeOrganizationRepositories.cs` — deterministic command/transaction fakes
- `src/Modules/Organization/Domain/Hierarchy.cs` — Site/Area/Asset/MeasurementPoint aggregates
- `src/Modules/Organization/Contracts/OrganizationPersistenceContracts.cs` — command repository port
- `src/Modules/Organization/Contracts/OrganizationQueryContracts.cs` — query/scope port
- `src/Modules/Organization/Application/HierarchyCommands.cs` — authorized commands with events
- `src/Modules/Organization/Application/HierarchyQueries.cs` — scope-filtered query service
- `src/Modules/IAM/Application/PostSiteFixtureOrganizationAdapter.cs` — IAM→Organization adapter
- `database/migrations/0004_organization_hierarchy.sql` — hierarchy SQL migration
- `tests/Integration/Organization/OrganizationRepositoryTests.cs` — contract-runner test source
- `tests/Verification/architecture.tests.ps1` — extended with Organization schema/naming checks
- `specs/002-asset-simulator-latest/checklists/phase-03-red.md` — RED evidence
- `specs/002-asset-simulator-latest/checklists/phase-03-review.md` — Standards/Spec review

## Capability status

| Capability | Status |
|------------|--------|
| Site/Area/Asset/Point lifecycle | PASS |
| Code uniqueness (global site, scoped area/asset/site-point) | PASS |
| Top-down activation (Site→Area→Asset→Point) | PASS |
| Decommission with no-cascade constraint | PASS |
| Running Simulator blocks Point decommission | PASS |
| Authorization: Administrator global, Engineer scoped, others denied | PASS |
| Scope-filtered queries | PASS |
| Post-Site fixture wiring | PASS |
| PostgreSQL adapters | BLOCKED_BY_PACKAGE_POLICY |
| Migration execution | BLOCKED_BY_DATABASE_ACCESS |

## Progression decision

**YES** — all runnable tasks pass, blockers are external/classified, no runnable dependent needs blocked behavior.

## Release blockers

- T072/T073: package sources needed for Npgsql/EF adapters
- T074: package + database access needed for migration execution
