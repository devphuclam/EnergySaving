# Phase 7 — Convergence checkpoint (corrective)

## 1. Corrective baseline

- Original baseline: `70b45ddfad3090dfc58c36e023441ed2673a6760`
- Constitution: `1.1.0`
- Convergence diff: Phase 7 terminal-result corrective fixes (defects A–H per Spec Kit analysis)

## 2. Corrective scope

No new behavior beyond Phase 7 canonical Telemetry ingestion, Acquisition finalization, and
provider-neutral contracts. The nine corrections (A–I) address test-side and migration-side
incompleteness that produced false-negative checks or uncovered edges.

## 3. Defects corrected

| Tag | Defect | Resolution |
|-----|--------|------------|
| A | `FakeTelemetryRepositories.PublishRaceWinner` used hardcoded fixture values | Uses exact `numericValue`/`unitCode` from the winner tuple |
| B | `PublishRaceWinner` used same timestamp for sourceTs/receivedAt/processingAt | Uses three distinct timestamps: `sourceTs = CompletedAtUtc - 2s`, `receivedAt = CompletedAtUtc - 1s`, `processingAt = CompletedAtUtc` |
| C | `PublishRaceWinner` built an empty `After` dictionary | `After` contains 15 populated measurement fields |
| D | `PublishRaceWinner.LatestProjectionCandidate` used `CompletedAtUtc` as source timestamp | Uses `sourceTs` |
| E | `IngestionPersistenceContractTests` unique-race/conflict/slot cases tested only result surface | Extended: raw value/unit, event type + payload, exact terminal equality via `TerminalEqual` |
| F | `FakeAcquisitionRunRepositories.FinalizeAsync` stored only 5 of 11 `TelemetryDispatchResult` fields | Stores all 11 fields: `MeasurementPersisted`, `PersistedMeasurementId`, `QualityCode`, `ReasonCode`, `FinalClassification`, `LatestAdvanced`, `ErrorCode`, `RejectionCode`, `OriginalCorrelationId`, `OriginalLineageId`, `CompletedAtUtc` |
| G | Replay conflict detection in `FakeAcquisitionRunRepositories` compared only `FinalClassification` | Compares all 11 stored fields |
| H | Provider validation in `IngestionOrchestrationTests` covered only 5 of 16 expected variants | Covers 16 variants: Missing, Untrusted, +14 provider-variant checks (SourceType, Site/Area/Asset/Point/Metric/Unit/Compatibility versions, CompatibilityIdentity, CompatibilityStatus) |
| I | `0007_acquisition_run.sql` lacked Acquisition-owned fields and provenance constraint | Added `measurement_persisted`, `persisted_measurement_id`, `quality_code`, `reason_code`, `original_correlation_id`, `original_lineage_id`; updated terminal pair constraint; added `ck_simulator_production_attempt_original_provenance` |

## 4. Changed files

1. `database/migrations/0007_acquisition_run.sql` — Acquisition-owned fields + provenance constraint
2. `tests/Unit/Fakes/FakeAcquisitionRunRepositories.cs` — 11-field storage + replay comparison
3. `tests/Unit/Fakes/FakeTelemetryRepositories.cs` — distinct timestamps, populated event payload
4. `tests/Unit/Telemetry/IngestionOrchestrationTests.cs` — 14 provider validation variants, NaN fix
5. `tests/Unit/Telemetry/IngestionPersistenceContractTests.cs` — deeper race winner assertions
6. `tests/Unit/Telemetry/ArchitectureVerification.cs` — NEW, 28 architecture checks per T149
7. `tests/Unit/Telemetry/Phase7ReviewCheck.cs` — NEW, 11 review sign-off checks per T150
8. `tests/Unit/Program.cs` — wire T149 and T150

## 5. Verification

```
dotnet build tests/Unit/IUMP.Tests.Unit.csproj -c Release --no-restore
  -> Build succeeded / 0 Error(s)
dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj -c Release --no-restore
  -> EXIT: 0
```

## 6. Phase 7 final ledger

| Task | Status |
|------|--------|
| T131 | PASS — 16 cases, 52 checks |
| T132 | PASS — 14 cases, 149 checks |
| T133 | PASS — 8 cases, 68 checks |
| T134 | PASS — 10 cases, 23 checks |
| T135 | PASS — 8 cases, 25 checks |
| T145 | PASS — 20 scenarios, 32 assertions |
| T149 | PASS — 28 checks |
| T150 | PASS — 11 checks |
| T146 | BLOCKED — `BLOCKED_BY_PACKAGE_POLICY` |
| T147 | BLOCKED — `BLOCKED_BY_PACKAGE_POLICY` |
| T148 | BLOCKED — `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE` |

Final counts: PASS 18 / BLOCKED 3 / FAIL 0 / NOT_RUN 0

## 7. Gate readiness

- Ready for Phase 8: **YES** — all runnable implementation tasks pass at Release
- Release-ready: **NO** — mandatory package/runtime/PostgreSQL/company environment evidence is
  incomplete
- 5432 port contact: NO
- Migration execution: NOT_RUN
