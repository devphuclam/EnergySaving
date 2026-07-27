# Phase 3 checkpoint: Organization hierarchy (T077)

**Stories**: US1, US4, US5. **Checkpoint scope**: T056–T077 only. T078 and later
tasks remain unchecked and no Phase 4 implementation was performed.

## Task ledger and evidence

| Tasks | State | Evidence |
|---|---|---|
| T056–T060 | PASS | Corrected Organization domain, decommission, command, query, and Post-Site fixture tests; focused Debug/Release runs exit 0. |
| T061 | PASS | Post-hoc RED evidence at `fd2cf0d858fc8fce0041e1343b64d966d33d5d46`; build exit 0, focused exit 1, no production fixes in temporary worktree. |
| T062–T063 | PASS | Public Organization command/query contracts compile and expose trusted ancestry, scope, immutable snapshots, paging, totals, and version surfaces. |
| T064 | PASS | Deterministic command/query fakes enforce ancestry, uniqueness, reservation, explicit history, scope filtering, stable order, and summaries. |
| T065 | PASS | Hierarchy aggregates retain lifecycle, code, interval, terminal, and optimistic-version rules. |
| T066 | PASS | `DecommissionPolicy.cs` evaluates active child Points and `IRunningSimulatorQuery` dependency without cascade. |
| T067 | PASS | Authorization precedes target details, trusted ancestry is enforced, Point activate/reactivate are Phase 5-only, actor username and exact owner events are covered. |
| T068 | PASS | Query application service enforces IAM Site/Area scope, filter-before-paging/totals, child summaries, deterministic order, and out-of-scope NotFound. |
| T069 | PASS | IAM adapter uses public Organization query plus the existing idempotent IAM fixture; real role scopes/capability rows are asserted. |
| T070 | PASS | Migration static review repaired status domains, ancestry composite FKs, lifecycle history UUID/FK/version/immutability/index rules. Execution was not run. |
| T071 | PASS | Provider-neutral Organization contract runner compiles and executes without concrete fake casts. |
| T072 | BLOCKED_BY_PACKAGE_POLICY | PostgreSQL adapter package/project surface is not approved; task remains unchecked. |
| T073 | BLOCKED_BY_PACKAGE_POLICY | Host registration depends on T072; task remains unchecked. |
| T074 | BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE | Migration execution depends on T072/T073. PostgreSQL capability is available, but 0004 execution was intentionally not run. |
| T075 | PASS | Architecture boundary and Organization ownership checks pass. |
| T076 | PASS | Separate Standards/Specification review has zero unresolved Critical/High findings. |
| T077 | PASS | This corrected checkpoint records counts, capability state, exact scope, and stop decision. |

## Result counts

| Category | Count |
|---|---:|
| Runnable PASS | **19** (T056–T071, T075–T077) |
| FAIL | **0** |
| BLOCKED_BY_PACKAGE_POLICY | **2** (T072, T073) |
| BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE | **1** (T074) |
| BLOCKED_BY_DATABASE_ACCESS | **0** |
| BLOCKED_BY_MISSING_TOOL | **0** |
| BLOCKED_BY_COMPANY_APPROVAL | **0** |
| Runnable NOT_RUN | **0** |

## Database capability evidence

| Item | State |
|---|---|
| Approved target | `127.0.0.1:5433/iump_dev` (PostgreSQL 18) |
| Credential source | Existing repository-local `.env`; verified without printing or recording its value |
| Connectivity capability | **AVAILABLE / VERIFIED** |
| Migration mutation in this invocation | **NOT_RUN** (T074 is transitively package-policy blocked) |
| Port 5432 | **NOT CONTACTED** |
| SQLite/InMemory/Docker/package install | **NOT USED** |

## Exact changed files

```text
database/migrations/0004_organization_hierarchy.sql
src/Modules/IAM/Application/PostSiteFixtureOrganizationAdapter.cs
src/Modules/Organization/Application/HierarchyCommands.cs
src/Modules/Organization/Application/HierarchyQueries.cs
src/Modules/Organization/Contracts/OrganizationPersistenceContracts.cs
src/Modules/Organization/Contracts/OrganizationQueryContracts.cs
src/Modules/Organization/Domain/DecommissionPolicy.cs
src/Modules/Organization/Domain/Hierarchy.cs
tests/Integration/Organization/OrganizationRepositoryTests.cs
tests/Unit/Fakes/FakeIamRepositories.cs
tests/Unit/Fakes/FakeOrganizationRepositories.cs
tests/Unit/Organization/DecommissionTests.cs
tests/Unit/Organization/HierarchyDomainTests.cs
tests/Unit/Organization/HierarchyCommandTests.cs
tests/Unit/Organization/HierarchyQueryTests.cs
tests/Unit/Organization/PostSiteFixtureTests.cs
tests/Unit/Program.cs
tests/Verification/architecture.tests.ps1
specs/002-asset-simulator-latest/checklists/phase-03-red.md
specs/002-asset-simulator-latest/checklists/phase-03-review.md
specs/002-asset-simulator-latest/checklists/phase-03-organization.md
specs/002-asset-simulator-latest/tasks.md
```

## Progression and release decision

**Phase 4 progression: YES** — T077 is complete; T078+ remain the next governed
work and were not executed here.

**Release-ready: NO** — T072/T073 are package-policy blocked and T074 is
transitively blocked, so no PostgreSQL adapter/migration execution evidence exists.

**Explicit stop:** stop after T077. Do not execute T078 or any later Phase 4 task
in this invocation.
