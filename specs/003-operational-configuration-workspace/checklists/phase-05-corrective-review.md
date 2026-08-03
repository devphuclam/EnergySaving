# Feature 003 Phase 5 corrective review

## Scope and provenance

- Starting main SHA: `02cc4b6dd932f08368b60ccddfdcf55c09131069`.
- Corrective branch: `fix/feature-003-phase-05-corrective`.
- Merged Phase 5 implementation under review: `c4195862305a115649163df5b7df86b56c685687`.
- Phase 4 baseline: `771353438dfbd943d3598dd6daffa776fb562fcb`.
- Constitution: 1.1.0.
- Scope is corrective closure of findings 1–7 and regression evidence only.
  T073–T080, Phase 6, Spec 004, Rule, Alert, CSV, and Reporting work were not
  started.

## Finding disposition

1. Audit rejects malformed/out-of-range `pageSize` and malformed/out-of-range
   cursor values with `VALIDATION` and HTTP 422; invalid requests do not query
   or mutate data.
2. The Postgres composition port resolves Area→Site ancestry through the public
   Organization query contract. A Site-scoped caller may query a child Area;
   foreign and unknown Areas fail closed.
3. Audit PostgreSQL fetches `pageSize + 1`, omits `OFFSET` for cursor
   continuation, and emits `nextCursor` only when another visible row exists.
   The response field is `itemCount`, explicitly page-scoped rather than a
   fabricated total count.
4. Dashboard mapping accepts `time`, `occurredAtUtc`, and `OccurredAtUtc`.
5. Dashboard operational runs include Running and Paused, exclude Stopped, and
   set `runtime.simulatorRunning` only when at least one run is Running. Web
   presentation distinguishes running, paused, and idle states.
6. Audit datetime-local labels identify local time, helper text documents UTC
   conversion, and gateway conversion rejects invalid/impossible local dates
   before `toISOString()`.
7. Evidence uses the repository vocabulary. The historical provider run is
   `status=NOT_RUN; reason=provider quota unavailable`; no invented
   provider-quota blocker class is introduced. Unavailable frontend and
   authenticated runners are recorded as `BLOCKED` with
   `BLOCKED_BY_PACKAGE_POLICY` or `BLOCKED_BY_MISSING_TOOL` respectively.

## TDD evidence

Historical `phase-05-red.md` remains unchanged. Corrective red execution was
against the merged baseline before the first corrective production fixes:

| Seam | Command | Result |
|---|---|---|
| Audit service / endpoint | `dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore` | exit 1; T065 `cases=4`, `assertions=18`, `failures=7`; T181 `cases=12`, `assertions=12`, `failures=5` |

The added Web and operational-run seams were added after this first red run and
were verified in the green pass; no red result is invented for them.

## Green and verification evidence

| Check | Exact result | Status/classification |
|---|---|---|
| Build | `dotnet build .\IUMP.slnx --no-restore` exit 0; 0 warnings/errors | PASS / RUNNABLE_NOW |
| Unit | T065 `5/21/0`; T181 `12/12/0`; full Unit exit 0 | PASS / RUNNABLE_NOW |
| PostgreSQL Integration | `target=127.0.0.1:5433/iump_dev`; T066 `cases=14`, `assertions=15`, `failures=0`; 15 suites, 0 failures | PASS / RUNNABLE_NOW |
| Web lint | `npm run lint` exit 0; existing Fast Refresh warnings only | PASS / RUNNABLE_NOW |
| Web build | `npm run build` exit 0 | PASS / RUNNABLE_NOW |
| Architecture policy | `tests/Verification/architecture.tests.ps1` exit 0 | PASS / RUNNABLE_NOW |
| Repository policy | `tests/Verification/repository-policy.tests.ps1` exit 0 | PASS / RUNNABLE_NOW |
| Observability | `tests/Verification/observability.tests.ps1` exit 0; 12 checks | PASS / RUNNABLE_NOW |
| Hosted HTTP | API on `127.0.0.1:5000` with Host `localhost:5000`: live 200, ready 200, anonymous Dashboard 401, anonymous Audit 401 | PASS / RUNNABLE_NOW |
| Fast harness | `scripts/harness.ps1 -Mode Fast -Feature 003-operational-configuration-workspace` exit 0; PASS=10 | PASS / RUNNABLE_NOW |
| Full harness | `scripts/harness.ps1 -Mode Full -Feature 003-operational-configuration-workspace` exit 20; PASS=13; two company blockers, no mandatory FAIL | BLOCKED / BLOCKED_BY_COMPANY_APPROVAL |
| Frontend behavior runner | Not executable without unapproved package capability | BLOCKED / BLOCKED_BY_PACKAGE_POLICY |
| Authenticated browser runner | No approved runner available for this corrective run; no credentials were recorded | BLOCKED / BLOCKED_BY_MISSING_TOOL |

Approved database target was verified before integration/mutation checks:
`127.0.0.1:5433/iump_dev`. Port 5432, substitute databases, Docker, package
installation, and secret persistence were not used.

## Spec Kit and convergence ledger

- Exact command `speckit-analyze`: command-not-found; `status=NOT_RUN`,
  `reason=SpecKit provider command unavailable`.
- Exact command `speckit-converge`: command-not-found; `status=NOT_RUN`,
  `reason=SpecKit provider command unavailable`.
- Direct read-only comparison of `spec.md`, `plan.md`, `tasks.md`, contracts,
  implementation, tests, and checkpoint found no unresolved Critical/High
  Phase 5 conflict. No T073+ task was added or checked.
- OpenCode/DeepSeek advisory request timed out after 600 seconds; it was
  `status=NOT_RUN`, and no implementation decision depended on it.

## Independent reviews

### Standards review

Two-axis review of the diff from the starting SHA found no remaining Critical,
High, or actionable Medium standards findings after the evidence-status and
traceability corrections in this branch. The review explicitly checked module
ownership, server-side scope, keyset validation, redaction, timezone handling,
database restrictions, naming, and test seams. A possible duplication smell in
the intentionally separate Running/Operational repository queries is a
non-blocking judgement call; existing Running semantics are preserved for
Worker and control callers.

### Specification review

No remaining Critical, High, or Medium T065–T072 compliance findings. The review
confirmed FR-010, FR-023, FR-024, FR-025, FR-026, FR-027, FR-030 and AC-013,
AC-014, AC-015 coverage for the seven corrective findings, and confirmed no
T073+ scope creep.

## Decision and stop

- Phase 5 corrective accepted: **YES** for this bounded corrective scope.
- Planning-ready: **YES**.
- Implementation-ready: **YES**.
- Release-ready: **NO**; Full-harness company-approval blockers and the
  package-policy/browser blockers remain.
- Explicit stop before T073: **YES**.
