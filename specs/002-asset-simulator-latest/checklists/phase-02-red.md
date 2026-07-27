# Phase 2 RED Evidence — Catalog

## Command and exit evidence

**Command**: `dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore --no-build`
**Start time**: 2026-07-24 16:55:41
**Exit code**: 1 (FAIL)

## Failure summary

| Test file | Test class | Failure count | Evidence |
|---|---|---|---|
| `tests/Unit/Catalog/MetricUnitTests.cs` | MetricUnitTests | 10 | Metric/Unit lifecycle, normalization, uniqueness, compatibility, canonical, eligibility, seeds not implemented |
| `tests/Unit/Catalog/SourceMappingTests.cs` | SourceMappingTests | 12 | Source/Mapping lifecycle, transitions, intervals, overlap, coexistence, delete policy not implemented |
| `tests/Unit/Catalog/CatalogCommandTests.cs` | CatalogCommandTests | 12 | Authorization, event construction, redaction not implemented |

**Total assertions**: 34
**PASS**: 21 (existing Phase 1)
**FAIL**: 34 (expected: no Catalog domain/application/contracts exist yet)

## Blocking classification

All failures are **expected RED** — the Catalog implementation is intentionally absent. No external dependency blocks these failures.

## Progression

RED evidence is complete. Proceeding to T042-T049 (contracts, domain, application, migration, integration source) to make tests PASS.
