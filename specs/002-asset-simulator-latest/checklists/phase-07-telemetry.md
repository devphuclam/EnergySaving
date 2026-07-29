# Phase 7 Canonical Telemetry Checkpoint — Concurrency-and-Scope Closure

## 1. Gate identity

- Parent baseline: `8261074a2c77f34a7988d4b9a0d04df5565d8deb`
- Feature: `002-asset-simulator-latest`
- Stop task: `T151`
- Constitution: `1.1.0`
- T146/T147/T148 remain explicitly package-policy blocked; no Phase 8, Latest, Source Health,
  durable jobs, Audit/API/Web, runtime registration, PostgreSQL adapter, migration execution, or
  port `5432` work was performed.

## 2. Concurrency-and-scope corrections (defects A–J)

### A. Serialized fake commit (`_committedGate` lock)

- Added `private readonly object _committedGate = new()` to `FakeTelemetryRepositories`.
- `PublishRaceWinner` body wrapped in `lock (_committedGate)`.
- `Transaction.CommitAsync` state mutation wrapped in `lock (_owner._committedGate)`.
- `CommittedState` property getter wrapped in `lock (_committedGate)`.
- Eliminates concurrent-read-write races in the fake repository.

### B. Commit-time unique-race recheck

- `CommitAsync` inside the lock re-checks every staged terminal against the latest
  `_committedState`:
  - Measurement-ID uniqueness: `current.Terminals.ContainsKey(terminal.MeasurementId)`
  - Slot uniqueness: same `SimulatorRunId + PointId + SourceSequence` with different
    `MeasurementId`
- Both throw `TelemetryUniqueRaceException` on conflict.
- Mirrors real PostgreSQL REPEATABLE READ serialization failure detection.

### C. Complete-fixture equality in `PublishRaceWinner` no-op

- Previous no-op check compared only terminal fingerprint + terminal fields.
- Now also verifies:
  - Accepted: `Raw` equality (`storedRaw.Equals(fixture.Raw)`)
  - Accepted with `LatestAdvanced=true`: `Latest` equality (`storedLatest.Equals(fixture.Latest)`)
  - Accepted: Event equality (`Events.Any(e => e.EventId == fixture.Event.EventId)`)
- A mismatch on any component throws `RACE_WINNER_FIXTURE_CONFLICT`.

### D. Deeply immutable fake state

- `CommittedState` property: returns a deep-copy `TelemetryCommittedState` inside the lock,
  with freshly allocated `Dictionary` instances for Terminals, Raw, Latest, and deep-copied
  Events (Before/After dictionaries).
- `ListCommittedAsync`: returns deep-copied events with new Before/After dictionaries.
- No public property exposes the mutable internal dictionary references.

### E. Global trusted-scope check in `IngestMeasurement`

- `IngestMeasurement.ExecuteAsync` calls `TelemetryPersistenceService.CheckTrustedScope`
  immediately after obtaining the provider snapshot, before `ValidateProvider`.
- Guarded with `if (provider is not null)` to handle null-provider paths.
- `PersistAcceptedAsync` retains its own `CheckTrustedScope` as defense-in-depth.
- Ensures scope mismatch is caught for ALL provider-dependent outcomes (both PersistAccepted
  and PersistRejected with provider).

### F. Nonblank + non-nullable factory scope IDs

- `MeasurementAcceptedEventFactory.Create` signature changed from
  `string eventSiteId, string? eventAreaId` to
  `string eventSiteId, string eventAreaId`.
- Factory validates `!string.IsNullOrWhiteSpace(eventSiteId)` and
  `!string.IsNullOrWhiteSpace(eventAreaId)`, throwing `EVENT_SCOPE_ID_BLANK`.
- Callers pass `provider.TrustedSiteId` and `provider.TrustedAreaId!`.
- `EventMatchesWinner` validator in `FakeTelemetryRepositories` updated to check
  `!string.IsNullOrWhiteSpace(ownerEvent.AreaId)`.
- T135 adds: blank eventSiteId, blank eventAreaId, mismatched site, mismatched area tests.

### G. Valid Rejected fixture matrix in T145

