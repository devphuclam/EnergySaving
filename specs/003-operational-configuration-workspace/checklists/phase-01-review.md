# Phase 1 Corrective Standards and Specification Review

Date: 2026-07-31
Baseline: `a08e28eb0e2299d12403af37f275cb9d862421a9`

Two independent read-only reviews were run over the corrective implementation diff. The initial
reviews found route synchronization, malformed-query handling, HTTP outcome mapping, request-type,
and test-strength findings. All findings were resolved and the focused tests/build were rerun.

## Final severity

| Axis | Critical | High | Medium actionable | Low |
|---|---:|---:|---:|---:|
| Standards | 0 | 0 | 0 | 0 |
| Specification | 0 | 0 | 0 | 0 |

## Corrective findings closed

- Preserved HTTP status classification through a typed `WorkspaceGatewayError`; the UI now renders
  validation, forbidden, not-found, dependency, and runtime states separately.
- Added one URL/history source of truth with `popstate` reloads so Back/Forward and server-selected
  setup context cannot diverge; Site creation dispatches the same navigation event.
- Strengthened the safe selected-Site endpoint test to assert the concrete 404 status code.
- Replaced the ambiguous status-request shape with a discriminated union and reject both
  `mode=new` plus `selectedSiteId` in the client parser.
- Kept malformed setup query parameters server-visible for the required HTTP 400 validation state;
  direct setup refresh starts on `/setup`, and explicit non-workspace routes are not overwritten by
  landing resolution.
- Made the server status-request constructor private and validated query factory-only, preserving
  the mutual-exclusion invariant for direct port callers; runtime failures now remain distinct from
  dependency failures in the UI.
- Retained relationship-owned chain evaluation, scope-before-counts filtering, server-authorized
  NEW mode, ordered activation, idempotency, and the no-auto-start invariant.

## Verification

- Unit runner: PASS; T013 and all suites report zero failures.
- Web lint/build: PASS; only the repository's existing Fast Refresh warnings remain.
- PostgreSQL integration: PASS; 14 suites, 0 failures against `127.0.0.1:5433/iump_dev`.
- No Critical, High, Medium, or Low finding remains. No Phase 2 task was started.
