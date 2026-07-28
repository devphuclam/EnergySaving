# Phase 5 Activation Checkpoint (T107)

## Result commit identity

Working tree at parent baseline `b1270473ec63ab432affeeb98016c661b81a42e9` with all Phase 5
corrections applied via the micro-closure. Clean worktree; no unrelated tracked changes.

## Changed files (transaction-safety micro-closure)

- `src/BuildingBlocks/Persistence/HostTransactionCoordinator.cs` — CommitAsync catch uses `CancellationToken.None` for rollback (not caller `ct`), LockAsync enforces canonical order via `(int)target + 1`, BeginAsync defers `_begun=true` until after backend succeeds; added `IsBegun`, `RegisteredTargets`, `HasParticipant`
- `tests/Unit/Fakes/FakeAtomicBackend.cs` — added `FailOnBegin`, `FailOnRollback`; `RollbackAsync` increments count before FailOnRollback check
- `tests/Unit/Organization/PointActivationTransactionTests.cs` — 19 cases (was 17), 66 checks; added `LockOrderCanonical` (positive nine-target + 6 negatives), `RollbackFailurePreservesCommitException`, `BeginFailureSafety`; renamed `AssertionCount`→`CompositeCheckCount`
- `tests/Unit/Organization/PointActivationTests.cs` — renamed `AssertionCount`→`CompositeCheckCount`; 52 cases, 52 checks
- `tests/Integration/Organization/PointActivationTransactionTests.cs` — renamed `AssertionCount`→`CompositeCheckCount`; 6 cases, 30 checks; AtomicCommitFailure verifies workspace/rollback/commit counts
- `tests/Unit/Program.cs` — prints runtime TestCount/CompositeCheckCount
- `tests/Verification/architecture.tests.ps1` — T105 extended with `CancellationToken.None`, canonical lock order, begin-failure, `CompositeCheckCount`, exact RED commands, T106/T107 rewritten findings
- `specs/002-asset-simulator-latest/checklists/phase-05-review.md` — T106 with 14 findings (F01–F14)
- `specs/002-asset-simulator-latest/checklists/phase-05-activation.md` — T107 (this file)
- `specs/002-asset-simulator-latest/checklists/phase-05-red.md` — post-hoc RED evidence (9 failures)

## Post-hoc RED output

Baseline: `b1270473`. Tests against buggy coordinator (CommitAsync catch uses caller ct,
LockAsync validates only `expectedOrder > _lastOrder`, BeginAsync sets `_begun=true` before
backend succeeds).

Build: `dotnet build .\IUMP.slnx --no-restore` → exit 0

```
T094: cases=52; checks=52; failures=0
T095: cases=19; checks=67; failures=9
T096: cases=1; failures=0
T103: cases=6; checks=30; failures=0
FAILURES:
  canonical: Point-first: must throw
  canonical: IAM order=2: must throw
  canonical: skip to Metric: must throw
  canonical: skip Area: must throw
  canonical: after Integration: must throw
  cancel: rollback=1: expected 1, got 0
  cancel: workspace null: must be removed
  begin-fail: _begun false: must be false
  begin-fail: rollback not called after dispose: must not call rollback
```

9 natural failures from 3 production defects:
- Defect A: CommitAsync catch uses caller `ct` for rollback (2 failures)
- Defect B: LockAsync validates only `expectedOrder > _lastOrder` (5 failures)
- Defect D: BeginAsync sets `_begun=true` before backend succeeds (2 failures)

No sabotage, placeholder, database/container/secret use.

## Runtime test counts (GREEN)

All tests PASS with fixed coordinator:

| Test | Cases | Checks | Failures |
|---|---|---|---|
| T094 | 52 | 52 | 0 |
| T095 | 19 | 66 | 0 |
| T096 | 1 | — | 0 |
| T103 | 6 | 30 | 0 |

T079: 87 assertions, T080: 62 assertions, T071: 19 tests/39 assertions, T088: 24 scenarios/24 assertions — all pass.

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
CommitAsync throws `OperationCanceledException`, rollback is called once (with
`CancellationToken.None`, not the cancelled caller token).

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
| T095 | PASS (19 cases, 0 failures) |
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
| T106 | PASS (0 Critical, 0 High; 14 findings F01–F14 resolved) |
| T107 | PASS |

**PASS 13, BLOCKED 1, FAIL 0, runnable NOT_RUN 0**.

## Capability and progression

- PostgreSQL capability: AVAILABLE at `127.0.0.1:5433/iump_dev`; no database mutation.
- Port `5432`: not used.
- Ready for Phase 6: **YES**.
- Release-ready: **NO** (T104 blocked).
- T108 and later: **not executed**.

Stop after T107.
