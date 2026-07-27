# Phase 4 Corrective Convergence Checkpoint (T093)

## 1. Parent baseline

- Repository: `devphuclam/EnergySaving`
- Feature: `specs/002-asset-simulator-latest/`
- Parent baseline HEAD: `8331b6f57512d205af6eecac8ffce212e5e364d8`
- Accepted predecessor: T077 / Phase 3 (corrective micro-closure)

## 2. Result-commit identity semantics

No commit was created by this invocation. The result is the working-tree diff
relative to the parent baseline above. A commit SHA must be resolved
externally after `git add` and `git commit`.

## 3. Exact changed files

- `database/migrations/0006_catalog_source_mapping.sql`
- `src/Modules/Catalog/Application/CatalogSourceScopeQueryAdapter.cs`
- `src/Modules/Acquisition/Application/SimulatorConfiguration.cs`
- `tests/Integration/Acquisition/ConfigurationRepositoryTests.cs`
- `tests/Unit/Acquisition/ConfigurationCommandTests.cs`
- `tests/Unit/Catalog/MappingReadinessTests.cs`
- `tests/Unit/Program.cs`
- `tests/Verification/architecture.tests.ps1`
- `specs/002-asset-simulator-latest/checklists/phase-04-red.md`
- `specs/002-asset-simulator-latest/checklists/phase-04-review.md`
- `specs/002-asset-simulator-latest/checklists/phase-04-configuration.md`

No prohibited file was changed.

## 4. RED commands/exits/failures

Chronological RED was captured BEFORE production corrections:

```
dotnet build tests\Unit\IUMP.Tests.Unit.csproj --no-restore -c Debug
exit 0; 0 Warning(s) 0 Error(s)

dotnet run --project tests\Unit\IUMP.Tests.Unit.csproj --no-build -c Debug
T079: assertions=87; failures=4
T080: assertions=62; failures=0
T071: tests=19; assertions=39; failures=0
T088: scenarios=24; assertions=24; failures=0
FAILURES:
  T079: Multi-Site SiteIds are sorted.
  T079: Adapter returns null for readiness with empty SiteId (fail-closed).
  T079: Adapter returns null for readiness with empty AreaId (fail-closed).
  T079: Adapter returns null for readiness with zero PointVersion.
exit code: 1
```

Detailed RED analysis appended to `phase-04-red.md`.

## 5. GREEN commands/exits/counts

```
dotnet build tests\Unit\IUMP.Tests.Unit.csproj --no-restore -c Debug: exit 0, 0 warnings, 0 errors
dotnet run --project tests\Unit\IUMP.Tests.Unit.csproj --no-build -c Debug: exit 0
T079: assertions=87; failures=0
T080: assertions=62; failures=0
T071: tests=19; assertions=39; failures=0
T088: scenarios=24; assertions=24; failures=0
PASS: all tests

dotnet build tests\Unit\IUMP.Tests.Unit.csproj --no-restore -c Release: exit 0, 0 warnings, 0 errors
dotnet run --project tests\Unit\IUMP.Tests.Unit.csproj --no-build -c Release: exit 0
T079: assertions=87; failures=0
T080: assertions=62; failures=0
T071: tests=19; assertions=39; failures=0
T088: scenarios=24; assertions=24; failures=0
PASS: all tests

Architecture: PASS (15 semantic checks)
Harness Full: 10 PASS, 3 BLOCKED (database psql, CI, container)
```

## 6. Deterministic seed result (unchanged)

- Contract model: `ulong` accepting 0 through `UInt64.MaxValue`.
- Event payload: `deterministicSeed` (invariant decimal string),
  `deterministicSeedHex` (lowercase 16-hex).
- Tests: 0, 42, 123456789, `UInt64.MaxValue` accepted; historical immutable.
- Already proven in prior baseline; no change required.

## 7. Source scope adapter result (CORR-B/C/J)

- `CatalogSourceScopeQueryAdapter` implements `ICatalogSourceScopeQuery` using
  `ICatalogCommandRepository` (source + mappings) and `ICatalogPointReadinessQuery`
  (readiness version tuple).
- Returns `CatalogSourceScopeSnapshot` with `IReadOnlyList<CatalogSourceMappedScopeSnapshot>`.
- **NEW: Fail-closed on empty SiteId/AreaId** — adapter validates both are non-empty.
- **NEW: Fail-closed on zero version** — all ReadinessVersionTuple components must be > 0.
- **NEW: Multi-Site SiteIds sorted ordinally** — `SimulatorConfigurationService` sorts
  and deduplicates trusted SiteIds.
