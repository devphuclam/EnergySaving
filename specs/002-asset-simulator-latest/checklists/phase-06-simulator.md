# Phase 6 Simulator Run and Worker Checkpoint (T130)

## Scope, baseline and result identity

- Repository: `devphuclam/EnergySaving`
- Parent baseline and current `HEAD`: `05cb231066655bd5259e4dc2a478b8dc44c52c05`
- Scope executed: T108 through T130 only.
- Result-commit semantics: no commit was created; the Phase 6 result is the reviewed working-tree
  delta from the exact parent baseline. A future commit identity must name this exact source state.
- Explicit stop: T130. T131 and later were not executed.

## Exact changed files

- `database/migrations/0007_acquisition_run.sql`
- `docs/blocker-report.md`
- `specs/002-asset-simulator-latest/tasks.md`
- `specs/002-asset-simulator-latest/checklists/phase-06-red.md`
- `specs/002-asset-simulator-latest/checklists/phase-06-review.md`
- `specs/002-asset-simulator-latest/checklists/phase-06-simulator.md`
- `src/Modules/Acquisition/IUMP.Modules.Acquisition.csproj`
- `src/Modules/Acquisition/Application/ProductionAttemptService.cs`
- `src/Modules/Acquisition/Application/RunCommands.cs`
- `src/Modules/Acquisition/Contracts/ProductionAttemptContracts.cs`
- `src/Modules/Acquisition/Contracts/RunPersistenceContracts.cs`
- `src/Modules/Acquisition/Domain/DeterministicGenerator.cs`
- `src/Modules/Acquisition/Domain/MeasurementIdentity.cs`
- `src/Worker/IUMP.Worker.csproj`
- `src/Worker/SimulatorProductionWorker.cs`
- `tests/Integration/Acquisition/RunAttemptRepositoryTests.cs`
- `tests/Unit/IUMP.Tests.Unit.csproj`
- `tests/Unit/Program.cs`
- `tests/Unit/Acquisition/AcquisitionEventTests.cs`
- `tests/Unit/Acquisition/DeterministicGeneratorVectorTests.cs`
- `tests/Unit/Acquisition/MeasurementIdentityTests.cs`
- `tests/Unit/Acquisition/ProductionAttemptTests.cs`
- `tests/Unit/Acquisition/RunControlTests.cs`
- `tests/Unit/Fakes/FakeAcquisitionRunRepositories.cs`
- `tests/Unit/Worker/ProductionDispatchTests.cs`
- `tests/Verification/architecture.tests.ps1`

No API/Worker composition root, PostgreSQL adapter, package reference, Phase 7 source, `.env`, or
local database information file changed.

## Test-first evidence

Before production implementation:

```text
dotnet build tests/Unit/IUMP.Tests.Unit.csproj --no-restore --configuration Debug
Exit 0; 0 warnings; 0 errors.

dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj --no-build --configuration Debug
Exit 1.
```

The true RED run reported missing Phase 6 behavior only:

```text
T108 3 cases / 4 checks / 4 failures
T109 3 cases / 3 checks / 3 failures
T110 4 cases / 4 checks / 4 failures
T111 3 cases / 3 checks / 3 failures
T112 4 cases / 4 checks / 4 failures
T113 4 cases / 4 checks / 4 failures
```

The exact literal failures and compile-shim statement are retained in `phase-06-red.md`. There was
no restore/download, database connection/mutation, container, secret, or port 5432 contact.

## GREEN build and executable evidence

Fresh Debug and Release builds both exited `0` with zero warnings/errors. Debug and Release unit
executables both exited `0` with:

```text
T079: assertions=87; failures=0
T080: assertions=62; failures=0
T094: cases=52; checks=52; failures=0
T095: cases=20; checks=75; failures=0
T096: cases=1; failures=0
T103: cases=7; checks=40; failures=0
T108: cases=10; checks=19; failures=0
T109: cases=7; checks=12; failures=0
T110: cases=8; checks=27; failures=0
T111: cases=8; checks=34; failures=0
T112: cases=7; checks=25; failures=0
T113: cases=4; checks=14; failures=0
T071: tests=19; assertions=39; failures=0
T088: scenarios=24; assertions=24; failures=0
T124: scenarios=8; assertions=28; failures=0
PASS: all tests
```

## Behavioral evidence

- T108 literal initial state:
  `032ba308f46f1f8e4f8167f77e7b0514000000000000000000`.
- Constant output: `12.5000`, zero draws, unchanged state.
- Normal first: `11.6519`, two draws, state
  `ed99faae39338fb74f8167f77e7b0514013f80c23bc5fbfb3f`.
- Normal restart: `17.9149`, zero draws, state
  `ed99faae39338fb74f8167f77e7b0514000000000000000000`.
- Serialization is exactly 25 bytes; malformed length/flag/increment and unknown algorithm
  ID/version are rejected; literal cached-spare cases prove ties-to-even and round-then-clamp.
- T109 literal UUIDv5 identities:
  `e118cea2-3d28-5dd4-9726-b3d7d4425ea4`,
  `bf5a3f14-0774-5b13-88b1-fa782872b01c`, and
  `442c323f-dddb-516b-96ff-88dab38133ce`.
- Start authorizes global Administrator or Engineer scoped to every trusted Site. Failure and
  provider drift publish no Run, Run-Point, or event.
- Start deterministically locks Site/Area/Asset/Point, Catalog and Acquisition, then rechecks
  provider state with the active transaction.
