# Phase 4 Corrective Convergence Review (T092)

Scope: T078–T093 corrective convergence against parent baseline
`8331b6f57512d205af6eecac8ffce212e5e364d8`. No Phase 5/6 behavior was
reviewed as implemented.

## Standards review

| ID | Severity | Evidence | Resolution | State |
|---|---|---|---|---|
| STD-001 | High | Acquisition owns `SimulatorConfigurationHead` and immutable version contracts; Catalog owns source/mapping. | Ownership is represented by module contracts, project references are limited to public-contract seams, and architecture verification passes. | Closed |
| STD-002 | High | `SimulatorConfigurationService` resolves caller and source scope through public ports. | Administrator-global and multi-Site Engineer scope are allowed; no-Mapping Administrator succeeds; missing/decommissioned source is FORBIDDEN; unscoped/missing-scope Engineer is NOT_FOUND; inactive caller, Operator/Manager/Viewer are FORBIDDEN. | Closed |
| STD-003 | High | `IAcquisitionConfigurationRepository` exposes create/append only for versions. | Aggregate ExpectedVersion is enforced; fake transactions deep-rollback; no update/delete version operation exists. | Closed |
| STD-004 | High | Event construction is explicit in `SimulatorConfiguration.cs`. | `SimulatorConfigurationChanged.v1` uses safe field allowlist, UTC timestamp, actor snapshot, trusted multi-Site collection (sorted, distinct), exact correlation/causation; it is owner-event construction only, not Audit persistence. | Closed |
| STD-005 | High | `OrganizationPointReadinessAdapter` consumes `IOrganizationQueryRepository` snapshots. | The adapter is read-only, validates trusted ancestry, returns version tuple (Point/Asset/Area/Site), and never consumes Catalog command input as authority. | Closed |
| STD-006 | High | `CatalogSourceScopeQueryAdapter` is the production implementation of `ICatalogSourceScopeQuery`. | Resolves source existence/type/status/version, all non-superseded Mappings, readiness snapshots, and returns trusted multi-Site scope collection; fail-closed on missing/incomplete readiness (empty SiteId, empty AreaId, zero version). | Closed |
| STD-007 | Medium | Migrations 0005/0006 are source-reviewed only. | 0005 has immutable-version constraints and `numeric(20,0)` seed with range/scale checks. 0006 has executable `EXCLUDE USING gist` constraint for active-period overlap via DO block with `conrelid` filter. | Closed |
| STD-008 | Medium | Architecture check verifies 15 Phase 4 semantic invariants. | Checks cover ulong seed, numeric(20,0) migration, multi-site scope, adapter existence, no empty fallback, version positivity, real adapter in tests, DO block, conrelid, T088 scenarios, no Phase 5 files, all required test methods, role checks, producingReady events. | Closed |
| STD-009 | High | Package and database restrictions remain explicit. | No PackageReference, restore, psql, migration execution, Docker, SQLite, API/Worker composition, or PostgreSQL adapter was added. T089/T090 remain blocked. | Closed |
| STD-010 | Medium | Phase boundary scan covers Acquisition source. | No Run, Worker, Telemetry, Start/Pause/Resume/Stop implementation or Point activation was added. | Closed |

## Specification review

| ID | Severity | Evidence | Resolution | State |
|---|---|---|---|---|
| SPEC-001 | High | Deterministic seed is `ulong`. | C# contract accepts 0 through `UInt64.MaxValue`; event payload includes `deterministicSeed` (invariant decimal) and `deterministicSeedHex` (lowercase 16-hex). Migration 0005 uses `numeric(20,0)` with range and integer-only checks. | Closed |
| SPEC-002 | High | Source scope is multi-Site. | `CatalogSourceScopeSnapshot` holds `IReadOnlyList<CatalogSourceMappedScopeSnapshot>` with MappingId/Version/PointId/SiteId/AreaId. Authorization requires Engineer to have ALL distinct trusted mapped Sites. Administrator is global. No-Mapping/partial-scope Engineer is denied. | Closed |
| SPEC-003 | High | Readiness version is a tuple. | `PointReadinessSnapshot` carries `ReadinessVersionTuple(PointVersion, AssetVersion, AreaVersion, SiteVersion)` in addition to backward-compatible `ProviderVersion` (Max). Four independent tests prove changing one ancestor's version changes the snapshot. | Closed |
| SPEC-004 | High | Migration 0006 has executable overlap protection. | `DO $$ ... ALTER TABLE ... ADD CONSTRAINT ... EXCLUDE USING gist ...` with `conrelid = 'catalog.source_point_mapping'::regclass`. | Closed |
| SPEC-005 | High | T088 separates test count from assertion count. | `ConfigurationRepositoryContractRunner` increments `_testCount` once per scenario method and `_assertionCount` per assertion. 24 scenarios, 24 assertions. | Closed |
| SPEC-006 | High | Event metadata assertions are exact. | Create/edit events assert exact envelope plus deterministicSeed/Hex, safe allowlist, identical Before/After key sets, SiteIds sorted distinct. Rejected/stale/no-op commands emit no event. | Closed |
| SPEC-007 | Medium | Mapping activation end-to-end readiness. | `CatalogCommandHandler` → real `OrganizationPointReadinessAdapter` → public `IOrganizationQueryRepository` test double. Draft Point activates with `producingReady=false`; Active hierarchy activates with `producingReady=true`; events carry producingReady in Before/After. | Closed |
| SPEC-008 | Medium | Migration invariant checks. | No Organization FK; no `CREATE EXTENSION`; required indexes exist; period `[)`; predicate Active-only; conrelid filter. | Closed |
| SPEC-009 | Medium | T088 contract runner scenarios complete. | 24 scenarios: create/lookup, append/order, rollback, stale/duplicate, interval, constant/normal bounds (positive and negative), NaN/Infinity rejection (both min and max), all seed values, actor/correlation snapshot. | Closed |
| SPEC-010 | High | Chronological RED evidence. | RED captured before production fixes: 4 failures documented with actual build/run output, exit codes, and failure analysis. | Closed |

