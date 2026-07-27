# Phase 2 review — Final closure

## Repaired defects

| Defect | Fix location | Change |
|---|---|---|
| A: Missing Source/Mapping lifecycle tests | `tests/Unit/Catalog/SourceMappingTests.cs` | Rewritten with Draft→Active→Suspended→Decommissioned transitions for Source; Draft→Active→Inactive→Superseded for Mapping; terminal-state enforcement; rejected-transition version preservation |
| B: No readiness port for Mapping activation | `src/Modules/Catalog/Contracts/CatalogEligibilityContracts.cs` | Added `PointReadinessSnapshot` record and `ICatalogPointReadinessQuery` interface |
| C: TargetSiteId used for authorization | `src/Modules/Catalog/Application/CatalogCommands.cs` | Mapping activation authorizes against `readiness.SiteId` from readiness query result |
| D: T049 hard-coded fake cast | `tests/Integration/Catalog/CatalogRepositoryTests.cs` | Introduced provider-neutral `ICatalogRepositoryTestProviderFactory` / `CatalogRepositoryTestProvider`; no `FakeCatalogCommandRepository` cast |
| E: T049 missing test scenarios | `tests/Integration/Catalog/CatalogRepositoryTests.cs` | 9 contract tests: Source uniqueness, lifecycle, Mapping overlap, Draft deletion, audit-only dep, operational dep, commit, rollback, optimistic version |
| F: CausationId = CorrelationId | `src/Modules/Catalog/Application/CatalogCommands.cs` | `CatalogCommandContext` carries distinct `CorrelationId`/`CausationId`; `AddEvent` uses context `CausationId` |
| G: Missing event families | `tests/Unit/Catalog/CatalogCommandTests.cs` | All 5 event families tested; distinct correlation/causation; no-op emits zero events; sensitive key allowlist verified |
| H: Wrong T055 checkpoint | `specs/002-asset-simulator-latest/checklists/phase-02-catalog.md` | Baseline updated to `908bddbc1eb68cf8fcdbb095a561e2323bb4e6eb` |

## New files and interfaces

| Artifact | Purpose |
|---|---|
| `PointReadinessSnapshot` | Readiness result record: SiteId, PointStatus, last data timestamp |
| `ICatalogPointReadinessQuery` | Port for resolving Point readiness before Mapping activation |
| `FakePointReadinessQuery` | Test double returning configured readiness; supports missing-point, scoped, and non-producing scenarios |
| `CatalogCommandContext` | Command metadata: ActorUserId, CorrelationId, CausationId (distinct) |
| `ICatalogRepositoryTestProviderFactory` / `CatalogRepositoryTestProvider` | Provider-neutral test abstraction for contract tests |

## Remaining blocked items

- T050: BLOCKED_BY_PACKAGE_POLICY (PostgreSQL adapters)
- T051: BLOCKED_BY_PACKAGE_POLICY (composition-root registration)
- T052: BLOCKED_BY_DATABASE_ACCESS (migration execution)