- Authorization: Administrator global for existing Simulator Source; Engineer
  requires ALL distinct mapped Site scopes; no Mapping = Administrator only;
  missing/decommissioned/non-Simulator Source = FORBIDDEN; Operator/Manager/
  Viewer = FORBIDDEN; inactive/missing caller = FORBIDDEN; out-of-scope = NOT_FOUND.
- Tests prove: one-Site scoped Engineer succeeds, multi-Site with all scopes
  succeeds, multi-Site partial denied, Manager denied, Viewer denied, Operator
  denied, inactive caller denied, missing/decommissioned source denied,
  unresolved readiness fail-closed, empty SiteId/AreaId fail-closed, zero
  version fail-closed, duplicate mappings deduplicate.

## 8. Readiness version tuple result (CORR-D)

- `PointReadinessSnapshot` extended with `ReadinessVersionTuple(PointVersion,
  AssetVersion, AreaVersion, SiteVersion)`.
- `ProviderVersion` retained as backward-compatible `Max()`.
- Tests prove changing only Site/Area/Asset/Point Version changes the
  readiness snapshot even when another object has a larger Version.
- Four independent version cases: each ancestor changed alone.

## 9. Mapping integration (CORR-K)

- `CatalogCommandHandler` → `OrganizationPointReadinessAdapter` → public
  `IOrganizationQueryRepository` test double (not `FakePointReadinessQuery`).
- Draft Point Mapping activation succeeds with `producingReady=false`.
- Active hierarchy Mapping produces `producingReady=true`.
- **NEW: Catalog events contain `producingReady` in Before and After** —
  verified for create and activate events on both Draft and Active points.
- Invalid hierarchy prevents activation.
- Catalog performs no Organization write.

## 10. Event metadata result (CORR-G)

- Create/edit events assert exact `EventType`, `SchemaVersion`, `Producer`,
  `AggregateType`, `AggregateId`, `AggregateVersion`, `ActorId`, `ActorUsername`,
  `Action`, `Summary`, `OccurredAtUtc`, `CorrelationId`, `CausationId`, `SiteIds`
  (multi-site collection, distinct, ordinally sorted), safe allowlist keys with
  `deterministicSeed`/`deterministicSeedHex`, and empty `Before` dictionary for
  create.
- Before/After key sets are identical for edit events.
- No credentials/secrets/connection fields in events.
- Rejected/stale/no-op commands emit no event.
- Manager, Viewer, Operator, inactive caller denial tests emit no events and
  return `FORBIDDEN`.

## 11. Migration 0005 static result (unchanged)

- `deterministic_seed numeric(20,0) NOT NULL` with range/scale checks.
- Immutable version constraints, Constant/Normal rules, fixed algorithm,
  same-schema FK, append-only trigger.
- Not executed.

## 12. Migration 0006 static result (CORR-E)

- Executable `DO $$ ... ALTER TABLE ... ADD CONSTRAINT ex_source_point_mapping_active_period
  EXCLUDE USING gist (point_id WITH =, tstzrange(effective_from, effective_to, '[)') WITH &&)
  WHERE (status = 'Active')` — idempotent DO block.
- **NEW: `conrelid = 'catalog.source_point_mapping'::regclass` filter added to
  pg_constraint lookup** for safe constraint presence check.
- No `CREATE EXTENSION`; predicate is Active only; range is `[)`.
- No Organization FK; required Source/Point/time/status indexes exist.
- Not executed.

## 13. T088 contract runner result (CORR-H)

- Provider-neutral `ConfigurationRepositoryContractRunner`:
  scenarios=24, assertions=24, failures=0.
- Separated test count (per method) from assertion count (per assertion).
- 24 scenarios: create/lookup head by source/config id, first version lookup,
  append/order, historical immutable, stale version, duplicate source, new-head
  rollback, deep rollback, interval positive, constant bounds reject/match,
  normal bounds reject/match/accepted, NaN minimum/maximum rejected,
  +/-Infinity minimum/maximum rejected, seed 0/MaxValue/mid accepted, actor
  username, correlation/causation.
- All required Constant/Normal positive and negative bound scenarios present.

## 14. Architecture result (CORR-I)

