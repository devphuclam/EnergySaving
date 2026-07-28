# Phase 6 True RED Evidence (T114)

Repository: `devphuclam/EnergySaving`

Parent baseline: `05cb231066655bd5259e4dc2a478b8dc44c52c05`

Captured UTC: `2026-07-28T03:42:33.6897722Z`

## Test-only changes

- `tests/Unit/Acquisition/DeterministicGeneratorVectorTests.cs`
- `tests/Unit/Acquisition/MeasurementIdentityTests.cs`
- `tests/Unit/Acquisition/RunControlTests.cs`
- `tests/Unit/Worker/ProductionDispatchTests.cs`
- `tests/Unit/Acquisition/ProductionAttemptTests.cs`
- `tests/Unit/Acquisition/AcquisitionEventTests.cs`
- `tests/Unit/Program.cs`

The RED source used reflection-only compile probes for production types that did not yet exist.
These were test-only compile shims, not Phase 6 production placeholders. No production source,
project reference, package, migration, database, container, secret, or configuration file was
changed before the RED run.

## Exact commands and exits

```text
dotnet build tests/Unit/IUMP.Tests.Unit.csproj --no-restore --configuration Debug
Exit code: 0
Build succeeded; 0 warnings; 0 errors.

dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj --no-build --configuration Debug
Exit code: 1
```

## Literal failures

```text
T108: cases=3; checks=4; failures=4
T109: cases=3; checks=3; failures=3
T110: cases=4; checks=4; failures=4
T111: cases=3; checks=3; failures=3
T112: cases=4; checks=4; failures=4
T113: cases=4; checks=4; failures=4

T108 Constant literal: expected 12.5000, unchanged state 032ba308f46f1f8e4f8167f77e7b0514000000000000000000, next sequence 1.
T108 Normal first literal: expected 11.6519 and state ed99faae39338fb74f8167f77e7b0514013f80c23bc5fbfb3f.
T108 Normal restart literal: expected 17.9149, zero draws, and state ed99faae39338fb74f8167f77e7b0514000000000000000000.
T108 state serialization: expected exactly 25 bytes and malformed states rejected.
T109 sequence 0 identity: expected e118cea2-3d28-5dd4-9726-b3d7d4425ea4.
T109 sequence 1 identity: expected bf5a3f14-0774-5b13-88b1-fa782872b01c.
T109 sequence 42 identity: expected 442c323f-dddb-516b-96ff-88dab38133ce.
T110 Start: expected one Running Run with immutable configuration and pinned Point/Mapping snapshots.
T110 authorization/readiness: expected Administrator or fully scoped Engineer only and atomic failure.
T110 lifecycle: expected Running->Paused->Running/Stopped with cursor and PRNG preserved.
T110 restart: expected only persisted Running Runs to recover.
T111 existing Pending: expected exact persisted payload dispatch with zero generator/state/sequence/Generated changes.
T111 new slot: expected one reservation, Telemetry outside reservation transaction, and one finalization.
T111 Paused/Stopped: expected no claim, generator, dispatch, or counter change.
T112 reserve: expected immutable Pending plus state/sequence/Generated atomicity.
T112 rollback/race: expected no partial publish and uniqueness winner reload.
T112 finalize: expected Pending->Completed once and exactly one Accepted or Rejected counter.
T112 replay: expected identical no-op, Duplicate original classification, and conflicting terminal invariant error.
T113 Start event: expected SimulatorRunStateChanged.v1 with empty Before and safe allowlisted After.
T113 Pause event: expected exact Running->Paused old/new state.
T113 Resume event: expected exact Paused->Running old/new state.
T113 Stop event: expected exact prior->Stopped state and no event for rejected/no-op commands.
```

All Phase 5 suites remained green in this RED run. No restore/download occurred. PostgreSQL was
not connected or mutated, migration 0007 was not executed, no container was used, no secret was
read or printed, and prohibited port `5432` was not contacted.
