# Phase 7 Canonical Telemetry Checkpoint — Atomic-Evidence Closure

## 1. Gate identity

- Parent baseline: `f8521159802fd39732c4cfa24605aed912c18419`
- Feature: `002-asset-simulator-latest`
- Stop task: `T151`
- Constitution: `1.1.0`
- T146/T147/T148 remain explicitly package-policy blocked; no Phase 8, Latest, Source Health,
  durable jobs, Audit/API/Web, runtime registration, PostgreSQL adapter, migration execution, or
  port `5432` work was performed.

## 2. Atomic-evidence corrections

### 2a. Aggregate committed state

- Added `TelemetryCommittedState(Terminals, Raw, Latest, Events)` record to `FakeTelemetryRepositories`.
- All committed reads (`GetTerminalAsync`, `ListCommittedTerminalsAsync`, `ReplayTerminal`,
  `GetCommittedLatestAsync`, `LatestCount`, `ListCommittedRawAsync`, `ListCommittedAsync`) read
  from `_committedState` snapshot.
- `PublishRaceWinner`: reads one `_committedState`, validates fixture, checks conflicts, builds
  complete next state, assigns `_committedState = nextState` exactly once.
- `Transaction.CommitAsync`: reads one `_committedState`, validates, builds complete next state,
  assigns atomically.

### 2b. Winner conflict detection

- Same Measurement ID + exact terminal/fingerprint match: keeps existing winner (no-op).
- Same Measurement ID + different immutable terminal: `RACE_WINNER_FIXTURE_CONFLICT`.
- Different Measurement ID for same Run+Point+sequence: `RACE_WINNER_SLOT_CONFLICT`.
- Immutable committed terminal is never overwritten.

### 2c. Invalid fixture zero-publication evidence

T145 (provider-neutral contract) adds:

- 8 invalid Accepted fixture cases: Raw null, Raw identity mismatch, Latest null when
  LatestAdvanced=true, Latest present when LatestAdvanced=false, Latest field mismatch,
  Event null, Event envelope mismatch, Event payload mismatch.
- 3 invalid Rejected fixture cases: Raw present, Latest present, Event present.
- Each proves: terminal count unchanged, raw count unchanged, Latest count unchanged,
  event count unchanged, existing committed Latest entry unchanged.

T133 (orchestration) adds:

- 8 invalid Accepted and 3 invalid Rejected cases through `StageTerminalAsync`/`PublishRaceWinner`.
- Each proves: exact stable exception code `RACE_WINNER_FIXTURE_INVALID`, terminal/raw/latest/event
  counts unchanged.

### 2d. Exact Latest evidence

T145 adds:

- `GetCommittedLatestAsync(data.Terminal.PointId)` called and all fields compared:
  MeasurementId, PointId, SourceTimestampUtc, SourceSequence, ProcessingAtUtc, QualityCode.
- Accepted LatestAdvanced=false scenario: exact terminal, exact Raw, `GetCommittedLatestAsync`
  returns null, `LatestCount == 0`, terminal stores `LatestAdvanced=false`.
- `LatestCount` retained only as supplementary cardinality evidence.

### 2e. Stable trusted-scope result

- `CheckTrustedScope(provider, correlationId)` returns `TelemetryIngestionResult.Failed(
  "PROVIDER_SCOPE_MISMATCH", correlationId)` for blank TrustedSiteId, blank TrustedAreaId,
  TrustedSiteId != SiteId, or TrustedAreaId != AreaId.
- Called before transaction begins. Result checked with `if (scopeResult is not null) return scopeResult`.
- No exception escapes, no transaction begins, no terminal/raw/latest/event produced.
- `PersistAcceptedAsync` retains no defensive throw; the stable result is the only path.

### 2f. Event factory trust boundary

- `MeasurementAcceptedEventFactory.Create` no longer has optional `eventSiteId`/`eventAreaId`
  parameters with `?? provider.SiteId` fallback.
- Signature: `Create(RawMeasurement, bool latestAdvanced, TelemetryProviderSnapshot, string eventSiteId, string? eventAreaId)`.
- Factory validates `provider.TrustedSiteId == eventSiteId && provider.TrustedAreaId == eventAreaId`.
- T135 adds scope mismatch case: mismatched provider returns `PROVIDER_SCOPE_MISMATCH` result,
  zero terminal, zero event.

## 3. RED evidence

- Temporary native worktree at parent baseline `f8521159802fd39732c4cfa24605aed912c18419`:
  `C:\Users\TD-999\AppData\Local\Temp\opencode\phase7-atomic-red`.
- Test/static-only RED build: `dotnet build IUMP.slnx -c Debug --no-restore` -> exit **0**.
- Focused RED run: `dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj -c Debug --no-build --no-restore` -> exit **1**.
- Focused failures: exactly 8 assertions — one per natural defect (RED-1 through RED-8, see
  `phase-07-red.md` for full details).
