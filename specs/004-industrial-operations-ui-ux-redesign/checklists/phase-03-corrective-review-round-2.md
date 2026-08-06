# Feature 004 Phase 3 Corrective Review Round 2 — Configuration Management

**Feature**: `004-industrial-operations-ui-ux-redesign`
**Phase**: 3 — Configuration management (T037–T047)
**Baseline**: `ae02aacb2ce476f07ae8b6eb6491406c749cae9b` (round-1 corrective closure, merged `main`)
**Round-1 corrective production commit**: `218e802fee7017575d8db197a33a8f90e19b71c3`
**Branch**: `fix/004-phase-03-corrective-round-2`
**Round-2 corrective production commit**: `621f706cac0582efabd8c8d976007129e651eef3`
**Date**: 2026-08-06
**Scope**: Corrective closure of the reopened findings P3-R2-01–P3-R2-08 against the Phase 3
configuration management implementation. Frontend-only; no backend/API/Worker/database,
migration, package, lockfile, route, authorization, Simulator workspace, Audit workspace, or
Docker change. `webGateways.ts` was not modified. No merge, no T048, no Phase 4.

## Findings and corrections

| # | Finding | Severity | Root cause | Correction | Evidence | Status |
|---|---|---|---|---|---|---|
| P3-R2-01 | Retry callback captures stale mutation state and can generate a new key | High | Round-1 `performMutation` stored `mutationRetryRun.current = () => void performMutation(attempt)`; the callback closed over the render-time `mutationRetry` state and re-derived the retry key by re-comparing a render-time intent, so a retry click could run a stale closure or issue a fresh `crypto.randomUUID()` instead of reusing the stored key. | Mutation intent is a serializable `PendingManagementMutation` descriptor (resource, kind, entityId, expectedVersion, payload, targetSourceId, draftVersion, retryKey) persisted in `pendingMutationRef` plus a state mirror. `executeManagementMutation(descriptor)` builds every gateway call from the descriptor itself, always passing `descriptor.retryKey`. The "Thử lại cùng yêu cầu" button reads `pendingMutationRef.current` and passes that descriptor explicitly — no closure over render-time state, no auto-retry, exactly one execution per click. | `ConfigurationManagementRoutes.tsx` (`executeManagementMutation`, retry action); red-evidence retry matrix | RESOLVED |
| P3-R2-02 | Mutation intent omits expected version and is not invalidated on payload, target Source or context change | High | Intent equality was `resource + kind + identity + payload` only; `expectedVersion`, `targetSourceId` and `draftVersion` were not part of the identity, payload comparison used raw JSON string order, and only a payload change cleared the retry. | `pendingManagementMutationFingerprint` (deterministic sorted-key JSON via `canonicalJson`) covers resource, kind, entityId, expectedVersion, payload, targetSourceId and draftVersion; `samePendingManagementMutation` compares fingerprints. Invalidation rules: an editor form change or Cancel clears a pending create/update retry; a `duplicateSourceId` change clears a pending duplicate; a resource transition clears all pending intents; success, definitive rejection (403/404/409/422…) and expired clear; only 503 / `RUNTIME_FAILURE` / `DEPENDENCY_UNAVAILABLE` retain the descriptor with its original key. Every mutation completion verifies `descriptor.resource` against `latestResourceRef` before touching current-page feedback; a late success from a previous resource is recorded without corrupting the new page. | Components helpers (`canonicalJson`, fingerprint, disposition); Routes invalidation wiring; red-evidence matrix | RESOLVED |
| P3-R2-03 | Simulator activation readiness requires a status field absent from the actual Simulator Configuration contract | High | `simulatorActivationReadiness` checked `textValue(item.status) !== 'Draft'`, but the authoritative `SimulatorConfigurationManagementItem` (`ConfigurationManagementPorts.cs`, read-only) has **no** `Status` field, so readiness was always false; the table also offered a generic lifecycle `activate` action for simulator-configurations that the server `LifecycleOperation` does not support (would be `UNSUPPORTED_ACTION`). | Readiness derives Draft purely from the contract: present identity, `draftConfigurationVersion` present and > `currentConfigurationVersion`, `relationshipReviewed && !relationshipReceiptStale`, `validationRecorded && !validationReceiptStale`. `lifecycleActionsFor('simulator-configurations', …)` returns `[]` and the `lifecycle()` handler refuses the generic path; Simulator activation uses only `activateSimulatorConfigurationVersion` with expected head version and draft version. | Components `simulatorActivationReadiness`, `lifecycleActionsFor`; Routes `lifecycle`/`activate`; entity-flow + lifecycle evidence | RESOLVED |
| P3-R2-04 | Source-visible evidence contains assertions that are logically known to fail | High | Round-1 evidence carried inverted or contract-invalid assertions: min>max checks used `errors.some(...)` (fires when the rejection works) instead of `!errors.some(...)`; zero was asserted valid for positive interval fields although the server domain requires `expectedIntervalSeconds > 0` and `noDataAfterSeconds > expectedIntervalSeconds` (`Hierarchy.cs` lines 270–271, 329; `SimulatorConfigurationVersion` line 58); Simulator fixtures fabricated `status`; the mapping detail allowlist was asserted to exclude `dataSourceId` (the actual server field) and include the nonexistent `sourceId`; `duplicateIdentityFromResult` was asserted to accept `code`. | All six evidence files rewritten against the authoritative server contract: `!errors.some` for min>max; zero/negative/fractional interval rejection; `noDataAfter > expectedInterval`; finite decimals (`1.5`/`2.75`) for `double?` bounds; unsigned seed rules (reject negative, fractional, > `Number.MAX_SAFE_INTEGER`); no-status Simulator fixtures; `dataSourceId` required in the mapping allowlist and `sourceId` forbidden; duplicate identity accepts only `id`/`configurationId`; retry matrix and detail-ownership matrix added. | All six `src/Web/src/test/configuration-*.test.tsx` evidence sources | RESOLVED (TYPE_CHECKED; runtime NOT_RUN) |
| P3-R2-05 | Detail request token does not invalidate requests when the Drawer closes or the resource/entity owner changes | High | A monotonic token protected only "newer request wins"; closing the Drawer, switching tabs, unmounting, or opening a post-duplicate detail on another resource left the old owner alive, so a late response could re-open the Drawer or overwrite the wrong record. | Explicit `DetailRequestOwner { token, resource, entityId }` via `detailRequestOwner`/`detailResponseApplies`; `detailOwnerRef` is invalidated on Drawer close, tab switch, unmount and session recovery; every `loadDetail` sets a new owner; a response applies only when token + resource + entityId all match the current owner. | Routes `loadDetail`/owner wiring; red-evidence ownership matrix | RESOLVED |
| P3-R2-06 | Numeric and Source Mapping normalization do not match actual contract types | Medium | All numeric fields were treated as plain integers with property-key messages; decimals were rejected for `double?` Simulator bounds, the seed had no unsigned/safe-range rule, no `noDataAfter > expectedInterval` rule, and mapping dates were passed through as `effectiveFromUtc`/`effectiveToUtc` strings without fail-closed validation. | `NUMERIC_FIELDS` now declares field kinds: positive-int (`expectedIntervalSeconds`, `noDataAfterSeconds`, `intervalSeconds`), unsigned-int (`deterministicSeed`: integer ≥ 0 and ≤ `Number.MAX_SAFE_INTEGER`), finite-decimal (`minimumValue`, `maximumValue` — `1.5`/`2.75` accepted); messages use Vietnamese field labels, never property keys; cross-field rules `min ≤ max` and `noDataAfter > expectedInterval`; mapping dates fail closed on malformed text, omit a blank `effectiveTo`, reject end before start, preserve the approved contract representation (`effectiveFrom`/`effectiveTo` in the body) and retain entered values after failure. | Components `NUMERIC_FIELDS`, `normalizeConfigurationForm`; red-evidence + source-mapping matrices | RESOLVED |
| P3-R2-07 | List, option and mutation expiry/recovery paths remain incomplete | Medium | Dependency/runtime list failures had no retry action; an expired option still offered "Thử lại" like a dependency error; a mutation 401/`expired` fell into the generic error message with no session-recovery action. | List dependency/runtime renders `ErrorState` with "Thử lại" that reloads the exact current request (`reloadCurrent`, filter context retained); list expired offers only one session-recovery action; option expired shows "Đăng nhập lại" (session recovery), never "Thử lại", and disables Tạo mới/Lưu with the exact reason; mutation disposition `expired` (401/`errorCode 'expired'`) retains the editor and its form values, shows session-recovery feedback and clears the retry; forbidden never offers an ordinary retry. | Routes recovery wiring; `ConfigurationTable`/`EditorPanel` props; lifecycle evidence disposition matrix | RESOLVED |
| P3-R2-08 | Duplicate/detail identity mappings accept unsupported aliases and may fabricate relationship context | Medium | `duplicateIdentityFromResult` accepted `code`; `reviewFromItem` fabricated `['Data Source']` when `reviewRelationships` was absent from the item. | Duplicate identity accepts only the server-returned `id` or `configurationId`; when the server confirms duplication without an identity, the success is retained, the list reloads, the message states the response contained no Draft identity, and no detail call is attempted; `reviewFromItem` uses the authoritative `reviewRelationships` array when present, else `[]`, and the review card displays "Chưa có thông tin quan hệ trong contract hiện tại." | Components `duplicateIdentityFromResult`; Routes `reviewFromItem`/`completeMutation`; entity-flow + source-mapping evidence | RESOLVED |