- `tests/Verification/architecture.tests.ps1` passes with 15 semantic checks:
  1. ulong seed
  2. numeric(20,0) migration
  3. multi-site scope with CatalogSourceMappedScopeSnapshot
  4. CatalogSourceScopeQueryAdapter existence
  5. No empty SiteId/AreaId fallback
  6. ReadinessVersions component positivity validation
  7. Mapping tests use OrganizationPointReadinessAdapter (not Fake)
  8. Migration 0006 DO block
  9. Executable EXCLUDE + conrelid filter
  10. T088 24 test counts + all required scenario methods present
  11. No Phase 5 files
  12. ConfigurationCommandTests uses real adapter chain + all required scenarios
  13. MappingReadinessTests has EventProducingReadyAssertions + FourIndependentVersionCases
  14. Service checks Engineer and Administrator roles
  15. CatalogCommandHandler events contain producingReady

## 15. T078–T093 task ledger

| Task | Result |
|---|---|
| T078 | PASS — immutable configuration with `ulong` seed |
| T079 | PASS — 87 assertions, 19 scenarios, real adapter chain, exact events, multi-site auth, all role denials, fail-closed validation |
| T080 | PASS — 62 assertions, producingReady events, 4 independent version cases |
| T081 | PASS — chronological RED captured (4 failures, build exit 0, run exit 1) |
| T082 | PASS — `ulong` seed contract, multi-site scope contract |
| T083 | PASS — fake with `ulong` seed |
| T084 | PASS — `ulong` seed validation, multi-site auth, exact event envelope |
| T085 | PASS — version tuple readiness adapter |
| T086 | PASS — `numeric(20,0)` seed migration |
| T087 | PASS — executable EXCLUDE + conrelid, DO block, no `CREATE EXTENSION` |
| T088 | PASS — 24 scenarios, 24 assertions, NaN/Infinity/bound coverage |
| T089 | BLOCKED_BY_PACKAGE_POLICY |
| T090 | BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE |
| T091 | PASS — 15 semantic architecture checks |
| T092 | PASS — review with zero Critical/High unresolved |
| T093 | PASS — this checkpoint |

## 16. Counts

- PASS: 14
- BLOCKED: 2
- FAIL: 0
- Runnable NOT_RUN: 0

Blocked tasks are not counted as PASS. Three harness checks blocked
(database/CI/container) are environment constraints, not task failures.

## 17. Database capability

PostgreSQL 18 is AVAILABLE / VERIFIED at `127.0.0.1:5433/iump_dev` through
the approved ignored local `.env`. No mutation was run, migrations 0005/0006
were not executed, `psql` was not run, port 5432 was not contacted, and the
database-access blocker count is 0. `btree_gist` provisioning is an external
company/DB-capability dependency of migration execution, not a reason to omit
the migration constraint.

## 18. Package blockers

No package was installed, downloaded, restored from a public source or added.
T089 remains `BLOCKED_BY_PACKAGE_POLICY`; T090 is
`BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE`, not a database-access blocker.

## 19. Phase 5 progression decision

**YES.** All runnable Phase 4 tasks pass.

## 20. Release readiness

**NO.** This checkpoint is not release approval.

## 21. Demo-readiness statement

Configuration backend capability exists with corrected seed, multi-site source
scope, version-tuple readiness, executable Migration overlap constraint,
fail-closed adapter validation, and chronological RED evidence. The Simulator
does not run yet; no Telemetry exists; no Web demo exists.
Configuration-only Demo 0.1 becomes feasible after Phase 5. A live monitoring
demo requires Phases 6–8 plus a thin API/Web slice.

## 22. Explicit stop

Stop after T093. Do not execute T094+, Point activation orchestration,
Simulator Run controls, Worker production, Telemetry ingestion, API/Web
endpoints, or Phase 5 files in this invocation.

## Final verification evidence

- Fresh Debug build/run: PASS (0 warnings/errors; T079 87/0, T080 62/0,
  T071 19/39/0, T088 24/24/0).
- Fresh Release build/run: PASS (same counts).
- Architecture (`tests/Verification/architecture.tests.ps1`): PASS (15 checks).
- Harness Full: 10 PASS, 3 BLOCKED (expected env gaps).
- Chronological RED captured with actual test output.
- Working tree clean at baseline `8331b6f57512d205af6eecac8ffce212e5e364d8`.
