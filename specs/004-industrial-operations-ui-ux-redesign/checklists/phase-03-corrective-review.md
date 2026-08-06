# Feature 004 Phase 3 Corrective Review — Configuration Management

**Feature**: `004-industrial-operations-ui-ux-redesign`
**Phase**: 3 — Configuration management (T037–T047)
**Baseline**: `f69c7b8d0cdf9093374e174106d29c05ab55f7c9` (original Phase 3 checkpoint, merged `main`)
**Original Phase 3 production commit**: `8e740d01c9925f7fd14e7af7692683b28669c418`
**Branch**: `fix/004-phase-03-corrective-remediation`
**Date**: 2026-08-06
**Scope**: Corrective closure of the post-merge findings P3-C01–P3-C08 against the Phase 3
configuration management implementation. Frontend-only; no backend/API/Worker/database,
migration, package, lockfile, route, authorization, Simulator workspace, Audit workspace, or
Docker change. No merge, no T048, no Phase 4.

## Findings and corrections

| # | Finding | Severity | Root cause | Correction | Evidence | Status |
|---|---|---|---|---|---|---|
| P3-C01 | Edit identity coupled to the modal Detail Drawer | Critical | `beginEdit` stored the record into the shared `detail` state and the submit derived the update identity from `detail`; closing the Drawer cleared `detail`, so an update after closing became a local `NOT_FOUND`. Detail loads had no request token, so stale responses could land on the wrong record/resource. | `detailRecord`/`detailState` (Drawer) are separated from `editingRecord`/`editorMode`/`form`/`initialForm` (editor). `beginEdit` never opens or touches the Drawer. A monotonic `detailRequestToken` guards every `openDetail`/`refreshDetail`: responses with a stale token, or for a different record/resource, are ignored. The update identity and expected version come only from `editingRecord`. | `src/Web/src/features/configuration/ConfigurationManagementRoutes.tsx` (`editingRecord`, `detailRequestToken`, `openDetail`, `submitEditor`); entity-flow evidence | RESOLVED |
| P3-C02 | Dirty forms silently discarded by internal transitions | High | Tab clicks called `setEditor(null)` directly and create/edit opened over a dirty form without confirmation; the close path was the only guarded transition. | One guarded transition model: `ConfigurationTransition` + `requestConfigurationTransition` covers tab switch, create, edit, and close-editor; a dirty form first opens the existing `ConfirmDialog` and only a confirmed choice performs the transition. Dirty is a canonical comparison (`configurationFormDirty`): restoring the original values is not dirty, and invalid text stays dirty. | Routes (`requestConfigurationTransition`, `performTransition`); `configurationFormDirty` in components; forms evidence | RESOLVED |
| P3-C03 | Mutation idempotency identity not stable | High | Every management mutation omitted `retryKey`, so `ManagementGateway` generated a fresh `crypto.randomUUID()` per call (including per retry), defeating the server `Idempotency-Key` contract. | Pure `managementMutationFingerprint`, `sameManagementMutationIntent`, `isRetryableManagementMutationResult`. The key is created on the first submit, retained after a retryable result (503 / `RUNTIME_FAILURE` / `DEPENDENCY_UNAVAILABLE`), and reused only for the exact same intent (`resource` + kind + identity + payload). No auto-retry. The key and pending intent are cleared on success, definitive rejection, cancel, or payload change. Every gateway mutation now receives an explicit `retryKey`; a "Thử lại cùng yêu cầu" action re-runs the identical request with the same key. `webGateways.ts` was not modified. | Routes (`performMutation`, `mutationRetry`); components helpers; red-evidence matrix | RESOLVED |
| P3-C04 | Numeric normalization fail-open | High | `normalizedForm` converted only truthy values (`if (result[key])`), silently dropped zero, never rejected `NaN`/`Infinity`, and enforced no integer, positivity, or `min ≤ max` rule. | Pure `normalizeConfigurationForm(resource, mode, form)` returns `{ body, errors, canonical }`: empty optional numeric fields are absent from the body, zero stays zero, `NaN`/`Infinity`/fractional/non-positive values are rejected, Simulator `minimumValue ≤ maximumValue` is enforced, and the canonical view keeps invalid text visible so the form remains dirty. Errors drive `FieldErrorSummary` first-invalid focus with `aria-invalid`/`aria-describedby`. | Components `normalizeConfigurationForm`; routes `submitEditor`/errors; forms + red-evidence matrices | RESOLVED |
| P3-C05 | Simulator duplicate/activation regression | High | After a successful duplicate only the list was reloaded — no identity shown, no new Draft detail loaded, no review/validation next step; `ActivateVersionButton` received the default `readyForActivation = true`, so activation stayed enabled for records whose review/validation receipts were missing or stale, and success was not gated on `result.ok`. | The duplicate adopts the exact identity returned by the server (`duplicateIdentityFromResult`), never invents one, shows the identity in the success message, and token-guards a reload of the new Draft detail (identity is retained if the detail refresh fails). `simulatorActivationReadiness` requires Draft status, a draft version above the current version, a confirmed review receipt that is not stale, and a confirmed validation receipt that is not stale; it gates both the button and the handler. Activation feedback is emitted only after `result.ok`. | Routes (`duplicate`, `activate`, `refreshDetail`); components `simulatorActivationReadiness`, `duplicateIdentityFromResult`; entity-flow + lifecycle evidence | RESOLVED |
| P3-C06 | List, editor, mutation and detail shared one state variable | High | A single `state` served the list, editor validation, mutation failures and detail `not-found`; a validation or conflict therefore replaced the whole list presentation; option states had no `expired` value and there was no session-recovery path. | Dedicated `listState` (list loads only), editor errors through `errors`/`feedback`/`invalidField`, mutation outcomes through `feedback` + `mutationRetry`, and detail through `detailState` — the list stays visible during editing and after validation/conflict/dependency failures. `OptionState` gains `'expired'` with its own message and retry path. One session-recovery path (`onSessionRecovery` → `window.location.reload()`, identical to the Telemetry route) is wired from `App.tsx` through `ConfigurationRoutes`. | Routes state model; `App.tsx`/`ConfigurationRoutes.tsx` wiring; lifecycle evidence (`Phiên đã hết hạn`) | RESOLVED |
| P3-C07 | Filter/sort/detail/date contract violations | Medium | `useDebouncedSearch` triggered a list request per keystroke; `FilterBar` mutated the applied filter directly; sort silently fell back to the first column; the Detail Drawer dumped every object field (filtered only by `secret`/`token` name substring); date columns rendered `Invalid Date`. | Draft/applied filter model: draft edits never trigger requests, "Áp dụng" normalizes and resets to page 1, "Xóa bộ lọc" retains only contract scope (`page`/`pageSize`); `useDebouncedSearch` removed. Pure `effectiveConfigurationSort` uses explicit per-resource defaults (a valid key is never implicitly the first column). `detailFieldsFor` exposes an explicit allowlist with Vietnamese labels for all seven entities (no raw dump, no secret/unknown fields). `safeConfigurationDate` renders "—" for absent and "Không hợp lệ" for malformed dates, never `Invalid Date`. True-empty is distinguished from filtered-empty in the empty message. | Routes (`draftFilter`/`appliedFilter`, sort select, Drawer, date columns); components helpers; tables evidence | RESOLVED |
| P3-C08 | Evidence did not cover behavior | High | The six Phase 3 evidence files checked importability and a few constants only, so the corrective behavior had no source-visible evidence. | The six evidence files now hold pure, type-checked matrices: fingerprint stability and intent identity; retryability classification; form normalization (empty-omit, zero-preserved, `NaN`/`Infinity`, integer, positive interval, `min ≤ max`); dirty semantics (restore-original, invalid text); date safety; detail allowlists for all seven entities (no secrets); sort contract and fallback; duplicate server identity (never fabricated); activation readiness incl. stale receipts; lifecycle action policy per resource/status; expired-session presentation. Runtime execution remains `NOT_RUN`/`BLOCKED_BY_PACKAGE_POLICY`; nothing is claimed as executed. | `src/Web/src/test/{configuration-red-evidence, configuration-tables, configuration-entity-flows, configuration-source-mapping, configuration-forms, configuration-lifecycle-states}.test.tsx` | RESOLVED (TYPE_CHECKED; runtime NOT_RUN) |