## Compile-visible evidence

| Check | Owner | Invariant |
|---|---|---|
| Mutation identity and retry | `configuration-red-evidence.test.tsx` | fingerprint stable for identical descriptor and ignores the retry key; differs on payload/entity/expectedVersion/targetSourceId/draftVersion change; an exact retry reuses the stored key; disposition matrix: success / retryable (503, RUNTIME_FAILURE, DEPENDENCY_UNAVAILABLE) / expired (401, `expired`) / definitive (403/404/409/422) |
| Form normalization | `configuration-forms.test.tsx`, `configuration-red-evidence.test.tsx` | empty optional numeric absent; zero interval rejected (positive field); zero decimals preserved; negative/fractional/non-finite rejected; `noDataAfter > expectedInterval`; seed: 0 valid, −1/1.5/> MAX_SAFE_INTEGER rejected; min ≤ max with `!errors.some`; Vietnamese labels; dirty semantics |
| Source Mapping | `configuration-source-mapping.test.tsx` | `dataSourceId` in the detail allowlist, `sourceId` forbidden; malformed dates fail closed; blank `effectiveTo` omitted; end before start rejected; body uses `effectiveFrom`/`effectiveTo`, never internal keys |
| Duplicate identity | `configuration-entity-flows.test.tsx`, `configuration-red-evidence.test.tsx` | only `id`/`configurationId`; `code` never accepted; empty body never fabricates an identity |
| Activation readiness | `configuration-entity-flows.test.tsx`, `configuration-lifecycle-states.test.tsx` | contract-realistic fixtures without `status`; draft > current + confirmed non-stale review + confirmed non-stale validation → ready; every other combination → not ready with reason; generic lifecycle never offered for simulator-configurations |
| Detail ownership | `configuration-red-evidence.test.tsx` | owner accepts its own response; closed/invalidated owner rejects all; newer request, resource switch, entity switch and cross-resource post-duplicate detail all rejected |
| Sort/date/allowlist/lifecycle | `configuration-tables.test.tsx`, `configuration-red-evidence.test.tsx`, `configuration-lifecycle-states.test.tsx` | unchanged round-1 invariants retained: explicit per-resource sort defaults, safe dates, Vietnamese-labeled allowlists without secrets, lifecycle policy matrix |

