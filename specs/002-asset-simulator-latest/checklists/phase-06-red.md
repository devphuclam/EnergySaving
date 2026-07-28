# Post-hoc reproduced Phase 6 invariant RED

Repository: `devphuclam/EnergySaving`

Baseline SHA: `89b32b7595f03fc90145f993a8ad77a61343433d`

Temporary native worktree:
`C:\Users\TD-999\Research\EnergySaving\Codespace-phase6-corrective-red`

## Corrected test/static-only delta

- `tests/Verification/phase06-corrective-red.tests.ps1`

No production source was changed or sabotaged in the worktree. The ignored local `obj` cache was
copied from the primary workspace solely so the detached worktree could build with `--no-restore`;
no restore, download, install, database access, migration execution, container, secret read, or
port `5432` contact occurred.

## Exact commands and exits

```text
dotnet build IUMP.slnx --no-restore
Exit code: 0
Build succeeded; 0 warnings; 0 errors.

powershell -NoProfile -File .\tests\Verification\phase06-corrective-red.tests.ps1
Exit code: 1
```

## Exact failed assertions

1. `algorithm_version=2` was not rejected as `CONFIGURATION_INVALID` before generator
   initialization.
2. An unknown `SimulatorScenario` was not rejected before Run creation.
3. Empty Configuration/Point/Mapping/Metric/Unit identities were not completely rejected.
4. Duplicate Point or Mapping input was not rejected before partial Run state.
5. The uniqueness-race winner committed only Pending, not PRNG/cursor/Generated state.
6. Reservation accepted a complete replaceable Run-Point record with mutable pinned fields.
7. Accepted outcome with Rejected classification was not rejected.
8. Rejected outcome with Accepted classification was not rejected.
9. Invalid Rejected terminal metadata was not rejected.
10. Migration `0007` lacked pinned immutability and terminal-pair constraints.
11. T124 incorrectly expected the race winner's global Generated/cursor state to remain zero.
12. T108-T113 contained manually assigned scenario/assertion counters.

Classification: natural business-invariant failure at the supplied baseline. The focused test made
no production change and manufactured no failure.
