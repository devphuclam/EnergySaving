# Source Register

| Priority | Source | Version/status | Repository use |
|---:|---|---|---|
| 1 | DOC-01 Product Vision and Scope | v0.3, In Review | Product boundary, roles, MVP/POC, retention, read-only OT |
| 2 | DOC-03 Business Requirements | v0.1, Draft | Workflow, Missing semantics, rule/Alert/report responsibilities |
| 3 | DOC-04 Software Requirements Specification | v0.2, Draft | Functional/non-functional requirements and acceptance |
| 4 | DOC-05 Software Architecture Document | v0.1, Draft | Modular monolith, API/Worker, PostgreSQL, outbox/inbox, target deployment |
| 5 | DOC-06 Data and Integration Specification | v0.1, Draft | Schema ownership, contracts, idempotency, migrations, retention |
| 6 | DOC-07 MVP Roadmap and Delivery Plan | v0.1, Draft | R0 boundary, DoR/DoD, gates, dependencies and evidence |
| Supporting | DOC-02 Feasibility Assessment | v0.1, Draft | GO/conditional gates, feasibility assumptions and ownership gaps |
| 7 | Repository ADRs | Current | Implementation decisions; cannot override DOC-01..DOC-07 |
| 8 | Active feature under `specs/` | Current | Canonical delivery specification, plan, tasks, and evidence |
| Historical | `specs/001-r0-engineering-foundation/` | Complete | R0 foundation artifacts and historical evidence |
| 9 | `CONTEXT.md` | Current | Ubiquitous language only |
| 10 | Source and automated tests | Current | Executable evidence, subordinate to documented decisions |

All seven business documents were read in full from `Business Docs/`. The `.docx` structures were
parsed and their corresponding UTF-8 text exports were used for semantic review. Source documents
were not edited.