## Verification commands (corrective round 2)

| Command/check | Result | Notes |
|---|---|---|
| `npx tsc -b` (from `src/Web`) | **PASS** | Exit 0; all six rewritten evidence sources type-check. |
| `npm run lint` (from `src/Web`) | **PASS** | Exit 0; only the pre-existing Fast Refresh/deps warnings. |
| `npm run build` (from `src/Web`) | **PASS** | `tsc -b` + Vite build exit 0. |
| `scripts/harness.ps1 -Mode Fast -Feature 004-industrial-operations-ui-ux-redesign` | **PASS** | `Harness Fast summary: PASS=11`, exit 0. |
| `git diff --check` | **PASS** | No whitespace errors. |
| Exported evidence execution | **NOT_RUN** | No approved frontend runner exists; exported checks are TYPE_CHECKED only, never claimed as executed. |
| Browser/visual/automated a11y | **NOT_RUN / BLOCKED_BY_PACKAGE_POLICY** | No package installation or rendering evidence; no visual PASS claimed. |

## Scope and safety checks

- Changed production files are limited to `src/Web/src/features/configuration/ConfigurationManagementRoutes.tsx`
  and `ConfigurationManagementComponents.tsx`, plus the six source-visible evidence files under
  `src/Web/src/test/`. `webGateways.ts`, gateway contracts, routes, `App.tsx`,
  `ConfigurationRoutes.tsx` and the server are untouched in this round.
