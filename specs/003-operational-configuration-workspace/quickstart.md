# Quickstart: Validate Feature 003 Operational Setup

This guide proves the Feature 003 Phase 1 vertical slice without implementing or claiming later
phases.

## Preconditions

- Repository branch: `codex/003-operational-configuration-workspace`.
- Approved tools and locked dependencies are already installed/cached.
- `.env` remains ignored and is loaded through the repository’s supported mechanism.
- PostgreSQL target resolves to `127.0.0.1:5433/iump_dev`.
- Port 5432 is never contacted.
- No Docker, install, restore, or public download command is used.

## Repository checks

```powershell
& .\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks
& .\scripts\harness.ps1 -Mode Fast -Feature 003-operational-configuration-workspace
```

Expected during implementation: Fast reports exact PASS/FAIL/BLOCKED evidence. A fresh Full run is
required after the final Phase 1 change:

```powershell
& .\scripts\harness.ps1 -Mode Full -Feature 003-operational-configuration-workspace
```

Full may remain `BLOCKED_BY_COMPANY_APPROVAL`; this is not a pass or release-ready claim.

## Build and test commands

Use existing no-restore/no-install surfaces only:

```powershell
& dotnet build .\IUMP.slnx --no-restore
& dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore
& dotnet run --project .\tests\Integration\IUMP.Tests.Integration.csproj --no-restore

Push-Location .\src\Web
try {
    & npm run lint
    & npm run build
}
finally {
    Pop-Location
}
```

Record numeric exit codes. Frontend compile/lint/build do not substitute for a blocked browser
behavior runner.

## Phase 1 acceptance journey

1. Verify database target before mutation: host `127.0.0.1`, port `5433`, database `iump_dev`.
2. Start API, Worker, and Web with existing repository scripts.
3. Sign in as Administrator.
4. Confirm server-derived landing opens Setup Wizard when no usable chain exists.
5. Create Site with an idempotency key; retry the same request and verify one Site/outcome.
6. Activate Site with current version.
7. List existing eligible Engineers and assign one to Site scope through the UI.
8. Log out and sign in as the assigned Engineer.
9. Confirm Site is read-only and no root Site creation action exists.
10. Create Area, Asset, Draft Measurement Point, Simulator Data Source, Source Mapping, and Simulator
    Configuration through the wizard.
11. Refresh the browser, restart Web/API, and sign in again; confirm completed and next steps
    reconstruct from PostgreSQL.
12. Run complete-chain validation and inspect step-local failures.
13. Correct failures and activate in legal order: Area, Asset, Source, Mapping, Point. Site was
    activated by Administrator; Simulator Configuration has no invented activation operation.
14. On any partial failure, confirm committed states remain correct, failed step is shown, and retry
    creates no duplicate entity or event.
15. Confirm completion navigates to Simulator.
16. Confirm no Source/configuration was selected implicitly and no Simulator Run was created.

## Negative scenarios

- Engineer without scope sees No Authorized Scope/Administrator setup required and no global counts.
- Engineer root Site create is rejected server-side even if a request is handcrafted.
- Out-of-scope chain status/validation returns safe 403/404 with no metadata.
- Stale `If-Match` returns conflict and does not overwrite.
- Same idempotency key with changed canonical fields returns `IDEMPOTENCY_CONFLICT`.
- API/database failure shows dependency error and no local fallback data.
- Evidence and logs contain no password, token, credential, connection secret, or port 5432.

## Phase 1 checkpoint

Create `checklists/phase-01-checkpoint.md` containing:

- completed Phase 1 task IDs;
- red and green evidence commands/exit codes;
- PostgreSQL journey results;
- standards/spec review findings and resolutions;
- frontend behavior-runner blocker;
- Fast/Full results and all blocker classifications;
- `Simulator auto-started: NO`;
- explicit stop before Phase 2.
