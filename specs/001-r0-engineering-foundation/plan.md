# Implementation Plan: R0 Engineering Foundation

**Branch**: `001-r0-engineering-foundation` | **Date**: 2026-07-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-r0-engineering-foundation/spec.md`

## Summary

Constrain the repository to a truthful R0 foundation: canonical documents and decisions, separate
API/Worker/Web skeletons, explicit module ownership, PostgreSQL migration structure, local
non-installing PowerShell entry points, and observable health/correlation/job/outbox foundations.
Only seams executable with approved installed dependencies may be implemented/tested; package,
database, CI, and container-dependent evidence remains explicitly blocked.

## Technical Context

**Language/Version**: C# on installed .NET SDK 10.0.300; TypeScript on Node.js 24.16.0

**Primary Dependencies**: ASP.NET Core; React/TypeScript; EF Core/Npgsql; Serilog. Versions remain
locked in manifests and may be used only from approved local/internal sources.

**Storage**: PostgreSQL with module-owned schemas; local filesystem behind a file-storage seam.
PostgreSQL execution is Mode C / blocked in this workstation session.

**Testing**: xUnit, NetArchTest, frontend lint/typecheck/build where the installed dependency tree
supports it; real PostgreSQL integration tests only after approved access.

**Target Platform**: Target architecture is on-premise per DOC-05. Current workstation execution is
Windows, non-containerized, non-administrator. Container target verification is deferred.

**Project Type**: Internal web application with Web, HTTP host, background host, modules, tests,
database artifacts, scripts, and documentation in one repository.

**Performance Goals**: R0 has no business throughput claim. Verification must be deterministic and
local; host startup/health targets are evidence-gated after dependencies and database are available.

**Constraints**: No network download, public package source, tool installation/upgrade, container,
administrator rights, security-control change, real credential, database substitute, fabricated
test result, R1/VS-01, Modbus, production Edge, or AI.

**Scale/Scope**: One canonical R0 feature; separate API and Worker hosts; minimal technical kernel;
module contracts/skeletons only; no complete business capability.

## Constitution Check

### Pre-design gate

- **PASS — Product boundary**: R0 only; later/conditional scope is explicitly excluded.
- **PASS — Traceability**: source register and FR/SC identifiers are defined.
- **PASS — Module ownership**: plan uses module contracts and host composition roots; no cross-module
  business write is authorized.
- **PASS — Test-first evidence**: executable seams require red-green-refactor; blocked checks remain
  blocked tasks.
- **PASS — Restricted execution**: sources/caches/tools/database/CI/container states are inventoried.
- **PASS — Operability**: logging, correlation, health, jobs/outbox/inbox and scripts are in scope.

### Post-design gate

PASS with environment blockers. Design artifacts do not require a forbidden substitution. The
implementation phase may proceed only for local documentation/source/script seams whose prerequisites
are already approved. Restore, backend tests requiring assets, PostgreSQL checks, hosted CI, and
container verification are prohibited or blocked.

## Project Structure

### Documentation (this feature)

```text
specs/001-r0-engineering-foundation/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── module-boundaries.md
│   └── verification-result.schema.json
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
src/
├── Api/                         # HTTP composition root and health/correlation foundation
├── Worker/                      # Background composition root and lifecycle/health foundation
├── Web/                         # Frontend shell only; no R1 business pages
├── BuildingBlocks/              # Stable technical primitives; no business model
└── Modules/                     # Module contracts/skeletons and owned persistence structure

tests/
├── Unit/                        # Executable pure/config/correlation behavior
├── Architecture/                # Project/module dependency and prohibited-surface rules
└── Integration/                 # PostgreSQL-only; blocked until approved database exists

database/
├── migrations/                  # Ordered PostgreSQL migration source and validation metadata
└── seeds/                       # Synthetic idempotent R0 seeds; execution database-gated

scripts/                         # Non-installing PowerShell build/test/start/db/verify entry points
docs/                            # ADR, architecture, contracts, runbooks, inventory and blockers
```

**Structure Decision**: Keep the established host/module names to avoid a cosmetic repository move.
The external interfaces are host entry points and local scripts. Module implementations remain
behind contracts; persistence, clock, file, and host integrations are adapters only where a real
production/test variation exists. Business CRUD and workflow models are removed or deferred.

## Design Decisions

1. **Verification orchestrator is a deep module**: one script owns prerequisite detection, result
   classification, redaction, ordering, and exit aggregation; callers learn one interface.
2. **Hosts are composition roots**: API and Worker may reference module public contracts and concrete
   adapters; modules do not reference hosts or other modules' implementations.
3. **Database seam is not faked**: migration and outbox/inbox storage contracts exist in R0, but the
   PostgreSQL adapter is verified only against an approved PostgreSQL instance.
4. **Frontend is a shell**: typecheck/lint/build can be attempted against the installed tree; no
   business dashboard or fake API behavior is accepted.
5. **Environment blockers are data**: inventory and verification result classifications form an
   auditable contract rather than prose-only optimism.

## Complexity Tracking

No constitution violation is justified. Separate API and Worker hosts and module schemas are direct
DOC-05 decisions, not discretionary complexity. Local scripts replace no runtime architecture; they
only provide a restricted-workstation execution interface.
