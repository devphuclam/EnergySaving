# Phase 4 Corrective Convergence Checkpoint (T093)

## 1. Parent baseline

- Repository: `devphuclam/EnergySaving`
- Feature: `specs/002-asset-simulator-latest/`
- Parent baseline HEAD: `e2b61554042509169f3ffa7bd41d6aca0e08573e`
- Accepted predecessor: T077 / Phase 3 (corrective micro-closure)

## 2. Result-commit identity semantics

No commit was created by this invocation. The result is the working-tree diff
relative to the parent baseline above. A commit SHA must be resolved
externally after `git add` and `git commit`.

## 3. Exact changed files

- `database/migrations/0005_acquisition_configuration.sql`
- `database/migrations/0006_catalog_source_mapping.sql`
- `src/Modules/Catalog/Application/CatalogSourceScopeQueryAdapter.cs` (new)
- `src/Modules/Catalog/Application/OrganizationPointReadinessAdapter.cs`
- `src/Modules/Catalog/Contracts/CatalogEligibilityContracts.cs`
- `src/Modules/Acquisition/Application/SimulatorConfiguration.cs`
- `src/Modules/Acquisition/Contracts/ConfigurationPersistenceContracts.cs`
- `tests/Integration/Acquisition/ConfigurationRepositoryTests.cs`
- `tests/Unit/Acquisition/ConfigurationCommandTests.cs`
- `tests/Unit/Acquisition/ConfigurationTests.cs`
- `tests/Unit/Catalog/MappingReadinessTests.cs`
- `tests/Unit/Fakes/FakeAcquisitionConfigurationRepository.cs`
- `tests/Unit/Program.cs`
- `tests/Verification/architecture.tests.ps1`
- `specs/002-asset-simulator-latest/checklists/phase-04-red.md`
- `specs/002-asset-simulator-latest/checklists/phase-04-review.md`
- `specs/002-asset-simulator-latest/checklists/phase-04-configuration.md`

No prohibited file was changed.

## 4. RED commands/exits/failures

See `phase-04-red.md`. RED is post-hoc reproduced from the pre-correction
baseline documenting 12 business assertion failures across seed type, multi-site
scope, version tuple, migration constraint, event metadata, and test
organisation.

## 5. GREEN commands/exits/counts

```text
dotnet build tests/Unit/IUMP.Tests.Unit.csproj --no-restore -c Debug: exit 0, 0 warnings, 0 errors
dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj --no-build -c Debug: exit 0
T071: tests=19; assertions=39; failures=0
T088: scenarios=11; assertions=12; failures=0
PASS: all tests

dotnet build tests/Unit/IUMP.Tests.Unit.csproj --no-restore -c Release: exit 0, 0 warnings, 0 errors
dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj --no-build -c Release: exit 0
T071: tests=19; assertions=39; failures=0
T088: scenarios=11; assertions=12; failures=0
PASS: all tests
```

## 6. Deterministic seed result (CORR-A)

- Contract model: `ulong` accepting 0 through `UInt64.MaxValue`.
- Event payload: `deterministicSeed` (invariant decimal string),
  `deterministicSeedHex` (lowercase 16-hex).
- Migration 0005: `numeric(20,0)` with `CHECK (deterministic_seed >= 0 AND
  deterministic_seed <= 18446744073709551615 AND scale(deterministic_seed) = 0)`.
- Tests: 0, 42, `UInt64.MaxValue` accepted; historical immutable.

## 7. Source scope adapter result (CORR-B/C)

- `CatalogSourceScopeQueryAdapter` implements `ICatalogSourceScopeQuery` using
  `ICatalogCommandRepository` (source + mappings) and `ICatalogPointReadinessQuery`
  (readiness version tuple).
- Returns `CatalogSourceScopeSnapshot` with `IReadOnlyList<CatalogSourceMappedScopeSnapshot>`.
- Authorization: Administrator global for existing Simulator Source; Engineer
  requires ALL distinct mapped Site scopes; no Mapping = Administrator only;
  missing/decommissioned/non-Simulator Source = FORBIDDEN; Operator/Manager/
  Viewer = FORBIDDEN; inactive/missing caller = FORBIDDEN; out-of-scope = NOT_FOUND.
- Tests prove one-Site scoped Engineer succeeds, multi-Site with all scopes
  succeeds, multi-Site missing one scope denied, no Mapping Engineer denied and
  Administrator allowed.

## 8. Readiness version tuple result (CORR-D)

- `PointReadinessSnapshot` extended with `ReadinessVersionTuple(PointVersion,
  AssetVersion, AreaVersion, SiteVersion)`.
- `ProviderVersion` retained as backward-compatible `Max()`.
- Tests prove changing only Site/Area/Asset/Point Version changes the
  readiness snapshot even when another object has a larger Version.

