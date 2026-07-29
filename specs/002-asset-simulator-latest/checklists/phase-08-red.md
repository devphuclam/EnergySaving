# Phase 8 RED Evidence — Latest and Source Health

Feature: `002-asset-simulator-latest`  
Parent baseline: `8f9edde5c39a0370f944ce8c8e12f48af7b353a0`  
Captured: 2026-07-29 (Asia/Bangkok)

## T152–T154 true RED

The Phase 8 tests were added before the production ports/services. They compile
cleanly and fail only on the named missing behavior assertions; no production
placeholder or package reference was added.

Exact files:

- `tests/Unit/Telemetry/PointLatestTests.cs`
- `tests/Unit/Telemetry/SourceHealthTests.cs`
- `tests/Unit/Operations/DurableJobTests.cs`
- `tests/Unit/Program.cs`

Commands and exits:

```text
dotnet build IUMP.slnx -c Debug --no-restore
exit 0 (Build succeeded; 0 warnings; 0 errors)

dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj -c Debug --no-build --no-restore
exit 1
T152: cases=4; checks=4; failures=4
T153: cases=4; checks=4; failures=4
T154: cases=4; checks=4; failures=4
```

The 12 failures identify missing Latest ordering/CAS/event behavior, missing
Online/Stale/NoData threshold and recovery behavior, and missing durable job
uniqueness/lease/retry/reconciliation behavior. Existing Phase 0–7 suites
remained green in the same executable.

## Boundary evidence

- No restore/download was requested or performed.
- No database connection, migration execution, or data mutation was performed.
- PostgreSQL port `5432` was not contacted; PostgreSQL adapter work remains out of scope.
- No Docker/container was used.
- No secret value was read, printed, serialized, or stored.
- Production implementation was absent when the red run was captured.
