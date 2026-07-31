# Phase 1 Corrective Verification

Date: 2026-07-31
Baseline: `a08e28eb0e2299d12403af37f275cb9d862421a9`
Database target: `127.0.0.1:5433/iump_dev` only
Secret handling: PASS; credentials and `.env` values are not recorded.

## Fresh automated evidence

| Check | Result | Exit / evidence |
|---|---|---:|
| Solution build | PASS | `dotnet build .\IUMP.slnx --no-restore` -> exit 0; 0 warnings, 0 errors |
| Unit runner | PASS | `dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore` -> exit 0; all suites PASS |
| PostgreSQL integration | PASS | `dotnet run --project .\tests\Integration\IUMP.Tests.Integration.csproj --no-restore` -> exit 0; 14 suites, 0 failures |
| Web lint | PASS | `npm run lint` in `src/Web` -> exit 0; three pre-existing Fast Refresh warnings |
| Web production build | PASS | `npm run build` in `src/Web` -> exit 0 |
| Runtime auth/session | PASS | real API `ready=200 login=200 me=200 logout=200`; no credential recorded |
| Architecture | PASS | `./tests/Verification/architecture.tests.ps1` -> exit 0 |
| Repository policy | PASS | `./tests/Verification/repository-policy.tests.ps1` -> exit 0 |
| Observability | PASS | `./tests/Verification/observability.tests.ps1` -> exit 0; 12 checks, 0 failures |
| Fast harness | PASS | `./scripts/harness.ps1 -Mode Fast -Feature 003-operational-configuration-workspace` -> exit 0; PASS 8 |
| Full harness | BLOCKED | `./scripts/harness.ps1 -Mode Full -Feature 003-operational-configuration-workspace` -> exit 20; PASS 11 and 2 company-approval blockers |

The Vite proxy remains `http://localhost:5000` because the repository API startup contract uses
`AllowedHosts=localhost` and `launchSettings.json` binds `http://localhost:5000`. This is the
HTTP loopback host only; the PostgreSQL target remains the approved
`127.0.0.1:5433/iump_dev`.

## Corrective red/green evidence

- The new PostgreSQL overlapping-Mapping/idempotency regression failed before the savepoint fix:
  integration exit 1 with one `25P02` transaction-aborted failure.
- After rolling a rejected Mapping transition back to its nested savepoint, the same integration
  runner exited 0 with 14 suites and 0 failures. The rejected activation now returns and exactly
  replays `409 CATALOG_CONFLICT` instead of becoming HTTP 500.
- Unit status coverage proves an operational Site need not be first, reversing repository order
  does not change selection, ties use stable identity, unrelated hierarchy branches are not
  combined, scope filtering happens before counts, and an unmapped Source is not attached to an
  arbitrary one of multiple Points.
- The Area-only mapped-chain regression failed before authorization repair (integration exit 1)
  and passes afterward; Area scope hides a pre-Mapping Site-wide Source but permits validation
  once a persisted Mapping relates the Source to the authorized Point.
- Session unit coverage fails closed for malformed cookies and proves the request-scoped principal
  reuses server-resolved role, Site, Area, and capability claims without a second IAM lookup.
- PostgreSQL T014 evaluates two authorized Sites, selects the later operational chain, reports one
  operational and one incomplete chain, completes the legal Area -> Asset -> Source -> Mapping ->
  Point activation sequence, reconstructs persisted state, and creates no Simulator Run.

## Manual browser evidence

The in-app browser used the real local Web/API/PostgreSQL runtime and a newly created Site. No
database reset, truncation, deletion, or replacement was used.

- Administrator sign-in: PASS. The server returned the Dashboard.
- Dashboard NEW action: PASS. Exactly one visible `Tạo chuỗi cấu hình mới` action was present for
  Administrator and was not exposed as a general-user action.
- NEW state contract: PASS. Clicking the action navigated to `/setup` with server-authorized
  `mode=new` state and an empty new-chain snapshot; no localStorage or list-position was used.
- Administrator Site creation: PASS through the UI, followed by activation and Engineer handoff.
- Engineer continuation: PASS through Area, Asset, Measurement Point, Data Source, Source Mapping,
  and Simulator Configuration (steps 1 through 7); the Engineer saw the assigned Site read-only.
- Browser refresh: PASS. Refresh after step 7 reconstructed `7/8` and the exact
  `Kiểm tra và kích hoạt` action from server state.
- Ordered activation: PASS. The validation and ordered activation completed and redirected to the
  Simulator page.
- Simulator page: PASS. The page was visited, no Start control was clicked, and the UI did not
  auto-start a run.
- Browser console errors: PASS, fresh Simulator tab reported 0 error entries.
- Database zero-Run evidence: PASS, read-only PostgreSQL query against the newly created Site
  returned `site_runs=0`; no mutation was executed.

## Frontend behavior runner

Status: **BLOCKED_BY_PACKAGE_POLICY**
Blocker ID: `BLK-003-PH1-WEB-RUNNER`

`src/Web/package.json` has no approved frontend behavior-test runner. The source
`src/Web/src/test/app-shell.test.tsx` is type-checked by the production build but was not executed
as a behavior suite. No package was installed or downloaded, so T034 remains unchecked.