## Compile-visible evidence

| Check | Owner | Invariant |
|---|---|---|
| Mutation identity | `configuration-red-evidence.test.tsx` | fingerprint stable for identical intent, differs on payload/identity change; `sameManagementMutationIntent` symmetric; `isRetryableManagementMutationResult` accepts only 503/RUNTIME_FAILURE/DEPENDENCY_UNAVAILABLE, never 409/422/success |
| Form normalization | `configuration-forms.test.tsx`, `configuration-red-evidence.test.tsx` | empty optional numeric absent; zero stays the number zero; `NaN`/`Infinity`/fraction/non-positive rejected; `min ≤ max`; whitespace-only name required; canonical dirty: unchanged and trailing-space-restored not dirty, changed and invalid text dirty |
| Duplicate identity | `configuration-entity-flows.test.tsx`, `configuration-source-mapping.test.tsx` | duplicate adopts server-returned `id`/`configurationId`/`code` only; empty body never fabricates an identity |
| Activation readiness | `configuration-entity-flows.test.tsx`, `configuration-lifecycle-states.test.tsx` | Draft + draft>current + review receipt confirmed + not stale + validation confirmed + not stale → ready; every other combination → not ready with reason |
| Sort contract | `configuration-tables.test.tsx`, `configuration-red-evidence.test.tsx` | all seven resources declare explicit current-page keys; invalid key falls back to the explicit default (never an implicit first column); direction normalizes to ascending/descending |
| Detail allowlist | `configuration-red-evidence.test.tsx`, `configuration-source-mapping.test.tsx` | all seven entities have Vietnamese-labeled allowlists; no secret/token/password/connection keys; unknown resource exposes nothing |
| Date safety | `configuration-tables.test.tsx`, `configuration-source-mapping.test.tsx`, `configuration-red-evidence.test.tsx` | absent → "—", malformed → "Không hợp lệ", valid → real localized date |
| Lifecycle policy | `configuration-lifecycle-states.test.tsx` | per-resource/status action matrix; delete only safe Draft Data Sources/Source Mappings; expired-session title distinct; dependency renders blocked |

