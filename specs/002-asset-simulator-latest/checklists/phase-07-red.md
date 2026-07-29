# Phase 7 Atomic-Evidence RED Evidence

- Parent baseline: `f8521159802fd39732c4cfa24605aed912c18419`
- Corrective checkpoint: T151 atomic-evidence closure (T152+ not in scope).
- Reproduction: temporary native worktree
  `C:\Users\TD-999\AppData\Local\Temp\opencode\phase7-atomic-red`
- Scope: post-hoc Phase 7 atomic-evidence assertions only; no production implementation, database,
  migration execution, API/Worker composition root, PostgreSQL adapter, or Phase 8 work.
- Temporary changes: one test-only `Phase7AtomicEvidenceRedTests.cs` and one test-runner line.
- Build: `dotnet build IUMP.slnx -c Debug --no-restore` -> exit `0` (0 warnings, 0 errors).
- Focused run: `dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj -c Debug --no-build --no-restore` -> exit `1`.
- Focused failures: exactly 8 assertions, all natural baseline defects:
  1. **RED-1**: T145 cannot prove exact Latest via GetCommittedLatestAsync.
  2. **RED-2**: No invalid Accepted fixture with zero-publication proof in T145.
  3. **RED-3**: No invalid Rejected fixture in T145.
  4. **RED-4**: No TelemetryCommittedState aggregate; four separate committed-field assignments.
  5. **RED-5**: No winner conflict/slot conflict detection; existing committed winner can be overwritten.
  6. **RED-6**: Scope mismatch throws exception instead of returning stable result.
  7. **RED-7**: Event factory permits unverified fallback scope (eventSiteId ?? provider.SiteId).
  8. **RED-8**: T133 missing invalid Accepted/Rejected fixture orchestration tests.
- The temporary worktree was removed after capture. No restore/download, Docker, PostgreSQL
  connection, port `5432` contact, secret output, or source-code sabotage occurred.

## Historical pre-correction records

Prior checkpoints were recorded at `d5c71ed42a45c6fee189c3a67580b0cf096c9bf6` (atomic-race and
compatibility-lock closure) and `b6b2510820f5ab8f0af5569a2fc18b4ee4b2f892` (exact-result closure).
Both historical records are retained and not reclassified by this post-hoc atomic-evidence RED.
