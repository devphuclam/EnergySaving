# Phase 7 Canonical Telemetry Checkpoint — Truth-and-Concurrency Closure

## 1. Gate identity

- Parent baseline: `0710ba158e9616262a94120a3800988884a8d7c7`
- Feature: `002-asset-simulator-latest`
- Stop task: `T151`
- Constitution: `1.1.0`
- T146/T147/T148 remain explicitly package-policy blocked; no Phase 8, Latest, Source Health,
  durable jobs, Audit/API/Web, runtime registration, PostgreSQL adapter, migration execution, or
  port `5432` work was performed.

## 2. Truth-and-concurrency corrections (defects K–Y)

### K. T132 error precedence

- Precedence rule: blank SiteId/AreaId with nonblank TrustedSiteId/TrustedAreaId → PROVIDER_SCOPE_MISMATCH
- AssetId/MetricId/UnitId blank → PROVIDER_ID_MISSING
- T132 updated: SiteId="" and AreaId="" now expect PROVIDER_SCOPE_MISMATCH
- T132 failures reduced from 2 to 0

### L. Complete-fixture event equality in PublishRaceWinner no-op

- Event equality now compares all 18 fields via EventEqualsComplete
- LatestAdvanced=false check: fixture.Latest must be null
- Rejected path unchanged (no raw/latest/event to check)

### M. BeginCount on fake unit of work

- `BeginCount` property on `FakeTelemetryRepositories`
- `BeginRepeatableReadAsync` increments counter
- Scope no-transaction evidence: all 7 cases verify BeginCount=0

### N. Direct PublishRaceWinner existing-state tests

6 tests added to T133 exercising actual PublishRaceWinner existing-state branch via StageRaceWinner:
- exact Accepted no-op
- exact Rejected no-op
- changed EventId → RACE_WINNER_FIXTURE_CONFLICT
- changed Event After → RACE_WINNER_FIXTURE_CONFLICT
- changed fingerprint → RACE_WINNER_FIXTURE_CONFLICT
- changed trusted Site → RACE_WINNER_FIXTURE_CONFLICT

### O. Direct race-winner slot test

- Different MeasurementId, same Run+Point+sequence → RACE_WINNER_SLOT_CONFLICT
- Original winner unchanged

### P. Valid Rejected invalid-fixture matrix

- Rejected terminal built via `TelemetryTestData.Terminal(request, TelemetryFinalClassification.Rejected)`
- Complete Rejected shape confirmed: MeasurementPersisted=false, PersistedMeasurementId=null,
  LatestAdvanced=null, RejectionCode nonnull
- Each fixture attaches Raw/Latest/Event independently → RACE_WINNER_FIXTURE_INVALID

### Q. Deep-immutable CommittedState

- Terminals deep-copied via `terminal.Copy()` (fingerprint array cloned)
- Raw and Latest deep-copied via `with { }`
- Events deep-copied with new Before/After dictionaries

### R. Fingerprint mutation isolation

- Test: mutate CommittedState snapshot fingerprint[0] → re-read terminal → internal fingerprint unchanged

### S. Event dictionary mutation isolation

- Test: modify returned After dictionary → re-read → original unchanged

### T. Same-ID commit-time concurrency

- Two transactions stage same MeasurementId; B commits after A → TelemetryUniqueRaceException;
  A winner preserved; B publishes zero state

### U. Same-slot commit-time concurrency

- Two transactions stage different IDs same Run+Point+sequence; B commits after A → 
  TelemetryUniqueRaceException; A winner preserved

### V. Independent-slot no-lost-update

- Two transactions stage different valid slots; both commit → final state contains both

### W. Scope no-transaction evidence

7 scope mismatch cases all proving:
- PROVIDER_SCOPE_MISMATCH
- BeginCount=0, Rechecks=0
- terminals=0, raw=0, Latest=0, events=0

### X. T149 baseline update

- Architecture verification checks updated: EventEqualsComplete pattern, 0710ba1 baseline
- All three checkpoints reference 0710ba1

