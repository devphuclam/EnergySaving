# Phase 3 Stop Checkpoint

Date: 2026-08-01
Feature: `003-operational-configuration-workspace`
Implemented phase: Phase 3 only (`T049`-`T056`)
Authoritative baseline: `b07261ff8affd16eef7c2473b5ead3ab0719d25a`

## Scope and ledger

This corrective run started from the authoritative merged `main` baseline above and reopened
only the Phase 3 Simulator workspace tasks. `T057` and every later task were not started. Feature
002 was not modified, no new task IDs were created, and this corrective branch was not merged into
`main` automatically.

| Disposition | Count | Tasks / evidence |
|---|---:|---|
| PASS | 8 | T049-T056 |
| FAIL / incomplete | 0 | — |
| Runnable NOT_RUN | 0 | Hosted matrix and authenticated browser journey completed |
| Capability blocked | 3 | Full CI runner/template and Full container target (`BLOCKED_BY_COMPANY_APPROVAL`); frontend behavior runner (`BLOCKED_BY_PACKAGE_POLICY`) |

## Red-to-green evidence

- T049 red evidence: before the legacy-route and selected-context corrections, the Unit harness
  exited 1 with `T049: cases=3; assertions=6; failures=3` (legacy source/run routes and missing
  antiforgery). The current Unit result is `T049: cases=4; assertions=11; failures=0`.
- T050 red evidence: before route retirement, the PostgreSQL Integration harness exited 1 with
  `T050: cases=1; assertions=33; failures=6`. The current result is
  `T050: cases=1; assertions=34; failures=0`, against `127.0.0.1:5433/iump_dev`.
- The static closure contract initially failed on the mapped legacy Start route and now reports
  `simulator-phase3-closure: failures=0`.
- The selected-start concurrency red case covers configuration drift between preflight and the
  transactional recheck; the current Unit suite reports `T110: cases=66; checks=192; failures=0`.

## Phase 3 implementation evidence

- T049/T053: only four operational POST routes remain: selected workspace Start and selected
  workspace Pause/Resume/Stop. Every route carries antiforgery metadata, requires
  `Idempotency-Key`, and consumes the complete selected context. Legacy source-only and Run-only
  mutation paths are no longer mapped and hosted requests fail closed (404/405).
- T050/T051/T052: selected Start passes Site/Area/Asset/Source/Configuration/version to the
  Acquisition owner. PostgreSQL rechecks the exact active eligible configuration, mapping,
  points, and scope on the transaction connection; a drift or mismatch returns a safe conflict or
  ineligible result without creating a Run. Created Runs retain the exact configuration identity
  and version. History is pinned to the selected context and exposes status, optimistic version,
  counters, interval, and last production time.
- T054/T055: Web uses a pure retry identity helper. One key is created per operation and reused
  across retryable network/dependency failures; the pending Run/version and complete selection are
  preserved. Success, replay, non-retryable failure, selection change, and cancellation clear the
  pending identity. URL query state reconstructs the complete selection after refresh and
  logout/login. Site → Area → Asset → Source → active Configuration selectors are explicit,
  dependent, Vietnamese, and never auto-select or auto-start a Run.

## Hosted HTTP matrix

The authenticated matrix ran against `http://127.0.0.1:5000` with `Host: localhost`, backed by
PostgreSQL `127.0.0.1:5433/iump_dev`; the health endpoint confirmed `database=iump_dev`,
`port=5433`, and `migrationLevel=15`. It exercised unauthenticated rejection, login/session and
antiforgery, selectors, explicit selected Start, exact configuration response, same-key Start
replay, changed-selection key conflict, Pause/Resume/Stop, same-key Pause replay, stale
`If-Match`, out-of-scope selection, all four legacy route rejections, history refresh,
Audit read, logout/login rehydration, and no SQL mutation.

Result: `PASS`, failures `0`; observed statuses included login/selectors `200`, legacy `404/405`,
Start/replay `202`, controls/replays `200`, stale version `409`, Audit `200`, and rehydrated
workspace `200`.

Read-only PostgreSQL evidence after the matrix reported Run rows, Audit rows, and Integration
outbox rows present; no database writes were performed through SQL. No connection to port 5432,
Docker, SQLite/InMemory, package installation, or secret output was used.

## Authenticated real-browser journey

Chrome journey against `http://127.0.0.1:5173` completed with the approved local Administrator
session. Credentials were not recorded. Evidence:

1. Sign in and open Simulator; no Site/Source/configuration was selected and no Run started.
2. Explicitly select Site, Area, Asset, Source, and active Configuration; URL query parameters
   contained the complete selection.
3. Start once, verify Run ID/status/version/counters, refresh the URL, and verify the same Run.
4. Pause, stop the API to force a retryable `request-502`, restore the API, click “Thử lại thao
   tác”, and verify the same Run resumed with the preserved request identity.
5. Resume, logout/login, verify URL and Run reconstruction, Stop, and verify the stopped Run in
   history. No unrelated navigation or automatic Start occurred.
6. Browser console error count: `0`.

## Verification

| Check | Result | Evidence |
|---|---:|---|
| Solution/backend build | PASS | `dotnet build .\\IUMP.slnx --no-restore`; 0 warnings / 0 errors |
| Unit | PASS | Full Unit harness; T049 0 failures and T110 0 failures |
| PostgreSQL integration | PASS | T038 + T050; 15 suites / 0 failures; target `127.0.0.1:5433/iump_dev` |
| Web lint | PASS | `npm run lint`; only pre-existing Fast Refresh warnings |
| Web build | PASS | `npm run build` (`tsc -b && vite build`) |
| Architecture / policy / observability | PASS | Verification contracts exit 0 |
| Fast harness | PASS | `harness.ps1 -Mode Fast -Feature 003-operational-configuration-workspace`; PASS=9 |
| Full harness | BLOCKED | Exit 1 by design: 12 PASS, `BLK-ENV-003` and `BLK-ENV-004` `BLOCKED_BY_COMPANY_APPROVAL` |
| Frontend behavior runner | BLOCKED | `BLOCKED_BY_PACKAGE_POLICY`; no package or runner installed |
| Hosted HTTP matrix | PASS | Real API/DB matrix; failures=0 |
| Authenticated browser journey | PASS | Chrome journey; URL refresh/logout-login, retry, history; console errors=0 |

## Review and stop gate

Fresh Standards and Specification reviews against the baseline completed with **C0 / H0 / M0 /
L0** on both axes. The reviews found no documented-standard violation, no actionable smell, no
missing Phase 3 requirement, and no scope creep. Architecture, repository-policy, and
observability contracts also pass.

Implementation acceptance for this phase: **YES** for the runnable provider-neutral, PostgreSQL,
hosted HTTP, and authenticated browser paths. Release-ready: **NO** while the two company-approved
Full environments and the frontend behavior runner remain unavailable, and because later feature
phases are intentionally out of scope.

Stop here. Do not begin `T057` or Phase 4.
