# R0 Architecture Boundaries

- IUMP is one product/release boundary with separate API and Worker processes.
- Hosts are composition roots; R0 modules expose contracts only and own their canonical schema names.
- `BuildingBlocks` contains correlation and stable technical primitives only; it has no persistence or
  business dependencies.
- Operations owns job persistence contracts; Integration owns outbox/inbox persistence contracts.
  PostgreSQL is the only accepted adapter; execution remains blocked until approved access exists.
- No R1 business workflow is registered. Web is an engineering-readiness shell.
- `tests/Verification/architecture.tests.ps1` checks authored project references and public module
  source without requiring third-party test packages.
