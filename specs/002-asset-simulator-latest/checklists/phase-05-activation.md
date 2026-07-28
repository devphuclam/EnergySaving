# Phase 5 Activation Checkpoint (T107)

## Result commit identity

Working tree at parent baseline `50c4c311ebe874e4b9ae42161666a9dd6bddb7e9` with all Phase 5
corrections applied and committed.

## Changed files

- `src/BuildingBlocks/Persistence/HostTransactionCoordinator.cs` — CommitAsync catch rolls back backend before `_completed=true`
- `src/BuildingBlocks/Persistence/IHostTransactionBackend.cs`
- `src/BuildingBlocks/Persistence/IHostTransaction.cs`
- `src/BuildingBlocks/Persistence/IHostTransactionParticipant.cs`
- `src/Modules/Organization/Application/ActivateMeasurementPoint.cs` — provider-version checks
- `src/Modules/Organization/IUMP.Modules.Organization.csproj` — project references
- `tests/Unit/Fakes/FakeAtomicBackend.cs`
- `tests/Unit/Fakes/NullBackend.cs`
- `tests/Unit/Fakes/FakeActivationOrganizationParticipant.cs`
- `tests/Unit/Fakes/FakeActivationProviders.cs`
- `tests/Unit/Fakes/FakeTransactionalOutboxWriter.cs`
- `tests/Unit/Fakes/FakePointActivationProviderFactory.cs`
- `tests/Unit/Fakes/FakeOrganizationRepositories.cs`
- `tests/Unit/Organization/PointActivationTests.cs` — runtime TestCount/AssertionCount, 52 cases
- `tests/Unit/Organization/PointActivationTransactionTests.cs` — runtime counters, 17 cases, fixed SameTransactionId/AtomicCommitFailure/LockFailure/Cancellation/RetryDelays
- `tests/Integration/Organization/PointActivationTransactionTests.cs` — runtime counters, 6 cases, AtomicCommitFailure workspace+rollback assertions
- `tests/Unit/Program.cs` — prints actual runtime counts
- `tests/Verification/architecture.tests.ps1` — T105 checks for new defect patterns
- `specs/002-asset-simulator-latest/checklists/phase-05-review.md` — T106
- `specs/002-asset-simulator-latest/checklists/phase-05-activation.md` — T107 (this file)
- `specs/002-asset-simulator-latest/checklists/phase-05-red.md` — post-hoc RED evidence

## Post-hoc RED output

Baseline: `50c4c311`. Tests against buggy coordinator (CommitAsync sets completed before rollback).

```
T094: cases=52; assertions=52; failures=0
T095: cases=17; assertions=48; failures=3
T096: cases=1; failures=0
T103: cases=6; assertions=30; failures=2
FAILURES:
  commit-fail: workspace null: workspace must be removed
  commit-fail: backend rollback=1: expected 1, got 0
  cancel: rollback=1: expected 1, got 0
  AtomicCommitFailure: workspace must be removed after commit failure
  AtomicCommitFailure: backend rollback must be called exactly once after commit failure, got 0
```

5 natural failures from exactly one production defect (coordinator rollback ordering). No
sabotage, no placeholder, no database/container/secret use.

## Runtime test counts (GREEN)

| Test | Cases | Assertions | Failures |
|---|---|---|---|
| T094 | 52 | 52 | 0 |
| T095 | 17 | 48 | 0 |
| T096 | 1 | — | 0 |
| T103 | 6 | 30 | 0 |

## Commit-failure cleanup evidence

T095 AtomicCommitFailurePublishesNone and T103 AtomicCommitFailure prove:
- Point unchanged (version, status preserved)
- Lifecycle unchanged (count preserved)
- Outbox unchanged (count preserved)
- Workspace removed (`GetWorkspace` returns null)
- Backend `RollbackCount == 1`
- Backend `CommitCount == 0`
- Coordinator `IsCompleted == true`

## Workspace cleanup evidence

T095 LockFailureRollback and CancellationRollback prove:
- Workspace null after rollback
- Backend `RollbackCount == 1`

## Exact retry trace

T095 RetryDelays proves: `[50, 150, 450]` with exactly 3 recorded delays.

## Cancellation result

T095 CancellationRollback passes a cancelled `CancellationToken` to `CommitAsync`.
CommitAsync throws `OperationCanceledException`, rollback is called once.

## Provider-version validation evidence

T094 verifies all 8 provider version checks (`UserVersion > 0`, `ScopeVersion > 0`,
`MetricVersion > 0`, `UnitVersion > 0`, `CompatibilityVersion > 0`, `MappingVersion > 0`,
`SourceVersion > 0`) plus `CompatibilityIdentity` nonblank and `CompatibilityStatus` exactly
`"Active"`.

## Architecture verification

`& .\tests\Verification\architecture.tests.ps1` → `PASS`

## T094–T107 ledger

| Task | Result |
|---|---|
| T094 | PASS (52 cases, 0 failures) |
| T095 | PASS (17 cases, 0 failures) |
| T096 | PASS (1 case, 0 failures) |
| T097 | PASS |
| T098 | PASS |
| T099 | PASS |
| T100 | PASS |
| T101 | PASS |
| T102 | PASS |
| T103 | PASS (6 cases, 0 failures) |
| T104 | BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE |
| T105 | PASS |
| T106 | PASS (0 Critical, 0 High) |
| T107 | PASS |

**PASS 13, BLOCKED 1, FAIL 0, runnable NOT_RUN 0**.

## Capability and progression

- PostgreSQL capability: AVAILABLE at `127.0.0.1:5433/iump_dev`; no database mutation.
- Port `5432`: not used.
- Ready for Phase 6: **YES**.
- Release-ready: **NO** (T104 blocked).
- T108 and later: **not executed**.

Stop after T107.
