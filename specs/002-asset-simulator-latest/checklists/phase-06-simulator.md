# Phase 6 Corrective Simulator Run and Worker Checkpoint (T130)

## Scope and identity

- Repository: `devphuclam/EnergySaving`
- Parent baseline: `89b32b7595f03fc90145f993a8ad77a61343433d`
- Scope executed: Phase 6 corrective convergence through T130 only.
- Result-commit semantics: no commit was created. The result is the reviewed working-tree delta
  from the exact parent baseline; a later commit identity must name this exact corrected source
  state.
- Explicit stop: T130. T131 and later were not executed.

## Exact corrected files

- `database/migrations/0007_acquisition_run.sql`
- `specs/002-asset-simulator-latest/checklists/phase-06-red.md`
- `specs/002-asset-simulator-latest/checklists/phase-06-review.md`
- `specs/002-asset-simulator-latest/checklists/phase-06-simulator.md`
- `src/Modules/Acquisition/Application/ProductionAttemptService.cs`
- `src/Modules/Acquisition/Application/RunCommands.cs`
- `src/Modules/Acquisition/Contracts/ProductionAttemptContracts.cs`
- `src/Modules/Acquisition/Contracts/RunPersistenceContracts.cs`
- `tests/Integration/Acquisition/RunAttemptRepositoryTests.cs`
- `tests/Unit/Acquisition/AcquisitionEventTests.cs`
- `tests/Unit/Acquisition/DeterministicGeneratorVectorTests.cs`
- `tests/Unit/Acquisition/MeasurementIdentityTests.cs`
- `tests/Unit/Acquisition/ProductionAttemptTests.cs`
- `tests/Unit/Acquisition/RunControlTests.cs`
- `tests/Unit/Fakes/FakeAcquisitionRunRepositories.cs`
- `tests/Unit/Worker/ProductionDispatchTests.cs`
- `tests/Verification/architecture.tests.ps1`

No package/project reference, API/Worker `Program.cs`, PostgreSQL adapter, Phase 7 source, `.env`, or
local database information file changed.

## Post-hoc reproduced Phase 6 invariant RED

- Worktree:
  `C:\Users\TD-999\Research\EnergySaving\Codespace-phase6-corrective-red`
- Baseline: `89b32b7595f03fc90145f993a8ad77a61343433d`
- Test/static-only change:
  `tests/Verification/phase06-corrective-red.tests.ps1`

```text
dotnet build IUMP.slnx --no-restore
Exit code: 0

powershell -NoProfile -File .\tests\Verification\phase06-corrective-red.tests.ps1
Exit code: 1
```

The focused run produced the exact 12 rejected invariant failures listed in `phase-06-red.md`. No
production sabotage, restore/download, database access/mutation, migration execution, container,
secret read, or port `5432` contact occurred.

## Runtime scenario and assertion counts

The final unit executable exited `0`:

```text
T108: cases=13; checks=19; failures=0
T109: cases=12; checks=12; failures=0
T110: cases=50; checks=150; failures=0
T111: cases=8; checks=32; failures=0
T112: cases=12; checks=31; failures=0
T113: cases=4; checks=14; failures=0
T124: scenarios=37; assertions=55; failures=0
PASS: all tests
```

T108-T113 initialize counters to zero, increment `TestCount` once at each executed scenario
boundary, and increment `CheckCount` only through the assertion helper.

## Corrective behavioral evidence

- Algorithm ID is exactly `IUMP-DETERMINISTIC-V1`; version `0`, version `2`, and unknown IDs return
  `CONFIGURATION_INVALID`.
- Only `Constant` and `Normal` scenarios are accepted. Unknown enum values return
  `CONFIGURATION_INVALID` before PRNG initialization, Run ID creation, transaction begin or
  repository/event mutation.
- T110 covers empty Source/Configuration/Point/Mapping/Asset/Metric/Unit identities, blank
  Site/Area/Unit, duplicate Point/Mapping, all required Source/Mapping/Point/ancestor statuses and
  effective dates, every provider-version family, Operator/Manager/Viewer/inactive caller denial,
  one-Site and all-sites Engineer success, missing-scope `NOT_FOUND`, atomic invalid multi-Point
  Start, Paused-to-Stopped, and stable repeated Running Start.
- Every rejected T110 scenario proves no Run, zero committed Run-Point records, no event, no active
  transaction and no PRNG initialization.
- `StageReservationAsync` takes a mutable-only transition with expected Run, Point-state and cursor
  versions. Run/Point identity and provider snapshots are never accepted as replacement values.
- T124 executes rejection attempts for every pinned field (`run_id`, `point_id`,
  `point_version_at_start`, Mapping/Metric/Unit/Source versions and identities, Site and Area) and
  proves no committed change.
- Pending insertion wins before state staging. A simulated competing winner independently commits
  exactly one Pending plus 25-byte resulting PRNG state, cursor `1`, Generated `1`, Run version `2`,
  Run-Point version `2`, and the next due time. The loser rolls back without a second advancement.
  Accepted finalization ends at Generated `1`, Accepted `1`, Rejected `0`.
- Terminal validation accepts consistent Accepted, Rejected and Duplicate-original Accepted or
  Rejected metadata. Mismatched, unknown, Latest-invalid and rejection-code-invalid results fail
  with `TERMINAL_RESULT_INVALID`, leaving Pending, completion time, attempt version and counters
  unchanged.
- Attempt payload mutation and finalization commit failure are rejected/rolled back; optimistic
  Run-Point version conflict commits nothing.

## Migration and static evidence

- Migration `0007` remains source-only and unexecuted.
- It contains a Run-Point trigger covering all pinned columns and only permits approved mutable
  cursor/PRNG/due/lease/version transitions.
- NULL-safe terminal-pair constraints enforce Accepted, Rejected, Duplicate and Pending metadata.
- It contains no cross-schema FK, `CREATE EXTENSION`, credential or execution claim.
- T128 architecture/static verification exits `0` and detects all required negative shapes,
  including reserve-before-stage order, race atomicity, pinned/payload mutation coverage, terminal
  consistency, migration rules, runtime counters, missing T124 scenarios, and Phase 7 files.

## Reviews and harness

- Standards re-review: Critical `0`, High `0`; two accepted Low judgement-call smells.
- Specification re-review: Critical `0`, High `0`; scope creep `0`.
- Fast harness: exit `0`, `PASS=8`.
- Fresh Full harness: exit `20`; `PASS=10`,
  `BLOCKED_BY_MISSING_TOOL=1`, `BLOCKED_BY_COMPANY_APPROVAL=2`.
- Full database check remains `BLOCKED_BY_MISSING_TOOL` because `psql` is unavailable. CI and
  container-target checks remain `BLOCKED_BY_COMPANY_APPROVAL`. None is represented as PASS.

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

## Capability and progression decision

- Database capability: `AVAILABLE` at the approved `127.0.0.1:5433/iump_dev` target.
- Database-access blocker count: `0`.
- Database mutation: `NOT_RUN`.
- Migration `0007`: `NOT_RUN`.
- `psql`: `BLOCKED_BY_MISSING_TOOL`.
- Port `5432` contacted: **NO**.
- Ready for Phase 7: **YES**, only through a future explicit invocation.
- Release-ready: **NO**; package/tool/approval blockers and later phases remain.

Stop after T130.
