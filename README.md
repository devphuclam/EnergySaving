# IDEA Utility Monitoring Platform

R0 established the engineering foundation. R1/VS-01 is the current delivery stage, represented by
the active Spec Kit feature under `specs/`. Start with the
[repository harness](docs/repository-harness.md), [CONTEXT.md](CONTEXT.md), and
[source register](docs/source-register.md).

## Restricted workstation rules

- Do not use or create containers, public marketplace actions, or downloaded tools.
- Do not run package install/restore against public sources.
- Do not place real credentials in source or command output.
- Do not substitute another database for PostgreSQL.
- Work only inside the included scope of the active Spec Kit feature.

## Approved local flow

Use the repository harness while iterating and before completion:

```powershell
& .\scripts\harness.ps1 -Mode Fast
& .\scripts\harness.ps1 -Mode Full
```

The backend foundation projects have zero PackageReference entries. Their assets were generated
using the repository `NuGet.Config`, which clears all package sources, so no package registry was
contacted:

```powershell
dotnet nuget list source --configfile .\NuGet.Config
dotnet restore .\IUMP.slnx --configfile .\NuGet.Config --no-cache --force-evaluate
& .\scripts\build.ps1
& .\scripts\test.ps1
```

`scripts/verify.ps1` remains available as a compatibility wrapper for Full harness mode.

The existing `src/Web/node_modules` tree may be used directly without install:

```powershell
Set-Location .\src\Web
npm run lint
npm run build
```

Do not run `npm ci --offline` until lockfile cache completeness and company approval are recorded.

## VERIFIED

- DOC-01 through DOC-07 read in full; source hierarchy, glossary, decisions, ADRs, and Spec Kit
  artifacts exist.
- Git 2.54.0, .NET SDK 10.0.300, Node 24.16.0, npm 11.13.0, Spec Kit 0.13.2.
- No-source backend restore and Release build: 17 projects, 0 warnings, 0 errors.
- PowerShell verification contracts: result classification, repository policy, permanent product
  invariants, and module boundaries PASS.
- Frontend lint and production build PASS using the existing installed dependency tree.
- API smoke: `/health/live` 200; supplied correlation ID echoed; `/health/ready` 503 while database
  is unavailable.
- Worker starts and emits structured JSON with `BLOCKED_BY_DATABASE_ACCESS`, then shuts down cleanly.
- No container/public-CI artifact or hard-coded database credential.

## NOT VERIFIED

- PostgreSQL migrations and seed idempotency.
- PostgreSQL health, outbox/inbox duplicate behavior, backup/restore, and N-1 migration.
- Offline completeness for a clean frontend install.
- Target containerized on-premise deployment from DOC-05.

## BLOCKED BY ENVIRONMENT

- `psql` is missing and no approved PostgreSQL endpoint/credential was supplied.
- Integration tests and database-backed Worker readiness therefore remain blocked.

## REQUIRES COMPANY APPROVAL

- Internal NuGet/npm mirrors for future dependencies.
- Company CI runner and controlled templates/actions.
- Approved local/internal PostgreSQL and least-privilege service profile.
- Infrastructure/Security decision for TEST/UAT/PROD deployment topology.

Full harness mode exits non-zero while mandatory failures or blockers remain. That is expected and
prevents a false completion claim.
