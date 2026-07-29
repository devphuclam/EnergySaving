# Phase 7 Exact-Result RED Evidence

- Parent baseline: `b6b2510820f5ab8f0af5569a2fc18b4ee4b2f892`
- Corrective checkpoint: T151 exact-result closure (T152+ not in scope).
- Reproduction: temporary native worktree
  `C:\Users\TD-999\AppData\Local\Temp\iump-phase7-exact-red-8108d367ace44e3bbff46835a6bbe42b`
- Scope: post-hoc Phase 7 exact-result assertions only; no production implementation, database,
  migration execution, API/Worker composition root, PostgreSQL adapter, or Phase 8 work.
- Temporary changes: one test-only `Phase7ExactResultRedTests.cs` and one test-runner line.
- Build: `dotnet build IUMP.slnx -c Debug --no-restore` -> exit `0` (0 warnings, 0 errors).
- Focused run: `dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj -c Debug --no-build --no-restore` -> exit `1`.
- Focused failures: exactly 12 assertions, all natural baseline defects:
  1. canonical client supplied a default legacy metadata bridge;
  2. canonical validation lacked payload context;
  3. fake client fabricated canonical metadata;
  4. Rejected `LatestAdvanced` could not preserve null;
  5. finalization could fall back to a local clock;
  6. provider snapshot did not expose the complete exact tuple;
  7. provider fake used a generic recheck boolean;
  8. race winner fake synthesized timestamps/raw/event values;
  9. 0007 lacked strict terminal shape/persisted-ID invariants;
  10. T134 lacked an actual repository round-trip;
  11. T149/T145 evidence did not prove exact field equality;
  12. T150 review checks used unconditional `Check(true)` and stale evidence.
- The temporary worktree was removed after capture. No restore/download, Docker, PostgreSQL
  connection, port `5432` contact, secret output, or source-code sabotage occurred.

## Historical pre-correction record (retained)

The prior checkpoint was recorded against parent `fdc56735dbd6c9c44599fdf498b010bab151f11e` at
`2026-07-28T07:11:03.6118212Z`. It covered the original T131-T135 RED surface, built with
`dotnet build IUMP.slnx -c Debug --no-restore` (exit `0`) and ran the focused unit executable
(exit `1`). Its missing seams were identity/registry, canonical orchestration, atomic
persistence, Acquisition finalization, and safe event creation. It explicitly recorded no
production implementation, restore/download, database or migration execution, container use,
secret output, or contact with port `5432`. This historical record is not reclassified by the
post-hoc exact-result closure above.
