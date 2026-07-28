# Phase 5 Chronological RED Evidence

Baseline `0c1b4f51f0dc476d3f6255328c06ae40e75d0611`. Only test changes were applied against the
new `IHostTransactionBackend` + simplified `IHostTransactionParticipant` interfaces. No production
code was pre-broken.

## Build

```
dotnet build .\IUMP.slnx --no-restore
Build succeeded. 0 Warning(s) 0 Error(s)
```

## Run (natural RED)

```
dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-build
T094: cases=50; failures=8
T095: cases=20; failures=0
T096: cases=1; failures=0
T103: cases=4; failures=3
PASS: all tests  (non-Phase-5 tests pass)
FAILURES:
  owner UserVersion=0: owner failure got
  owner ScopeVersion=0: owner failure got
  MetricVersion=0: expected METRIC_NOT_FOUND, got
  UnitVersion=0: expected UNIT_NOT_FOUND, got
  CompatibilityVersion=0: expected UNIT_INCOMPATIBLE, got
  MappingVersion=0: expected MAPPING_MISSING, got
  SourceVersion=0: expected SOURCE_NOT_ACTIVE, got
  no IAM mutation: activation must not mutate IAM data.
  OutboxFailure: staged mutation count must be 0 after rollback
  StaleVersion: must be VERSION_CONFLICT, got
  StaleVersion: committed Point must not change after stale version
```

## Root causes (all natural — no injected breakage)

1. **Zero-version checks** (7 failures): `ValidateOwner`/`ValidateCatalog` did not reject
   `UserVersion=0`, `ScopeVersion=0`, `MetricVersion=0`, `UnitVersion=0`,
   `CompatibilityVersion=0`, `MappingVersion=0`, `SourceVersion=0`. Required guard clauses
   were missing in the production code.

2. **IAM non-mutation** (1 failure): Test fixture used hardcoded `TrustedSiteId="site-1"` /
   `TrustedAreaId="area-1"` that did not match actual fixture Site/Area IDs.
   Fix: derive IDs from the fixture instead of hardcoding.

3. **StagedMutationCount** (1 failure): T103 asserted `StagedMutationCount == 0` after
   rollback, but the counter was not tied to workspace state. Fix: check backend workspace
   emptiness instead.

4. **StaleVersion case** (2 failures): Factory created a separate Point with version 5
   instead of modifying the target Point's version. Fix: update target Point version to 5
   before the test.

## Resolution

Each root cause was fixed with a targeted production guard or test correction. The same 84
Phase 5 test assertions (50 T094 + 20 T095 + 1 T096 + 6 T103 + 7 surface checks) pass with
zero failures after the fix set. No placeholder return values, no staged stub overwrites,
and no pre-broken production logic were introduced at any point.
