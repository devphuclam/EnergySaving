# Feature 004 Phase 3 Final Consistency Review — Configuration Management

**Feature**: `004-industrial-operations-ui-ux-redesign`
**Phase**: 3 — Configuration management (T037–T047)
**Baseline**: `1dc2ec3c596bf9aee88f750eaf5e04752fcf84bd` (round-2 corrective closure, merged `main`)
**Round-2 corrective production commit**: `621f706cac0582efabd8c8d976007129e651eef3`
**Branch**: `fix/004-phase-03-final-consistency`
**Final consistency production commit**: `29985b392dd5a5eebd21c5fd735d94e00b2bdb1f`
**Date**: 2026-08-06
**Scope**: Final source-contract consistency correction of the Phase 3 configuration management
implementation: Source Mapping write contract, inverted compile-visible evidence, retry workflow
state machine and retry-key reuse, antiforgery session-expiry classification, Point required
fields, stale retry UI, and checkpoint honesty. Frontend-only; no backend/API/Worker/database,
migration, package, lockfile, route, authorization, Simulator workspace, Audit workspace, or
Docker change. `webGateways.ts` changes are limited to `classifyAntiforgeryFailure` and the
`managementMutation` catch path; no gateway request shape was changed. No merge, no T048, no
Phase 4.

## Findings and corrections