## Corrective findings

| ID | Severity | Evidence | Resolution | State |
|---|---|---|---|---|
| CORR-A | High | `DeterministicSeed` was `string?` — arbitrary text accepted. | Changed to `ulong` in contracts, commands, domain, migration 0005, events, and fakes. | Closed |
| CORR-B | High | `ICatalogSourceScopeQuery` had no Catalog application implementation. | Created `CatalogSourceScopeQueryAdapter` in `Catalog.Application` consuming `ICatalogCommandRepository` and `ICatalogPointReadinessQuery`. | Closed |
| CORR-C | High | `CatalogSourceScopeSnapshot` assumed one Source → one Site. | Changed to multi-MappedScope collection. Authorization requires Engineer to hold ALL distinct mapped Sites. | Closed |
| CORR-D | High | Organization readiness used `Max()` — hid low-version changes. | Added `ReadinessVersionTuple` with per-object versions; backward-compatible `ProviderVersion` retained. Four independent version cases tested. | Closed |
| CORR-E | High | Migration 0006 used `ADD CONSTRAINT IF NOT EXISTS` with comment-only EXCLUDE; no conrelid filter. | Replaced with idempotent DO block wrapping executable `ALTER TABLE ... ADD CONSTRAINT ... EXCLUDE USING gist` with `conrelid = 'catalog.source_point_mapping'::regclass`. | Closed |
| CORR-F | High | RED evidence was conceptual without chronological capture. | Replaced with chronological RED: tests added first, build exit 0, run exit 1, 4 actual failures captured with output. | Closed |
| CORR-G | High | T079 lacked exact event metadata and multi-site assertions; missing denial tests. | Added exact CorrelationId/CausationId, SiteIds collection (sorted, distinct), AggregateType/Id/Version, ActorId, Summary, Action, deterministicSeed/Hex, Before/After key sets, multi-site/all-scope/partial-scope/no-Mapping auth scenarios, Operator/Manager/Viewer/Inactive denial. 87 assertions total. | Closed |
| CORR-H | High | T088 counted assertions as tests; had weak scenario coverage. | Separated `_testCount` (per scenario) from `_assertionCount` (per assertion); expanded to 24 scenarios including NaN/Infinity/bound positive and negative cases, constant accepted, normal accepted. | Closed |
| CORR-I | High | T091/T092/T093 contained unsupported PASS with missing checks. | T091 now includes 15 semantic checks. T092 enumerates all 10 corrective findings closed. T093 records truthful state with chronological RED. | Closed |
| CORR-J | High | `CatalogSourceScopeQueryAdapter` returned empty SiteId/AreaId on unresolved readiness (fail-open); no version positivity validation. | Changed to return `null` when SiteId/AreaId is empty or any version component ≤ 0. | Closed |
| CORR-K | High | MappingReadinessTests used `FakePointReadinessQuery` instead of real adapter chain; missed producingReady events and independent version tests. | Changed to use `OrganizationPointReadinessAdapter` via `ReadinessQueryDouble`; added `EventProducingReadyAssertions` (8 assertions on event Before/After) and `FourIndependentVersionCases` (12 assertions). | Closed |

## Review result

| Severity | Unresolved findings |
|---|---|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 0 |

T092 status: **PASS**. All corrective findings (CORR-A through CORR-K) are
closed; T089/T090 remain package-policy blocked as documented.
