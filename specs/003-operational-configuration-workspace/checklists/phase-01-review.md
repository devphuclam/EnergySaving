# Phase 1 Standards and Specification Review

Date: 2026-07-30
Baseline: `8ff3d398e4c1358238ae9044962a40813a7374f1`

Two independent read-only reviews were run in parallel. `.gitignore` and
`src/Web/vite.config.ts` were explicitly excluded as pre-existing user changes.

## Final severity

| Axis | Critical | High | Medium |
|---|---:|---:|---:|
| Standards | 0 | 0 | 0 |
| Specification | 0 | 0 | 0 |

## Closed findings

- Persisted `DataSource.SiteId` through forward migration 0014; removed global Draft fallback and
  required Site-scope authorization when a Source is bound to a Point.
- Replaced caller-boolean IAM authorization with active Administrator lookup by actor ID.
- Added atomic root Site-scope uniqueness and conflict-safe insert outcome detection so only the
  insert winner emits an assignment event.
- Replaced count-only validation with exact requested-chain loading, scope, ancestry, Source Site,
  configuration identity, lifecycle, versions, and pending activation steps.
- Made activation resume state-derived, skip committed transitions, retain per-step idempotency
  keys for uncertain outcomes, and reload after each success.
- Added read-only Back navigation, Cancel for unsaved form state, conflict focus/preservation, and
  persisted Metric/Unit/Data Owner selections.
- Added safe 503 dependency outcomes for status, Engineer listing, and validation.
- Completed Vietnamese shell/wizard visible copy.
- Added PostgreSQL evidence that a partial restart skips an already committed transition.

Low-level refactoring suggestions about splitting the wizard dispatcher are non-blocking and are
not required to satisfy Phase 1 behavior.
