# Phase 5 RED Evidence (T097)

## Scope and baseline

- Repository: `devphuclam/EnergySaving`
- Feature: `specs/002-asset-simulator-latest/`
- Parent baseline: `4e68ca46d124d867a0737b17711a069bd83417aa`
- Captured: `2026-07-27`
- The RED reproduction used only the local deterministic unit harness. No package restore,
  database connection, migration, Docker, or substitute database was used.

## Chronological reproduction

After the Phase 5 test sources were compiled against the pre-green command path, the exact
commands were run:

```text
dotnet build tests/Unit/IUMP.Tests.Unit.csproj --no-restore --configuration Debug
exit 0; Build succeeded; 0 warnings; 0 errors

dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj --no-build --configuration Debug
exit 1
T079: assertions=87; failures=0
T080: assertions=62; failures=0
T071: tests=19; assertions=39; failures=0
T088: scenarios=24; assertions=24; failures=0
FAILURES:
  Phase 5 orchestrator must handle Admin activation of Draft Point (currently PHASE5_REQUIRED).
  Phase 5 orchestrator must handle Admin reactivation of Inactive Point (currently PHASE5_REQUIRED).
  Phase 5 orchestrator must handle scoped Engineer activation of Draft Point (currently PHASE5_REQUIRED).
  Phase 5 orchestrator must handle Active -> Active as silent no-op (currently PHASE5_REQUIRED).
  Phase 5 orchestrator must reject Decommissioned -> Active with INVALID_STATE (currently PHASE5_REQUIRED).
  Phase 5 orchestrator must emit a PointStatusChanged.v1 event on activation (none emitted).
```

The failed assertions are the intended red proof: the ordinary Phase 3 status handler deferred
activation/reactivation to Phase 5, and no owner envelope was emitted by that path.

## RED coverage

The red suite covered prerequisite-specific outcomes (parent Asset, Metric, Unit/compatibility,
Data Owner, mapping cardinality/effectivity), stale/provider-version rechecks, global lock order,
host rollback, outbox atomicity, and owner-event metadata. T094–T096 therefore proved the behavior
was absent before the Phase 5 coordinator/orchestrator/envelope implementation.

## Security and boundary evidence

- Secret values were not printed or stored.
- No PostgreSQL endpoint was contacted; T104 remains a separate package-policy-transitive block.
- No API, Worker, Telemetry, Simulator Run, migration, or Phase 6 source was added.