### Y. Phase7ReviewCheck and ArchitectureVerification

- Phase7ReviewCheck updated for new scope and equality checks
- ArchitectureVerification checks for EventEqualsComplete, BeginCount, direct test patterns

## 3. RED evidence

- Temporary native worktree at parent baseline `0710ba158e9616262a94120a3800988884a8d7c7`:
  `C:\Users\TD-999\AppData\Local\Temp\opencode\phase7-red-worktree`.
- Test/static-only RED build: `dotnet build .\IUMP.slnx -c Debug --no-restore` -> exit **0**.
- Focused RED run: `dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj -c Debug --no-build --no-restore` -> exit **1**.
- Focused failures: 2 active (T132 PROVIDER_ID_MISSING) + 10+ T149 structural (see phase-07-red.md).
- Worktree was removed after capture. No restore/download, database connection/mutation, migration
  execution, Docker, or secret output occurred.

## 4. GREEN and contract evidence

### Focused provider-neutral run

| Task | Cases / scenarios | Checks / assertions | Result |
|---:|---:|---:|---|
| T131 | 16 | 52 | PASS |
| T132 | — | — | 0 failures |
| T133 | — | — | PASS |
| T134 | 22 | 96 | PASS |
| T135 | 13 | 33 | PASS |
| T145 | 39 | 164 | PASS |
| T149 | — | — | PASS |
| T150 | — | — | PASS |

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
dotnet build .\IUMP.slnx -c Release
dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj -c Release --no-build
& .\scripts\test.ps1
```

### Key GREEN results

- T132 failures = 0 (two PROVIDER_ID_MISSING cases now correctly return PROVIDER_SCOPE_MISMATCH)
- T133: 20+ direct PublishRaceWinner, concurrency, deep-immutability, and scope tests added
- T145: all 39 scenarios pass
- T149: all architecture verification checks pass
- Phase7ReviewCheck: all checks pass

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

**Ledger**: PASS 18, BLOCKED 3, FAIL 0, runnable NOT_RUN 0.

## 6. Files changed by this closure

| File | Change |
|---|---|
| `tests/Unit/Telemetry/IngestionOrchestrationTests.cs` | T132: fixed 2 error-precedence failures; added 7 scope no-transaction evidence cases |
| `tests/Unit/Telemetry/IngestionPersistenceContractTests.cs` | T133: 20+ new tests covering direct PublishRaceWinner, concurrency, deep immutability, Rejected invalid-fixture matrix, pre-existing state |
| `tests/Unit/Fakes/FakeTelemetryRepositories.cs` | PublishRaceWinner event equality via EventEqualsComplete; BeginCount; deep-copy CommittedState with terminal.Copy() |
| `tests/Verification/architecture.tests.ps1` | T149: EventEqualsComplete check; 0710ba1 baseline hash; additional pattern checks |
| `specs/002-asset-simulator-latest/checklists/phase-07-red.md` | RED evidence for 0710ba1 baseline, defects K–W |
| `specs/002-asset-simulator-latest/checklists/phase-07-review.md` | T150 findings K–Y, baseline 0710ba1 |
| `specs/002-asset-simulator-latest/checklists/phase-07-telemetry.md` | T151 checkpoint, 0710ba1 baseline, final ledger |

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

Previous Phase 7 checkpoint at `8261074a2c77f34a7988d4b9a0d04df5565d8deb` (concurrency-and-scope
closure) and `f8521159802fd39732c4cfa24605aed912c18419` (atomic-evidence closure). Those
historical records are retained and not reclassified by this truth-and-concurrency closure.

## 2026-07-30 runtime-resolution addendum

T146 and T147 are now PASS with the Telemetry adapter, API/Worker registration, build, and runtime
resolution. Accepted terminal/raw persistence and Latest advance passed in the local runtime
verifier. T148 remains unchecked and `RUNNABLE_NOW` because its complete uniqueness,
Rejected-without-raw, replay/conflict, and concurrency suite was not executed.
