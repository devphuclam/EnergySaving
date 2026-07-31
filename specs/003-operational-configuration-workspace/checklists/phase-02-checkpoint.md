# Phase 2 Corrective Stop Checkpoint

Date: 2026-07-31
Feature: `003-operational-configuration-workspace`
Implemented phase: Phase 2 only (`T037`-`T048`)
Baseline: `09347b467b94b69275d1b69d212613be6cc37539`

## Scope and ledger

The rejected Phase 2 baseline was reopened for T038, T039, T040, T043, T044, T045, T046, and
T048. T037, T041, and T042 were re-evaluated for relationship/exclusion evidence; T047 remains
pass. No T049+ task was started and Feature 002 was not changed.

| Disposition | Count | Tasks |
|---|---:|---|
| PASS | 9 | T037, T039, T040, T041, T042, T044, T045, T046, T047 |
| FAIL / incomplete | 3 | T038, T043, T048 |
| BLOCKED | 1 evidence capability | Approved frontend/browser behavior runner unavailable under package policy |
| Runnable NOT_RUN | 1 evidence journey | Hosted PostgreSQL browser journey and complete HTTP lifecycle/replay matrix |

No incomplete task is marked PASS. The BLOCKED capability does not turn the runnable HTTP and
browser acceptance requirements into a pass.

## Corrective implementation evidence

- T037/T041/T042: owner duplication services still create a new Draft identity/version, preserve
  supported relationships, and return explicit review relationships plus excluded fields. History,
  Runs, Measurements, Latest, Source Health, Audit, sessions, tokens, credentials, and secrets
  remain excluded.
- T039/T040: typed management contracts and scope-before-paging queries are present. Simulator
  search matches configuration ID, Source ID, and current version before total/page slicing.
- T044: create/detail/edit/validate/lifecycle/duplicate/delete routes use owner commands, server
  principal, idempotency, antiforgery metadata, transaction, and If-Match where applicable; no
  controller SQL was added. Runtime failures are mapped to a safe dependency result.
- T045/T046: all seven resources have Vietnamese list/detail/editor/action surfaces and explicit
  loading, empty, success, validation, conflict, forbidden, not-found, dependency, and runtime
  states. Unsupported lineage edits are now omitted or read-only with an explanation. Duplicate
  feedback remains visible through refresh; relationship review is shown in the UI.
- T043 remains incomplete: the activation API still accepts client-supplied review/validation
  booleans. Owner validation checks the exact latest Draft fields, but no persisted/server-derived
  relationship-review receipt is tied to activation.
- T038/T048 remain incomplete: the required HTTP lifecycle/delete/replay/authorization/outbox
  matrix and real PostgreSQL browser journey are not evidenced. The available integration tests
  cover the public command seam plus HTTP validation and stale-update conflict only.

## Fresh verification

| Check | Result | Evidence |
|---|---:|---|
| Solution build | PASS (exit 0) | `dotnet build .\IUMP.slnx --no-restore` |
| API build | PASS (exit 0) | `dotnet build --no-restore .\src\Api\IUMP.Api.csproj` |
| Unit | PASS (exit 0) | all registered suites, zero failures; T037 15 cases/54 assertions |
| PostgreSQL integration | PASS (exit 0) | 14 suites, 0 failures; T038 9 cases/26 assertions; target `127.0.0.1:5433/iump_dev` |
| Web lint | PASS (exit 0) | `npm run lint`; existing Fast Refresh/hook warnings only |
| Web build | PASS (exit 0) | `npm run build` (`tsc -b && vite build`) |
| Architecture | PASS (exit 0) | `tests/Verification/architecture.tests.ps1` |
| Repository policy | PASS (exit 0) | `tests/Verification/repository-policy.tests.ps1` |
| Observability | PASS (exit 0) | `tests/Verification/observability.tests.ps1`; 12 checks/0 failures |
| Fast harness | PASS (exit 0) | `scripts/harness.ps1 -Mode Fast -Feature 003-operational-configuration-workspace`; PASS=8 |
| Frontend/browser behavior runner | BLOCKED | `BLOCKED_BY_PACKAGE_POLICY`; no approved runner installed and none downloaded |
| Full harness | BLOCKED (exit 20) | company approval checks `BLK-ENV-003` and `BLK-ENV-004`; no database-access blocker |

The PostgreSQL run used only the repository-approved `.env` loading path and verified
`127.0.0.1:5433/iump_dev`; port 5432, Docker, SQLite/InMemory, package downloads, and secrets were
not used or printed.

## Review and readiness

The fresh two-axis review is recorded in `phase-02-review.md` and is **NOT ACCEPTED**: Critical
0, but High and actionable Medium findings remain. Implementation-ready closure: **NO**. Release-
ready: **NO**. Next phase remains T049-T056 only after a separate explicit invocation. Stop
condition: **met; execution stops before T049**.
