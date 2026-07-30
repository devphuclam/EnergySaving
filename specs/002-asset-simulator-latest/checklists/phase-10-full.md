# Phase 10 Full Harness Capability Probe

- Timestamp start: `2026-07-29T16:40:50.4220874+07:00`
- Timestamp end: `2026-07-29T16:41:13.4179889+07:00`
- Baseline SHA: `e2b3c40d00055de8e836801664595f21a6a36204`
- Exact command: `& .\scripts\harness.ps1 -Mode Full -Feature 002-asset-simulator-latest`
- Actual numeric exit: **20**
- Actual summary: **PASS=10**, **BLOCKED_BY_MISSING_TOOL=1**,
  **BLOCKED_BY_COMPANY_APPROVAL=2**
- Full result: **NON-PASSING / BLOCKED**

Passing checks were feature artifacts, verification contract, repository harness, repository
policy, repository scope, architecture, architecture red fixture, Unit executable, backend build
and frontend lint/build. Frontend lint emitted three non-failing fast-refresh warnings.

## Mandatory blocker breakdown

| Task | Required classification | Probe evidence | State |
|---|---|---|---|
| T242 | `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE` | PostgreSQL adapter/host registration and T233/T236 execution prerequisites are unavailable; no database test ran | BLOCKED / unchecked |
| T243 | `BLOCKED_BY_PACKAGE_POLICY` | Existing cached build inputs pass, but approved PostgreSQL adapter/runtime packages are not available and no package was installed/downloaded | BLOCKED / unchecked |
| T244 | `BLOCKED_BY_MISSING_TOOL` | harness database check: `psql executable is missing` (`BLK-ENV-002`) | BLOCKED / unchecked |
| T245 | `BLOCKED_BY_COMPANY_APPROVAL` | no approved company runner/template (`BLK-ENV-003`) and container/target approval absent (`BLK-ENV-004`) | BLOCKED / unchecked |

The approved PostgreSQL database capability remains **AVAILABLE** at
`127.0.0.1:5433/iump_dev`; the probe did not connect to it. The database check was blocked by the
missing client tool, not by database access. No secret was read or emitted.

Boundary evidence:

- database connection/mutation: **NOT_RUN**
- migration execution: **NOT_RUN**
- package restore/install/download: **NOT_RUN**
- port `5432` contacted: **NO**
- Docker/container started: **NO**
- public/company CI executed: **NO**
- Full PASS claimed: **NO**
- release evidence generated: **NO**

## 2026-07-30 runtime-resolution addendum

Fresh closure evidence supersedes the stale local package/CLI/database probe:

- backend build: PASS
- frontend lint/build: PASS
- database target check as `iump_app`: PASS
- T233: PASS
- T243: PASS
- T244: PASS
- T242: unchecked because its T236 dependency is not complete, even though the Full database check
  itself passes
- company CI and deployment target: BLOCKED_BY_COMPANY_APPROVAL

Full remains non-passing and release readiness remains NO. Exact final numeric exit and summary
are recorded in `runtime-blocker-resolution.md`.

## 2026-07-30 final functional/recovery Full addendum

- T234 complete PostgreSQL quickstart: PASS.
- T236 PostgreSQL recovery/race E2E: PASS, 6 scenarios, 0 failures.
- T242 Full database check: PASS (`database target=PASS`).
- Backend build/unit: PASS.
- Frontend lint/build: PASS; three non-failing fast-refresh warnings.
- Full summary: `PASS=11`, `BLOCKED_BY_COMPANY_APPROVAL=2`.
- Captured process exit: **20**.
- Full overall result: **NON-PASSING / BLOCKED**, as required while mandatory company approval
  is absent.
- Port 5432 contacted: NO.
- Secret emitted: NO.

T245 remains `BLOCKED_BY_COMPANY_APPROVAL`. The Full database check is no longer blocked, but
release readiness remains NO.

## 2026-07-30 post-review evidence correction

The Full database check itself remains PASS, but T236 and T242 are unchecked:

- the six-scenario PostgreSQL recovery probe is useful partial evidence;
- it is not execution of the complete T226-T229 suite required by T236;
- therefore T236 is `NOT_RUN / runnable`;
- T242 cannot complete while its explicit T236 dependency is incomplete.

The earlier T236/T242 PASS statements are superseded. No Full PASS or release-ready state is
claimed.

## 2026-07-30 fresh Full and exact-coverage correction

The fresh Full process completed with numeric exit **20**, `PASS=11`, and
`BLOCKED_BY_COMPANY_APPROVAL=2`. Its database target check passed. T236 nevertheless remains
`NOT_RUN / runnable` because the complete T226-T229 matrix has not executed, so dependency-bound
T242 remains unchecked. This supersedes every earlier T236/T242 PASS statement.

The ordered migration command evidence remains PASS, but T233 is unchecked because its declared
runnable task dependencies are incomplete.

## 2026-07-30 exact runnable-dependency resolution

The PostgreSQL leaf suite now executes the previously incomplete task-specific dependencies:
T031, T052, T074, T090, T127, T148, T166, and T206. T219/T220 and the complete combined
T226-T229 race/crash/E2E matrix also pass. Therefore T233 and T236 are checked PASS.

T242 requires one fresh Full execution after these final changes. T034/T235/T245 remain company
approval blockers, so Full and release readiness must remain non-passing even when every runnable
local check passes.

## 2026-07-30 final fresh Full execution

- T242 database check: **PASS**.
- Exact command: `& .\scripts\harness.ps1 -Mode Full -Feature 002-asset-simulator-latest`.
- Fresh numeric exit: **20**.
- Fresh summary: **PASS=11**, **BLOCKED_BY_COMPANY_APPROVAL=2**, **FAIL=0**.
- Backend Release build/unit: **PASS**, exit **0/0**.
- Frontend lint/build: **PASS**; three non-failing fast-refresh warnings.
- Database target check: **PASS**.
- Company CI and container/deployment target: **BLOCKED_BY_COMPANY_APPROVAL**.
- Full overall: **NON-PASSING / BLOCKED**.
- Release-ready: **NO**.
- Secret emitted: **NO**. Port 5432 contacted: **NO**.
