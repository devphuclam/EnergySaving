# Quickstart: Validate Asset Simulator Latest

This is the planned validation path for R1/VS-01. It does not claim that the feature is implemented.
Use it after `/speckit.tasks` and implementation are complete.

## Prerequisites

- .NET SDK 10.0.300 and Node 24.16.0.
- An approved PostgreSQL instance and credentials supplied through local environment configuration.
- Only locked/approved package sources; no public download and no container dependency.
- Current migrations applied from `0001_r0_foundation` through the R1 ordered set.
- Deterministic POC users/scopes and Catalog seeds applied idempotently.
- The constitution's feature-001/R0 implementation restriction amended or superseded before source
  implementation begins.

Never commit local credentials, generated document extracts, or environment-specific config.

## Repository Checks

Run the existing repository harness from the workspace root:

```powershell
.\scripts\harness.ps1 -Mode Fast -Feature 002-asset-simulator-latest
```

Fast is the developer loop: repository structure, architecture boundaries, formatting/static
checks, and pure tests that require no external service.

```powershell
.\scripts\harness.ps1 -Mode Full -Feature 002-asset-simulator-latest
```

Full is the evidence gate: Fast plus build, clean-database migrations, PostgreSQL integration,
concurrency/idempotency, API/Worker integration, Web checks, and acceptance scenarios. While this
planning command intentionally omits `tasks.md`, Full may report that required artifact missing;
that is expected until `/speckit.tasks`.

## Phase Checkpoints

### 1. IAM

Seed Administrator, Engineer, Operator, Manager, and Viewer users with Site/Area assignments.
Confirm login and `/api/v1/me`; then parameterize every protected command/query with an allowed and
out-of-scope principal. Expected: server-side allow/deny, no target-data leakage, deterministic
seeds, and Data Owner assignment does not grant privilege.

### 2. Catalog

Apply seeds twice. Expected: one Electric Power/kW pair and one Electrical Energy/kWh pair, active
compatibility, no duplicates. Try an inactive or incompatible pair during Point activation and
expect a specific prerequisite error.

### 3. Hierarchy

As an in-scope Engineer create Site -> Area -> Asset -> Point. Prepare Draft children before parents
activate, then activate top-down. Verify a Point cannot activate until parent, Metric, Unit,
intervals, Data Owner, and exactly one mapping are valid. Time the happy path for `SC-001`.

### 4. Simulator and mapping

Create Constant and Normal configurations, map one source to multiple Points, and attempt an
overlapping second Active mapping. Expected: the second activation fails with a domain conflict
(`SC-007`). Start, pause, resume, stop, restart the Worker, and verify persisted state, lease
exclusion, per-Run+Point deterministic sequence, and no output while Paused/Stopped.

### 5. Canonical ingestion

Submit controlled Good, future-skew, out-of-range, duplicate, late, malformed, and unauthorized
records. Expected:

- Good: accepted and eligible for Latest.
- Future beyond configured 300-second default: accepted Uncertain,
  `SOURCE_TIMESTAMP_FUTURE`, Latest-eligible.
- Out of range: accepted Bad, `VALUE_OUT_OF_RANGE`, not Latest-eligible.
- Duplicate: one stored identity/Measurement and no doubled counters.
- Rejected input: no Measurement.

Run race tests against the same identity and Point using PostgreSQL; an in-memory replacement is not
valid evidence.

### 6. Latest and Source Health

Feed equal source timestamps with different sequence, processing time, and IDs. Expected: strict
P-003 tuple ordering and no regression from late/concurrent arrivals. Advance a controlled clock
through Online, Stale, and No Data thresholds; verify No Data is derived and never numeric zero
(`SC-004`). Suspend/decommission the source and verify administrative precedence.

### 7. API and Web slice

As an Operator scoped to one Site, load the current Point page once. Expected: every active Point in
scope exposes Latest value/unit/timestamps/quality and Source Health (`SC-003`), while another Site
is absent from lists and direct access is denied (`SC-005`). Start a Simulator and verify the first
accepted Measurement becomes visible within two minutes (`SC-002`).

### 8. Audit and retention

Create/update/status-change hierarchy records, change a mapping, and perform all four Simulator
commands. Query by correlation ID. Expected: immutable actor/time/object/action/before/after/summary
evidence appears within five seconds (`SC-006`). Attempt hard deletion after run/Measurement
history; expect `DEPENDENT_HISTORY` (`SC-008`). Confirm Draft-unused deletion preserves readable
audit snapshots.

## Test Classification

| Classification | Runs in | Examples |
|---|---|---|
| Pure domain/unit | Fast | state transitions, validation, quality, tuple comparison, deterministic generator |
| Architecture/static | Fast | module references, schema ownership, endpoint authorization metadata |
| Repository/migration | Full | clean and upgrade migration, indexes, constraints, permissions, seeds |
| PostgreSQL integration | Full | overlap exclusion, atomic Latest, duplicate races, outbox/inbox |
| Worker integration | Full | leases, restart recovery, generation schedule, health evaluator |
| API/Web integration | Full | role/scope matrix, response contracts, single-screen current view |
| Acceptance/performance | Full | `SC-001..SC-008`, two-minute/five-second thresholds |

## Evidence to Retain

Retain harness output, migration version, test classification/results, correlation IDs for the timed
flows, and any approved blocker record. Do not substitute a manual screenshot for authorization,
concurrency, migration, or idempotency evidence.
