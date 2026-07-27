# Phase 3 RED evidence (post-hoc business reproduction)

This evidence was reproduced after the implementation attempt, against the
accepted Phase 2 parent baseline. The temporary worktree contains only
corrected business-behavior tests and a test-only compile shim; no Phase 3
production fix was applied there.

| Field | Evidence |
|---|---|
| Parent baseline SHA | `fd2cf0d858fc8fce0041e1343b64d966d33d5d46` |
| Temporary native worktree | `C:\Users\TD-999\AppData\Local\Temp\iump-phase3-business-red-final` |
| Captured at | `2026-07-27 12:40:06 +07:00` |
| Test-only files | `tests/Unit/Phase3BusinessRedEvidenceTests.cs`, `tests/Unit/Program.cs` |
| Production files changed in RED worktree | **None** |
| Test-only compile shim | `Phase2OrganizationBehavior` in `Phase3BusinessRedEvidenceTests.cs` |
| Restore/download | Not used (`--no-restore`) |
| Database/migration/Docker | Not used; no PostgreSQL command and no migration execution |
| Secret handling | No secret was printed, copied, serialized, or recorded |
| Cleanup | Temporary worktree removed after evidence capture |

## Exact commands and exits

```powershell
dotnet build .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore -c Debug
# exit 0; Build succeeded; 0 Warning(s), 0 Error(s)

dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-build -c Debug
# exit 1
```

## Failed business assertions

The focused run failed on actual behavior assertions, not file/type presence:

1. T056: Inactive parent child creation was not rejected with `PARENT_NOT_CONFIGURABLE`.
2. T056: Decommissioned parent child creation was not rejected with `PARENT_NOT_CONFIGURABLE`.
3. T056: stale `ExpectedVersion` did not return `VERSION_CONFLICT` without mutation.
4. T057: a running Simulator dependency did not block Point decommission.
5. T058: the complete five-family Organization event contract was not emitted.
6. T058: Asset event ancestry/owner keys did not preserve trusted `AreaId` and exact keys.
7. T059: Area-scoped Site visibility failed beyond the first 200 Areas.
8. T060: repeated IAM fixture application was not idempotent.

The worktree status at capture was exactly one modified existing test entry
point and one new test-only evidence file. It contained no source, migration,
database, container, package, or credential change. This is explicitly labeled
**Post-hoc reproduced Phase 3 business RED evidence** and is not a chronological
claim that RED was captured before the earlier implementation attempt.

# Final chronological micro-RED

**Captured at**: `2026-07-27` against baseline `8f6ee4dd9471d6d3ed8eb9836b6e0a5644a0a058`

## Commands

```powershell
dotnet build .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore -c Debug
# exit 0; Build succeeded; 0 Warning(s), 0 Error(s)

dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-build -c Debug
# exit 1
```

## Failed assertions

1. Active Point configuration update must return PHASE5_REQUIRED with no event.
2. Active Point configuration update must not mutate state.
3. Decommissioned Point configuration update must return INVALID_STATE with no event.
4. Decommissioned Point configuration update must not mutate state.
5. Accepted inactivation must append exactly one lifecycle history entry.
6. Rejected inactivation must not append additional history.
7. Stale version inactivation must not append history.

All seven failures are caused by absent Organization behavior (point config
state guards and inactivation lifecycle history). T071 contract runner passes
with 19 tests, 39 assertions, 0 failures.
