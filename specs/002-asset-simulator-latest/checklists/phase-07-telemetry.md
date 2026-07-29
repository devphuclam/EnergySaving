# Phase 7 Canonical Telemetry Checkpoint — Atomic-Race and Compatibility-Lock Closure

## 1. Gate identity

- Baseline: `d5c71ed42a45c6fee189c3a67580b0cf096c9bf6`
- Feature: `002-asset-simulator-latest`
- Stop task: `T151`
- Constitution: `1.1.0`
- T146/T147/T148 remain explicitly package-policy blocked; no Phase 8, Latest, Source Health,
  durable jobs, Audit/API/Web, runtime registration, PostgreSQL adapter, migration execution, or
  port `5432` work was performed.

## 2. Atomic-race and compatibility-lock corrections

- `PublishRaceWinner` uses validate-then-publish: Phase A validates the complete fixture without
  mutation, Phase B clones the current committed state, constructs new state, and replaces atomically.
- Invalid Accepted fixture (missing raw, wrong Latest) throws `RACE_WINNER_FIXTURE_INVALID` before
  any terminal/raw/Latest/event dictionary mutation. Terminal count, raw count, Latest state, and
  event count remain unchanged.
- Invalid Rejected fixture with non-null Raw/Latest/Event throws `RACE_WINNER_FIXTURE_INVALID`
  before any mutation.
- `TelemetryFlowLockTarget.CatalogCompatibility = 9` locks the Compatibility row. Lock order:
  OrganizationSite → OrganizationArea → OrganizationAsset → OrganizationPoint → CatalogSource →
  CatalogMapping → CatalogMetric → CatalogUnit → **CatalogCompatibility** →
  TelemetryIdentityRawLatest → IntegrationOutbox.
- Compatibility lock acquired in `AcquireOwnerLocksAsync` after `CatalogUnit` using
  `CompatibilityIdentity` as key. Provider recheck occurs while Compatibility row is locked.
- `ITelemetryRaceWinnerProbe` exposes `GetCommittedLatestAsync(Guid pointId, CancellationToken)`
  for exact committed Latest comparison.
- T145 includes exact Rejected race-winner scenario: terminal-only fixture committed, zero raw,
  zero Latest, zero event, Duplicate replay returns exact original.
- `ValidateTrustedScope` enforces `TrustedSiteId == SiteId` and `TrustedAreaId == AreaId` before
  any transaction. Mismatch returns `PROVIDER_SCOPE_MISMATCH`.
- `MeasurementAcceptedEventFactory` accepts optional `eventSiteId`/`eventAreaId`; production callers
  pass `provider.TrustedSiteId`/`provider.TrustedAreaId`.

## 3. RED evidence

- Temporary native worktree at the d5c71ed baseline:
  `C:\Users\TD-999\AppData\Local\Temp\opencode\phase7-red`.
- Test/static-only RED build: exit `0`.
- Focused RED run: exit `1`, exactly 8 assertions — one per natural defect (A–H).
- Worktree was removed after capture. No restore/download, database connection/mutation, migration
  execution, Docker, or secret output occurred.

### Natural RED failures

1. **RED-1**: Invalid Accepted fixture commits terminal before throwing.
2. **RED-2**: Invalid Rejected fixture with Raw/Latest/Event commits terminal before validation.
3. **RED-3**: No `CatalogCompatibility` lock target in `TelemetryFlowLockTarget`.
4. **RED-4**: No Compatibility lock acquired in `AcquireOwnerLocksAsync`.
5. **RED-5**: No `GetCommittedLatest` on `ITelemetryRaceWinnerProbe`; only `LatestCount` available.
6. **RED-6**: T145 lacks exact Rejected race-winner scenario.
7. **RED-7**: No `PROVIDER_SCOPE_MISMATCH` check; event uses unverified `SiteId`/`AreaId`.
8. **RED-8**: T149/Phase7ReviewCheck misses atomic-race, compatibility, Latest probe, Rejected
   fixture, and trusted-scope defects.

## 4. GREEN and contract evidence

Focused provider-neutral run:

