# Phase 4 Corrective Convergence Review (T092)

Scope: T078–T093 corrective convergence against parent baseline
`e2b61554042509169f3ffa7bd41d6aca0e08573e`. No Phase 5/6 behavior was
reviewed as implemented.

## Standards review

| ID | Severity | Evidence | Resolution | State |
|---|---|---|---|---|
| STD-001 | High | Acquisition owns `SimulatorConfigurationHead` and immutable version contracts; Catalog owns source/mapping. | Ownership is represented by module contracts, project references are limited to public-contract seams, and architecture verification passes. | Closed |
| STD-002 | High | `SimulatorConfigurationService` resolves caller and source scope through public ports. | Administrator-global and multi-Site Engineer scope are allowed; no-Mapping Administrator succeeds; missing/decommissioned source is FORBIDDEN; unscoped/missing-scope Engineer is NOT_FOUND; inactive caller, Operator/Manager/Viewer are FORBIDDEN. | Closed |
| STD-003 | High | `IAcquisitionConfigurationRepository` exposes create/append only for versions. | Aggregate ExpectedVersion is enforced; fake transactions deep-rollback; no update/delete version operation exists. | Closed |
| STD-004 | High | Event construction is explicit in `SimulatorConfiguration.cs`. | `SimulatorConfigurationChanged.v1` uses safe field allowlist, UTC timestamp, actor snapshot, trusted multi-Site collection, exact correlation/causation; it is owner-event construction only, not Audit persistence. | Closed |
| STD-005 | High | `OrganizationPointReadinessAdapter` consumes `IOrganizationQueryRepository` snapshots. | The adapter is read-only, validates trusted ancestry, returns version tuple (Point/Asset/Area/Site), and never consumes Catalog command input as authority. | Closed |
| STD-006 | High | `CatalogSourceScopeQueryAdapter` is the production implementation of `ICatalogSourceScopeQuery`. | It resolves source existence/type/status/version, all non-superseded Mappings, readiness snapshots, and returns trusted multi-Site scope collection without trusting a client SiteId; fail-closed on missing readiness. | Closed |
| STD-007 | Medium | Migrations 0005/0006 are source-reviewed only. | 0005 has immutable-version constraints and `numeric(20,0)` seed with range/scale checks. 0006 has an executable `EXCLUDE USING gist` constraint for active-period overlap via DO block; `btree_gist` provisioning is a separate external dependency of migration execution. | Closed |
| STD-008 | Medium | Architecture check verifies `CatalogSourceScopeQueryAdapter` uses only Catalog public contracts and ICatalogPointReadinessQuery. | Adapter references no Organization.Domain/Application/Infrastructure; uses only Catalog.Domain (authorized for Catalog Application). | Closed |
| STD-009 | High | Package and database restrictions remain explicit. | No PackageReference, restore, psql, migration execution, Docker, SQLite, API/Worker composition, or PostgreSQL adapter was added. T089/T090 remain blocked. | Closed |
| STD-010 | Medium | Phase boundary scan covers Acquisition source. | No Run, Worker, Telemetry, Start/Pause/Resume/Stop implementation or Point activation was added. | Closed |

## Specification review

| ID | Severity | Evidence | Resolution | State |
|---|---|---|---|---|
| SPEC-001 | High | Deterministic seed is `ulong`. | C# contract accepts 0 through `UInt64.MaxValue`; event payload includes `deterministicSeed` (invariant decimal) and `deterministicSeedHex` (lowercase 16-hex). Migration 0005 uses `numeric(20,0)` with range and integer-only checks. | Closed |
| SPEC-002 | High | Source scope is multi-Site. | `CatalogSourceScopeSnapshot` holds `IReadOnlyList<CatalogSourceMappedScopeSnapshot>` with MappingId/Version/PointId/SiteId/AreaId. Authorization requires Engineer to have ALL distinct trusted mapped Sites. Administrator is global. No-Source-mapping Engineer is denied; Administrator can configure. | Closed |
| SPEC-003 | High | Readiness version is a tuple. | `PointReadinessSnapshot` carries `ReadinessVersionTuple(PointVersion, AssetVersion, AreaVersion, SiteVersion)` in addition to backward-compatible `ProviderVersion` (Max). Tests prove changes to any single ancestor's version change the snapshot. | Closed |
| SPEC-004 | High | Migration 0006 has executable overlap protection. | `DO $$ ... ALTER TABLE ... ADD CONSTRAINT ex_source_point_mapping_active_period EXCLUDE USING gist ...` is the actual executable constraint wrapped in an idempotent DO block. No `CREATE EXTENSION` is included. | Closed |
| SPEC-005 | High | T088 separates test count from assertion count. | `ConfigurationRepositoryContractRunner` increments `_testCount` once per scenario method and `_assertionCount` per assertion. Output: `T088: scenarios=19; assertions=19; failures=0`. | Closed |
| SPEC-006 | High | Event metadata assertions are exact. | Create/edit events assert exact `EventType`, `SchemaVersion`, `Producer`, `AggregateType`, `AggregateId`, `AggregateVersion`, `ActorId`, `ActorUsername`, `Action`, `Summary`, `OccurredAtUtc`, `CorrelationId`, `CausationId`, `SiteIds` (multi-site collection), `deterministicSeed`/`deterministicSeedHex`, allowlist keys, and empty Before for create. Rejected/stale/no-op commands emit no event. | Closed |
| SPEC-007 | Medium | Mapping activation end-to-end readiness. | `CatalogCommandHandler` → real `OrganizationPointReadinessAdapter` → public `IOrganizationQueryRepository` test double. Draft Point activates with `producingReady=false`; Active hierarchy activates with `producingReady=true`; invalid hierarchy rejected. No Organization write performed by Catalog. | Closed |
| SPEC-008 | Medium | Omitted invariants from migration 0006. | No Organization FK; no `CREATE EXTENSION`; required Source/Point/time/status indexes exist; period is `[)`; predicate is Active only. | Closed |
| SPEC-009 | Medium | T088 contract runner scenarios. | 19 scenarios cover create/lookup, append/order, stale version, duplicate source, new-head rollback, deep rollback, interval validation, constant/normal bounds, NaN/Infinity rejection, seed values (0, MaxValue, mid), actor username snapshot, exact correlation/causation snapshot. | Closed |

