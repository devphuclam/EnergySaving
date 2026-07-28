# Phase 7 Canonical Telemetry Checkpoint

## 1. Parent baseline

- Baseline/HEAD: `fdc56735dbd6c9c44599fdf498b010bab151f11e`
- Remote: `https://github.com/devphuclam/EnergySaving.git`
- Constitution: `1.1.0`
- Parent gate: T130 accepted

## 2. Result-commit identity semantics

Trusted Simulator context and a positive, exact UUIDv5 identity shape are verified before any
registry lookup. The Telemetry-specific 32-byte SHA-256 fingerprint uses deterministic typed
encoding and excludes receipt/processing/retry/lease/transport state. A matching immutable result
returns Duplicate with a deep copy of the complete original Accepted or Rejected result. A
different fingerprint returns `IDEMPOTENCY_CONFLICT`. A different ID winning the same
Run+Point+sequence slot returns `MEASUREMENT_SLOT_CONFLICT`. No Pending/InProgress Telemetry state
exists.

## 3. Exact changed files

1. `database/migrations/0008_telemetry_measurement.sql`
2. `docs/blocker-report.md`
3. `specs/002-asset-simulator-latest/checklists/phase-07-red.md`
4. `specs/002-asset-simulator-latest/checklists/phase-07-review.md`
5. `specs/002-asset-simulator-latest/checklists/phase-07-telemetry.md`
6. `specs/002-asset-simulator-latest/tasks.md`
7. `src/Modules/Acquisition/Application/FinalizeTelemetryAttempt.cs`
8. `src/Modules/Acquisition/Application/ProductionAttemptService.cs`
9. `src/Modules/Acquisition/Contracts/ProductionAttemptContracts.cs`
10. `src/Modules/Telemetry/Application/IngestMeasurement.cs`
11. `src/Modules/Telemetry/Application/TelemetryPersistenceService.cs`
12. `src/Modules/Telemetry/Contracts/TelemetryPersistenceContracts.cs`
13. `src/Modules/Telemetry/Contracts/TelemetryProjectionContracts.cs`
14. `src/Modules/Telemetry/Domain/MeasurementIdentityResult.cs`
15. `tests/Integration/Telemetry/TelemetryIngestionRepositoryTests.cs`
16. `tests/Unit/Acquisition/TelemetryFinalizationTests.cs`
17. `tests/Unit/Fakes/FakeTelemetryRepositories.cs`
18. `tests/Unit/IUMP.Tests.Unit.csproj`
19. `tests/Unit/Program.cs`
20. `tests/Unit/Telemetry/IngestionOrchestrationTests.cs`
21. `tests/Unit/Telemetry/IngestionPersistenceContractTests.cs`
22. `tests/Unit/Telemetry/MeasurementIdentityRegistryTests.cs`
23. `tests/Unit/Telemetry/TelemetryEventTests.cs`
24. `tests/Verification/architecture.tests.ps1`

No API/Worker composition root, PostgreSQL adapter, Phase 8 source/test/evidence, `.env`, or
database-information file changed.

## 4. RED evidence

- Timestamp: `2026-07-28T07:11:03.6118212Z`
- Build: `dotnet build IUMP.slnx -c Debug --no-restore` -> exit `0`
- Focused executable:
  `dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj -c Debug --no-build --no-restore`
  -> exit `1`
- Failures: four missing T131 public seams; missing T132 canonical orchestration; missing T133
  atomic persistence; missing T134 finalizer; missing T135 safe event factory.
- Failure class: missing Phase 7 business behavior; not syntax/package/project/harness.
- Production placeholder, restore/download, DB connection/mutation, migration execution, container,
  secret, and port 5432 contact: none.

## 5. GREEN Debug

- Build command: `dotnet build IUMP.slnx -c Debug --no-restore` -> exit `0`, warnings `0`,
  errors `0`.
- Unit command:
  `dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj -c Debug --no-build --no-restore`
  -> exit `0`.

## 6. GREEN Release

- Build command: `dotnet build IUMP.slnx -c Release --no-restore` -> exit `0`, warnings `0`,
  errors `0`.
- Unit command:
  `dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj -c Release --no-build --no-restore`
  -> exit `0`.

## 7. Executable behavior results

