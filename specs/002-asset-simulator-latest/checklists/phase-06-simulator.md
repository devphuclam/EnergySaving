# Phase 6 Final Scope-and-Isolation Checkpoint (T130)

## Scope and identity

- Repository: `devphuclam/EnergySaving`
- Parent baseline: `651c22c9db04f9cb01091f9873558f5be50530a8`
- Scope: Phase 6 final scope-and-isolation closure through T130 only.
- Result-commit semantics: no commit was created; the result is the reviewed working-tree delta
  from the exact parent baseline.
- Stop: T130. T131 and later were not executed.

## Exact changed files

- `specs/002-asset-simulator-latest/checklists/phase-06-red.md`
- `specs/002-asset-simulator-latest/checklists/phase-06-review.md`
- `specs/002-asset-simulator-latest/checklists/phase-06-simulator.md`
- `specs/002-asset-simulator-latest/contracts/simulator.md`
- `src/Modules/Acquisition/Application/ProductionAttemptService.cs`
- `src/Modules/Acquisition/Application/RunCommands.cs`
- `tests/Unit/Acquisition/RunControlTests.cs`
- `tests/Unit/Fakes/FakeAcquisitionRunRepositories.cs`
- `tests/Unit/Worker/ProductionDispatchTests.cs`
- `tests/Verification/architecture.tests.ps1`

No feature spec, package/project reference, database migration, PostgreSQL adapter, API/Worker
composition root, Phase 7 source/test/evidence, `.env`, or local database information file changed.

## Natural RED

Label: **Post-hoc reproduced Phase 6 scope-and-isolation RED**

- Worktree:
  `C:\Users\TD-999\Research\EnergySaving\Codespace-phase6-scope-isolation-red`
- Baseline: `651c22c9db04f9cb01091f9873558f5be50530a8`
- Test/static/contract-only files:
  - `specs/002-asset-simulator-latest/contracts/simulator.md`
  - `tests/Verification/phase06-scope-isolation-red.tests.ps1`

```text
dotnet build IUMP.slnx --no-restore
Exit code: 0

powershell -NoProfile -File .\tests\Verification\phase06-scope-isolation-red.tests.ps1
Exit code: 1
```

The focused run reproduced six exact failures: global Stop from a Point-specific error,
current-vs-pinned Site authorization, unnecessary existing-Run snapshot validation, Source
mismatch, missing T110/T111 cases, and missing T129 contradiction evidence. No production
sabotage, restore/download, database/migration, container, secret or port `5432` activity occurred.

## Runtime evidence

Final unit execution:

```text
T110: cases=63; checks=189; failures=0
T111: cases=9; checks=38; failures=0
PASS: all tests
```

Counters are actual runtime increments: one `TestCount` per executed scenario and `CheckCount` only
through assertion helpers.

### Existing Run and Source consistency

- Existing Run lookup occurs before current snapshot resolution/validation.
- Authorization uses distinct pinned Run-Point Site IDs.
- Administrator and Site-A pinned Engineer obtain the same Run ID/version.
- Engineer scoped only to changed current Site B receives `NOT_FOUND`.
- Existing Running Start resolves/rechecks no snapshot, initializes no generator, starts no
  transaction and emits no event.
- Existing Paused Start returns `PRECONDITION_FAILED` with the same short-circuit properties.
- Snapshot Source mismatch returns `NOT_FOUND`; no Run, Run-Point, event, PRNG, recheck or
  transaction occurs for either Source.

### Multi-Point owner isolation

- Point-specific `MAPPING_INACTIVE`: both due Points are considered independently; Point A reports
  the exact error, has no attempt, generation, identity, dispatch or finalization, preserves
  PRNG/cursor and releases its lease. Point B reserves/dispatches/finalizes once. The Run remains
  Running with Generated `1`, Accepted `1`, Rejected `0`; no global Stop event is emitted.
- Source-wide `SOURCE_INACTIVE`: the two-Point Run becomes Stopped with the stable error, neither
  Point produces or changes counters, and exactly one safe `SimulatorRunStateChanged.v1` Stop event
  is committed.

### Contract reconciliation

Only the conflicting Simulator owner-state section changed. It now distinguishes Source-wide Stop
from Mapping/Point/ancestor Point isolation and retains pinned Mapping/no-silent-switch rules. The
feature spec remains unchanged.

## T128, reviews and harness

- T128 architecture/static checks: PASS.
- T128 guards existing-run-first order, pinned Site authorization, Source equality, T110/T111
  coverage, Point-specific-vs-Source-wide paths, contract wording and canonical T131+ Phase 7
  source/test/evidence paths.
- Standards review: unresolved Critical `0`, High `0`.
- Specification review: unresolved Critical `0`, High `0`; scope creep `0`.
- Fast harness: exit `0`, `PASS=8`.
- Fresh Full harness:

```text
& .\scripts\harness.ps1 -Mode Full -Feature 002-asset-simulator-latest
Exit code: 20
PASS=10
BLOCKED=3
FAIL=0
NOT_RUN=0
```

The three non-passing capability checks were recorded exactly:

- database: `BLOCKED_BY_MISSING_TOOL` / `BLK-ENV-002` because `psql` is unavailable;
- CI: `BLOCKED_BY_COMPANY_APPROVAL` / `BLK-ENV-003`;
- container target: `BLOCKED_BY_COMPANY_APPROVAL` / `BLK-ENV-004`.

None is represented as PASS.

## T108-T130 ledger

| Tasks | Result |
|---|---|
| T108-T124 | PASS |
| T125 | BLOCKED_BY_PACKAGE_POLICY |
| T126 | BLOCKED_BY_PACKAGE_POLICY |
| T127 | BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE |
| T128 | PASS |
| T129 | PASS (Critical 0, High 0) |
| T130 | PASS |

Final Phase 6 ledger: **PASS 20, BLOCKED 3, FAIL 0, runnable NOT_RUN 0**.

## Capability and progression

- Database capability: `AVAILABLE` at approved `127.0.0.1:5433/iump_dev`.
- Database-access blocker count: `0`.
- Database mutation: `NOT_RUN`.
- `psql`: `BLOCKED_BY_MISSING_TOOL`.
- Port `5432` contacted: **NO**.
- Ready for Phase 7: **YES**, only by a future explicit invocation.
- Release-ready: **NO**.

Stop after T130.

## 2026-07-30 runtime-resolution addendum

T125 and T126 are now PASS with Run/attempt adapters, API/Worker registration, build, and runtime
resolution. T127 is `RUNNABLE_NOW` but remains unchecked because its full unique-slot,
lease-reclaim, and cursor/PRNG/counter atomicity PostgreSQL suite was not executed. Basic Run
persistence passed in the local runtime verifier; that narrower evidence is not treated as T127.
