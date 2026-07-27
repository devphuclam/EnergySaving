# Phase 4 RED evidence

Captured: 2026-07-27T13:49:14.7349946+07:00 (Asia/Bangkok)

The Phase 4 focused executable was built successfully before the RED run, so
the non-zero result is behavioral rather than a syntax, project, package, or
restore failure.

Commands and results:

```text
dotnet build tests/Unit/IUMP.Tests.Unit.csproj --no-restore --configuration Debug
exit 0
Build succeeded. 0 Warning(s) 0 Error(s)

dotnet run --project tests/Unit/IUMP.Tests.Unit.csproj --no-build --configuration Debug
exit 1
T071: tests=19; assertions=39; failures=0
FAILURES:
  T079: Administrator can create globally and source identity is resolved server-side.
```

The failing assertion represented the missing accepted configuration-create
behavior and its owner event. No package or project failure caused RED. The
implementation was then restored and completed; the final GREEN run is
recorded in `phase-04-configuration.md`.