- Worktree was removed after capture. No restore/download, database connection/mutation, migration
  execution, Docker, or secret output occurred.

## 4. GREEN and contract evidence

### Focused provider-neutral run

| Task | Cases / scenarios | Checks / assertions | Result |
|---:|---:|---:|---|
| T131 | 16 | 52 | PASS |
| T132 | 15 | 217 | PASS |
| T133 | 20 | 162 | PASS |
| T134 | 22 | 96 | PASS |
| T135 | 9 | 29 | PASS |
| T145 | 35 | 123 | PASS |
| T149 | — | 52 | PASS |
| T150 | — | 36 | PASS |

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

### Exact Accepted Latest result

T145 "Accepted race winner exact Latest evidence": `GetCommittedLatestAsync` returns non-null;
all fields match the committed fixture's `LatestProjectionCandidate`.

### Accepted-no-Latest result

T145 "Accepted race winner LatestAdvanced=false returns null Latest": `GetCommittedLatestAsync`
returns null; `LatestCount == 0`; terminal stored with `LatestAdvanced=false`.

### Exact Rejected winner

T145 "exact Rejected race winner": terminal-only fixture committed; zero raw, zero Latest,
zero event; Duplicate replay returns exact original.

### Existing-winner conflict results

T133 "conflicting unique-race winner returns conflict": different terminal for same Measurement ID
returns `IDEMPOTENCY_CONFLICT`; winner state is preserved.
T133 "different-ID slot-race winner returns slot conflict": different Measurement ID for same
Run+Point+sequence returns `MEASUREMENT_SLOT_CONFLICT`; winner state is preserved.

### All invalid fixture zero-publication results

T145: 8 invalid Accepted + 3 invalid Rejected cases — each terminal/raw/latest/event count
unchanged, existing committed state intact.
T133: 8 invalid Accepted + 3 invalid Rejected cases through orchestration — each throws
`RACE_WINNER_FIXTURE_INVALID` with unchanged committed counts.

### Stable Site/Area mismatch results

T135 "scope mismatch produces no event and factory rejects untrusted scope": mismatched provider
returns `PROVIDER_SCOPE_MISMATCH` disposition; zero terminal, zero event, zero raw.

## 5. Task evidence

| Task | Status | Classification |
|---|---|---|
| T131 | PASS | RUNNABLE_NOW |
| T132 | PASS | RUNNABLE_NOW |
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

**Ledger**: PASS 18, BLOCKED 3, FAIL 0, NOT_RUN 0.

## 6. Files changed by this closure

| File | Change |
|---|---|
| `tests/Unit/Fakes/FakeTelemetryRepositories.cs` | Added `TelemetryCommittedState` aggregate; single-state atomic `PublishRaceWinner` with conflict detection; updated `CommitAsync` for aggregate swap |
| `src/Modules/Telemetry/Application/TelemetryPersistenceService.cs` | `CheckTrustedScope` returns stable `PROVIDER_SCOPE_MISMATCH` result before transaction; factory signature removes optional fallback, validates trusted scope equality |
| `tests/Integration/Telemetry/TelemetryIngestionRepositoryTests.cs` | T145: 11 invalid fixture cases, exact Latest field comparison, `latestAdvanced` parameter in `Data()`, Accepted LatestAdvanced=false scenario |
| `tests/Unit/Telemetry/IngestionPersistenceContractTests.cs` | T133: 11 invalid fixture orchestration cases |
| `tests/Unit/Telemetry/TelemetryEventTests.cs` | T135: scope mismatch no-event test |
| `tests/Unit/Telemetry/Phase7ReviewCheck.cs` | 36 atomic-evidence checks (aggregate state, conflict codes, exact Latest, invalid fixtures, trusted scope result, factory boundary) |
| `tests/Verification/architecture.tests.ps1` | T149: aggregate state, conflict detection, T145 GetCommittedLatestAsync field comparison, invalid fixture presence, trusted scope stable result, factory boundary |
| `specs/002-asset-simulator-latest/checklists/phase-07-red.md` | RED evidence for f852 baseline, 8 natural defects |
| `specs/002-asset-simulator-latest/checklists/phase-07-review.md` | T150 findings A–I, 36 Phase7ReviewCheck, baseline f852 |
| `specs/002-asset-simulator-latest/checklists/phase-07-telemetry.md` | T151 checkpoint, f852 baseline, final ledger, exact evidence |

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

Previous Phase 7 checkpoints at `d5c71ed42a45c6fee189c3a67580b0cf096c9bf6` (atomic-race and
compatibility-lock closure) and `b6b2510820f5ab8f0af5569a2fc18b4ee4b2f892` (exact-result closure).
Both historical records are retained and not reclassified by this atomic-evidence closure.
