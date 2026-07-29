# Phase 7 Concurrency-and-Scope RED Evidence

- Parent baseline: `8261074a2c77f34a7988d4b9a0d04df5565d8deb`
- Corrective checkpoint: T151 concurrency-and-scope closure (T152+ not in scope).
- Reproduction: temporary native worktree
  `C:\Users\TD-999\AppData\Local\Temp\opencode\phase7-red-worktree`
- Scope: post-hoc Phase 7 concurrency-and-scope assertions only; no production implementation,
  database, migration execution, API/Worker composition root, PostgreSQL adapter, or Phase 8 work.
- Temporary changes: updated `Phase7ReviewCheck.cs`, `TelemetryEventTests.cs`,
  `TelemetryIngestionRepositoryTests.cs`, and `architecture.tests.ps1` with corrected
  test-only checks for defects A–J.
- Build: `dotnet build IUMP.slnx -c Debug --no-restore` -> exit `0` (0 warnings, 0 errors).
- Focused run: `dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj -c Debug --no-build --no-restore` -> exit `1`.
- Focused failures: 5 active assertion failures plus 15+ structural T149 failures (stopped after
  first Write-Error due to `$ErrorActionPreference = 'Stop'`):

### Unit-test active failures (5)

| ID | Test | Failure | Defect |
|---|---|---|---|
| RED-A | T135 factory rejects blank eventSiteId | Baseline throws `EVENT_TRUSTED_SCOPE_MISMATCH` not `EVENT_SCOPE_ID_BLANK` | **F** |
| RED-B | T135 factory rejects blank eventAreaId | Same — no nonblank validation in baseline factory | **F** |
| RED-C | Phase7ReviewCheck eventAreaId signature | Checks `string eventAreaId` (non-nullable) but baseline had `string?` | **F** |
| RED-D | Phase7ReviewCheck blank scope validation | Baseline `Create` has no `EVENT_SCOPE_ID_BLANK` guard | **F** |
| RED-E | Phase7ReviewCheck IngestMeasurement scope | Baseline `IngestMeasurement` never calls `CheckTrustedScope` | **E** |

### T149 architecture failures (15+, first error shown)

```
T149 FAIL: FakeTelemetryRepositories lacks _committedGate synchronization field.
T149 FAIL: CommitAsync not serialized inside _committedGate lock.
T149 FAIL: CommitAsync missing commit-time Measurement-ID recheck.
T149 FAIL: CommitAsync missing commit-time slot recheck.
T149 FAIL: PublishRaceWinner not serialized inside _committedGate lock.
T149 FAIL: PublishRaceWinner no-op does not verify raw equality.
T149 FAIL: PublishRaceWinner no-op does not verify Latest equality.
T149 FAIL: PublishRaceWinner no-op does not verify event equality.
T149 FAIL: CommittedState getter returns shallow reference without lock.
T149 FAIL: CommittedState getter does not return a deep-copy snapshot.
T149 FAIL: ListCommittedAsync does not deep-copy event dictionaries.
T149 FAIL: CheckTrustedScope must precede ValidateProvider in IngestMeasurement.
T149 FAIL: Event factory missing nonblank scope ID validation.
T149 FAIL: Event factory eventAreaId parameter is still nullable.
... (remaining checks fail; script stops at first Write-Error)
```

### Defect mapping

| Defect | Description | RED evidence |
|---|---|---|
| **A** | Serialized fake commit via `_committedGate` lock | T149: `_committedGate` field, CommitAsync/PublishRaceWinner lock |
| **B** | Commit-time unique-race recheck | T149: Measurement-ID and Run+Point+sequence recheck |
| **C** | Complete-fixture equality in RaceWinner no-op | T149: raw/Latest/event equality verification |
| **D** | Deeply immutable fake state | T149: CommittedState deep-copy, ListCommittedAsync deep-copy |
| **E** | Global trusted-scope check in IngestMeasurement | Phase7ReviewCheck + T149: CheckTrustedScope missing |
| **F** | Nonblank + non-nullable factory scope IDs | T135 + Phase7ReviewCheck + T149: blank validation missing |
| **G** | Valid Rejected fixture matrix in T145 | PASS (baseline already supports Rejected fixtures) |
| **H** | Direct fixture/slot conflict probes in T145 | PASS (baseline already detects slot conflicts) |
| **I** | T149 architecture check updates | T149 failures listed above |

- The temporary worktree was removed after capture. No restore/download, Docker, PostgreSQL
  connection, port `5432` contact, secret output, or source-code sabotage occurred.

## Historical pre-correction records

Prior atomic-evidence RED checkpoint was recorded at `f8521159802fd39732c4cfa24605aed912c18419`.
That historical record is retained and not reclassified by this concurrency-and-scope RED.
