# Phase 1 Verification

Date: 2026-07-30
Target: `127.0.0.1:5433/iump_dev` only
Secret handling: PASS; no credential value is recorded here.

## Automated evidence

| Check | Result | Exit / evidence |
|---|---|---:|
| Solution build | PASS | `dotnet build .\IUMP.slnx --no-restore` → exit 0, 0 warnings, 0 errors |
| Unit runner | PASS | `dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore` → exit 0; T013 9/9; all suites PASS |
| PostgreSQL integration | PASS | `dotnet run --project .\tests\Integration\IUMP.Tests.Integration.csproj --no-restore` → exit 0; 14 suites, 0 failures |
| Forward migration 0014 | PASS | approved PostgreSQL helper, target recheck `iump_dev|5433`, `ON_ERROR_STOP=1` → exit 0 |
| Web lint | PASS | `npm run lint` in `src/Web` → exit 0; three non-blocking pre-existing Fast Refresh warnings |
| Web production build | PASS | `npm run build` in `src/Web` → exit 0 |
| Runtime readiness | PASS | `GET http://localhost:5000/health/ready` → HTTP 200 |
| Anonymous workspace safety | PASS | `GET /api/v1/operational-workspace/status` without a session → HTTP 401 |
| Fast harness | PASS | `.\scripts\harness.ps1 -Mode Fast -Feature 003-operational-configuration-workspace` → exit 0; PASS 8 |
| Full harness | BLOCKED | same command with `-Mode Full` → exit 20; PASS 11, `BLK-ENV-003` and `BLK-ENV-004` blocked by company approval |

The PostgreSQL T014 journey created an isolated unique chain through real repositories, proved
Administrator Site activation and duplicate-safe Engineer handoff, Engineer continuation,
server-derived resume before Mapping and before activation, partial restart that skips the already
committed Area transition, the legal Area → Asset → Source → Mapping → Point activation order, a
stable post-restart Dashboard landing, and zero Simulator Runs for the new Source.

## Manual browser acceptance

The local Web application was exercised through the in-app browser against the running API:

- an authenticated Engineer landed on the server-selected operational Dashboard;
- opening Setup displayed all eight steps as complete and the assigned Site;
- the summary displayed `8/8 bước hoàn tất`;
- the summary explicitly displayed `Simulator tự khởi động: Không`;
- refreshing the browser returned to the Dashboard from persisted server state;
- browser console errors: 0.

The Administrator handoff mutation and Engineer continuation are exercised end-to-end by the
PostgreSQL T014 journey. No real credential is copied into this evidence.

## Frontend behavior runner

Status: **BLOCKED**
Classification: `BLOCKED_BY_PACKAGE_POLICY`
Blocker ID: `BLK-003-PH1-WEB-RUNNER`

`src/Web/package.json` has no approved frontend behavior-test runner script. The existing
`src/Web/src/test/app-shell.test.tsx` source is type-checked by the production build, but it cannot
truthfully be described as executed. Per repository policy, no package was installed or
downloaded. This separate optional evidence blocker does not replace or invalidate the passing
Web build, browser acceptance, Unit suites, PostgreSQL journey, or repository harness.
