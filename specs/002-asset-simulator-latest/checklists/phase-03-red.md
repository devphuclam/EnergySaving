# Phase 3 RED evidence (post-hoc reproduced)

This is the required post-hoc RED reproduction for T061. It was run against the
accepted Phase 2 checkpoint before applying any Phase 3 production implementation.

| Field | Evidence |
|---|---|
| Baseline SHA | `fd2cf0d858fc8fce0041e1343b64d966d33d5d46` |
| Temporary native worktree | `C:\Users\TD-999\AppData\Local\Temp\iump-phase3-red-cadeaa6` |
| Date/time | `2026-07-27 11:47:04 +07:00` |
| Corrected test-only files | `tests/Unit/Phase3RedEvidenceTests.cs`, `tests/Unit/Program.cs` (temporary worktree only) |
| Production changes | None in the temporary worktree |
| Restore/download | Not used (`--no-restore`) |
| Database/Docker | No database connection, migration, PostgreSQL command, or Docker use |
| Secret handling | No secret was printed, copied, or serialized |

## Exact commands and exit codes

```powershell
dotnet build .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore -c Debug
# exit 0
dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-build -c Debug
# exit 1
```

Build output was `Build succeeded. 0 Warning(s) 0 Error(s)`. The focused run failed
as required with these corrected RED assertions:

```text
T066 RED: DecommissionPolicy.cs is absent at the Phase 2 baseline.
T067 RED: Organization command handler is absent at the Phase 2 baseline.
T068 RED: Organization query service is absent at the Phase 2 baseline.
T056-T060 RED: corrected Phase 3 Organization test surface is absent.
```

The temporary worktree was removed after capture. This evidence is post-hoc and is
not represented as a chronological claim that RED was captured before the earlier
implementation attempt.