| # | Finding | Severity | Root cause | Correction | Evidence | Status |
|---|---|---|---|---|---|---|
| P3-FC-01 | Source Mapping request contract inverted | High | Round-2 `normalizeConfigurationForm` sent the approved server fields as `effectiveFrom`/`effectiveTo` and fabricated `dataSourceId` into requests, while the authoritative contract (`PostgresApplicationPorts.CreateMapping`/`UpdateMapping`, read-only) consumes `sourceId`, `pointId`, `effectiveFromUtc`, `effectiveToUtc`; update semantics are `HasField("effectiveToUtc") ? TimestampFieldOrNull(...) : preserve` — explicit `null` clears, omission preserves; `dataSourceId` is a read-model/detail field only. | Normalization now emits `effectiveFromUtc` (omitted when blank on create so the server default applies), `effectiveToUtc` (explicit `null` only when an existing value was cleared in edit mode, otherwise omitted), rejects malformed/reversed intervals and blank edit start before any request is built; the detail allowlist keeps `dataSourceId`/`pointId`/`effectiveFrom`/`effectiveTo` and never admits `sourceId`. | `ConfigurationManagementComponents.tsx` `normalizeConfigurationForm`; `configuration-source-mapping.test.tsx` (rewritten to the real write contract) | RESOLVED |
| P3-FC-02 | Compile-visible evidence contained assertions logically known to fail | High | Round-2 source-mapping evidence required body keys `effectiveFrom`/`effectiveTo` and asserted `effectiveFromUtc` absence — the exact inverse of the server contract; `configuration-forms.test.tsx` asserted `Infinity` rejection with `errors.some(...)` inverted; a duplicate/inconsistent assertion contradicted the Vietnamese-label requirement. | `configuration-source-mapping.test.tsx` and `configuration-forms.test.tsx` rewritten/patched to assert the real contract: `effectiveFromUtc` present, no legacy aliases, `effectiveToUtc: null` clear semantics, open-ended preserved, `!errors.some` for non-finite rejection, Point ID fixtures asserting `metricId`/`unitId`/`dataOwnerUserId` (with `Chỉ số`/`Đơn vị`/`Chủ dữ liệu` labels), never `name`, for the update path; contradictory assertion removed. | `configuration-source-mapping.test.tsx`, `configuration-forms.test.tsx` (rewritten; TYPE_CHECKED) | RESOLVED (TYPE_CHECKED; runtime NOT_RUN) |
| P3-FC-03 | Retry workflow could deadlock the workspace after a retryable failure | High | Round-2 kept the mutation locked while a retry intent existed (`mutationPending` remained true between attempts), so no corrective action except the retry button was possible; `samePendingManagementMutation` double-contradictory evidence masked the state model. | Explicit `ManagementMutationFlight` state machine: `idle` → `in-flight` → (`retry-intent` | `idle`). A settled retryable failure releases the in-flight lock and retains only the descriptor (`managementRetryIntent`); success, definitive rejection and expiry settle to `idle`; discard and session recovery clear the intent. All seven submit paths guard on `in-flight` only, so retry, edit, discard and every other operation remain available after a failure. | `ConfigurationManagementComponents.tsx` flight helpers; `ConfigurationManagementRoutes.tsx` wiring; `configuration-red-evidence.test.tsx` flight matrix | RESOLVED |
| P3-FC-04 | Antiforgery session expiry classified as a generic runtime failure | High | `webGateways.managementMutation` collapsed every rejection (including the `antiforgery-401` thrown by `antiforgeryToken()`) into `{ok:false,status:503,errorCode:'RUNTIME_FAILURE'}`, so an expired session was offered a retry and never a re-login. | New exported pure `classifyAntiforgeryFailure(error)` maps 401 → `{ok:false,status:401,errorCode:'expired'}` (session recovery UI, never retry), 403 → `FORBIDDEN` definitive, 5xx → retryable 503, everything else (transport/unknown) → 503 `RUNTIME_FAILURE`; `managementMutation` catch delegates to it. The management mutation disposition matrix now treats `expired` as non-retryable. | `webGateways.ts` (`classifyAntiforgeryFailure`, catch path); `configuration-red-evidence.test.tsx` antiforgery matrix | RESOLVED |
| P3-FC-05 | Point required fields did not match the server update contract | Medium | Round-2 validation required `name` on Point edit, but the authoritative update contract consumes only `metricId`, `unitId`, `dataOwnerUserId` (name is create-only); create additionally requires `name` and `assetId`. | `requiredFieldsFor('points')` now returns `[name, assetId, metricId, unitId, dataOwnerUserId]` for create and `[metricId, unitId, dataOwnerUserId]` for edit; `configurationValidationErrors` derives from it with Vietnamese labels; evidence asserts first-invalid determinism for both modes and that a valid edit without `name` passes. | `ConfigurationManagementComponents.tsx` `requiredFieldsFor`/`configurationValidationErrors`; red-evidence Point matrix | RESOLVED |
| P3-FC-06 | Stale retry intent survived a changed submission | Medium | After a retryable failure, editing the form or changing the duplicate target Source left the stored intent reachable, and any resubmission generated a fresh `crypto.randomUUID()` — a changed request could replay under the old idempotency scope or an identical one under a new scope. | A field change (create/update) or duplicate-target change discards the retry intent and its feedback; `retryKeyFor(intent, descriptor)` reuses the stored key only when the new descriptor is fingerprint-identical (`samePendingManagementMutation`, key excluded from the fingerprint) and generates a new key otherwise; wired into all seven submit sites. | Components `retryKeyFor`; Routes invalidation handlers; `configuration-red-evidence.test.tsx` reuse/change matrix | RESOLVED |
| P3-FC-07 | Round-2 checkpoint overstated closure honesty | Medium | The round-2 corrective review recorded its evidence checks as closed while the evidence itself contained known-failing assertions, and did not record the remaining runtime/visual blockers as non-PASS. | This review supersedes it: compile-visible checks are TYPE_CHECKED/NOT_RUN (no runner exists), browser/visual/a11y evidence stays NOT_RUN/BLOCKED_BY_PACKAGE_POLICY, no check is described as passing without execution; `phase-03-verification.md`, `phase-03-review.md`, `phase-03-corrective-review-round-2.md`, `phase-03-checkpoint.md` and `tasks.md` carry the superseding reference. | This document + superseding notes in the five governance artifacts | RESOLVED |

## Compile-visible evidence

