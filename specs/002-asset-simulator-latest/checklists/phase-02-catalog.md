# Phase 2 Catalog checkpoint (T055)

## 1. Provenance and preflight

- Repository: `devphuclam/EnergySaving`
- Parent baseline verified before implementation: `90aed828a673f15307f37e069b1e4b53d70d696f`
- Remote: `https://github.com/devphuclam/EnergySaving.git`
- Constitution: `1.1.0`.
- Phase 1 checkpoint: present (`phase-01-iam.md`).
- The operator-provided `IUMP_Local_Database_Connection_Info.md` is approved environment
  evidence and was not modified or committed as a project artifact.
- T056 and later tasks were not executed; no Phase 3 implementation files were created.
- T055 result is committed in the current HEAD. The exact current HEAD is resolved by
  `git rev-parse HEAD` at handoff; no uncommitted project correction is represented as PASS.

## 2. Task ledger (T038–T055)

| Task | Classification | Evidence status | Evidence | Notes |
|---|---|---|---|---|
| T038 | RUNNABLE_NOW | PASS | `MetricUnitTests.cs`; focused executable exit 0 | Metric/Unit invariants, canonical compatibility, and seed idempotency pass. |
| T039 | RUNNABLE_NOW | PASS | `SourceMappingTests.cs`; focused executable exit 0 | Source/Mapping lifecycle, terminal states, UTC intervals, overlap and dependency deletion pass. |
| T040 | RUNNABLE_NOW | PASS | `CatalogCommandTests.cs`; focused executable exit 0 | Trusted scope, missing Point, readiness activation, correlation/causation, and all owner-event families pass. |
| T041 | RUNNABLE_NOW | PASS | `checklists/phase-02-red.md` | Post-hoc reproduced RED evidence against baseline `908bddbc1eb68cf8fcdbb095a561e2323bb4e6eb`; build exit 0, focused run exit 1 with three exact failures. |
| T042 | RUNNABLE_NOW | PASS | `CatalogPersistenceContracts.cs`; Debug/Release builds exit 0 | Provider-neutral Catalog persistence contract compiles. |
| T043 | RUNNABLE_NOW | PASS | `CatalogEligibilityContracts.cs`; Debug/Release builds exit 0 | Eligibility and Point readiness query contracts compile without persistence leakage. |
| T044 | RUNNABLE_NOW | PASS | `FakeCatalogRepositories.cs`; focused executable exit 0 | Deterministic repository and readiness fakes pass contract coverage. |
| T045 | RUNNABLE_NOW | PASS | `MetricUnitModel.cs`; focused executable exit 0 | Domain invariants and deterministic seeds pass. |
| T046 | RUNNABLE_NOW | PASS | `SourceMappingModel.cs`; focused executable exit 0 | Lifecycle, half-open periods, overlap and deletion decisions pass. |
| T047 | RUNNABLE_NOW | PASS | `CatalogCommands.cs`; focused executable exit 0 | Trusted readiness authorization, transactional mutations, distinct metadata and explicit snapshots pass. |
| T048 | RUNNABLE_NOW | PASS | `database/migrations/0003_catalog_foundation.sql`; static review exit 0 | Reviewed schema remains provider-neutral; no migration was executed. |
| T049 | RUNNABLE_NOW | PASS | `CatalogRepositoryTests.cs`; focused executable exit 0 | Provider-neutral adapter contract runner covers commit/rollback, dependencies and version conflict. |
| T050 | BLOCKED_BY_PACKAGE_POLICY | BLOCKED | Locked PostgreSQL adapter package source is unavailable | No public restore/download or adapter implementation was attempted. |
| T051 | BLOCKED_BY_PACKAGE_POLICY | BLOCKED | Depends on T050 | Host registration and reachability are not claimed. |
| T052 | BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE | BLOCKED | Depends on T050/T051 | Migration execution is not run; this is not a database-access blocker. |
| T053 | RUNNABLE_NOW | PASS | `architecture.tests.ps1`; exit 0 | Catalog ownership and cross-schema write boundary pass. |
| T054 | RUNNABLE_NOW | PASS | `checklists/phase-02-review.md` | Separate Standards/Specification reviews; unresolved Critical/High/Medium/Low counts are all zero. |
| T055 | RUNNABLE_NOW | PASS | This checkpoint and committed HEAD | Counts, capability evidence, blockers and stop decision are recorded. |

## 3. Green verification

| Check | Result |
|---|---|
| Debug unit build `dotnet build .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore -c Debug` | Exit 0; 0 warnings, 0 errors |
| Release unit build `dotnet build .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore -c Release` | Exit 0; 0 warnings, 0 errors |
| Focused Debug executable (`dotnet run ... -c Debug --no-build`) | Exit 0; `PASS: all tests` |
| Focused Release executable (`dotnet run ... -c Release --no-build`) | Exit 0; `PASS: all tests` |
| Fast harness | Exit 0 |
| Architecture, verification-contract, repository-harness, repository-policy, repository-scope checks | Exit 0 |
| Secret scan, prohibited-port source/command scan, `git diff --check`, changed-file scope review | Exit 0; no secret literals; port 5432 not contacted |
| Database verification/mutation | `PASS` (read-only target/version query) / `NOT_RUN` |
| Full harness | Not invoked because the explicit instruction prohibits another DB/`psql` preflight |

## 4. Capability and counts

- PostgreSQL capability: **AVAILABLE** (connection verification PASS via existing local `.env`).
- Engine: PostgreSQL 18; host `127.0.0.1`; port `5433`; database `iump_dev`; credential source
  `IUMP_DB_PASSWORD`; password `REDACTED`.
- Old cluster `127.0.0.1:5432`: **PROHIBITED** and not contacted.
- `BLOCKED_BY_DATABASE_ACCESS` count: **0**.

| PASS | BLOCKED | FAIL | Runnable NOT_RUN |
|---:|---:|---:|---:|
| 15 | 3 | 0 | 0 |

Blocked tasks are exactly T050 (`BLOCKED_BY_PACKAGE_POLICY`), T051
(`BLOCKED_BY_PACKAGE_POLICY`), and T052 (`BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE`).

## 5. Phase decision

- Standards review T054: **PASS**.
- Specification review T054: **PASS**.
- Ready for Phase 3: **YES**, only on a later explicit invocation.
- Release-ready: **NO**; locked package capability and PostgreSQL adapter/migration execution remain
  external blockers.
- Explicit stop: Phase 2 ends at **T055**. Do not execute T056 or later tasks in this invocation.

## 2026-07-30 runtime-resolution addendum

T050 and T051 are now PASS with the approved local Npgsql package, Catalog adapter, host
registration, build, and runtime resolution. T052 is `RUNNABLE_NOW` but remains unchecked because
the complete task-specific overlap/dependency/rollback+outbox PostgreSQL suite was not executed.
Historical evidence above remains unchanged.
