# Phase 3 Stop Checkpoint

Date: 2026-08-01
Feature: `003-operational-configuration-workspace`
Implemented phase: Phase 3 only (`T049`-`T056`)
Authoritative baseline: `b3ccfcd1f86a8208236b70ddb1096b94f239a445`

## Scope and ledger

This run executes only the selected Simulator workspace phase from the authoritative merged
`main` baseline. `T057` and every later task were not started. Feature 002 was not modified, no
new task IDs were created, and the feature branch was not merged into `main`.

| Disposition | Count | Tasks / evidence |
|---|---:|---|
| PASS | 8 | T049-T056 |
| FAIL / incomplete | 0 | — |
| Runnable NOT_RUN | 1 | Authenticated browser journey; the browser tool URL policy rejected localhost |
| Capability blocked | 3 | Full CI runner/template, Full container target (`BLOCKED_BY_COMPANY_APPROVAL`), frontend behavior runner (`BLOCKED_BY_PACKAGE_POLICY`) |

## Red-to-green evidence

- T049 recorded genuine red evidence before implementation: the Unit harness failed to compile
  because `SimulatorSelectionOption` was not yet defined (`CS0246`). After the contracts and
  rules were added, T049 reports 3 cases / 3 assertions / 0 failures.
- T050 recorded the PostgreSQL integration seam before implementation and now exercises explicit
  selection, no-first-Source behavior, fail-closed out-of-scope context, ineligible version,
  protected route metadata, missing Idempotency-Key, missing `If-Match`, Start, replay, changed
  canonical selection conflict, pause/resume/stop, stale version conflict, and persisted history.
  Current result: 1 case / 28 assertions / 0 failures, including a scoped Engineer out-of-scope
  selection assertion.

## Phase 3 implementation evidence

- T051 defines provider-neutral selection, eligibility, Run-history, and workspace command/query
  ports. Selection resolution requires every identity/version field and an eligible option.
- T052 applies PostgreSQL authorization predicates before paging; history is pinned to the
  selected Source/configuration/version and exposes status, optimistic version, counters,
  interval, and last production time. Visible-but-ineligible and unknown/out-of-scope selections
  fail with explicit safe error codes; requested Site/Area/Asset identity is bound in the
  visibility query.
- T053 adds selected workspace selectors, history, and Start/Pause/Resume/Stop routes. Mutation
  routes require antiforgery metadata, Idempotency-Key, and optimistic `If-Match` for controls.
  Replay responses expose `X-Idempotency-Replay` without exposing secrets.
- T054 removes implicit first-Source lookup. The Web UI requires one explicit Site/Area/Asset/
  Source/configuration selection, sends the full selection context, and never starts a Run during
  navigation or initial effect loading.
- T055 renders Run ID, status, optimistic version, counters, interval, last production, paged
  history, replay/success feedback, no-selection, validation, conflict, not-found, forbidden,
  dependency, runtime, and retry states. A failed post-mutation refresh remains an error instead
  of being overwritten by a false success state.

## Verification

| Check | Result | Evidence |
|---|---:|---|
| Unit | PASS | T049 3 cases / 3 assertions / 0 failures; full Unit harness 0 failures |
| PostgreSQL integration | PASS | T038 + T050; 15 suites / 0 failures; target `127.0.0.1:5433/iump_dev` |
| Solution/backend build | PASS | `dotnet build .\\IUMP.slnx --no-restore`; 0 warnings / 0 errors |
| Web lint | PASS | `npm run lint`; only pre-existing Fast Refresh warnings |
| Web build | PASS | `npm run build` (`tsc -b && vite build`) |
| Architecture / policy / observability | PASS | Full harness checks pass |
| Fast harness | PASS | Feature 003 Fast checks pass; no blocked result treated as pass |
| Full harness | BLOCKED | 11 PASS; `BLK-ENV-003` and `BLK-ENV-004` are company-approval blockers |
| Frontend behavior runner | BLOCKED | `BLOCKED_BY_PACKAGE_POLICY`; no package or runner installed |
| Authenticated browser journey | NOT_RUN | Browser URL policy rejected localhost; no credentials were entered and no mutation was triggered |

All database evidence used the repository-approved `.env` loading path and only
`127.0.0.1:5433/iump_dev`. Port 5432, Docker, SQLite/InMemory substitutes, package downloads,
and secret output were not used.

## Review and stop gate

Standards and final Specification reviews against the baseline: 0 Critical / 0 High / 0 Medium
findings. The review corrections in this run are closed: checkpoint artifact added, scope
visibility bound to the requested context, refresh errors preserved, and T050 evidence expanded
for a persisted Engineer scope, stale versions, counters, last production, and antiforgery
metadata.

Implementation acceptance for this phase: **YES** for the runnable provider-neutral and PostgreSQL
paths. Release-ready: **NO** while company-approved Full environments and the frontend behavior
runner remain unavailable, and because later feature phases are intentionally out of scope.

Stop here. Do not begin `T057` or Phase 4.
