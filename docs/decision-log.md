# Decision Log

| ID | Date | Decision/status | Basis |
|---|---|---|---|
| DEC-R0-001 | 2026-07-23 | Execute only R0 Engineering Foundation; stop before R1/VS-01. | User request; DOC-07 R0 |
| DEC-R0-002 | 2026-07-23 | Use Spec Kit artifacts as the canonical feature lifecycle and Matt Pocock skills as engineering methods. | User request; `AGENTS.md` |
| DEC-R0-003 | 2026-07-23 | Use database execution Mode C and classify database checks as `DATABASE_EXECUTION_BLOCKED`. | `psql` missing; no approved endpoint/credentials |
| DEC-R0-004 | 2026-07-23 | Do not run package restore/install while only public registries are configured. | Company dependency policy |
| DEC-R0-005 | 2026-07-23 | Current workstation execution is non-containerized; DOC-05 container target remains deferred for infrastructure review. | Company policy; ADR-010 |
| DEC-R0-006 | 2026-07-23 | Do not create a public-action CI pipeline; provide local equivalent scripts and runner requirements. | Company policy; ADR-016 |
| DEC-R0-007 | 2026-07-23 | Do not substitute SQLite/InMemory for PostgreSQL and do not report unexecuted checks as passing. | Constitution; user request |
| DEC-R0-008 | 2026-07-23 | Treat `CONTEXT.md` as a glossary only; technical choices remain in ADRs and Spec Kit plans. | Matt Pocock domain-modeling |