## Verification commands (corrective round)

| Command/check | Result | Notes |
|---|---|---|
| `npx tsc -b` (from `src/Web`) | **PASS** | Exit 0; all six extended evidence sources type-check. |
| `npm run lint` (from `src/Web`) | **PASS** | Exit 0; only the pre-existing Fast Refresh/deps warnings. |
| `npm run build` (from `src/Web`) | **PASS** | `tsc -b` + Vite build exit 0. |
| `scripts/harness.ps1 -Mode Fast -Feature 004-industrial-operations-ui-ux-redesign` | **PASS** | `Harness Fast summary: PASS=11`, exit 0. |
| Exported evidence execution | **NOT_RUN** | No approved frontend runner exists; exported checks are TYPE_CHECKED only, never claimed as executed. |
| Browser/visual/automated a11y | **NOT_RUN / BLOCKED_BY_PACKAGE_POLICY** | No package installation or rendering evidence; no visual PASS claimed. |

## Scope and safety checks

- Changed production files are limited to `src/Web/src/features/configuration/ConfigurationManagementRoutes.tsx`,
  `ConfigurationManagementComponents.tsx`, `ConfigurationRoutes.tsx`, and `src/Web/src/App.tsx`
  (session-recovery wiring only, matching the Telemetry `onSessionRecovery` pattern).
  `webGateways.ts`, gateway contracts, routes, and the server are untouched.
- The original Phase 3 production commit `8e740d01c9925f7fd14e7af7692683b28669c418` and the
  original checkpoint `f69c7b8d0cdf9093374e174106d29c05ab55f7c9` are preserved; this document and
  the phase-03 verification/review/checkpoint superseding notes append to, and never rewrite,
  historical evidence.
- No backend/API/Worker/database/migration, package/lockfile, secret, Docker, route, authorization
  or PostgreSQL target change. Port `5432` was not used. No merge and no T048/Phase 4 work.

## Readiness after corrective closure

- Critical findings: **0** (P3-C01 closed). High findings: **0** (P3-C02, P3-C03, P3-C04, P3-C05,
  P3-C06, P3-C08 closed). Medium: P3-C07 closed.
- Progression to Phase 4: **YES, only after a new explicit `/speckit.implement` invocation**.
- Release-ready: **NO**. Historical Full evidence retains its known environment/company approval
  blockers; this corrective round does not rerun Full or promote blocked/runtime evidence.
- Explicit stop: the corrective round ends here. No T048+ work was started.
