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
