# Feature 003 Phase 4 corrective checkpoint — T057–T064

Status: implementation and runnable evidence are complete. T057–T063 are complete; T064 remains
unchecked until the fresh Standards and Specification reviews finish with no actionable High or
Medium findings. Release-ready remains **NO** and execution stops before T065.

## Execution gate

- Repository: `devphuclam/EnergySaving`
- Baseline: `8f7a9bd3ec3e98401ffba95ef6d2b4efe7190648`
- Branch: `003-operational-configuration-workspace`
- Scope: T057–T064 only. T065–T072 remain unchecked; Feature 002 is unchanged.
- Database: PostgreSQL `127.0.0.1:5433/iump_dev` only. No port 5432, container, SQLite,
  InMemory substitute, package install, or public package download was used.

## Red-green evidence

The final review identified a selection-change stale-data seam and it was repaired red/green. Before
the fix, the browser loaded Point `...0004` with value `42`, the API was made unavailable, and the UI
explicitly selected Point `...0003`. Red evidence showed the URL and selector had changed to
`...0003` while the old `...0004` article and value `42` were still rendered. The implementation now
increments the request sequence and clears selection-bound snapshot/error/loading state synchronously
for every hierarchy or Point change. Repeating the same browser sequence produced green evidence:
the new Point remained selected, the old article/value were absent, the new network error was visible,
and reload/retry recovered the correct numeric zero. Browser console errors remained `0`.

Earlier compile logs from the original Phase 4 implementation predate this corrective baseline and
are not represented as corrective red evidence. The unrelated hosted-DLL lock described below is
also not counted as behavioral red evidence.

## Corrective implementation

- The selector contract is level-aware: Sites are independent of Point paging; Areas require and
  are filtered by Site; Assets require and are filtered by Site/Area; Points require the complete
  hierarchy and support server-side search plus deterministic 100-row paging.
- Scope and hierarchy predicates precede search, count, sort, and paging. A separate filtered count
  preserves the exact authorized total even when the requested page is empty.
- Paging rejects page `0`, negative values, pageSize `0`, negative/excessive pageSize, malformed
  numbers, pages above `10,000,000`, and overflow attempts with HTTP 422. Offset is checked as
  `bigint`; no negative OFFSET is possible.
- Parent changes clear all descendants. URL values are requests only; the server authorizes the
  complete hierarchy. Repository ordering never selects a Point.
- Polling performs an immediate fetch, schedules the next fetch only after completion, cancels its
  timer on disable/unmount, and invalidates old selection/option responses with request sequences.
  A dependency/runtime error preserves the last valid value while displaying retry state.

## Deterministic PostgreSQL fixture and assertions

T058 creates unique data only through module repositories/application services. It creates two
Sites, two Areas, two Assets, 507 active Points total (506 in the paged hierarchy and one real
out-of-scope Point), Site- and Area-scoped engineers, no-Mapping, one-Mapping/no-Measurement,
accepted zero, accepted non-zero, newer rejected, older accepted, unrelated Measurement, related
and unrelated Sources/Runs, canonical Metric/Unit, Health, and counters.

Fresh result: `cases=13; assertions=19; failures=0`; PostgreSQL suites `15`, failures `0`.

- Page 1 returns 100/506; page 6 returns 6/506; page 7 returns 0/506; exact server search returns
  the Point beyond row 500.
- Administrator sees both deterministic Sites. The Site engineer sees only the assigned Site;
  querying the real Point under the second Site is indistinguishable from not found. Out-of-scope
  options expose zero rows and zero count.
- Accepted non-zero is `42`; accepted zero is `hasData=true, value=0`; no Measurement is
  `hasData=false, value=null`. Newer rejected, older/stale, and unrelated Measurements do not
  displace Latest Accepted. Metric, canonical Unit, quality, source/received timestamps are exact.
- No active Mapping is `NotConfigured`; one eligible Mapping without Measurement is `NoData`.
  Migration `0006` authoritatively forbids simultaneously overlapping active Mappings. The fixture
  proves the second active Mapping is rejected as `CATALOG_CONFLICT` without damaging the first;
  the defensive `Ambiguous` response for legacy/corrupt data remains unit-covered. The exclusion
  invariant was not weakened.
- Selected Health and Run ID/status/counters belong to the selected Point's Source and Mapping;
  the unrelated Run and its counters never appear.
- Before/after read snapshots prove no changes to command-idempotency, Audit, outbox, Run,
  Measurement identity/raw, Latest, Health, Organization Point count, Catalog Mapping count, or
  selected Run version.

## Hosted HTTP matrix

API `http://127.0.0.1:5000` and Web `http://localhost:5173` were started with repository scripts and
an approved local credential that was never printed or persisted. Hosted results:

- live/ready/login, authorized Sites/Areas/Assets, Point page 1/page 6, exact search, no-Mapping,
  accepted zero, accepted non-zero: HTTP 200;
- no selection/current request: HTTP 422; deterministic No Data: HTTP 200 with
  `dataState=NoData`, `hasData=false`, and `value=null`;