| Check | Owner | Invariant |
|---|---|---|
| Source Mapping write contract | `configuration-source-mapping.test.tsx` | create sends `effectiveFromUtc`; blank end omitted; edit sends the edited start as `effectiveFromUtc`; clearing an existing end emits `effectiveToUtc: null`; open-ended preserved by omission; no `effectiveFrom`/`effectiveTo` aliases in the request; malformed/reversed rejected and never transmitted; edit blank start invalid; `sourceId`/`pointId` immutable; detail allowlist contains `dataSourceId`/`pointId`/`effectiveFrom`/`effectiveTo` and excludes `sourceId` |
| Antiforgery classification | `configuration-red-evidence.test.tsx` | `classifyAntiforgeryFailure` maps 401 → expired, 403 → FORBIDDEN definitive, 5xx → retryable 503, transport/unknown → 503 `RUNTIME_FAILURE`; `managementMutation` catch never emits a false success |
| Flight/retry state machine | `configuration-red-evidence.test.tsx` | `begin` → in-flight; retryable settle → `retry-intent` with the original descriptor and key, in-flight released; success/expired/definitive → idle; discard removes the intent; `retryKeyFor` reuses the stored key for fingerprint-identical resubmission and generates a new key otherwise |
| Point required fields | `configuration-red-evidence.test.tsx`, `configuration-forms.test.tsx` | edit requires `metricId`/`unitId`/`dataOwnerUserId` and never `name`; create requires `name`/`assetId` plus the three IDs; first-invalid order deterministic; Vietnamese labels |
| Numeric and form normalization | `configuration-forms.test.tsx`, `configuration-red-evidence.test.tsx` | zero/negative/fractional/unsafe/NaN/Infinity rejected for the right field kinds; zero seed valid; finite decimals preserved; `noDataAfter > expectedInterval`; min ≤ max with `!errors.some`; dirty semantics |
| Unchanged invariants | `configuration-entity-flows.test.tsx`, `configuration-lifecycle-states.test.tsx`, `configuration-tables.test.tsx` | self-reviewed against the implementation; no inverted condition found; readiness/sort/date/allowlist/disposition matrices retained |

## Verification commands (final consistency round)

| Command/check | Result | Notes |
|---|---|---|
| `npx tsc -b` (from `src/Web`) | **PASS** | Exit 0; production files and all evidence sources type-check. |
| `npm run lint` (from `src/Web`) | **PASS** | Exit 0; only the pre-existing Fast Refresh/deps warnings. |
| `npm run build` (from `src/Web`) | **PASS** | `tsc -b` + Vite build exit 0. |
| `scripts/harness.ps1 -Mode Fast -Feature 004-industrial-operations-ui-ux-redesign` | **PASS** | `Harness Fast summary: PASS=11`, exit 0. |
| `git diff --check` | **PASS** | No whitespace errors. |
| Exported evidence execution | **NOT_RUN** | No approved frontend runner exists; exported checks are TYPE_CHECKED only, never claimed as executed. |
| Browser/visual/automated a11y | **NOT_RUN / BLOCKED_BY_PACKAGE_POLICY** | No package installation or rendering evidence; no visual PASS claimed. |
| Full harness mode | **NOT_RUN** | Not rerun in this corrective round; historical Full evidence keeps its blockers. |

## Scope and safety checks

- Changed production files are limited to `ConfigurationManagementRoutes.tsx`,
  `ConfigurationManagementComponents.tsx`, `webGateways.ts` (classification only) and three
  source-visible evidence files (`configuration-forms`, `configuration-red-evidence`,
  `configuration-source-mapping`); the remaining three evidence sources were self-reviewed and are
  unchanged.
- The historical Phase 3 chain — implementation `8e740d01c9925f7fd14e7af7692683b28669c418`,
  checkpoint `f69c7b8d0cdf9093374e174106d29c05ab55f7c9`, round-1 `218e802fee7017575d8db197a33a8f90e19b71c3` /
  `ae02aacb2ce476f07ae8b6eb6491406c749cae9b`, round-2 `621f706cac0582efabd8c8d976007129e651eef3` /
  `1dc2ec3c596bf9aee88f750eaf5e04752fcf84bd` — is preserved; this document and the superseding
  notes append to, and never rewrite, historical evidence.
- No backend/API/Worker/database/migration, package/lockfile, secret, Docker, route, authorization
  or PostgreSQL target change. Port `5432` was not used. No merge and no T048/Phase 4 work.

## Readiness after final consistency closure

- Critical findings: **0**. High findings: **0** (P3-FC-01–P3-FC-04 closed). Medium: **0**
  (P3-FC-05–P3-FC-07 closed).
- Progression to Phase 4: **YES, only after a new explicit `/speckit.implement` invocation**.
- Release-ready: **NO**. Historical Full evidence retains its known environment/company approval
  blockers; this corrective round does not rerun Full or promote blocked/runtime evidence.
- Explicit stop: the final consistency round ends here. No T048+ work was started.
