# Phase 2 Corrective Verification

Date: 2026-07-31
Feature: `003-operational-configuration-workspace`
Baseline: `f9a6c740780d699e3da7479cfa5639e18c558b94`

## Commands and outcomes

| Command | Exit | Outcome |
|---|---:|---|
| `dotnet build .\\IUMP.slnx --no-restore` | 0 | PASS; solution builds with 0 warnings/errors |
| `dotnet run --project .\\tests\\Unit\\IUMP.Tests.Unit.csproj --no-restore` | 0 | PASS; all suites zero failures; T037 15 cases/57 assertions; T079 102 assertions |
| `dotnet run --project .\\tests\\Integration\\IUMP.Tests.Integration.csproj --no-restore` | 0 | PASS; 14 suites/0 failures; T038 9 cases/34 assertions |
| `npm run lint` in `src/Web` | 0 | PASS; existing Fast Refresh warnings only |
| `npm run build` in `src/Web` | 0 | PASS; TypeScript and Vite build |
| `tests/Verification/architecture.tests.ps1` | 0 | PASS |
| `tests/Verification/repository-policy.tests.ps1` | 0 | PASS |
| `tests/Verification/observability.tests.ps1` | 0 | PASS; 12/12 checks |
| `scripts/harness.ps1 -Mode Fast -Feature 003-operational-configuration-workspace` | 0 | PASS=8 |
| `scripts/harness.ps1 -Mode Full -Feature 003-operational-configuration-workspace` | 20 | BLOCKED-only; PASS=11; `BLK-ENV-003` and `BLK-ENV-004` are company-approval blockers |
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
  Data Source create uses an explicit Site selector; Simulator Configuration create uses an
  explicit authorized Source selector; edit relationships are immutable/read-only.
- Endpoint tests prove malformed JSON is HTTP 400 with `INVALID_JSON`; arrays/scalars and
  unsupported content types are rejected rather than converted into empty fields, including the
  activation endpoint. Optional empty bodies remain distinguishable from malformed JSON.

## Evidence boundary and blockers

The available integration suite is a public command/endpoint seam, not the requested complete
authenticated hosted HTTP matrix. Actual hosted evidence still lacking includes authentication and
antiforgery, full create/list/detail/edit/duplicate/replay/same-key conflict, review/validation/
activation, lifecycle/delete rejection, unauthorized/out-of-scope/not-found, dependency failure,
and Audit/outbox assertions. The real browser tab reached the UI but remained unauthenticated with
a session-expired notice; no approved app-session credential capability was available. The frontend
behavior runner remains `BLOCKED_BY_PACKAGE_POLICY`; no package was installed or downloaded.

No Simulator Run was started. No direct SQL substituted for UI actions. No T049 or later task was
executed.
