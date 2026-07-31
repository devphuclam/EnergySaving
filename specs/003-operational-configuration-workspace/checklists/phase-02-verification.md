# Phase 2 Corrective Verification

Date: 2026-07-31
Feature: `003-operational-configuration-workspace`
Baseline: `09347b467b94b69275d1b69d212613be6cc37539`

## Commands and outcomes

| Command | Exit | Outcome |
|---|---:|---|
| `dotnet build --no-restore src/Api/IUMP.Api.csproj` | 0 | PASS |
| `dotnet build --no-restore tests/Integration/IUMP.Tests.Integration.csproj` | 0 | PASS |
| `dotnet run --no-build --project tests/Unit/IUMP.Tests.Unit.csproj` | 0 | PASS; all suites zero failures |
| `dotnet run --no-build --project tests/Integration/IUMP.Tests.Integration.csproj` | 0 | PASS; 14 suites, 0 failures; T038 9/26/0 |
| `npm run lint` in `src/Web` | 0 | PASS; existing Fast Refresh/hook warnings only |
| `npm run build` in `src/Web` | 0 | PASS; TypeScript and Vite build |
| `scripts/harness.ps1 -Mode Fast -Feature 003-operational-configuration-workspace` | 0 | PASS=8 |
| `scripts/harness.ps1 -Mode Full -Feature 003-operational-configuration-workspace` | 20 | BLOCKED; `BLK-ENV-003`, `BLK-ENV-004` company approval checks |

## Database and policy evidence

The integration executable printed only the approved redacted target marker:
`postgres-integration target=127.0.0.1:5433/iump_dev suites=14 failures=0`.
The repository-local `.env` loader supplied credentials at runtime; no secret value was printed,
serialized, committed, or copied to evidence. Port 5432, Docker, alternate databases, SQLite,
InMemory, package installation, and public downloads were not used.

## Phase 2 acceptance evidence

- Scope-before-paging covers all seven resources; Simulator search is applied to configuration ID,
  Source ID, and current version before total/page slicing.
- The real owner command path is used for create/edit/lifecycle/delete. Mutations carry server
  principal, idempotency, antiforgery, transaction, and expected version where applicable.
- Active/referenced/historical deletion remains dependency-protected. Organization/configuration
  resources show no destructive button when their owner contract has no safe Draft delete; the UI
  explains the unsupported action in Vietnamese.
- Duplicate responses expose copied relationships and an explicit excluded-field list. The UI
  preserves success feedback across reload and blocks Simulator activation until review and
  validation are complete.
- Unsupported lineage fields are omitted or read-only during edit, and Simulator detail uses the
  latest Draft payload when one exists so behavior is not silently overwritten from the active
  version.
- Frontend behavior automation was not run because no approved runner is available; this is a
  package-policy blocker, not a PASS claim.

The remaining T038/T043/T048 requirements are not closed: the available tests do not provide the
hosted browser journey, the complete HTTP lifecycle/delete/replay/authorization/outbox matrix, or
a persisted/server-derived Simulator relationship-review receipt.

## Stop

No Simulator Run was started by this phase. No T049 or later task was executed.
