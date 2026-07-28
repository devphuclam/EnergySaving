# Phase 5 Post-hoc Reproduced Micro-RED

Parent baseline: `50c4c311ebe874e4b9ae42161666a9dd6bddb7e9`.

## Reproduction method

The corrected tests (T094/T095/T103 with executable counters, SameTransactionId lock
acquisition, LockFailureRollback assertions, CancellationRollback with cancelled token,
RetryDelays with exact [50,150,450], and T103 AtomicCommitFailure assertions) were applied
against the unmodified production code at the baseline.

The production code's `HostTransactionCoordinator.CommitAsync` catch set `_completed = true`
before calling backend rollback, making the subsequent rollback a no-op. This is the only
production defect — no other production code was changed.

## Build

```
dotnet build .\IUMP.slnx --no-restore
Build succeeded. 0 Warning(s) 0 Error(s)
```

## Run (RED exit non-zero)

```
T094: cases=52; assertions=52; failures=0
T095: cases=17; assertions=48; failures=3
T096: cases=1; failures=0
T103: cases=6; assertions=30; failures=2
T071: tests=19; assertions=39; failures=0
T088: scenarios=24; assertions=24; failures=0
FAILURES:
  commit-fail: workspace null: workspace must be removed
  commit-fail: backend rollback=1: expected 1, got 0
  cancel: rollback=1: expected 1, got 0
  AtomicCommitFailure: workspace must be removed after commit failure
  AtomicCommitFailure: backend rollback must be called exactly once after commit failure, got 0
```

## Defects demonstrated

| # | Defect | Evidence |
|---|---|---|
| 1 | Commit failure does not invoke backend rollback | T095 commit-fail: rollback=1 expected, got 0 |
| 2 | Commit failure leaves workspace allocated | T095 AtomicCommitFailure workspace not null |
| 3 | T095 Cancel path does not rollback | T095 cancel: rollback=1 expected, got 0 |
| 4 | T103 AtomicCommitFailure workspace leak | T103 workspace not removed after failure |
| 5 | T103 AtomicCommitFailure no rollback | T103 backend rollback=0 after failure |

## What was not reproduced

The following defects existed in the original test code before the micro-closure
corrections and are not re-testable as production RED because they were test-side
only:

- T094 declared 50, executed 52 (fixed by runtime counters, no production dependency)
- T095 declared 20, executed 17 (same)
- SameTransactionId had no lock acquisition (fixed by test-only changes)
- RetryDelays had weak `clock.Count < 1` check (fixed by test-only changes)
- LockFailureRollback had no rollback assertion (fixed by test-only changes)
- CancellationRollback had no cancellation (fixed by test-only changes — the
  3rd RED failure above still shows the residual rollback-count defect)

## Clean evidence

- No database access, no package restore, no containers, no secrets.
- No production sabotage or PHASE5_REQUIRED placeholder was used.
- The single production defect reverted (coordinator CommitAsync catch order) is
  exactly the defect that was corrected.
