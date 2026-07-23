# R0 Restricted-Workstation Validation Guide

## Preconditions

1. Read `docs/environment-inventory.md` and `docs/blocker-report.md`.
2. Do not run restore/install. Do not supply production credentials.
3. Confirm the current terminal is non-administrative and the working directory is repository root.

## Safe validation flow

```powershell
git status --short
& .\scripts\verify.ps1
```

Expected: the script runs only installed-tool checks and emits a classification per check. With the
inventory captured on 2026-07-23, dependency restore, PostgreSQL execution, hosted CI, and container
verification are expected to remain blocked, not passed.

Individual entry points:

```powershell
& .\scripts\build.ps1
& .\scripts\test.ps1
& .\scripts\db-migrate.ps1
& .\scripts\db-seed.ps1
& .\scripts\start-api.ps1
& .\scripts\start-worker.ps1
& .\scripts\start-web.ps1
```

Each entry point detects prerequisites and must avoid secret output. Start scripts require explicit
environment configuration; database scripts must return `BLOCKED_BY_DATABASE_ACCESS` until approved
PostgreSQL details exist.

## Success interpretation

- PASS means the documented command actually completed successfully.
- FAIL means it ran and contradicted the expected behavior.
- NOT_RUN means it was intentionally omitted and is not evidence.
- BLOCKED means a prerequisite or approval prevented execution and the blocker is cited.

The overall R0 status cannot be `FULLY_COMPLETE` while mandatory checks are blocked. Stop after R0;
do not begin R1/VS-01.
