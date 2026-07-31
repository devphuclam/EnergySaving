# Phase 1 Corrective Standards and Specification Review

Date: 2026-07-31
Baseline: `0165719c0ee9f8477efd336c16b5887c58ae3a8f`

Two independent read-only reviews were run in parallel over the complete corrective
implementation diff.

## Final severity

| Axis | Critical | High | Medium actionable | Low |
|---|---:|---:|---:|---:|
| Standards | 0 | 0 | 0 | 0 |
| Specification | 0 | 0 | 0 | 0 |

## Corrective findings closed

- Replaced list-position derivation with relationship-owned evaluation of all authorized chains,
  operational landing precedence, highest-completion resume, and stable-identity tie-breaking.
- Filtered Site/Area scope before Source/Mapping loading and before chain counts; an Area-only
  principal cannot observe an unrelated Site-wide unmapped Source.
- Kept persisted reconstruction records internal to the hosting/composition implementation
  boundary instead of adding public hosting contracts.
- Implemented the four Administrator Site/Engineer substates, removed unrelated name input from
  activation/assignment, disabled assignment when no active Engineer exists, and kept the
  Engineer Site step read-only.
- Propagated Engineer/options dependency failures instead of converting them into successful
  empty data.
- Added server-owned session authentication, explicit authentication/authorization/antiforgery
  middleware ordering, and fail-closed invalid-cookie behavior.
- Restored the Mapping activation savepoint before translating a PostgreSQL exclusion conflict
  to a replayable 409, preserving the outer idempotency transaction.
- Preserved the no-auto-start invariant and stopped before all Phase 2 tasks.

T035 is complete: both implementation review axes contain no Critical, High, or actionable Medium
finding. Verification/checkpoint evidence is assessed separately and does not convert an unrun
manual acceptance step into PASS.
