# Phase 5 Activation Checkpoint (T107)

## Scope and baseline

- Repository: `devphuclam/EnergySaving`
- Feature: `specs/002-asset-simulator-latest/`
- Parent baseline: `0c1b4f51f0dc476d3f6255328c06ae40e75d0611`
- Scope: T094-T107 only; stop before T108.

## Changed files

- `src/BuildingBlocks/Persistence/IHostTransactionBackend.cs` (new)
- `src/BuildingBlocks/Persistence/IHostTransaction.cs` (rewritten)
- `src/BuildingBlocks/Persistence/IHostTransactionParticipant.cs` (simplified)
- `src/BuildingBlocks/Persistence/HostTransactionCoordinator.cs` (rewritten)
- `src/Modules/Organization/Application/ActivateMeasurementPoint.cs` (version checks)
- `tests/Unit/Fakes/FakeAtomicBackend.cs` (new)
- `tests/Unit/Fakes/NullBackend.cs` (new)
- `tests/Unit/Fakes/FakeActivationOrganizationParticipant.cs` (rewritten)
- `tests/Unit/Fakes/FakeActivationProviders.cs` (simplified)
- `tests/Unit/Fakes/FakeTransactionalOutboxWriter.cs` (rewritten)
- `tests/Unit/Fakes/FakePointActivationProviderFactory.cs` (rewritten)
- `tests/Unit/Fakes/FakeOrganizationRepositories.cs` (helpers added)
- `tests/Unit/Organization/PointActivationTests.cs` (rewritten T094)
- `tests/Unit/Organization/PointActivationTransactionTests.cs` (rewritten T095)
- `tests/Integration/Organization/PointActivationTransactionTests.cs` (rewritten T103)
- `tests/Unit/Program.cs` (updated)
- `tests/Verification/architecture.tests.ps1` (updated T105)
- `specs/002-asset-simulator-latest/checklists/phase-05-review.md` (rewritten T106)
- `specs/002-asset-simulator-latest/checklists/phase-05-activation.md` (rewritten T107)
- `src/Modules/Organization/IUMP.Modules.Organization.csproj` (added references)

## Chronological RED output

Baseline `0c1b4f5` with test-only changes. Build exit 0.

```
T094: cases=50; failures=8
T095: cases=20; failures=0       (surface checks compile-fail counted at build)
T096: cases=1; failures=0
T103: cases=4; failures=3
...
FAILURES:
  owner UserVersion=0: owner failure got
  owner ScopeVersion=0: owner failure got
  MetricVersion=0: expected METRIC_NOT_FOUND, got
  UnitVersion=0: expected UNIT_NOT_FOUND, got
  CompatibilityVersion=0: expected UNIT_INCOMPATIBLE, got
  MappingVersion=0: expected MAPPING_MISSING, got
  SourceVersion=0: expected SOURCE_NOT_ACTIVE, got
  no IAM mutation: activation must not mutate IAM data.
  OutboxFailure: staged mutation count must be 0 after rollback
  StaleVersion: must be VERSION_CONFLICT, got
  StaleVersion: committed Point must not change after stale version
```

8 T094 + 3 T103 = 11 natural RED failures. No production sabotage. No `PHASE5_REQUIRED` trick.

## Runnable GREEN evidence

Debug build: exit `0`, `0` warnings, `0` errors.

```
T094: cases=50; failures=0
T095: cases=20; failures=0
T096: cases=1; failures=0
T103: cases=6; failures=0
T071: tests=19; assertions=39; failures=0
T088: scenarios=24; assertions=24; failures=0
PASS: all tests
```

## Pre-commit invisibility evidence

T095 proves:
- `PreCommitPointInvisible` — committed Point status unchanged before host commit
- `PreCommitLifecycleInvisible` — committed lifecycle count 0 before host commit
- `PreCommitOutboxInvisible` — `Backend.CommittedEnvelopes` count 0 before host commit

## One-backend commit/rollback evidence

T095 proves:
- `OneBackendCommit` — exactly one `Backend.CommitAsync` call on success
- `OneBackendRollback` — exactly one `Backend.RollbackAsync` call on rollback
- `NoParticipantCommitSurface` — `IHostTransactionParticipant` has no `CommitAsync`/`RollbackAsync`

## Atomic commit-failure evidence

T103 `AtomicCommitFailure` case proves:
- Point version unchanged after failed commit
- Point status unchanged after failed commit
- Lifecycle count unchanged after failed commit
- Outbox count unchanged after failed commit

## Provider-version validation evidence

`ActivateMeasurementPoint.ValidateOwner` rejects `UserVersion <= 0` and `ScopeVersion <= 0`.
`ActivateMeasurementPoint.ValidateCatalog` rejects `MetricVersion <= 0`, `UnitVersion <= 0`, `CompatibilityVersion <= 0`, `MappingVersion <= 0`, `SourceVersion <= 0`, blank `CompatibilityIdentity`, and non-Active `CompatibilityStatus`.

## Architecture verification

`& .\tests\Verification\architecture.tests.ps1` returned:

```
PASS: architecture boundary contract
```

## T094-T107 ledger

| Task | Result |
|---|---|
| T094 | PASS |
| T095 | PASS |
| T096 | PASS |
| T097 | PASS |
| T098 | PASS |
| T099 | PASS |
| T100 | PASS |
| T101 | PASS |
| T102 | PASS |
| T103 | PASS |
| T104 | BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE |
| T105 | PASS |
| T106 | PASS |
| T107 | PASS |

Counts: **PASS 13, BLOCKED 1, FAIL 0, runnable NOT_RUN 0**.

## Capability and progression

- PostgreSQL capability: AVAILABLE/VERIFIED at `127.0.0.1:5433/iump_dev`; no database mutation in this invocation.
- Port `5432`: not used.
- Ready for Phase 6: **YES** (runnable Phase 5 scope complete).
- Release-ready: **NO** (T104 remains blocked and this is not a release checkpoint).
- T108 and later: **not executed**.

Stop after T107.
