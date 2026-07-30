# Phase 3 checkpoint: Organization hierarchy (T077)

**Scope:** T056-T077 only (US1, US4, US5). T078 and later tasks were not
executed. Migration `0004` was not executed.

## Exact provenance

| Item | Evidence |
|---|---|---|
| Parent baseline | `8f6ee4dd9471d6d3ed8eb9836b6e0a5644a0a058` |
| Current HEAD (`git rev-parse HEAD`) | `8f6ee4dd9471d6d3ed8eb9836b6e0a5644a0a058` |
| Worktree | Intentionally dirty with only the exact files listed below; no unrelated changes observed. |
| Chronological RED parent baseline | `8f6ee4dd9471d6d3ed8eb9836b6e0a5644a0a058` (same as current; micro-RED added then GREEN implemented in sequence) |
| RED build/run | `dotnet build ... --no-restore -c Debug` exit **0**; `dotnet run ... --no-build -c Debug` exit **1** with seven state-guard and lifecycle-history assertions (see updated `phase-03-red.md`). |
| Green Debug | `dotnet build tests/Unit/IUMP.Tests.Unit.csproj --no-restore -c Debug` exit **0**; run exit **0**, 0 warnings/0 errors, PASS=all tests. |
| Green Release | Same build/run commands with `-c Release`, both exit **0**, 0 warnings/0 errors, PASS=all tests. |
| Focused Organization suites | 5 suites pass within the unit executable. |
| T071 provider-neutral runner | **19 tests, 39 assertions, 0 failures**, emitted by the executable. |
| Architecture | `tests/Verification/architecture.tests.ps1` exit **0**. |
| Fast harness | Not re-executed (unchanged; previous evidence stands). |
| Full harness | Not re-executed (no package/database change; previous evidence stands). |
| Diff hygiene | `git diff --check` exit **0** (CRLF warnings are cosmetic autocrlf). |

## Task ledger and evidence

| Tasks | State | Evidence |
|---|---|---|
| T056-T060 | PASS | Corrected domain, decommission, command/event, query, and IAM fixture business suites pass; post-hoc business RED is recorded separately. |
| T061 | PASS | Corrected post-hoc RED at `fd2cf0d858fc8fce0041e1343b64d966d33d5d46`; build 0/run 1; no production fix in the temporary worktree. |
| T062-T063 | PASS | Public command/query contracts compile, including expected-version and trusted ancestry surfaces. |
| T064 | PASS | Deterministic fakes enforce scoped uniqueness/reservation, ancestry, transactions, history, filtering, ordering, and summaries. |
| T065 | PASS | Aggregates enforce immutable identity/code, configuration validation, lifecycle, no-op, and optimistic version rules. |
| T066 | PASS | Decommission policy is non-cascading, blocks active children/running Simulator, and preserves terminal-state behavior. |
| T067 | PASS | Authorized explicit updates/status/decommission commands enforce scope, ExpectedVersion, parent status, Phase 5 Point activation guard, actor username, exact events, and trusted ancestry. |
| T068 | PASS | Query service uses IAM scope, filter-before-paging/totals, deterministic order, child summaries, and trusted ancestry beyond 200 Areas. |
| T069 | PASS | IAM post-Site fixture remains on public Organization query contracts and is idempotent. |
| T070 | PASS | Migration SQL remains statically reviewed; execution intentionally not run. |
| T071 | PASS | Provider-neutral runner executes 19 tests/39 assertions without fake casts or adapter-specific dependencies. |
| T072 | BLOCKED_BY_PACKAGE_POLICY | PostgreSQL adapter package/project surface is not approved; task remains unchecked. |
| T073 | BLOCKED_BY_PACKAGE_POLICY | Host registration depends on T072; task remains unchecked. |
| T074 | BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE | Migration execution depends on T072/T073; task remains unchecked and `0004` was not run. |
| T075 | PASS | Architecture boundary checks pass, including required Simulator dependency, ExpectedVersion command surface, event families, parent guards, and ancestry query. |
| T076 | PASS | Standards/Specification review enumerates CORR-A-O (A-K from earlier handoff, L-O from micro-closure) and has zero unresolved Critical/High findings. |
| T077 | PASS | This checkpoint records exact provenance, command exits, counts, capabilities, blockers, and stop decision. |

## Result counts

| Category | Count |
|---|---:|
| Runnable PASS | **19** (T056-T071, T075-T077) |
| FAIL | **0** |
| BLOCKED_BY_PACKAGE_POLICY | **2** (T072, T073) |
| BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE | **1** (T074) |
| BLOCKED_BY_DATABASE_ACCESS | **0** |
| BLOCKED_BY_MISSING_TOOL | **0** for the runnable task ledger |
| BLOCKED_BY_COMPANY_APPROVAL | **0** for the runnable task ledger |
| Runnable NOT_RUN | **0** |

## Database capability and restrictions

| Item | State |
|---|---|
| Approved runtime target | PostgreSQL 18 at `127.0.0.1:5433/iump_dev` |
| Credential source | Existing repository-local `.env`; value never printed or recorded |
| Connectivity capability | **AVAILABLE / VERIFIED** (authoritative local capability update) |
| Database mutation | **NOT_RUN**; no migration or `psql` command executed |
| Port 5432 | **NOT CONTACTED** |
| SQLite/InMemory/Docker/package install | **NOT USED** |

The Full harness's missing `psql` executable is an environment/tool check, not
a reclassification of the verified database capability and not a T074 database
access blocker.

## Exact changed files in this invocation

```text
specs/002-asset-simulator-latest/checklists/phase-03-red.md
specs/002-asset-simulator-latest/checklists/phase-03-review.md
specs/002-asset-simulator-latest/checklists/phase-03-organization.md
src/Modules/Organization/Application/HierarchyCommands.cs
tests/Integration/Organization/OrganizationRepositoryTests.cs
tests/Unit/Organization/DecommissionTests.cs
tests/Unit/Organization/HierarchyCommandTests.cs
```

## Progression and release decision

**Phase 4 progression: YES.** T077 is complete; T078+ remain the next governed
work and were not executed here.

**Release-ready: NO.** T072/T073 are package-policy blocked and T074 is
transitively blocked, so no PostgreSQL adapter or migration execution evidence
exists.

**Explicit stop:** stop after T077. Do not execute T078 or any later Phase 4
task in this invocation, and do not execute migration `0004`.

**Result-commit identity**: The working tree is intentionally dirty and has not
been committed. A commit SHA must be resolved externally (after `git add` and
`git commit`) and recorded as the final result-commit identity for this Phase 3
micro-closure. All pre-commit verification evidence (build, run, architecture,
diff hygiene) has been captured above and in the updated `phase-03-red.md` and
`phase-03-review.md`.

## 2026-07-30 runtime-resolution addendum

T072 and T073 are now PASS with the approved local Npgsql package, Organization adapter, host
registration, build, and runtime resolution. T074 is `RUNNABLE_NOW` but remains unchecked because
its complete lock/concurrent-decommission/rollback+outbox PostgreSQL suite was not executed.
Historical evidence above remains unchanged.
