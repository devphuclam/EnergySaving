# Phase 1 Red Evidence

**Command**:
`dotnet run --project .\tests\Unit\IUMP.Tests.Unit.csproj --no-restore`

**Exit code**: 1

**Expected failures**:

- `IWorkspaceSiteExistence` missing at the IAM assignment seam.
- `WorkspaceLanding` and operational status contract missing at the workspace query seam.

The failures are compile-time red evidence produced by the new public behavior tests before green
implementation. No secret or database connection value was emitted.
