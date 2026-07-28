# Post-hoc Reproduced Phase 5 Begin-Failure RED

Repository: `devphuclam/EnergySaving`
Parent baseline: `cb5b6b46c10b90be5501e6c9ff9f3dc47522fd89`
Temporary native worktree: `C:\Users\TD-999\AppData\Local\Temp\iump-phase5-begin-red-cb5b6`

Only these test/static files were changed in the RED worktree:

- `tests/Unit/Organization/PointActivationTransactionTests.cs`
- `tests/Unit/Fakes/FakePointActivationProviderFactory.cs`
- `tests/Integration/Organization/PointActivationTransactionTests.cs`
- `tests/Verification/architecture.tests.ps1`

No production source was modified. No database, package restore, container, migration,
secret, or port `5432` activity occurred. Existing local ignored build metadata was copied into the
temporary worktree only so `--no-restore` could compile.

## Exact RED commands and exits

```text
dotnet build tests/Unit/IUMP.Tests.Unit.csproj --no-restore --configuration Debug
Exit code: **0** (Build succeeded; 0 warnings; 0 errors)

dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj --no-build --configuration Debug
Exit code: **1**
```

Focused run output:

```text
T079: assertions=87; failures=0
T080: assertions=62; failures=0
T094: cases=52; checks=52; failures=0
T095: cases=20; checks=75; failures=5
T096: cases=1; failures=0
T103: cases=7; checks=40; failures=4
T071: tests=19; assertions=39; failures=0
T088: scenarios=24; assertions=24; failures=0
FAILURES:
  begin-retry: pre-begin rollback safe: must not throw NullReferenceException
  begin-retry: no backend rollback before begin: must remain zero
  begin-fail: direct rollback safe: must not throw NullReferenceException
  begin-fail: rollback not called after direct rollback: must remain zero
  begin-fail: rollback not called after dispose: must not call rollback
  BeginFailure: must return a stable result, got NullReferenceException
  BeginFailure: must return TRANSACTION_ROLLED_BACK, got EXECUTION_EXCEPTION
  BeginFailure: backend rollback count must be 0 because begin created no transaction
  BeginFailure: host must remain unbegun, incomplete, and empty
RED_RUN_EXIT=1
```

Static command:

```text
& .\tests\Verification\architecture.tests.ps1
exit 1
T105 FAIL: RollbackAsync must guard before begin and null backend transaction.
```

This is the required **Post-hoc reproduced Phase 5 begin-failure RED**: the baseline forwards a
null backend transaction during direct/orchestrator rollback, and the corrected same-coordinator
retry and static guard checks were absent.
