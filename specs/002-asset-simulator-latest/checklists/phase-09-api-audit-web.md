# Phase 9 checkpoint — API, Audit and Web

Parent baseline SHA: `6e7ff79942188517c644eb43ae541d6eddc23d06`.
Stop boundary: **T223 complete; T224+ not executed.**

## Task ledger

| Status | Count | Tasks |
|---|---:|---|
| PASS | 46 | T170–T191, T194–T201, T203–T204, T207–T217, T221–T223 |
| BLOCKED_BY_PACKAGE_POLICY | 5 | T192, T193, T202, T205, T218 |
| BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE | 3 | T206, T219, T220 |
| FAIL | 0 | — |
| Runnable NOT_RUN | 0 | — |

## Changed files

- `src/Api/Infrastructure/ApplicationPorts.cs`
- `src/Api/Infrastructure/IdempotentCommandExecutor.cs`
- `src/Api/ConfigurationEndpoints.cs`, `SimulatorEndpoints.cs`, `TelemetryQueryEndpoints.cs`, `AuditEndpoints.cs`
- `src/Worker/Integration/RequiredConsumerRegistry.cs`, `OutboxDispatcherWorker.cs`, `AuditDeliveryHandler.cs`
- `src/Modules/Integration/Application/CommandFingerprintV1.cs` and Contracts canonical fingerprint port
- `src/Modules/Audit/Contracts/AuditContracts.cs`, Audit consumer/query service
- `src/BuildingBlocks/Persistence/IHostTransactionFactory.cs`, `HostTransactionCoordinator.cs`
- `database/migrations/0010_audit_event.sql`, `0011_r1_infrastructure_expand.sql`
- T170–T181 unit sources, unit runner counters, `tests/Verification/architecture.tests.ps1`
- Typed Web gateway/context, AppShell and configuration/simulator/latest/audit routes, Web behavior matrix source
- This RED, review and checkpoint evidence plus `docs/blocker-report.md`

## Commands and exact results

| Command | Exit | Result |
|---|---:|---|
| Temporary RED Debug build | 0 | PASS; baseline compiled before corrective probe. |
| Temporary RED focused runner | 1 | Expected natural RED: 15 cases / 15 failures; Phase 0–8 suites green; no crash. |
| `dotnet build .\IUMP.slnx --no-restore --configuration Debug` | 0 | PASS |
| `dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore --configuration Debug` | 0 | PASS; T170–T181 expose cases/assertions/failures; all failures 0. |
| `dotnet build .\IUMP.slnx --no-restore --configuration Release` | 0 | PASS |
| Release focused unit runner | 0 | PASS |
| `tests/Verification/architecture.tests.ps1` | 0 | PASS; T221 result PASS |
| `git diff --check` | 0 | PASS |
| Web `npm run lint` | 0 | PASS (existing oxlint warning only) |
| Web `npm run build` | 0 | PASS |
| Fast harness | 0 | PASS |
| Full harness | 20 | **Non-passing**: PASS=10, BLOCKED_BY_MISSING_TOOL=1 (`psql`), BLOCKED_BY_COMPANY_APPROVAL=2 (CI/container). |

## Functional evidence

- T170: UUID/int/decimal/timestamp normalization, deterministic order, If-Match and exclusion rules.
- T172: live/expired Pending, exact replay metadata, typed transient handling, and transaction seam.
- T173/T174: hash conflict, leases, retry, per-consumer inbox and restart deduplication.
- T175/T176: schema/hash/redaction, immutable source identity, scope-before-paging/keyset.
- T177: 250ms/1s/2s/5s/30s-capped retry, exhaustion and reconciliation/replay seam.
- T178–T181: public endpoints delegate to typed command/query ports; no static response arrays.
- Web: typed gateways outside components; AppShell and routes expose loading, forbidden, expired and No Data states.
- Crash/replay: the RED probe proved no crash and the green tests prove exact replay; PostgreSQL crash/E2E execution is blocked, not claimed.

## Capability and progression

| Capability | State |
|---|---|
| Browser source/build ready | YES |
| Ready for Phase 10 | YES (source-level Phase 9 closure; Phase 10 remains a separate task phase) |
| Live API/Worker runtime | NO |
| PostgreSQL E2E/migrations | NO — DB capability available at 127.0.0.1:5433/iump_dev, execution NOT_RUN |
| Release | NO |

Blocked ledger is explicit: T192/T193/T202/T205/T218 are `BLOCKED_BY_PACKAGE_POLICY`; T206/T219/T220
are `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE`. None counts as PASS. No Phase 10 task was executed.