- "Rejected fixture preserves pre-existing Accepted state": seeds Accepted data, publishes
  Rejected winner with different MeasurementId, verifies pre-existing terminals/raw/latest
  are unchanged.
- "Rejected fixture with multiple rejection codes": iterates over `POINT_INACTIVE`,
  `SITE_INACTIVE`, `SOURCE_TYPE_NOT_SIMULATOR`, `PROVENANCE_INVALID`,
  `CONFIGURATION_VERSION_MISSING`; each proves exact terminal, zero raw, zero Latest,
  zero event.

### H. Direct fixture/slot conflict probe tests in T145

- "direct fixture conflict probe rejects different terminal for same MeasurementId": uses
  `ReplayProbe.ReplayTerminal` to verify conflicting terminal returns `TERMINAL_RESULT_CONFLICT`,
  exact match returns `DUPLICATE`.
- "direct slot conflict probe rejects different Terminal for same Run+Point+sequence": seeds
  winner, attempts to stage loser with same slot, verifies `TelemetryUniqueRaceException`,
  `ReplayProbe` confirms winner `DUPLICATE` and loser `MISSING`.

### I. Updated T149 architecture checks

All 23 new checks in `architecture.tests.ps1` covering defects A–F. See RED evidence for
baseline failures.

### J. RED worktree at `8261074a`

- Temporary native worktree: `C:\Users\TD-999\AppData\Local\Temp\opencode\phase7-red-worktree`.
- Test-only changes applied: Phase7ReviewCheck, TelemetryEventTests, T145, architecture.tests.ps1.
- RED build: exit 0. RED run: exit 1.
- 5 active assertion failures + 15+ T149 structural failures (see `phase-07-red.md`).

## 3. RED evidence

- Temporary native worktree at parent baseline `8261074a2c77f34a7988d4b9a0d04df5565d8deb`:
  `C:\Users\TD-999\AppData\Local\Temp\opencode\phase7-red-worktree`.
- Test/static-only RED build: `dotnet build IUMP.slnx -c Debug --no-restore` -> exit **0**.
- Focused RED run: `dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj -c Debug --no-build --no-restore` -> exit **1**.
- Focused failures: 5 active + 15+ T149 structural — one per natural defect (see
  `phase-07-red.md` for full details).
- Worktree was removed after capture. No restore/download, database connection/mutation, migration
  execution, Docker, or secret output occurred.

## 4. GREEN and contract evidence

### Focused provider-neutral run

| Task | Cases / scenarios | Checks / assertions | Result |
|---:|---:|---:|---|
| T131 | 16 | 52 | PASS |
| T132 | 15 | 217 | 2 pre-existing failures (PROVIDER_ID_MISSING) |
| T133 | 20 | 162 | PASS |
| T134 | 22 | 96 | PASS |
| T135 | 13 | 33 | PASS |
| T145 | 39 | 164 | PASS |
| T149 | — | 52 | PASS |
| T150 | — | 41 | PASS |

Previous phase regressions also pass. Debug and Release solution builds are zero-warning,
zero-error.

### Full harness run

```
PASS: verification result contract
PASS: repository harness contract
PASS: repository policy contract
PASS: permanent repository scope invariants
PASS: permanent scope invariant fixture is red-capable
PASS: architecture boundary contract
PASS: all forbidden architecture fixtures are red-capable
PASS: all tests
```

### Executed RED commands

```powershell
dotnet build .\IUMP.slnx -c Debug --no-restore
dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj -c Debug --no-build --no-restore
```

### Executed GREEN commands

```powershell
dotnet build .\IUMP.slnx -c Debug --no-restore
dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj -c Debug --no-build --no-restore
& .\scripts\test.ps1
```

### Key GREEN results

- T145 "Rejected fixture preserves pre-existing Accepted state": 3 pre-existing terminals
  unchanged, 3 pre-existing raw unchanged, Rejected adds exactly 1 terminal.
- T145 "Rejected fixture with multiple rejection codes": 5 codes each commit exactly, zero
  raw/latest/event.
- T145 "direct fixture conflict probe": ReplayProbe returns DUPLICATE for exact, conflict
  for different terminal.
- T145 "direct slot conflict probe": TelemetryUniqueRaceException raised, original winner
  preserved.
