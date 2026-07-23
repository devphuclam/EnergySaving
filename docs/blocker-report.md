# R0 Blocker Report

## BLK-R0-001 — Approved dependency source unavailable

- Evidence: `dotnet nuget list source`, NuGet configuration inspection, `npm config get registry`,
  lockfile/cache inventory.
- Impact: restore, reproducible backend build/test, certified offline frontend install.
- Completed safely: tool/source/cache inventory and dependency manifest inspection.
- Required: company-approved internal/local NuGet and npm sources containing locked versions.
- Lowest-risk path: IT provides read-only source endpoints and trust configuration; then run restore
  in locked/offline mode with network access constrained to approved endpoints.

## BLK-R0-002 — PostgreSQL execution unavailable

- Evidence: `where.exe psql`, `Get-Command psql`, and `psql --version` found no client; no internal
  endpoint or credential was supplied.
- Impact: migration execution, seed idempotency, database health, outbox/inbox integration tests.
- Completed safely: schema/contract planning and access request.
- Required: approved local PostgreSQL or internal development database per
  `docs/database-access-request.md`.
- Lowest-risk path: company-provisioned least-privilege development database and redacted secret
  delivery; no local installation by the project.

## BLK-R0-003 — Approved CI runner/template unavailable

- Evidence: existing workflow depends on public GitHub actions and a PostgreSQL container.
- Impact: hosted build/test/migration evidence and immutable release artifacts.
- Completed safely: local-equivalent design and runner requirements.
- Required: approved internal runner, templates/actions, tools, package mirrors, and database.
- Lowest-risk path: execute `scripts/verify.ps1` locally now; port identical checks to the supplied
  company template later.

## BLK-R0-004 — Container verification prohibited

- Evidence: explicit company policy in the task; prior Dockerfiles/Compose are prohibited artifacts.
- Impact: DOC-05 reference deployment validation and image promotion.
- Completed safely: non-container workstation decision and target-deployment deferment.
- Required: separate Infrastructure/Security decision for TEST/UAT/PROD if containers remain the
  target architecture.
- Lowest-risk path: use approved executables/services locally and review the deployment topology in
  a controlled infrastructure environment.
