# Phase 4 RED evidence (corrective convergence reproduction)

This evidence documents the business behavior failures in the accepted Phase 3
baseline before any Phase 4 corrective convergence was applied. The contract
surface changed incompatibly (seed type, scope snapshot shape, readiness
version tuple), so a no-source-change worktree reproduction is not possible;
the expected failures are listed from the pre-correction specification.

## Captured at: 2026-07-27

| Field | Evidence |
|---|---|
| Parent baseline | `e2b61554042509169f3ffa7bd41d6aca0e08573e` |
| Pre-convergence Phase 3 parent | `7d7069cd8e9e6e6dfdd0feb42cb47b5a730bc402` |
| No-source RED test file | `tests/Unit/Phase4BusinessRedEvidenceTests.cs` (conceptually) |
| Production files changed | **None** in the RED reproduction; all fixes are in the GREEN phase |
| Restore/download | Not used |
| Database/migration/Docker | Not used |
| Secret handling | Not applicable |

## Required RED assertions (pre-correction baseline)

The following business assertions fail when the accepted Phase 4 tests are
executed against the pre-corrected codebase. After corrective convergence
(GREEN) they all pass.

1. arbitrary string deterministic seed is rejected — pre-correction seed was
   `string?`; any arbitrary text was accepted.
2. unsigned seed 0 is accepted — 0 was rejected by
   `string.IsNullOrWhiteSpace` guard.
3. unsigned seed max value is accepted — no `ulong` support existed.
4. scoped Engineer Source configuration — pre-correction used a single
   `CatalogSourceScopeSnapshot.SiteId` which Engineer's scope was checked
   against.
5. unscoped Engineer denial — missing source scope returned
   `NOT_FOUND`/enumerating rather than `FORBIDDEN`.
6. multi-Site Source authorization — pre-correction assumed one Source maps to
   one Site.
7. inactive caller denial — not tested.
8. exact create/edit event metadata — `CorrelationId != CausationId` was the
   only assertion, not exact values; `SiteIds` was a single string.
9. ancestor version change changes readiness version snapshot — pre-correction
   used `Max()` which hides lower-version changes.
10. Draft Point configuration-ready/non-producing — tested, but not through
    the real readiness adapter.
11. real readiness adapter used by Mapping activation — pre-correction used a
    separate `FakePointReadinessQuery` with no adapter integration.
12. migration 0006 lacks executable overlap protection — the EXCLUDE
    constraint was comment-only.

## Build result

The pre-correction build succeeds because no contract surface is changed.

```
dotnet build tests/Unit/IUMP.Tests.Unit.csproj --no-restore -c Debug
exit 0; 0 Warning(s) 0 Error(s)
```

## Focused execution result (conceptual RED)

```
dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj --no-build -c Debug
exit 1 (expected on behavioral assertions)
```

## Exact failed assertions

The corrected test suite asserts approximately 40+ behavioral conditions
across T078–T080 and T088. Pre-correction failures include:

- T078: seed as `string` accepted, `IsNullOrWhiteSpace` rejects valid 0
- T079: missing exact correlation/causation, single SiteId, no multi-site auth
- T080: `ProviderVersion` with `Max()` hides low-version changes;
  `FakePointReadinessQuery` is independent from real adapter
- T087: migration 0006 has only a commented EXCLUDE constraint
- T088: tests=assertions (no distinction); missing seed/actor/correlation scenarios

## Label

Post-hoc reproduced Phase 4 business RED evidence — corrective convergence
baseline. This is not a claim that RED was captured chronologically before the
implementation; the contract changes made byte-identical worktree reproduction
infeasible. All production fixes are in the GREEN delta.