| Evidence | Result |
|---|---|
| T131 identity/fingerprint/registry | PASS — 15 cases, 41 checks, 0 failures |
| Accepted Duplicate | PASS — exact original classification, persisted ID, quality/reason, Latest, completed time, original correlation/lineage |
| Rejected Duplicate | PASS — exact original Rejected result without raw reconstruction |
| Same ID / different fingerprint | PASS — `IDEMPOTENCY_CONFLICT`, no mutation |
| Trusted-producer boundary | PASS — untrusted returns `UNTRUSTED_PRODUCER`; no fingerprint/registry/provider/raw/event |
| Malformed/mismatched identity | PASS — `MEASUREMENT_ID_INVALID`; no registry |
| T132 validation/quality | PASS — 13 cases, 97 checks, 0 failures |
| Good | PASS — in range, reason null, Latest-eligible |
| Future >300 seconds | PASS — Uncertain / `SOURCE_TIMESTAMP_FUTURE` |
| Exactly 300 seconds | PASS — not future-skew Uncertain |
| Below/above range | PASS — Accepted Bad / `VALUE_OUT_OF_RANGE` / Latest false |
| Future plus out-of-range | PASS — Bad range result takes precedence |
| Provider validation/recheck | PASS — hierarchy, Source, Mapping, Metric, Unit, versions and drift covered |
| T133 atomic persistence | PASS — 8 cases, 63 checks, 0 failures |
| Lock trace | PASS — Organization Point -> Catalog Source/Mapping/Metric/Unit -> Telemetry identity/raw/Latest -> Integration outbox |
| Accepted transaction | PASS — terminal + raw + Latest result + `MeasurementAccepted.v1`, one commit |
| Rejected transaction | PASS — terminal only; zero raw/Latest/accepted event |
| Failure injection | PASS — Organization, Catalog, terminal, raw, Latest, outbox and commit leave no partial local state |
| Unique race | PASS — matching winner Duplicate; fingerprint conflict; different-ID slot conflict; loser publishes nothing |
| T134 Acquisition finalization | PASS — 10 cases, 23 checks, 0 failures |
| Finalization semantics | PASS — canonical result converted once; Duplicate is never a third counter; replay no-op; conflict rejected; rollback preserves Pending/counters |
| T135 event | PASS — 8 cases, 25 checks, 0 failures |
| Event contract | PASS — safe allowlist, trusted actor/scope, empty Before, no secret/fingerprint/principal/PRNG, no `PointLatestAdvanced.v1` |
| Migration 0008 static review | PASS — immutable terminal/raw, 32-byte fingerprint, slot uniqueness, strict shapes, Accepted/raw matching provenance, no cross-schema FK/extension/Phase 8 table |
| T145 provider-neutral runner | PASS — 20 scenarios, 32 assertions, 0 failures |
| Phase 5/6 regressions | PASS — T094/T095/T096/T103 and T108-T113/T124 all remain green |

## 8. Blocked tasks and capabilities

- T146: `BLOCKED_BY_PACKAGE_POLICY`; unchecked; PostgreSQL Telemetry adapter absent.
- T147: `BLOCKED_BY_PACKAGE_POLICY`; unchecked; API/Worker registration unchanged.
- T148: `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE`; unchecked; depends on T146/T147.
- PostgreSQL 18 capability: `AVAILABLE` at approved `127.0.0.1:5433/iump_dev`.
- Database-access blocker count: `0`.
- `psql`: `BLOCKED_BY_MISSING_TOOL`.
- Migration 0008 execution and PostgreSQL Telemetry tests: `NOT_RUN`.
- Database connection/mutation: `NOT_RUN`.
- Port `5432` contact: `NO`.
- Package restore/download/install: `NO`.
- Docker/container: `NO`.

## 9. Architecture and reviews

- T149 architecture verification: PASS.
- Fast harness: exit `0`, PASS `8`.
- Full harness: exit `20`; PASS `10`, BLOCKED `3`:
  `database/BLOCKED_BY_MISSING_TOOL`, `ci/BLOCKED_BY_COMPANY_APPROVAL`,
  `container-target/BLOCKED_BY_COMPANY_APPROVAL`.
- Verification contract, repository harness, repository policy and repository scope: PASS.
- Secret-assignment scan: 0 findings.
- `.env` / database-information tracking: 0 tracked files.
- Prohibited-port scan of executable changes: 0 findings.
- `git diff --check`: exit `0`.
- T150 Standards review: Critical `0`, High `0`, actionable Medium `0`.
- T150 Specification review: Critical `0`, High `0`, scope creep `0`.

## 10. T131-T151 ledger

| Tasks | Evidence status |
|---|---|
| T131-T145 | PASS (15) |
| T146 | BLOCKED — `BLOCKED_BY_PACKAGE_POLICY` |
| T147 | BLOCKED — `BLOCKED_BY_PACKAGE_POLICY` |
| T148 | BLOCKED — `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE` |
| T149-T151 | PASS (3) |

Final phase counts: PASS `18`; BLOCKED `3`; FAIL `0`; runnable NOT_RUN `0`.

## 11. Progression, demo and release

- Ready for Phase 8: **YES**.
- Technical demo readiness: canonical provider-neutral Telemetry ingestion is executable; a
  persisted Simulator payload reaches stable Accepted or Rejected; exact Duplicate replay and
  Acquisition finalization are harness-ready; Accepted raw Measurement is visible in fake-backed
  storage. PostgreSQL/runtime registration is blocked. Full Latest ordering and Source Health
  remain Phase 8. No browser/API demo exists; live monitoring still needs Phase 8 and a thin
  API/Web slice.
- Release-ready: **NO** — mandatory package/runtime/PostgreSQL execution and company-controlled
  environment evidence remain incomplete.
- Explicit stop: Phase 7 stops after T151. T152 and all Phase 8+ work remain untouched.