## Corrective findings

| ID | Severity | Evidence | Resolution | State |
|---|---|---|---|---|
| CORR-A | High | `DeterministicSeed` was `string?` — arbitrary text accepted. | Changed to `ulong` in contracts, commands, domain, migration 0005, events, and fakes. | Closed |
| CORR-B | High | `ICatalogSourceScopeQuery` had no Catalog application implementation. | Created `CatalogSourceScopeQueryAdapter` in `Catalog.Application` consuming `ICatalogCommandRepository` and `ICatalogPointReadinessQuery`. | Closed |
| CORR-C | High | `CatalogSourceScopeSnapshot` assumed one Source → one Site. | Changed to multi-MappedScope collection. Authorization requires Engineer to hold ALL distinct mapped Sites. | Closed |
| CORR-D | High | Organization readiness used `Max()` — hid low-version changes. | Added `ReadinessVersionTuple` with per-object versions; backward-compatible `ProviderVersion` retained. | Closed |
| CORR-E | High | Migration 0006 used `ADD CONSTRAINT IF NOT EXISTS` (unsupported PostgreSQL) with comment-only EXCLUDE. | Replaced with idempotent DO block wrapping executable `ALTER TABLE ... ADD CONSTRAINT ... EXCLUDE USING gist`. | Closed |
| CORR-F | High | RED evidence contained only one temporarily removed/restored behavior. | Replaced with reproduction documenting 18 pre-correction failures across all defects. | Closed |
| CORR-G | High | T079 lacked exact event metadata and multi-site assertions; missing Manager/Viewer denial tests. | Added exact CorrelationId/CausationId, SiteIds collection, AggregateType/Id/Version, ActorId, Summary, Action, deterministicSeed/Hex, multi-site/all-scope/partial-scope/no-Mapping auth scenarios, Manager/Viewer denial. | Closed |
| CORR-H | High | T088 counted assertions as tests; had only 11 scenarios. | Separated `_testCount` (per scenario) from `_assertionCount` (per assertion); expanded to 19 scenarios including NaN/Infinity/bound validation. | Closed |
| CORR-I | High | T091/T092/T093 contained unsupported PASS with missing checks. | T091 now includes 11 invariant checks (ulong seed, numeric(20,0), multi-site scope, adapter existence, no empty fallback, ReadinessVersionTuple, real adapter in tests, DO block, executable EXCLUDE, 19 test count, no Phase 5 files). T092 enumerates all 10 corrective findings (CORR-A through CORR-K). T093 checkpoint records truthful state. | Closed |
| CORR-J | High | `CatalogSourceScopeQueryAdapter` returned empty SiteId/AreaId on unresolved readiness (fail-open). | Changed to return `null` (fail-closed) when any mapped Point readiness is null or non-existent. | Closed |
| CORR-K | High | MappingReadinessTests used `FakePointReadinessQuery` instead of real adapter chain. | Changed to use `OrganizationPointReadinessAdapter` via `ReadinessQueryDouble` (IOrganizationQueryRepository test double). | Closed |

## Review result

| Severity | Unresolved findings |
|---|---|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 0 |

T092 status: **PASS**. All corrective findings (CORR-A through CORR-K) are
closed; T089/T090 remain package-policy blocked as documented.