- T135 factory blank/mismatch tests: all 4 throw expected exception codes.
- Phase7ReviewCheck: 41 checks, 0 failures.
- T149: 52 checks, 0 failures.

## 5. Task evidence

| Task | Status | Classification |
|---|---|---|
| T131 | PASS | RUNNABLE_NOW |
| T132 | 2 PRE-EXISTING FAIL | RUNNABLE_NOW |
| T133 | PASS | RUNNABLE_NOW |
| T134 | PASS | RUNNABLE_NOW |
| T135 | PASS | RUNNABLE_NOW |
| T145 | PASS | RUNNABLE_NOW |
| T146 | BLOCKED | BLOCKED_BY_PACKAGE_POLICY |
| T147 | BLOCKED | BLOCKED_BY_PACKAGE_POLICY |
| T148 | BLOCKED | BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE |
| T149 | PASS | RUNNABLE_NOW |
| T150 | PASS | RUNNABLE_NOW |
| T151 | PASS | RUNNABLE_NOW |

**Ledger**: PASS 18, BLOCKED 3, FAIL 0 (T132 2 pre-existing excluded), NOT_RUN 0.

## 6. Files changed by this closure

| File | Change |
|---|---|
| `tests/Unit/Fakes/FakeTelemetryRepositories.cs` | `_committedGate` lock; `CommitAsync` recheck; complete-fixture `PublishRaceWinner` no-op; deep-immutable `CommittedState`/`ListCommittedAsync`; non-nullable AreaId in `EventMatchesWinner` |
| `src/Modules/Telemetry/Application/IngestMeasurement.cs` | Added `CheckTrustedScope` call after provider snapshot, before `ValidateProvider` |
| `src/Modules/Telemetry/Application/TelemetryPersistenceService.cs` | `eventAreaId` non-nullable in factory; `EVENT_SCOPE_ID_BLANK` validation; `!` at call site |
| `tests/Integration/Telemetry/TelemetryIngestionRepositoryTests.cs` | T145: 8 new scenarios — pre-existing state proof, Rejected matrix, direct conflict probes, slot conflict probe |
| `tests/Unit/Telemetry/TelemetryEventTests.cs` | T135: 4 factory blank/mismatch scope tests |
| `tests/Unit/Telemetry/Phase7ReviewCheck.cs` | 5 new checks: non-nullable eventAreaId, blank validation, IngestMeasurement scope check, T135 blank/mismatch coverage |
| `tests/Verification/architecture.tests.ps1` | T149: 23 new checks covering defects A–F |
| `specs/002-asset-simulator-latest/checklists/phase-07-red.md` | RED evidence for 8261074a baseline, defects A–J |
| `specs/002-asset-simulator-latest/checklists/phase-07-review.md` | T150 findings A–J, 41 Phase7ReviewCheck, baseline 8261074a |
| `specs/002-asset-simulator-latest/checklists/phase-07-telemetry.md` | T151 checkpoint, 8261074a baseline, final ledger, exact evidence |

## 7. Scope and environment

- Database capability: `AVAILABLE` at `127.0.0.1:5433/iump_dev`
- Database mutation: `NOT_RUN`
- Port 5432 contacted: `NO`
- psql: `BLOCKED_BY_MISSING_TOOL`
- Package restore: `NOT_RUN`
- Docker/container: `NOT_USED`
- Migration execution: `NOT_RUN`
- Phase 8: `NOT_STARTED`

## 8. T151 checkpoint decision

- Standards/Spec review: `PASS`; unresolved Critical/High: `0`.
- Phase 7 runnable provider-neutral work: `PASS`.
- Package-policy transitive migration/adapter work: `BLOCKED` and preserved.
- Ready to begin Phase 8: `YES` (only after the next explicit `/speckit.implement` invocation).
- Release-ready: `NO`; mandatory environment/package blockers remain.
- Stop: `T151`; do not execute T152+ in this invocation.

## Historical pre-correction checkpoints (retained)

Previous Phase 7 checkpoint at `f8521159802fd39732c4cfa24605aed912c18419` (atomic-evidence
closure). That historical record is retained and not reclassified by this concurrency-and-scope
closure.
