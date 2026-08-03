# Feature 003 Phase 5 checkpoint — T065–T072

Status: **ACCEPTED** for the bounded Phase 5 scope. Release-ready remains
**NO** because the Full harness still has two company-approval blockers and the
approved frontend behavior runner is unavailable under package policy.

This checkpoint is the stopping point for this run. T073 and later were not
implemented, checked, or opened.

## Execution gate

- Repository: `devphuclam/EnergySaving`
- Baseline: `771353438dfbd943d3598dd6daffa776fb562fcb`
- Branch: `003-operational-configuration-workspace`
- Scope: T065–T072 only; T001–T064 remain accepted historical work.
- Database: PostgreSQL `127.0.0.1:5433/iump_dev` only. No port 5432, Docker,
  SQLite, InMemory substitute, package installation, or public package download
  was used.
- No Simulator start was initiated by a read or dashboard load.

## Red-green evidence

The required TDD red seams were registered and run before the production fixes;
numeric evidence is preserved in [phase-05-red.md](phase-05-red.md).

- T065 Unit red: exit `1`; `cases=2`, `assertions=5`, `failures=3` for missing
  Administrator correlation permission, top-level audit redaction, and the
  no-authorized-scope dashboard state/count behavior.
- T066 PostgreSQL red: exit `1`; target `127.0.0.1:5433/iump_dev`,
  `cases=4`, `assertions=4`, `failures=2` for non-Administrator correlation
  omission and credential-like redaction.
- Green Unit result: exit `0`; `T065: cases=3; assertions=10; failures=0`.
- Green Integration result: exit `0`; `T066 target=127.0.0.1:5433/iump_dev
  cases=10; assertions=10; failures=0`.

## Implemented Phase 5 behavior

- Audit owns the query contract, filters, redaction, scope-before-count/page,
  Administrator-only correlation, validation, and strict tuple keyset paging.
  Cursor pages do not use `OFFSET`; malformed date ranges return HTTP 422.
- Audit redaction recursively removes sensitive keys from nested JSON before the
  result reaches the Web layer.
- Operational Dashboard is a typed server-side composition through public
  Organization, Catalog, Acquisition, Telemetry, and Audit contracts. It
  returns authorized Sites, Sources, Points, active Runs, Latest/Health
  summaries, setup readiness, dependency/runtime classification, and a safe
  recent-Audit DTO.
- Site/Area authorization is applied before counts and paging. Area-mapped
  Sources and Runs are filtered through public Mapping snapshots; no global
  resource metadata is exposed to a scoped user.
- API routes are `/api/v1/operational-dashboard` and the existing Audit route;
  endpoints contain orchestration only and no SQL.
- Web Dashboard and Audit screens have Vietnamese user-facing states for
  loading, empty, validation, forbidden, dependency, runtime/network error,
  retry, and incomplete setup. No fake/demo fallback, savings claim, AI
  conclusion, equipment control, or automatic Simulator start was added.

## Deterministic PostgreSQL evidence

The fresh integration run used the approved local target and completed all 15
PostgreSQL suites with zero failures. T066 passed `10/10` assertions, including
Audit date/actor/action/entity/site/area filters, scope-before-paging, stable
keyset continuation, redaction, correlation permission, no-scope behavior,
Administrator Dashboard summaries, Area scope, and no read-side mutation.

## Hosted HTTP and authenticated browser evidence

The API was hosted on `http://localhost:5000` and the Web app on
`http://localhost:5173`; both were stopped after evidence collection.

- `/health/live`: HTTP `200`.
- `/health/ready`: HTTP `200`; sanitized readiness body reported
  `status=ready`, `database=iump_dev`, `port=5433`, `migrationLevel=15`.
- Anonymous `/api/v1/operational-dashboard`: HTTP `401`.
- Authenticated Dashboard: real authorized cards rendered for Sites, Sources,
  Points, active Runs, Latest, and Health; setup readiness and recent Audit
  were visible with a Continue Setup action.
