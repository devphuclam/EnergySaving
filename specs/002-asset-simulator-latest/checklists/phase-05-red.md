# Phase 5 RED Evidence (T097)

Repository: `devphuclam/EnergySaving`
Feature: `specs/002-asset-simulator-latest/`
Parent baseline: `3ae683a14385c0272752e5b18a0fccd2b9b39ed0`
Scope: T094-T107 only. No database command, migration, package restore, Docker, or Phase 6 work was performed.

The corrected T094, T095, T096, and T103 sources were compiled before the production correction. A temporary pre-green defect set was used only to reproduce the red behavior, then immediately reverted: missing-participant rejection disabled, mapping target check disabled, Active no-op made a failure, 450ms changed to 0ms, causation fallback restored, and the successful orchestrator result changed to `PHASE5_REQUIRED`.

## Exact reproduction

Command:

```text
dotnet build tests/Unit/IUMP.Tests.Unit.csproj --no-restore --configuration Debug
```

Result: exit `0` (`Build succeeded`, `0 Warning(s)`, `0 Error(s)`).

Command:

```text
dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj --no-build --configuration Debug
```

Result: exit `1`. Combined captured output (including failure diagnostics):

```text
T079: assertions=87; failures=0
T080: assertions=62; failures=0
T094: cases=41; failures=6
T095: cases=12; failures=2
T096: cases=1; failures=1
T103: cases=4; failures=1
T071: tests=19; assertions=39; failures=0
T088: scenarios=24; assertions=24; failures=0
FAILURES:
  Administrator Draft: expected success, got PHASE5_REQUIRED
  scoped Engineer Draft: expected success, got PHASE5_REQUIRED
  Inactive reactivation: expected success, got PHASE5_REQUIRED
  Active no-op: Active must be successful NO_OP without mutation.
  mapping belongs to another Point: expected MAPPING_POINT_MISMATCH, got PHASE5_REQUIRED
  repeat activation: repeat activation must be a single transition and event (first=PHASE5_REQUIRED, second=INVALID_STATE, status=Active, version=2, history=1, outbox=1).
  missing participant: BeginAsync must fail closed when a required participant is missing.
  retry trace: retry must use 50/150/450ms after three failures.
  absent CausationId must remain null and separate from CorrelationId.
  T103 success case must activate through ActivateMeasurementPoint.
RED_RUN_EXIT=1
```

The same run exercised the transaction identity, partial-commit, prerequisite matrix, causation, retry, and provider-neutral T103 assertions; those assertions were present in the registered suites and are recorded as green after correction below. No secret, connection string, or PostgreSQL endpoint was printed.
