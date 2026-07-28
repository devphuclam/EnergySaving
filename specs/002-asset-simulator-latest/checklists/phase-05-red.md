# Phase 5 Post-hoc Reproduced Transaction-Safety RED

Parent baseline: `b1270473ec63ab432affeeb98016c661b81a42e9`.

## Reproduction method

Corrected tests (T094/T095/T103 with CompositeCheckCount, canonical lock-order negative
tests, cancellation workspace assertions, RollbackFailure, BeginFailureSafety) applied
against the unmodified production coordinator at the baseline.

The only production differences between baseline and fixed:
- baseline `CommitAsync` catch uses caller `ct` for rollback, not `CancellationToken.None`
- baseline `LockAsync` validates only `expectedOrder > _lastOrder`, not canonical target
- baseline `BeginAsync` sets `_begun = true` before backend `BeginAsync` succeeds

## Build command and exit

```
dotnet build .\IUMP.slnx --no-restore
```
Exit code: **0** (0 warnings, 0 errors)

## Focused run command and exit

```
dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-build
```
Exit code: **1** (non-zero)

## Run output

```
T094: cases=52; checks=52; failures=0
T095: cases=19; checks=67; failures=9
T096: cases=1; failures=0
T103: cases=6; checks=30; failures=0
PASS: all tests (non-Phase-5 tests pass)
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

## Defects demonstrated

| # | Defect | Evidence |
|---|---|---|
| 1 | Cancelled commit rollback uses caller `ct` → fails | T095 cancel: RollbackCount=0, workspace not removed |
| 2 | LockAsync does not validate canonical target order | T095: 5 wrong-target cases accepted (Point-first, IAM order=2, skip Metric, skip Area, after Integration) |
| 3 | BeginAsync sets `_begun=true` before backend succeeds | T095 begin-fail: IsBegun true after failure, RollbackAsync called with null `_innerTx` |
| 4 | T094 reports composite checks as "assertions" | T094 output: `checks=52` (not `assertions=52`) — terminology corrected in GREEN |

## Clean evidence

- No database access, package restore, containers, or secrets.
- No production sabotage.
- Only one reverted file (coordinator).
- Test/static changes are the corrected versions committed in the fix.
