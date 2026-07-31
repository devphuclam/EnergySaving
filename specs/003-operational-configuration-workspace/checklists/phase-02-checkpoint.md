# Phase 2 Corrective Stop Checkpoint

Date: 2026-07-31
Feature: `003-operational-configuration-workspace`
Implemented phase: Phase 2 only (`T037`-`T048`)
Baseline: `f9a6c740780d699e3da7479cfa5639e18c558b94`

## Scope and ledger

This corrective run reopened only T038, T043, T044, T046, and T048 from the requested main
baseline. T037, T039-T042, T045, and T047 were not regressed. No T049+ task was started and
Feature 002 was not changed.

| Disposition | Count | Tasks |
|---|---:|---|
| PASS | 10 | T037, T039-T047 |
| FAIL / incomplete | 2 | T038, T048 |
| Capability blocked | 2 | frontend behavior runner; authenticated browser/session capability |
| Runnable NOT_RUN | 1 | complete authenticated hosted HTTP lifecycle/replay/Audit-outbox matrix |

T043, T044, and T046 are closed by implementation and automated evidence. T038 and T048 remain
open because the required authenticated hosted matrix and browser journey could not be completed
without an approved application-session credential capability. Those gaps are not reported as
PASS.

## Corrective implementation evidence

- T043: `acquisition.simulator_configuration_receipt` is Acquisition-owned and Draft-version
  bound. Each row stores configuration ID, Draft version, Source ID, deterministic relationship
  fingerprint, review actor/time, validation payload fingerprint, validation actor/time. The
  relationship fingerprint includes Source identity/status/version, active mapping identity/version,
  Point/Site/Area IDs, and readiness versions. Review upsert clears validation; validation updates
  only an existing reviewed row; activation reads PostgreSQL and rejects missing, mismatched, or
  stale receipts. A composite FK also binds the receipt to the exact persisted configuration
  version. Client authority booleans are absent from the activation contract and Web payload.
- T043 receipt tests: T079 reports 102 assertions/0 failures. They cover missing review, missing
  validation, successful activation after persisted receipts, a subsequent edit requiring fresh
  receipts, and review/validation/activation event staging.
- T044: management endpoints use owner commands, server principal, idempotency, antiforgery,
  host transactions, optimistic versions, and safe JSON/content-type failures. Malformed JSON,
  arrays/scalars, or unsupported content types return `400` with `INVALID_JSON`.
- T046: Mapping creation loads authorized Sources and Points into explicit Vietnamese selectors;
  no first-item default is used. Data Source and Area creation use explicit Site selectors.
  Simulator Configuration creation requires an explicit authorized Source. Edit relationship
  fields are read-only/immutable.
- Duplicate Simulator behavior now persists a version-1 baseline plus explicit Draft version 2;
  the returned `draftConfigurationVersion` is activatable only after fresh server receipts. The
  duplicate event records aggregate version 2 and Draft version 2.
- The persisted receipt/activation authority in this corrective run is intentionally scoped to the
  Acquisition-owned Simulator Configuration Draft, matching the request. Other owner modules keep
  their existing Draft/lifecycle contracts and relationship metadata; a shared cross-module review
  workflow is not introduced here.
- T038 public seam evidence: T038 reports 9 cases/34 assertions/0 failures against PostgreSQL.
  Hosted API health was exercised against `http://127.0.0.1:5000` with `Host: localhost`:
  `/health/live` and `/health/ready` returned HTTP 200; ready reported `database=iump_dev`,
  `port=5433`, and `migrationLevel=15`.

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
| Unit | PASS (exit 0) | all suites, zero failures; T037 15 cases/57 assertions; T079 102 assertions |
| PostgreSQL integration | PASS (exit 0) | 14 suites, 0 failures; T038 9 cases/34 assertions; target `127.0.0.1:5433/iump_dev` |
| Web lint | PASS (exit 0) | `npm run lint`; existing Fast Refresh warnings only |
| Web build | PASS (exit 0) | `npm run build` (`tsc -b && vite build`) |
| Architecture | PASS (exit 0) | `tests/Verification/architecture.tests.ps1` |
| Repository policy | PASS (exit 0) | `tests/Verification/repository-policy.tests.ps1` |
| Observability | PASS (exit 0) | `tests/Verification/observability.tests.ps1`; 12/12 checks |
| Fast harness | PASS (exit 0) | `scripts/harness.ps1 -Mode Fast -Feature 003-operational-configuration-workspace`; PASS=8 |
| Full harness | BLOCKED (exit 20) | PASS=11; `BLK-ENV-003` and `BLK-ENV-004` are company-approval blockers |
| Frontend behavior runner | BLOCKED | `BLOCKED_BY_PACKAGE_POLICY`; no runner installed/downloaded |
| Authenticated browser journey | BLOCKED | no approved app-session credential capability; unauthenticated UI session expired |
| Hosted authenticated HTTP matrix | NOT_RUN | no approved app-session credential capability |

All database evidence used only the repository-approved `.env` loading path and
`127.0.0.1:5433/iump_dev`. Port 5432, Docker, SQLite/InMemory, package installation, public
downloads, and secret output were not used.

## Review and readiness

Fresh Standards and Specification reviews find no remaining implementation Critical/High/Medium
code defect in receipts, selectors, duplicate Draft handling, ambient transaction ownership, or
malformed JSON. They still cannot mark the phase accepted because the required hosted authenticated
matrix and real browser journey are evidence-bound capabilities. Phase 2 acceptance is **NO**;
release-ready is **NO**. Stop before T049; the next phase remains T049-T056 and requires a separate
explicit invocation.
