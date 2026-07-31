# Phase 1 Corrective Verification

Date: 2026-07-31
Baseline: `0165719c0ee9f8477efd336c16b5887c58ae3a8f`
Database target: `127.0.0.1:5433/iump_dev` only
Secret handling: PASS; credentials and `.env` values are not recorded.

## Fresh automated evidence

| Check | Result | Exit / evidence |
|---|---|---:|
| Solution build | PASS | `dotnet build .\IUMP.slnx --no-restore` → exit 0; 0 warnings, 0 errors |
| Unit runner | PASS | `dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore` → exit 0; all suites PASS |
| PostgreSQL integration | PASS | `dotnet run --project .\tests\Integration\IUMP.Tests.Integration.csproj --no-restore` → exit 0; 14 suites, 0 failures |
| Web lint | PASS | `npm run lint` in `src/Web` → exit 0; three pre-existing Fast Refresh warnings |
| Web production build | PASS | `npm run build` in `src/Web` → exit 0 |
| Runtime auth/session | PASS | real API `ready=200 login=200 me=200 logout=200`; no credential recorded |
| Architecture | PASS | `.\tests\Verification\architecture.tests.ps1` → exit 0 |
| Repository policy | PASS | `.\tests\Verification\repository-policy.tests.ps1` → exit 0 |
| Observability | PASS | `.\tests\Verification\observability.tests.ps1` → exit 0; 12 checks, 0 failures |
| Fast harness | PASS | `.\scripts\harness.ps1 -Mode Fast -Feature 003-operational-configuration-workspace` → exit 0; PASS 8 |
| Full harness | BLOCKED | `.\scripts\harness.ps1 -Mode Full -Feature 003-operational-configuration-workspace` → exit 20; PASS 11 and 2 company-approval blockers |

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
  operational and one incomplete chain, completes the legal
  Area → Asset → Source → Mapping → Point activation sequence, reconstructs persisted state, and
  creates no Simulator Run.

## Manual browser evidence

The in-app browser used the real local Web/API/PostgreSQL runtime:

- Administrator sign-in: PASS. The server returned a persisted operational Dashboard.
- Administrator Setup view: PASS for persisted reconstruction; it visibly showed `8/8` and
  `Simulator tự khởi động: Không`.
- Administrator create/activate/assign journey: **NOT RUN to completion**. The persistent
  development database already contained 59 Sites and at least one operational chain. Landing
  precedence therefore selected the operational chain and the Setup view exposed no `Tạo Site`
  action. No destructive database cleanup was authorized or performed.
- Administrator logout: PASS after registering server-owned session authentication; the browser
  returned to the sign-in state.
- Engineer sign-in and continuation: PASS. The Engineer resumed the selected persisted chain at
  Data Source, created a Source, Mapping, and Simulator Configuration, and reached `7/8`.
- Browser refresh: PASS. Refresh reconstructed `7/8` and `Kiểm tra và kích hoạt` from server state.
- Ordered activation on that pre-existing Point: safely stopped with visible
  `CATALOG_CONFLICT` because the Point already had a different active open-ended Mapping. The same
  conflict previously produced HTTP 500 and now remains a replayable HTTP 409.
- Simulator auto-started: **NO**. The newest browser-created Source has zero Runs.
- Browser console errors: **0**.

Because the exact Administrator create/activate/assign browser journey could not be run against
the non-empty persistent database, T033 remains unchecked. The complete isolated Administrator
handoff, duplicate-safe assignment, Engineer continuation, resume, activation, and zero-Run
journey is green in PostgreSQL T014, but automated evidence is not substituted for the required
manual browser evidence.

## Frontend behavior runner

Status: **BLOCKED_BY_PACKAGE_POLICY**
Blocker ID: `BLK-003-PH1-WEB-RUNNER`

`src/Web/package.json` has no approved frontend behavior-test runner. The source
`src/Web/src/test/app-shell.test.tsx` is type-checked by the production build but was not executed
as a behavior suite. No package was installed or downloaded, so T034 remains unchecked.