- The original Phase 3 production commit `8e740d01c9925f7fd14e7af7692683b28669c418`, the original
  checkpoint `f69c7b8d0cdf9093374e174106d29c05ab55f7c9`, the round-1 production commit
  `218e802fee7017575d8db197a33a8f90e19b71c3` and the round-1 evidence commit
  `ae02aacb2ce476f07ae8b6eb6491406c749cae9b` are preserved; this document and the round-1
  verification/review/checkpoint superseding notes append to, and never rewrite, historical evidence.
- No backend/API/Worker/database/migration, package/lockfile, secret, Docker, route, authorization
  or PostgreSQL target change. Port `5432` was not used. No merge and no T048/Phase 4 work.

## Readiness after corrective closure

- Critical findings: **0**. High findings: **0** (P3-R2-01–P3-R2-05 closed). Medium: **0**
  (P3-R2-06, P3-R2-07, P3-R2-08 closed).
- Progression to Phase 4: **YES, only after a new explicit `/speckit.implement` invocation**.
- Release-ready: **NO**. Historical Full evidence retains its known environment/company approval
  blockers; this corrective round does not rerun Full or promote blocked/runtime evidence.
- Explicit stop: the corrective round ends here. No T048+ work was started.

## Superseded by the Phase 3 final consistency review (2026-08-06)

A final consistency round on `fix/004-phase-03-final-consistency` (baseline
`1dc2ec3c596bf9aee88f750eaf5e04752fcf84bd`) reopened the closure statements above where the round-2
evidence itself was inverted or contract-invalid. Findings P3-FC-01–P3-FC-07 (Source Mapping write
contract `effectiveFromUtc`/`effectiveToUtc` and `null`-clear semantics; inverted source-mapping
and forms assertions; retry flight state machine and `retryKeyFor` reuse; antiforgery
session-expiry classification in `webGateways.ts`; Point required IDs; stale retry intent
invalidation; checkpoint honesty) were closed in production commit
`29985b392dd5a5eebd21c5fd735d94e00b2bdb1f`; the detailed table and verification are in
[phase-03-final-consistency-review.md](phase-03-final-consistency-review.md). The round-2 finding
table above remains historical evidence as performed; the final consistency review supersedes it
for current-state results: **0 Critical / 0 High / 0 actionable Medium**. Round-2 verification
statements are not amended in place and remain historical.
