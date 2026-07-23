# Phase 0 Research: R0 Engineering Foundation

## Installed implementation baseline

- **Decision**: Pin the repository to installed .NET SDK 10.0.300 and Node.js 24.16.0 without
  downloading or upgrading tools.
- **Rationale**: Both are present; the task authorizes only preinstalled SDK/runtime execution.
- **Alternatives considered**: Installing another LTS/Node version (prohibited); changing technology
  family (contradicts DOC-05).

## Dependency resolution

- **Decision**: Do not restore/install until company-approved NuGet/npm sources and complete locked
  contents are verified. Direct use of the existing frontend dependency tree may be tested without
  invoking install.
- **Rationale**: Configured sources are public. NuGet cache inspection found two referenced preview
  packages absent in a duplicate project; npm lock/tree inspection also cannot certify an offline
  install.
- **Alternatives considered**: Public restore, `--ignore-failed-sources`, version changes, lockfile
  regeneration, or silent online fallback (all prohibited).

## Database execution

- **Decision**: Use Mode C and mark PostgreSQL-dependent work `BLOCKED_BY_DATABASE_ACCESS`.
- **Rationale**: `psql` is missing and no approved endpoint or credential was supplied.
- **Alternatives considered**: Installing PostgreSQL, network discovery, SQLite, EF InMemory, or fake
  migration output (all prohibited).

## Workstation and target deployment

- **Decision**: Run local development only through approved executables/services; keep DOC-05's
  containerized on-premise target as Proposed/Deferred/Needs Infrastructure Review.
- **Rationale**: This separates a workstation policy constraint from the unmodified target
  architecture decision.
- **Alternatives considered**: Docker/Compose/Podman locally (prohibited); rewriting DOC-05
  deployment architecture without stakeholder review (unauthorized).

## CI strategy

- **Decision**: Provide a local equivalent and an internal-runner requirement, not a hosted workflow.
- **Rationale**: The existing pipeline downloads public actions and a container image; no approved
  company runner/template was supplied.
- **Alternatives considered**: Third-party GitHub Actions, tool bootstrap, service containers, or a
  pipeline that reports fake success (prohibited).

## Module and seam design

- **Decision**: Keep separate API and Worker composition roots, module-owned schemas, a minimal
  technical kernel, and small persistence/file/clock/verification interfaces.
- **Rationale**: These decisions maximize locality and testability while preserving the modular
  monolith and avoiding a speculative framework.
- **Alternatives considered**: Microservices/broker (unproven complexity); shared business model
  library or cross-module repositories (erodes ownership); one adapter behind a hypothetical port
  (unnecessary indirection).

## R0 scope handling

- **Decision**: Retain only foundation behavior and remove/flag pre-existing R1 business UI/models,
  container artifacts, weak credential setup, and public CI configuration.
- **Rationale**: The task explicitly stops before R1/VS-01 and requires zero container artifacts and
  real credentials.
- **Alternatives considered**: Leaving later capability active but undocumented (scope violation) or
  treating it as completed without verification (false evidence).
