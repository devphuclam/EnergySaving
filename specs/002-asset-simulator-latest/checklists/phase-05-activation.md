# Phase 5 Activation Checkpoint (T107)

## Scope and baseline

- Repository: `devphuclam/EnergySaving`
- Parent baseline: `cb5b6b46c10b90be5501e6c9ff9f3dc47522fd89`
- Closure scope: safe rollback before successful begin, T095 retry, T103 BeginFailure, T105-T107 evidence.
- Stop: T107. T108 and later were not executed.

## Changed files for this closure

- `src/BuildingBlocks/Persistence/HostTransactionCoordinator.cs`
- `tests/Unit/Organization/PointActivationTransactionTests.cs`
- `tests/Unit/Fakes/FakePointActivationProviderFactory.cs`
- `tests/Integration/Organization/PointActivationTransactionTests.cs`
- `tests/Verification/architecture.tests.ps1`
- `specs/002-asset-simulator-latest/checklists/phase-05-red.md`
- `specs/002-asset-simulator-latest/checklists/phase-05-review.md`
- `specs/002-asset-simulator-latest/checklists/phase-05-activation.md`

## Runtime evidence

Debug build and focused run both exited `0` after the correction:

```text
T079: assertions=87; failures=0
T080: assertions=62; failures=0
T094: cases=52; checks=52; failures=0
T095: cases=20; checks=75; failures=0
T096: cases=1; failures=0
T103: cases=7; checks=40; failures=0
T071: tests=19; assertions=39; failures=0
T088: scenarios=24; assertions=24; failures=0
PASS: all tests
```

`& .\tests\Verification\architecture.tests.ps1` returned `PASS: architecture boundary contract`.

Fresh harness verification after the final documentation/static-check correction:

```text
.\scripts\harness.ps1 -Mode Fast -Feature 002-asset-simulator-latest: exit 0
Harness Fast summary: PASS=8

.\scripts\harness.ps1 -Mode Full -Feature 002-asset-simulator-latest: exit 20
Harness Full summary: PASS=10, BLOCKED_BY_MISSING_TOOL=1, BLOCKED_BY_COMPANY_APPROVAL=2
```

Full-mode blocked checks are explicitly not passes: database check `BLK-ENV-002` is
`BLOCKED_BY_MISSING_TOOL` because `psql` is unavailable; CI `BLK-ENV-003` and container target
`BLK-ENV-004` are `BLOCKED_BY_COMPANY_APPROVAL`. These environment checks do not classify the
approved PostgreSQL capability as `BLOCKED_BY_DATABASE_ACCESS`.

## Begin-failure evidence

- Direct `RollbackAsync` after failed begin returns safely; backend rollback remains `0`.
- Failed begin preserves `IsBegun=false`, `IsCompleted=false`, `TransactionId=Guid.Empty`, and no workspace.
- T103 `BeginFailure` executes the real `ActivateMeasurementPoint.ExecuteAsync` and returns
  `TRANSACTION_ROLLED_BACK`; Point status/version, lifecycle, and outbox remain unchanged;
  backend CommitCount and RollbackCount remain `0`; disposal is safe.
- T095 same-coordinator retry changes `FailOnBegin` to false, begins successfully with a non-empty
  TransactionId, and performs exactly one backend rollback.
- Repeated rollback after a successful rollback is a no-op.

## Ledger and decision

| Task | Result |
|---|---|
| T094 | PASS |
| T095 | PASS (20 cases, 75 checks) |
| T096 | PASS |
| T097 | PASS |
| T098 | PASS |
| T099 | PASS |
| T100 | PASS |
| T101 | PASS |
| T102 | PASS |
| T103 | PASS (7 cases, 40 checks) |
| T104 | BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE |
| T105 | PASS |
| T106 | PASS (0 unresolved Critical/High) |
| T107 | PASS |

**PASS 13, BLOCKED 1, FAIL 0, runnable NOT_RUN 0**.

- PostgreSQL capability: AVAILABLE at `127.0.0.1:5433/iump_dev`; no database mutation was run.
- Port `5432`: not used.
- Ready for Phase 6: **YES**.
- Release-ready: **NO** while T104 remains blocked.

Stop after T107.