| Task | Cases / scenarios | Checks / assertions | Result |
|---:|---:|---:|---|
| T131 | 16 | 52 | PASS |
| T132 | 15 | 217 | PASS |
| T133 | 9 | 72 | PASS |
| T134 | 22 | 96 | PASS |
| T135 | 8 | 25 | PASS |
| T145 | 22 | 63 | PASS |
| T149 | — | 52 | PASS |
| T150 | — | 24 | PASS |

Previous phase regressions also pass. Debug and Release solution builds are zero-warning,
zero-error.

## 5. Task evidence

| Task | Status | Classification |
|---|---|---|
| T131 | PASS | RUNNABLE_NOW |
| T132 | PASS | RUNNABLE_NOW |
| T133 | PASS | RUNNABLE_NOW |
| T134 | PASS | RUNNABLE_NOW |
| T135 | PASS | RUNNABLE_NOW |
| T145 | PASS | RUNNABLE_NOW |
| T146 | BLOCKED | BLOCKED_BY_PACKAGE_POLICY |
| T147 | BLOCKED | BLOCKED_BY_PACKAGE_POLICY |
| T148 | BLOCKED | BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE |
| T149 | PASS | RUNNABLE_NOW |
| T150 | PASS | RUNNABLE_NOW |
| T151 | PASS | RUNNABLE_NOW |

**Ledger**: PASS 18, BLOCKED 3, FAIL 0, NOT_RUN 0.

## 6. Files changed by this closure

| File | Change |
|---|---|
| `src/Modules/Telemetry/Contracts/TelemetryPersistenceContracts.cs` | Added `CatalogCompatibility = 9` to enum |
| `src/Modules/Telemetry/Application/TelemetryPersistenceService.cs` | Compatibility lock, trusted scope validation, TrustedSiteId in event |
| `tests/Unit/Fakes/FakeTelemetryRepositories.cs` | Validate-then-publish atomic PublishRaceWinner, GetCommittedLatestAsync |
| `tests/Unit/Telemetry/IngestionPersistenceContractTests.cs` | Updated lock trace expectation |
| `tests/Unit/Telemetry/TelemetryEventTests.cs` | TrustedSiteId/TrustedAreaId in factory call and assertion |
| `tests/Unit/Telemetry/MeasurementIdentityRegistryTests.cs` | Provider TrustedSiteId/TrustedAreaId = SiteId/AreaId |
| `tests/Integration/Telemetry/TelemetryIngestionRepositoryTests.cs` | GetCommittedLatestAsync on probe, Rejected race winner scenario, TrustedSiteId in Data() |
| `tests/Unit/Telemetry/Phase7ReviewCheck.cs` | Atomic-race, compatibility lock, Latest probe, Rejected fixture, trusted scope checks |
| `tests/Verification/architecture.tests.ps1` | PublishRaceWinner order, CatalogCompatibility, lock acquisition, scope validation checks |

## 7. Scope and environment

- Database capability: `AVAILABLE` at `127.0.0.1:5433/iump_dev`
- Database mutation: `NOT_RUN`
- Port 5432 contacted: `NO`
- psql: `BLOCKED_BY_MISSING_TOOL`
- Package restore: `NOT_RUN`
- Docker/container: `NOT_USED`
- Migration execution: `NOT_RUN`
- Phase 8: `NOT_STARTED`

## 8. T151 checkpoint decision

- Standards/Spec review: `PASS`; unresolved Critical/High: `0`.
- Phase 7 runnable provider-neutral work: `PASS`.
- Package-policy transitive migration/adapter work: `BLOCKED` and preserved.
- Ready to begin Phase 8: `YES` (only after the next explicit `/speckit.implement` invocation).
- Release-ready: `NO`; mandatory environment/package blockers remain.
- Stop: `T151`; do not execute T152+ in this invocation.

## Historical pre-correction checkpoint (retained)

The previous Phase 7 exact-result checkpoint was recorded at baseline
`b6b2510820f5ab8f0af5569a2fc18b4ee4b2f892`. That corrective closure covered canonical validation,
exact fixture equality, and provider recheck facts. The current closure builds on that work,
adding atomic-race publication, compatibility lock, trusted scope, and extended T145 coverage.
Both historical records are retained.