## 9. Mapping integration (CORR-G)

- `CatalogCommandHandler` → `OrganizationPointReadinessAdapter` → public
  `IOrganizationQueryRepository` test double.
- Draft Point Mapping activation succeeds with `producingReady=false`.
- Active hierarchy Mapping produces `producingReady=true`.
- Invalid hierarchy prevents activation.
- Catalog performs no Organization write.

## 10. Event metadata result (CORR-G)

- Create/edit events assert exact `CorrelationId`, `CausationId`, `AggregateType`,
  `AggregateVersion`, `ActorUsername`, `SiteIds` (multi-site collection), UTC
  timestamp, safe allowlist keys with `deterministicSeed`/`deterministicSeedHex`.
- Rejected/stale/no-op commands emit no event.

## 11. Migration 0005 static result

- `deterministic_seed numeric(20,0) NOT NULL` with range/scale checks.
- Immutable version constraints, Constant/Normal rules, fixed algorithm,
  same-schema FK, append-only trigger.
- Not executed.

## 12. Migration 0006 static result

- Executable `EXCLUDE USING gist` constraint for active-period overlap.
- No `CREATE EXTENSION`; predicate is Active only; range is `[)`.
- No Organization FK; required Source/Point/time/status indexes exist.
- `btree_gist` provisioning recorded as external company/DB-capability
  dependency of migration execution (carried in T090 evidence).
- Not executed.

## 13. T088 contract runner result

- Provider-neutral `ConfigurationRepositoryContractRunner`:
  scenarios=11, assertions=12, failures=0.
- Separated test count (per method) from assertion count (per assertion).
- Scenarios: create/lookup, append/order, stale version, duplicate source,
  new-head rollback, deep rollback, constraint validation, seed values (0,
  UInt64.MaxValue), actor username snapshot, exact correlation/causation.
- No fake casts, concrete fake references, Skip/TODO, credentials or fallback
  connection strings.

## 14. Architecture result

- `tests/Verification/architecture.tests.ps1` passes with new
  `CatalogSourceScopeQueryAdapter` check (no Organization/Cross-module
  reference). Fast harness passes.
- Approved public-contract references: Acquisition → Catalog, Catalog → Organization.
- No cross-schema FK in 0005/0006.
- No PostgreSQL adapter, no Phase 5/6 behavior, no API/Worker composition root
  changes.

## 15. T078–T093 task ledger

| Task | Result |
|---|---|
| T078 | PASS — immutable configuration with `ulong` seed |
| T079 | PASS — exact event metadata, multi-site auth, inactive/missing denials |
| T080 | PASS — readiness version tuple, real adapter integration, Draft→Active transition |
| T081 | PASS — reproduced RED evidence documented |
| T082 | PASS — `ulong` seed contract, multi-site scope contract, |
| T083 | PASS — fake with `ulong` seed |
| T084 | PASS — `ulong` seed validation, multi-site auth, exact event envelope |
| T085 | PASS — version tuple readiness adapter |
| T086 | PASS — `numeric(20,0)` seed migration |
| T087 | PASS — executable EXCLUDE constraint, no `CREATE EXTENSION`, half-open `[)` |
| T088 | PASS — 11 scenarios, 12 assertions, no test/assertion conflation |
| T089 | BLOCKED_BY_PACKAGE_POLICY |
| T090 | BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE |
| T091 | PASS — architecture check covers `CatalogSourceScopeQueryAdapter` |
| T092 | PASS — 10 Standards + 9 Specification + 9 Corrective findings; zero Critical/High |
| T093 | PASS — this checkpoint |

## 16. Counts

- PASS: 14
- BLOCKED: 2
- FAIL: 0
- Runnable NOT_RUN: 0

Blocked tasks are not counted as PASS.

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
scope, version-tuple readiness, and executable Migration overlap constraint.
The Simulator does not run yet; no Telemetry exists; no Web demo exists.
Configuration-only Demo 0.1 becomes feasible after Phase 5. A live monitoring
demo requires Phases 6–8 plus a thin API/Web slice.

## 22. Explicit stop

Stop after T093. Do not execute T094+, Point activation orchestration,
Simulator Run controls, Worker production, Telemetry ingestion, API/Web
endpoints, or Phase 5 files in this invocation.

## Final verification evidence

- Fresh Debug and Release build/run: PASS (0 warnings/errors; T071 19/39;
  T088 11/12; 0 failures).
- Architecture (`tests/Verification/architecture.tests.ps1`) and
  `git diff --check`: PASS (CRLF warnings cosmetic).
- SQL static checks for 0005/0006, changed-file scope review, `.env`
  ignore/tracking check and prohibited-port scan: PASS; no secret values
  printed.
- Full harness not re-executed (no package/database change from prior evidence).
- `CatalogSourceScopeQueryAdapter` exists as new file in working tree.
