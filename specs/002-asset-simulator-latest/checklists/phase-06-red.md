# Post-hoc reproduced Phase 6 scope-and-isolation RED

Repository: `devphuclam/EnergySaving`

Baseline: `651c22c9db04f9cb01091f9873558f5be50530a8`

Temporary native worktree:
`C:\Users\TD-999\Research\EnergySaving\Codespace-phase6-scope-isolation-red`

## Corrected test/static/contract-only files

- `specs/002-asset-simulator-latest/contracts/simulator.md`
- `tests/Verification/phase06-scope-isolation-red.tests.ps1`

The contract edit reconciled only the owner-state contradiction. No production source was changed
or sabotaged. Ignored local `obj` caches were copied from the primary workspace solely to support
the detached `--no-restore` build.

## Exact commands and exits

```text
dotnet build IUMP.slnx --no-restore
Exit code: 0
Build succeeded; 0 warnings; 0 errors.

powershell -NoProfile -File .\tests\Verification\phase06-scope-isolation-red.tests.ps1
Exit code: 1
```

## Exact failed checks

1. Point A owner failure took the global Stop path and prevented independent Point B production.
2. Existing Run authorization was derived from current Mapping Sites instead of pinned Run-Point
   Sites.
3. Existing Running Start unnecessarily required current snapshot resolution and validation.
4. A mismatched snapshot Source was not rejected and could create a Run for the wrong Source.
5. Required T110 prerequisite/pinned-scope and T111 multi-Point owner-isolation cases were absent.
6. T129 missed the Simulator spec/contract contradiction and pinned-scope finding.

No restore/download/install, database connection/mutation, migration execution, container, secret
read/output, or port `5432` contact occurred.
