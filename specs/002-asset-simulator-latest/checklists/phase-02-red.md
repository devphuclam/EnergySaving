# Phase 2 RED evidence — post-hoc reproduced

This record is intentionally labelled **Post-hoc reproduced RED evidence**. It is not presented
as the original chronological RED run.

## Reproduction

- Repository: `devphuclam/EnergySaving`
- Baseline worktree commit: `908bddbc1eb68cf8fcdbb095a561e2323bb4e6eb`
- Temporary native Git worktree: `C:\Users\TD-999\AppData\Local\Temp\iump-phase2-red-686815e2199c4fd582583ce422eab383`; created at that commit and removed after capture
- Test-only change: corrected Catalog command tests exposing the remaining B–E behavior; no
  production correction was applied in the baseline worktree
- Database: not required, not connected, and not mutated
- Restore/download: not run

Commands and actual results:

```text
dotnet build C:\Users\TD-999\AppData\Local\Temp\iump-phase2-red-686815e2199c4fd582583ce422eab383\tests\Unit\IUMP.Tests.Unit.csproj --no-restore -c Debug
Exit: 0
0 warnings, 0 errors

dotnet run --project C:\Users\TD-999\AppData\Local\Temp\iump-phase2-red-686815e2199c4fd582583ce422eab383\tests\Unit\IUMP.Tests.Unit.csproj --no-restore -c Debug --no-build
Exit: 1
```

The focused executable reported these exact failures:

1. `CreateMapping must query trusted Point readiness before authorization`
2. `Mapping events must preserve producingReady readiness fact`
3. `Owner-event payloads must use explicit allowlists, not reflection`

These failures are the reproduced RED evidence for the narrow implementation-path corrections.
The current worktree contains the corresponding production fixes and its focused executable is
recorded separately as GREEN evidence in `phase-02-catalog.md`.
