# Feature 003 Phase 4 corrective checkpoint — T057–T064

Status: **ACCEPTED** for the bounded T057–T064 Phase 4 scope. The two Web findings are closed:
server-authorized Point rehydration beyond the default page and same-selection Latest request
coordination. T065+ remains out of scope; Release-ready remains **NO**.

## Execution gate

- Repository: `devphuclam/EnergySaving`
- Baseline: `50f5579a520caf2af34d68f80e34a85354d22849`
- Branch: `003-operational-configuration-workspace`
- Scope: T062–T064 corrective closure only; T057–T061 remain accepted. T065–T072 remain unchecked;
  Feature 002 is unchanged.
- Database: PostgreSQL `127.0.0.1:5433/iump_dev` only. No port 5432, container, SQLite,
  InMemory substitute, package install, or public package download was used.

## Red-green evidence

The corrective static contract was intentionally run against the merged baseline before production
changes: `telemetry-phase4-closure.tests.ps1` exited `1` because the coordinator, AbortSignal path,
and selected-Point rehydration tokens were absent. After the fix it exits `0` and runs the pure
deferred-request test (`requests=5; events=8`). This is the approved provider-neutral red/green seam
because no frontend behavior runner is installed.

The authenticated browser then supplied green evidence. Point `P49295FAFDF50505` (page `6 / 6`,
`506` authorized Points) was selected. After refresh and logout/login, the URL remained complete and
the Site, Area, Asset, and exact Point options were visibly selected; the current-data card named the
same Point. A mismatched URL Point returned the established safe hierarchy error, inserted no Point
label/option, and displayed no data card.

Changing Point cleared the prior selection-bound card before the new request completed; the old Point
never reappeared. Rapid auto-refresh off/on and manual refresh were exercised in the browser; the pure
deferred coordinator proved one in-flight request, no overlap, one post-completion timer, manual
refresh while disabled, abort/invalidation on selection change, and timer/request cancellation on
clear. Browser console errors remained `0`.

The pre-fix static red is the only red evidence claimed for this Web corrective seam; no frontend
behavior runner was installed or treated as available.

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
- Polling uses a pure `LatestRefreshCoordinator`: one immediate request per selection, an
  AbortController and current-request guard, a timer only after completion, no duplicate on auto
  toggles, manual refresh without overlap, and clear/unmount invalidation. A dependency/runtime error
  preserves the last valid value while displaying retry state.
- On a valid current response, `mergeSelectedPointOption` adds only the server-returned Point metadata
  to the selector, so a Point beyond page 1/search remains visible without client fabrication. Safe
  current errors never add URL-derived metadata.

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

API `http://localhost:5000` (listener bound to `127.0.0.1:5000`) and Web
`http://localhost:5173` were started with repository scripts and an approved local credential that
was never printed or persisted. Hosted results:

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

The authenticated journey ran: sign in; open Latest/Health; select the Site/Area/Asset; page to
`6 / 6` beyond row 500; select `P49295FAFDF50505`; refresh; sign out/in; verify URL, selector identity,
and current-card identity; exercise mismatched URL safety; toggle auto refresh off/on; run manual
refresh; change Point and verify no stale old card. Browser console errors: `0`.

The pure coordinator's deferred request evidence is `requests=5; events=8`: initial request, one
timer request, one manual request, then two selection requests with the old one aborted. The database
`acquisition.simulator_run` count was `183` before and after the browser journey, proving no automatic
Simulator Start. API, Web, and Worker were stopped after evidence collection.

## Fresh verification

| Command | Exit | Classification |
|---|---:|---|
| `dotnet build .\IUMP.slnx --no-restore` | 0 | PASS; 0 warnings, 0 errors |
| `dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore` | 0 | PASS; T057 17 assertions |
| `dotnet run --project .\tests\Integration\IUMP.Tests.Integration.csproj --no-restore` | 0 | PASS; T058 13/19/0; PostgreSQL 15 suites/0 failures |
| `npm run lint` from `src/Web` | 0 | PASS; only pre-existing Fast Refresh warnings outside changed telemetry files |
| `npm run build` from `src/Web` | 0 | PASS |
| `architecture.tests.ps1` | 0 | PASS |
| `repository-policy.tests.ps1` | 0 | PASS |
| `observability.tests.ps1` | 0 | PASS; 12 checks |
| `simulator-phase3-closure.tests.ps1` | 0 | PASS |
| `telemetry-phase4-closure.tests.ps1` | 0 | PASS |
| `harness.ps1 -Mode Fast -Feature 003-operational-configuration-workspace` | 0 | PASS=10 |
| `harness.ps1 -Mode Full -Feature 003-operational-configuration-workspace` | 20 | BLOCKED_BY_COMPANY_APPROVAL=2, PASS=13, mandatory FAIL=0 |

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

Fresh final reviews against the corrective baseline report:

- Standards: Critical `0`, High `0`, actionable Medium `0` (one optional Low note about repeated
  selector switches; no acceptance impact).
- Specification: Critical `0`, High `0`, actionable Medium `0`.

| Range | Current disposition |
|---|---|
| T057–T064 | PASS: 8 |
| T065–T072 | NOT STARTED; unchecked |

Current ledger: PASS **8**, FAIL **0**, runnable NOT_RUN **0**, PENDING **0**. Phase 4 accepted:
**YES**. Release-ready: **NO**. Explicit stop before T065.
