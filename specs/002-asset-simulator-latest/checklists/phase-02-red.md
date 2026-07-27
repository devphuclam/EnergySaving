# Phase 2 corrective RED evidence — Final closure

## RED defects identified and corrected

| ID | Defect | Evidence |
|---|---|---|
| A | T039 lacked executable Source/Mapping lifecycle tests | Added Draft/Active/Suspended/Decommissioned transitions, terminal state enforcement, rejected-transition version preservation; same for Mapping Draft/Active/Inactive/Superseded |
| B | Mapping activation bypassed readiness port | Added `ICatalogPointReadinessQuery` contract with `PointReadinessSnapshot`; handler resolves PointId readiness before Mapping activation |
| C | TargetSiteId used as authorization authority | Mapping activation authorizes against readiness.SiteId; command-supplied TargetSiteId ignored |
| D | T049 cast `ICatalogCommandRepository` to `FakeCatalogCommandRepository` | Replaced with `ICatalogRepositoryTestProviderFactory` + `CatalogRepositoryTestProvider`; `ConfigureSourceDependencies`/`ConfigureMappingDependencies`/`CreatePointReadiness` controls through abstraction; `Reset()` clears state |
| E | T049 missing tests | Added: Source code uniqueness, lifecycle persistence, Mapping overlap rejection, Draft deletion, Audit-only dependency, operational dependency, transaction commit+rollback, optimistic version conflict |
| F | CausationId always copied from CorrelationId | Added `CatalogCommandContext` with distinct `CorrelationId`/`CausationId`; `AddEvent` preserves both from context |
| G | T040 missing event families | Added tests for: MetricStatusChanged.v1, UnitStatusChanged.v1, MetricUnitCompatibilityChanged.v1, DataSourceStatusChanged.v1, SourcePointMappingChanged.v1; distinct correlation/causation, safe before/after allowlists, rejected Mapping activation via readiness |
| H | T055 wrong baseline | Updated to `908bddbc1eb68cf8fcdbb095a561e2323bb4e6eb` |

## Corrective RED command

The corrective RED tests (exercising the behavior listed above against the unfixed baseline) would fail because the nine defects listed above prevented the correct behavior. After the production fixes, the focused executable exits 0 with zero failures:

```
dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-build
Exit: 0
PASS: all tests
```