- Authenticated Audit: server-side filter controls for UTC range, actor,
  action, entity type/ID, Site, and Area were visible. Applying `Create`
  returned `33` filtered records; the next keyset page returned an empty page
  without a client-side fallback. The UI exposed safe before/after JSON and
  Administrator correlation IDs.
- Logout/login was exercised; the authenticated Audit surface returned with the
  same server-authorized Admin mode. Browser console errors: `0`.
- The repository's separate frontend behavior runner remains
  `BLOCKED_BY_PACKAGE_POLICY`; no package was installed to bypass that blocker.

## Verification

| Command/check | Result | Classification |
|---|---:|---|
| `dotnet build .\IUMP.slnx --no-restore` | exit 0 | PASS; 0 warnings, 0 errors |
| Unit test project | exit 0 | PASS; T065 3 cases / 10 assertions / 0 failures |
| Integration test project | exit 0 | PASS; T066 10/10; 15 PostgreSQL suites / 0 failures |
| `npm run lint` (`src/Web`) | exit 0 | PASS; pre-existing Fast Refresh warnings only |
| `npm run build` (`src/Web`) | exit 0 | PASS |
| architecture policy checks | exit 0 | PASS |
| repository policy checks | exit 0 | PASS |
| observability checks | exit 0 | PASS; 12 checks |
| Fast harness | exit 0 | PASS=10 |
| Fresh Full harness | exit 20 | PASS=13; `BLOCKED_BY_COMPANY_APPROVAL=2`; no mandatory FAIL |

Full blockers are unchanged and truthful:

- `BLK-ENV-003`: no approved company CI runner/template context.
- `BLK-ENV-004`: container target remains deferred pending company approval.

## Spec Kit, convergence, and review

- Read-only SpecKit analysis was run during the gate. Its intermediate findings
  (dashboard contract shape, registered tests, stale scope language, and strict
  keyset wording) were repaired minimally in the canonical artifacts. A final
  provider run terminated with `Insufficient Balance`; its classification is
  **BLOCKED_BY_PROVIDER_QUOTA / NOT_RUN**, not PASS. No Critical or High
  conflict remains based on the repaired artifacts and direct checks.
- Convergence against `spec.md`, `plan.md`, `tasks.md`, and the constitution
  found no remaining actionable Phase 5 gap; no convergence tasks were
  appended. T073+ remains untouched.
- Standards review: Critical `0`, High `0`, actionable Medium `0` after the
  final fixes. The remaining non-blocking notes are serial per-point dashboard
  reads versus the plan's performance target and the absence of a separate
  Engineer-area runtime fixture; T066's Admin/Area and scope-before-page
  coverage remains green.
- Specification review: Critical `0`, High `0`, Medium `0` for T065–T072.

## Files changed

Canonical Phase 5 artifacts and evidence:

- `specs/003-operational-configuration-workspace/spec.md`
- `specs/003-operational-configuration-workspace/plan.md`
- `specs/003-operational-configuration-workspace/tasks.md`
- `specs/003-operational-configuration-workspace/contracts/operational-workspace.md`
- `specs/003-operational-configuration-workspace/contracts/ui-state-model.md`
- `specs/003-operational-configuration-workspace/checklists/phase-05-red.md`
- `specs/003-operational-configuration-workspace/checklists/phase-05-checkpoint.md`

Implementation and tests are limited to Audit, Dashboard composition/API, Web
gateway/UI, public Catalog/Acquisition seams, and the registered T065/T066
tests under `src/` and `tests/`.

## Ledger and stop

| Range | Disposition |
|---|---|
| T065–T072 | **PASS: 8** |
| T073–T080 | **NOT STARTED; unchecked** |

Planning-ready: **YES**. Implementation-ready: **YES**. Release-ready:
**NO**. Phase 5 accepted: **YES**.

**Explicit stop before T073.**