- rejected-after-accepted/stale/unrelated exclusion: HTTP 200 with Latest `value=42`,
  `quality=Good`, and both source/received timestamps present;
- selected Source Health: `Online` with the selected Point and related Source IDs; selected Run:
  `1cd89f0a-edad-478c-94ae-2dc80fde0da6`, `Running`, counters `3/2/1`; unrelated Run
  `d4b9b64e-55a2-4e8f-8952-fe48a542c279` was absent;
- invalid/missing level, missing hierarchy, page 0, negative page, pageSize 0, excessive pageSize,
  and extreme page: HTTP 422;
- anonymous: 401; hierarchy mismatch/out-of-scope: 404;
- two repeated HTTP 200 GET reads preserved the eight-table write-side snapshot and started zero
  Simulator Runs;
- dependency 503 was distinguished from an actual network failure. Recovery/retry returned the
  prior selected state.

The canonical overlap constraint prevents provisioning an actually ambiguous valid PostgreSQL row;
the defensive response is verified at the provider-neutral contract level, while hosted PostgreSQL
verifies the authoritative conflict behavior.

## Authenticated browser and polling journey

The real browser journey ran end to end: sign in; open Latest/Health; verify no implicit Point;
select Site/Area/Asset; page/search beyond row 500; explicitly select the Point; verify full URL,
Metric, Unit, value, quality, timestamps, Health, related Run/status/counters; No Data; numeric zero;
browser reload reconstruction; sign out/in reconstruction; dependency state; network state; retry.

A real Worker session produced 1,738 persisted Measurements while observed. With auto refresh on,
the selected source timestamp advanced from `08:39:19.315060Z` to `08:39:30.234224Z` within one
10-second interval. After disabling auto refresh it stayed at `08:39:42.490735Z` for 11 seconds;
manual refresh then advanced it to `08:39:59.429259Z` without re-enabling auto. The valid value was
retained across the disabled interval and the earlier network failure. Browser console errors: `0`.
No browser action or read automatically started a Simulator Run. The temporary Worker, API, and Web
processes were stopped after evidence collection.

## Fresh verification

| Command | Exit | Classification |
|---|---:|---|
| `dotnet build .\IUMP.slnx --no-restore` | 0 | PASS; 0 warnings, 0 errors (fresh retry after stopping hosted API) |
| `dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore` | 0 | PASS; T057 17 assertions |
| `dotnet run --project .\tests\Integration\IUMP.Tests.Integration.csproj --no-restore` | 0 | PASS; T058 13/19/0; PostgreSQL 15 suites/0 failures |
| `npm run lint` from `src/Web` | 0 | PASS; only pre-existing Fast Refresh warnings outside the changed route |
| `npm run build` from `src/Web` | 0 | PASS |
| `architecture.tests.ps1` | 0 | PASS |
| `repository-policy.tests.ps1` | 0 | PASS |
| `observability.tests.ps1` | 0 | PASS; 12 checks |
| `simulator-phase3-closure.tests.ps1` | 0 | PASS |
| `telemetry-phase4-closure.tests.ps1` | 0 | PASS |
| `harness.ps1 -Mode Fast -Feature 003-operational-configuration-workspace` | 0 | PASS=10 |
| `harness.ps1 -Mode Full -Feature 003-operational-configuration-workspace` | 20 | PASS=13, mandatory FAIL=0, mandatory blockers=2 |

The first fresh build attempt exited `1` solely because the still-running hosted API held its output
DLL. After stopping API/Web, the required fresh build exited `0`; this was a runtime lock, not a code
failure.

Full blockers remain separate and truthful:

- `BLK-ENV-003`: no approved company CI runner/template context.
- `BLK-ENV-004`: container target deferred pending company approval.

## Spec Kit 0.15.1 governance

Commit `4c7e0887d4c5bad8524bb94bb6d3a71ba8e6a6c6` identifies the upgrade, but the repository history
does not record the exact approved source URL/package or whether public network access occurred;
provenance is therefore **NOT ESTABLISHED** and is not fabricated. No Spec Kit file was modified in
this correction.

- Manifest version: `0.15.1`.
- LF-normalized SHA-256: seven script/template entries match; the three repository-customized Spec
  Kit templates do not match their upstream manifest entries. This is reported as a governance
  mismatch, not silently repaired.
- Windows PowerShell `5.1.26100.7920` prerequisite smoke: exit `0`.
- `create-new-feature.ps1 -DryRun` for `999-governance-smoke`: exit `0`; no directory/file created.
- Feature 003 before/after dry-run: 19 files, zero hash changes; no artifact was regenerated or
  overwritten.

Spec Kit governance is not counted in T057–T064.

## Review and ledger

Fresh Standards and Specification review results are pending. Until both report Critical `0`, High
`0`, and actionable Medium `0`, T064 and Phase 4 acceptance remain **NO**.

| Range | Current disposition |
|---|---|
| T057–T063 | PASS: 7 |
| T064 | PENDING REVIEW |
| T065–T072 | NOT STARTED; unchecked |

Current ledger: PASS **7**, FAIL **0**, runnable NOT_RUN **0**, PENDING **1**. Release-ready: **NO**.
Explicit stop before T065.
