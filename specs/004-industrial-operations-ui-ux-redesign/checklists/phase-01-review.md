# Feature 004 Phase 1 Review

**Baseline**: `1cc45e3e64636e093aa0e714f0b2ecc08968ecbb`
**Branch**: `feat/004-phase-01-shell-foundations`
**Review scope**: T011–T027 only.

## Standards review (T025)

Result: **PASS — no unresolved Critical or High finding**.

- Repository policy is respected: no install/download, package or lockfile change, Docker, public
  registry, secret, backend/API/Worker/database change, or port 5432 use.
- Existing route owners and Feature 003 behavior remain the source of business behavior. The shell is
  composition-only and server authorization remains authoritative.
- Shared ownership follows `contracts/implementation-file-map.md`; all new components live under the
  planned `src/Web/src/components/` paths and all planned Phase 1 test/evidence owners exist.
- Evidence-First Industrial Light is light-only for MVP-1; tables are compact by default without a
  user-facing density switch; system fonts and inline SVG avoid unapproved dependencies.
- Keyboard semantics include accessible names, visible focus, skip link, route-title focus,
  `aria-current`, Escape, overlay focus containment/restoration, and live-region state feedback.
- Runtime/browser and visual checks are not overstated: unavailable checks remain
  `BLOCKED_BY_PACKAGE_POLICY` or `NOT_RUN` in the verification record.

## Specification review (T026)

Result: **PASS — Phase 1 scope aligns with the specification and locked clarifications**.

- US1 shell/orientation: identity, context, breadcrumbs, grouped navigation, route focus and safe
  landing are covered by T013–T015 and T022–T023.
- US3 shared operational foundations: data, filters, pagination, form/error, dialog and disclosure
  primitives are covered by T018–T021.
- US6 investigation foundations: detail disclosure, tabs, safe forbidden state and focus behavior are
  provided without implementing Audit business workflows (Phase 4).
- US7 quality foundations: status/data-quality/freshness, feedback states, compact metrics, focus and
  reduced motion are covered by T012, T016–T017.
- Locked decisions are preserved: light-only MVP-1, compact default, responsive sidebar with accessible
  rail/drawer and no persisted preference, permission-based landing/deep-link precedence, and desktop
  plus tablet first-class with mobile non-regression only.
- Phase 2 dashboard/telemetry, Phase 3 configuration behavior, Phase 4 Simulator/Audit workflows and
  Phase 5 hardening remain pending; no later-phase task was executed.

## Dispositions

No Critical/High issue remains for the Phase 1 boundary. Visual baseline validation, browser/axe
automation, pilot usability and full release evidence are intentionally deferred to the later phase
that owns them; these are not silently converted to PASS.

## Round-2 corrective review (supersedes the earlier review conclusions)

**Baseline**: `637b3504d195afa24bc1de938970d5a1cfa97fc6`; **Branch**:
`fix/004-phase-01-corrective-round-2`; evidence: `phase-01-corrective-review-round-2.md`.

The earlier "no unresolved Critical/High finding" conclusion was premature for four items; the
round-2 review re-examined them and records them **CLOSED**:

- **R2-01 (High)** — empty required reason confirmed on the first attempt. CLOSED:
  `reasonConfirmationDecision` computed inside the confirm handler; `onConfirm` never fires with an
  empty required reason; focus returns to the textarea; state resets on close/reopen.
- **R2-02 (High)** — route availability fail-open except Audit. CLOSED: explicit `RouteAccess`
  derived from authoritative server data (`roleMode` + landing from the workspace status,
  `AUDIT_READ` from `/api/v1/me` capabilities) drives every navigation entry path and fails closed
  before the status confirms scope; workspace-status failures map to expired/forbidden/blocked
  presentations on root and non-root entries. Server contract was sufficient — no
  `BLOCKED_BY_AUTHORIZATION_CONTRACT` declared; no capability code invented, no role-name
  authorization, no probing.
- **R2-03 (High)** — `beforeunload` lost and popstate cancel restored the already-popped URL.
  CLOSED: registry-driven `beforeunload` with `preventDefault`/`returnValue`; popstate cancel
  restores the last committed URL (`lastCommittedHrefRef`, captured before popstate) with no
  Back/Forward loops.
- **R2-04 (Medium)** — first-invalid focus worked only once. CLOSED: `activationKey` re-evaluates
  focus on every submit attempt; first invalid field else summary; mount-time errors stay passive.

Standards review after round 2: **PASS — no unresolved Critical or High finding**. Specification
review: **PASS** — the corrected shell stays within Phase 1 scope and the locked
route-and-permission-matrix contract (server-authoritative availability, fail-closed presentation,
no backend contract change).
