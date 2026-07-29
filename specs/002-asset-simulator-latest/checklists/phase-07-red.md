# Phase 7 Concurrency-and-Scope RED Evidence

- Parent baseline: `0710ba158e9616262a94120a3800988884a8d7c7`
- Corrective checkpoint: T151 concurrency-and-scope closure (T152+ not in scope).
- Reproduction: temporary native worktree
  `C:\Users\TD-999\AppData\Local\Temp\opencode\phase7-red-worktree`
- Scope: post-hoc Phase 7 concurrency-and-scope assertions only; no production implementation,
  database, migration execution, API/Worker composition root, PostgreSQL adapter, or Phase 8 work.
- Temporary changes: updated `IngestionOrchestrationTests.cs`, `IngestionPersistenceContractTests.cs`,
  `FakeTelemetryRepositories.cs`, `ArchitectureVerification.cs`, `Phase7ReviewCheck.cs`,
  and `architecture.tests.ps1` with corrected test-only checks for defects K–Y.
- Build: `dotnet build .\IUMP.slnx -c Debug --no-restore` -> exit `0` (0 warnings, 0 errors).
- Focused run: `dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj -c Debug --no-build --no-restore` -> exit `1`.
- Focused failures: 2 active assertion failures plus 10+ structural T149 failures.

### Unit-test active failures (2)

| ID | Test | Failure | Defect |
|---|---|---|---|
| RED-K | T132 SiteId blank | Expects `PROVIDER_ID_MISSING` but `CheckTrustedScope` returns `PROVIDER_SCOPE_MISMATCH` first | **K** |
| RED-L | T132 AreaId blank | Same — blank operational ID with nonblank trusted ID triggers scope mismatch | **L** |

### T149 architecture failures (10+, first error shown)

```
T149 FAIL: PublishRaceWinner no-op does not verify event equality.
T149 FAIL: Phase 7 checkpoint is missing the 0710ba1/T151 corrective baseline in phase-07-red.md.
T149 FAIL: Phase 7 checkpoint is missing the 0710ba1/T151 corrective baseline in phase-07-review.md.
T149 FAIL: Phase 7 checkpoint is missing the 0710ba1/T151 corrective baseline in phase-07-telemetry.md.
... (remaining checks fail; script stops at first Write-Error)
```

### Defect mapping

| Defect | Description | RED evidence |
|---|---|---|
| **K** | T132 error precedence — SiteId blank returns PROVIDER_SCOPE_MISMATCH not PROVIDER_ID_MISSING | T132: 2 failures |
| **L** | T132 AreaId blank same precedence issue | T132: same 2 failures |
| **M** | PublishRaceWinner event check is EventId-only, not complete field equality | T149: event equality check |
| **N** | No BeginCount on fake unit of work for scope no-transaction evidence | T149 absent |
| **O** | No direct PublishRaceWinner existing-state conflict tests (EventId, After, fingerprint, Site) | T149 absent |
| **P** | No direct race-winner slot conflict test via PublishRaceWinner | T149 absent |
| **Q** | No valid Rejected invalid-fixture matrix using Data(rejected:true) | T149 absent |
| **R** | CommittedState does not deep-copy terminal via terminal.Copy() | T149 absent |
| **S** | No fingerprint mutation isolation test | T149 absent |
| **T** | No commit-time concurrency tests (same-ID, same-slot, independent) | T149 absent |
| **U** | No scope no-transaction evidence tests | T149 absent |
| **V** | T149 baseline hash and T151 reference missing from checkpoints | T149: checkpoint baseline check |
| **W** | T132 has 2 pre-existing runnable failures that must be 0 | T132 exit non-zero |

- The temporary worktree was removed after capture. No restore/download, Docker, PostgreSQL
  connection, port `5432` contact, secret output, or source-code sabotage occurred.

## Historical pre-correction records

Prior atomic-evidence RED checkpoint was recorded at `8261074a2c77f34a7988d4b9a0d04df5565d8deb`.
That historical record is retained and not reclassified by this concurrency-and-scope RED.
