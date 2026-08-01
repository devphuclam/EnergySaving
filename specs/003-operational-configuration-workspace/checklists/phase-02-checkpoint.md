# Phase 2 Corrective Stop Checkpoint

Date: 2026-08-01
Feature: `003-operational-configuration-workspace`
Implemented phase: Phase 2 only (`T037`-`T048`)
Baseline: `c6da87b638ee9f55002a2deb98f7cba96a55abd5`

## Scope and ledger

This corrective run reopened only T038, T043, T044, T046, and T048 from the requested main
baseline. T037, T039-T042, T045, and T047 were not regressed. No T049+ task was started and
Feature 002 was not changed.

| Disposition | Count | Tasks |
|---|---:|---|
| PASS | 12 | T037-T048 |
| FAIL / incomplete | 0 | — |
| Capability blocked | 1 | frontend behavior runner (`BLOCKED_BY_PACKAGE_POLICY`) |
| Runnable NOT_RUN | 0 | hosted authenticated HTTP matrix and browser journey are complete |

T044 has been reevaluated with no endpoint regression. The reopened T038, T043, T046, and T048
corrections are now demonstrated through the hosted matrix and the real browser journey. The
frontend behavior runner remains a separate package-policy capability blocker and is not reported
as a pass.

## Corrective implementation evidence

- T043: `acquisition.simulator_configuration_receipt` is Acquisition-owned and Draft-version
  bound. Each row stores configuration ID, Draft version, Source ID, deterministic relationship
  fingerprint, review actor/time, validation payload fingerprint, validation actor/time. The
  relationship fingerprint includes Source identity/status/version, active mapping identity/version,
  Point/Site/Area IDs, and readiness versions. Review upsert clears validation; validation updates
  only an existing reviewed row; activation reads PostgreSQL and rejects missing, mismatched, or
  stale receipts. A composite FK also binds the receipt to the exact persisted configuration
  version. Client authority booleans are absent from the activation contract and Web payload.
- T043 receipt tests: T079 reports 106 assertions/0 failures. They cover missing review, missing
  validation, successful activation after persisted receipts, a subsequent edit requiring fresh
  receipts, and review/validation/activation event staging.
- T044: management endpoints use owner commands, server principal, idempotency, antiforgery,
  host transactions, optimistic versions, and safe JSON/content-type failures. Malformed JSON,
  arrays/scalars, or unsupported content types return `400` with `INVALID_JSON`.
- T043/T046 corrective seam: management list/detail responses include safe Source labels,
  relationship/exclusion metadata, persisted review/validation flags, and stale receipt flags.
  The Web review action is reconstructed from that response after refresh, direct detail, and
  behavior-changing edit; selector failures are explicit loading/ready/empty/forbidden/dependency/
  runtime states with Retry, and unavailable create forms cannot submit empty relationship IDs.
  Mapping creation loads authorized Sources and Points into explicit Vietnamese selectors; Data
  Source and Area creation use explicit Site selectors; Asset creation loads authorized Areas and
  Point creation loads authorized Assets; Simulator creation requires an explicit authorized
  Source; edit relationship fields remain immutable/read-only.
- Duplicate Simulator behavior now persists a version-1 baseline plus explicit Draft version 2;
  the returned `draftConfigurationVersion` is activatable only after fresh server receipts. The
  duplicate event records aggregate version 2 and Draft version 2.
- The persisted receipt/activation authority in this corrective run is intentionally scoped to the
  Acquisition-owned Simulator Configuration Draft, matching the request. Other owner modules keep
  their existing Draft/lifecycle contracts and relationship metadata; a shared cross-module review
  workflow is not introduced here.
- T038 PostgreSQL seam evidence: T038 reports 9 cases/41 assertions/0 failures against PostgreSQL.
  The hosted matrix against `http://127.0.0.1:5000` with `Host: localhost` returned: login 200,
  unauthenticated list 401, antiforgery 200, create 201, stale If-Match 409, duplicate/retry
  201/201, changed-key 409, review/validation/activation 200/200/200, safe Draft delete 200,
  active/dependent delete 409, malformed/unsupported JSON 400/400, not-found 404, logout/login
  200/200, and persisted session/detail 200.

## Database and migration evidence

The repository migration runner was attempted with `ON_ERROR_STOP=1` and stopped at the
pre-existing 0002 duplicate-role condition; it did not reset data or touch port 5432. Migration
`0015_acquisition_simulator_configuration_receipts.sql` was applied to the approved target with
`ON_ERROR_STOP=1`, and the receipt-version composite FK was subsequently applied idempotently;
the SQL command returned exit 0. No password value was printed or persisted.

## Fresh verification

| Check | Result | Evidence |
|---|---:|---|
| Solution build | PASS (exit 0) | `dotnet build .\\IUMP.slnx --no-restore` |
| Unit | PASS (exit 0) | all suites, zero failures; T037 15 cases/61 assertions; T079 106 assertions |
| PostgreSQL integration | PASS (exit 0) | 14 suites, 0 failures; T038 9 cases/41 assertions; target `127.0.0.1:5433/iump_dev` |
| Web lint | PASS (exit 0) | `npm run lint`; existing Fast Refresh warnings only |
| Web build | PASS (exit 0) | `npm run build` (`tsc -b && vite build`) |
| Architecture | PASS (exit 0) | `tests/Verification/architecture.tests.ps1` |
| Repository policy | PASS (exit 0) | `tests/Verification/repository-policy.tests.ps1` |
| Observability | PASS (exit 0) | `tests/Verification/observability.tests.ps1`; 12/12 checks |
| Fast harness | PASS (exit 0) | `scripts/harness.ps1 -Mode Fast -Feature 003-operational-configuration-workspace`; PASS=8 |
| Full harness | BLOCKED (exit 20) | PASS=11; `BLK-ENV-003` and `BLK-ENV-004` are company-approval blockers |
| Frontend behavior runner | BLOCKED | `BLOCKED_BY_PACKAGE_POLICY`; no runner installed/downloaded |
| Authenticated browser journey | PASS (12/12, 0 failures) | Real Chrome journey covered login, server-derived Draft review, refresh/direct detail, validation, activation, edit invalidation, logout/login persistence, re-review/re-validation/re-activation, and final detail |
| Hosted authenticated HTTP matrix | PASS (exit 0) | real HTTP matrix completed against `127.0.0.1:5000`/`iump_dev`; receipt, Audit, and outbox read-only evidence recorded |

All database evidence used only the repository-approved `.env` loading path and
`127.0.0.1:5433/iump_dev`. Port 5432, Docker, SQLite/InMemory, package installation, public
downloads, and secret output were not used.

## Review and readiness

Fresh Standards and Specification reviews are complete: Standards C0/H0/M0 and Specification
C0/H0 with one documented Medium evidence boundary for restart-specific receipt persistence and no
implementation blocker. Current implementation acceptance is **YES**: all twelve
Phase 2 tasks are complete and the browser journey is PASS12/FAIL0 with runnable NOT_RUN0.
The browser logout/login journey demonstrates persisted receipt state; a separate controlled API
process-restart probe reached ready/login but its synthetic Draft create returned 503, so this
checkpoint makes no restart-specific receipt claim. Release-ready remains **NO** because the
frontend behavior runner is `BLOCKED_BY_PACKAGE_POLICY` and Full harness environment checks remain
`BLOCKED_BY_COMPANY_APPROVAL`. A transient 2026-08-01 runtime failure was recovered; the approved
PostgreSQL target now accepts connections and the follow-up Integration and hosted HTTP matrix pass.
Stop before T049; the next phase remains T049-T056 and requires a separate explicit invocation.
