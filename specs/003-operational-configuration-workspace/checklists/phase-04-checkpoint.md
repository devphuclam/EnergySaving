# Feature 003 Phase 4 checkpoint — T057–T064

Status: code/task scope complete; Phase 4 acceptance is **NO** and release-ready is
**NO** because the hosted HTTP/browser journey was not runnable in this environment and
the required fixture-dependent matrix was not fabricated. This checkpoint stops before
T065 as required.

## Execution gate

- Repository: `devphuclam/EnergySaving`
- Baseline: `ebb4a17c6bb48ca1c90abd2f6c9a7583ac8ee8ab`
- Branch: `003-operational-configuration-workspace`
- Scope: exactly T057–T064; T065–T072 remain unchecked; no Phase 5; no Feature 002 changes.
- Database target used by integration: `127.0.0.1:5433/iump_dev` (PostgreSQL only).

## Changed files

- `src/Hosting/Abstractions/TelemetryWorkspacePorts.cs`
- `src/Composition/Postgres/PostgresTelemetryWorkspacePorts.cs`
- `src/Composition/Postgres/PostgresModuleRegistration.cs`
- `src/Api/Program.cs`
- `src/Api/TelemetryQueryEndpoints.cs`
- `src/Web/src/gateways/webGateways.ts`
- `src/Web/src/features/telemetry/PointCurrentRoute.tsx`
- `tests/Unit/Api/LatestSelectionTests.cs`
- `tests/Unit/Program.cs`
- `tests/Integration/OperationalWorkspace/LatestHealthTests.cs`
- `tests/Integration/IUMP.Tests.Integration.csproj`
- `tests/Integration/Program.cs`
- `tests/Verification/telemetry-phase4-closure.tests.ps1`
- `scripts/harness.ps1`
- `specs/003-operational-configuration-workspace/tasks.md`
- this checkpoint

## Red/green evidence

Genuine red evidence was captured before the provider contract and adapter existed:
`dotnet build .\IUMP.slnx --no-restore` exited **1** with missing T057/T058 contract and
test symbols; output is retained in `.scratch/phase4-red-build.txt` (no secret values).

Fresh green evidence:

| Command | Exit | Result |
|---|---:|---|
| `dotnet build .\IUMP.slnx --no-restore` | 0 | PASS |
| `dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore` | 0 | PASS; T057 11 assertions |
| `dotnet run --project .\tests\Integration\IUMP.Tests.Integration.csproj --no-restore` | 0 | PASS; T058 313 assertions in the latest observed run; PostgreSQL suites 15/0 failures |
| `npm run lint` (from `src/Web`) | 0 | PASS; existing Fast Refresh warnings only |
| `npm run build` (from `src/Web`) | 0 | PASS |
| `tests/Verification/architecture.tests.ps1` | 0 | PASS |
| `tests/Verification/repository-policy.tests.ps1` | 0 | PASS |
| `tests/Verification/observability.tests.ps1` | 0 | PASS; 12 checks |
| `tests/Verification/telemetry-phase4-closure.tests.ps1` | 0 | PASS |

## T057–T064 evidence

- T057: complete hierarchy is required; mismatch is safe; catalog ordering never selects a
  Point; two Points require explicit selection; No Data is `value=null` and accepted zero is
  numeric `0`; no Simulator route is exposed. Unit test passes.
- T058: selector rows are composed only from authorized active hierarchy rows, with
  `count(*) over()` after scope predicates and before paging. The read-only integration run
  exercised every returned Point against its selected Latest/Health context on the approved
  PostgreSQL target. Assertion totals are fixture-dynamic because earlier integration suites
  may provision rows; T058 itself performs no writes.
- T059–T061: typed provider-neutral contract, active Mapping/Source resolution, safe
  hierarchy validation, selected Latest/Health endpoints, and dependency/runtime `503`
  distinction are implemented. GET queries do not write idempotency records.
- T062–T063: Site → Area → Asset → Measurement Point selectors are explicit and URL-backed;
  changing a parent clears descendants; no first-option/`points[0]` fallback exists. Refresh
  defaults to 10 seconds, supports disable/manual refresh, sequence-invalidates stale
  responses, preserves a valid prior snapshot with a visible error status, and distinguishes
  No Data from numeric zero. The built bundle was checked for escaped Unicode and mojibake in
  the changed copy.
- T064: task ledger/checkpoint and focused verification contract are present; Fast/Full and
  review evidence are recorded below.

The attached mandatory matrix scenarios requiring dedicated fixtures or a hosted journey
(Engineer scope/out-of-scope identity, rejected/stale raw measurement, accepted zero, no
active mapping, ambiguous mapping, unrelated Source/Run exclusion, repeated browser refresh,
logout/login reconstruction, and no-write SQL observation) were **NOT_RUN** where no approved
fixture or hosted runtime was available. They are not claimed as runtime PASS.

## Hosted HTTP and browser evidence

- API matrix against `http://127.0.0.1:5000`: **NOT_RUN / BLOCKED_BY_RUNTIME_HOST**. Starting a
  local hosted process was unavailable through the approved tool surface; no localhost HTTP
  result is represented as PASS.
- Authenticated browser journey at `http://localhost:5173`: **NOT_RUN / BLOCKED_BY_RUNTIME_HOST**;
  the browser opened the URL and received `ERR_CONNECTION_REFUSED`. Console error count and
  Simulator auto-start observation are therefore **NOT_RUN**, not zero/pass.
- PostgreSQL integration capability itself is available and passed against
  `127.0.0.1:5433/iump_dev`; this is separate from hosted/browser runtime capability.

## Harness and review

- Fast harness: exit **0**, PASS **10**, no mandatory failures.
- Full harness: exit **20**, PASS **13**, mandatory blockers **BLK-ENV-003** and
  **BLK-ENV-004**, zero mandatory FAIL. The frontend lint/build check is included in the
  PASS set; no package-policy result is reclassified as a pass.
- Standards review: **C0/H0/M2/L0**. The two Medium items are optional robustness follow-ups
  (selector pagination beyond the first 500 rows and extreme-page offset overflow); fixture-
  dependent coverage is recorded as NOT_RUN rather than overstated. Stale prior data is
  intentionally preserved with a visible reconnecting status per the T064 contract.
- Specification review: **C0/H0/M0/L0** for the runnable T057–T064 path; unavailable
  fixture/browser scenarios remain evidence gaps and are explicitly NOT_RUN, so acceptance
  remains NO.

## Task ledger

| Task range | Disposition |
|---|---|
| T057–T064 | PASS for implemented code/tests and runnable verification |
| Runnable checks | PASS: no runnable NOT_RUN |
| Hosted/browser and unavailable fixtures | NOT_RUN/BLOCKED, reported separately; not converted to PASS |
| T065–T072 | NOT STARTED; remain unchecked |

Expected ledger: PASS **8**, FAIL **0**, runnable NOT_RUN **0**. Phase 4 accepted: **NO**.
Release-ready: **NO**.

Next authorized range is T065–T072 only. Stop immediately before T065 in this run.
