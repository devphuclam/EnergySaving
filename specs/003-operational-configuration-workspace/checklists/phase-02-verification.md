# Phase 2 Corrective Verification

Date: 2026-08-01
Feature: `003-operational-configuration-workspace`
Baseline: `c6da87b638ee9f55002a2deb98f7cba96a55abd5`

## Commands and outcomes

| Command | Exit | Outcome |
|---|---:|---|
| `dotnet build .\\IUMP.slnx --no-restore` | 0 | PASS; solution builds with 0 warnings/errors |
| `dotnet run --project .\\tests\\Unit\\IUMP.Tests.Unit.csproj --no-restore` | 0 | PASS; all suites zero failures; T037 15 cases/61 assertions; T079 106 assertions |
| `dotnet run --project .\\tests\\Integration\\IUMP.Tests.Integration.csproj --no-restore` | 0 | PASS; 14 suites/0 failures; T038 9 cases/41 assertions |
| `npm run lint` in `src/Web` | 0 | PASS; existing Fast Refresh warnings only |
| `npm run build` in `src/Web` | 0 | PASS; TypeScript and Vite build |
| `tests/Verification/architecture.tests.ps1` | 0 | PASS |
| `tests/Verification/repository-policy.tests.ps1` | 0 | PASS |
| `tests/Verification/observability.tests.ps1` | 0 | PASS; 12/12 checks |
| `scripts/harness.ps1 -Mode Fast -Feature 003-operational-configuration-workspace` | 0 | PASS=8 |
| `scripts/harness.ps1 -Mode Full -Feature 003-operational-configuration-workspace` | 20 | BLOCKED-only; PASS=11; `BLK-ENV-003` and `BLK-ENV-004` are company-approval blockers |
| hosted authenticated HTTP matrix against `http://127.0.0.1:5000` (`Host: localhost`) | 0 | PASS; login/session, antiforgery, create/list/detail/edit, stale If-Match, duplicate/retry/key-conflict, review, exact validation, activation, lifecycle/delete, unauthorized, not-found, malformed/unsupported JSON, and logout/login rehydration exercised |
| hosted API `/health/live` | 0 | PASS; HTTP 200 |
| hosted API `/health/ready` | 0 | PASS; HTTP 200, `database=iump_dev`, `port=5433`, `migrationLevel=15` |
| receipt composite-FK application | 0 | PASS; idempotent `ON_ERROR_STOP=1` SQL against approved target |

## Database and migration evidence

The integration executable reported only the redacted approved target marker:
`postgres-integration target=127.0.0.1:5433/iump_dev suites=14 failures=0`.
The repository-local `.env` loader supplied the credential at runtime. No secret value was printed,
serialized, committed, or copied to evidence. The standard migration runner stopped fail-fast at
the pre-existing 0002 duplicate-role condition; it did not reset data or connect to port 5432.
Migration `0015_acquisition_simulator_configuration_receipts.sql` and its exact-version composite
FK were applied to `127.0.0.1:5433/iump_dev` with `ON_ERROR_STOP=1`, exit 0.

## Behavioral evidence

- Acquisition receipt tests prove missing review, missing validation, successful activation after
  persisted receipts, fresh receipts after a subsequent edit, and review/validation/activation
  event staging. The relationship fingerprint includes Source and active mapping/readiness
  versions, so a changed relationship cannot reuse a receipt.
- Activation request and Web gateway payload contain only configuration/draft identifiers and the
  expected concurrency version; `relationshipReviewConfirmed` and `validationConfirmed` are absent.
- Simulator duplication returns a persisted Draft v2 with an aggregate-version-2 Duplicated event;
  the copied v1 baseline is not itself treated as an activatable Draft. The management route and
  Web UI require an explicit authorized target Source different from the original Source.
- Mapping create uses explicit authorized Source and Point selectors with no first-item default;
  Data Source and Area create use explicit Site selectors; Asset create uses an authorized Area
  selector; Point create uses an authorized Asset selector; Simulator Configuration create uses an
  explicit authorized Source selector; edit relationships are immutable/read-only.
- Endpoint tests prove malformed JSON is HTTP 400 with `INVALID_JSON`; arrays/scalars and
  unsupported content types are rejected rather than converted into empty fields, including the
  activation endpoint. Optional empty bodies remain distinguishable from malformed JSON.
- Management detail/list now expose safe Source code/name/status/version, copied relationship
  labels, excluded fields, relationship/validation receipt state, and stale flags. The Web action
  reconstructs the review card from that server response after refresh, direct detail navigation,
  logout/login, and a behavior-changing Draft edit; validation is disabled until review is current.
- Selector effects distinguish loading, ready, genuinely empty, forbidden, dependency, and runtime
  failures; unavailable create forms clear stale options and show Vietnamese guidance and Retry
  without fallback IDs. Form validation emits `Tên là bắt buộc.`, `Vui lòng chọn Nguồn dữ liệu.`,
  `Vui lòng chọn Điểm đo.`, and exact Site/Area/Asset parent messages while focusing the first
  invalid field.

## Evidence boundary and blockers

The hosted matrix is complete for the authenticated Administrator session. It returned the expected
HTTP results including 401 unauthorised, 409 stale/key/dependency conflicts, 400 malformed or
unsupported JSON, 404 not-found, and 422 missing-receipt activation. Read-only SQL after the
matrix confirmed receipt rows and Audit rows were persisted from the outbox path; the outbox worker
was run without changing the approved database target. The frontend behavior runner remains
`BLOCKED_BY_PACKAGE_POLICY`; no package was installed or downloaded.

The real browser journey completed with the already-authorized local Administrator session; no
credential value was printed or copied into repository artifacts. It covered refresh/detail,
edit invalidation, logout/login rehydration, re-review/re-validation/re-activation, exact field
validation/focus, and zero console errors. A separate controlled API process-restart probe reached
ready/login but its synthetic Draft create returned 503; no restart-specific receipt claim is made.

No Simulator Run was started. No direct SQL substituted for UI actions. No T049 or later task was
executed.

## Latest runtime retry and recovery

An initial 2026-08-01 retry classified the approved target as
`DATABASE_CONNECTION_RUNTIME_FAILURE` while `127.0.0.1:5433` was unavailable. After the approved
PostgreSQL runtime became available again, `pg_isready` reported accepting connections, the
Integration executable completed with `T038: 9 cases / 41 assertions`, 14 suites, 0 failures,
and the hosted matrix completed with the status matrix above. `127.0.0.1:5432` was not contacted,
no service was started or stopped by the agent, and no SQLite/InMemory substitute was used.