- Run pins source/configuration/Point/Mapping/Metric/Unit versions, 25-byte PRNG state, zero cursor,
  injected next-due time and zero counters for every selected Point.
- Lifecycle passes Running -> Paused -> Running/Stopped and Paused -> Stopped rules; Stopped is
  terminal; no-op/stale behavior is explicit; pause/resume preserves PRNG/cursor; new Start after
  Stop creates a new Run at sequence zero.
- Restart recovery returns persisted Running Runs only.
- Lease claim/renew/release and expiry/reclaim pass. A delayed Telemetry dispatch renews the
  versioned lease and blocks a competing Worker; cancellation releases with a non-cancelled cleanup
  token; `LEASE_LOST` is explicit.
- Existing Pending is loaded before owner eligibility, dispatched unchanged, and never invokes the
  generator or identity factory or changes PRNG/cursor/Generated.
- New reservation inserts immutable Pending and atomically advances PRNG/cursor/Generated once.
  Rollback publishes none; uniqueness loser reloads the winner without state/counter mutation.
- Telemetry dispatch occurs outside reservation/finalization transactions. One Point failure does
  not block an unrelated due Point.
- First finalization increments exactly one Accepted/Rejected counter. Same replay is a no-op;
  conflicting replay is an invariant error; Duplicate retains its original classification.
- Start/Pause/Resume/Stop owner events use `SimulatorRunStateChanged.v1`, safe allowlisted
  Before/After and sorted trusted Site IDs. Owner-drift Stop stages its event atomically.
- Worker consumes only `ISimulatorProductionCoordinator` and emits structured, correlation-aware
  lifecycle and Point failure logs.

## Migration and provider-neutral adapter evidence

- `0007_acquisition_run.sql` statically defines Acquisition-owned Run, Run-Point and production
  attempt tables, same-schema FKs, current/due/lease/reconciliation indexes, 25-byte state,
  nonnegative/idempotent counters, slot and Measurement uniqueness, terminal consistency, and
  immutable payload enforcement.
- Static review found no cross-schema FK, `CREATE EXTENSION`, credential, adapter, or database
  execution claim.
- T124 executed 8 provider-neutral scenarios and 28 assertions with 0 failures. The runner has no
  fake cast/concrete fake reference, Skip/TODO, credential, fallback connection, or PostgreSQL PASS
  claim.
- Migration `0007` execution: `NOT_RUN`.

## Architecture, policy and harness evidence

- `verification-contract.tests.ps1`: PASS, exit `0`.
- `repository-harness.tests.ps1`: PASS, exit `0`.
- `repository-policy.tests.ps1`: PASS, exit `0`.
- `repository-scope.tests.ps1`: PASS, exit `0`.
- `architecture.tests.ps1`: `PASS: architecture boundary contract`, exit `0`.
- Fast harness: exit `0`, `PASS=8`.
- Full harness: child exit `20`; `PASS=10`,
  `BLOCKED_BY_MISSING_TOOL=1`, `BLOCKED_BY_COMPANY_APPROVAL=2`.
- Full database check `BLK-ENV-002` is blocked because `psql` is unavailable. CI
  `BLK-ENV-003` and container target `BLK-ENV-004` are company-approval blockers. None is a PASS.
- Standards review: unresolved Critical `0`, High `0`.
- Specification review: unresolved Critical `0`, High `0`; scope creep `0`.

## T108-T130 ledger

| Task | Result |
|---|---|
| T108 | PASS |
| T109 | PASS |
| T110 | PASS |
| T111 | PASS |
| T112 | PASS |
| T113 | PASS |
| T114 | PASS |
| T115 | PASS |
| T116 | PASS |
| T117 | PASS |
| T118 | PASS |
| T119 | PASS |
| T120 | PASS |
| T121 | PASS |
| T122 | PASS |
| T123 | PASS (source/static only; not executed) |
| T124 | PASS (8 scenarios, 28 assertions) |
| T125 | BLOCKED_BY_PACKAGE_POLICY |
| T126 | BLOCKED_BY_PACKAGE_POLICY |
| T127 | BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE |
| T128 | PASS |
| T129 | PASS (Critical 0, High 0) |
| T130 | PASS |

Final Phase 6 ledger: **PASS 20, BLOCKED 3, FAIL 0, runnable NOT_RUN 0**.

## Capabilities and progression

- PostgreSQL capability: `AVAILABLE` at the approved `127.0.0.1:5433/iump_dev` target.
- Database-access blocker count: `0`.
- Package adapter capability: unavailable under approved package policy.
- `psql`: `BLOCKED_BY_MISSING_TOOL`.
- Package restore/download: none.
- Database connection/mutation: none in this phase.
- Migration `0007`: not executed.
- Port `5432` contacted: NO.
- `.env` and `IUMP_Local_Database_Connection_Info.md`: ignored and untracked.
- Ready for Phase 7: **YES** for the next explicit invocation.
- Technical demo readiness: deterministic Simulator, Run controls and provider-neutral Worker are
  test-harness ready; generated payloads are observable through the fake Telemetry sink.
- Live/browser demo readiness: **NO**. PostgreSQL-backed runtime remains blocked by adapters and
  packages; real Telemetry ingestion, Latest, Source Health, API and Web are absent.
- Release-ready: **NO** while mandatory package/tool/approval blockers and later phases remain.

Stop after T130.
