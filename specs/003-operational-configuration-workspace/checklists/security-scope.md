# Feature 003 Phase 6 — security and scope audit (T075)

Date: 2026-08-03
Baseline: `f93c2da8bcd71c0436c38d502ddd7a770c35e621` (historical Phase 6)
Corrective baseline: `045f3981f3ba6bb87425009ee8f8cf0e6cf4e56a`
Corrective branch: `fix/003-final-governance-corrective`
Database target for runnable evidence: PostgreSQL `127.0.0.1:5433/iump_dev` only.

## Control matrix

| Control | Evidence / inspection | Result |
|---|---|---|
| Secrets, passwords, hashes, tokens, credentials, connection strings, API keys | Repository policy and observability checks; source/evidence review; `.env` values are loaded only by the approved local configuration path and are never printed or persisted in artifacts | PASS |
| PostgreSQL port 5432 | Feature source, harness configuration, integration target, and phase evidence inspected; all current DB evidence names port 5433; no command in this run targets 5432 | PASS |
| Docker, containers, public downloads, package installation | `AGENTS.md`, repository harness, policy checks, and task gate prohibit these; no install/download/container command was run | PASS for repository scope; Full non-containerized target-host/service check is `BLK-ENV-005` and remains BLOCKED_BY_COMPANY_APPROVAL |
| SQLite, InMemory, Testcontainers, substitute/fake/demo fallback | Search and policy checks cover provider setup and fallback wording; production paths use PostgreSQL and dependency errors render no local/demo data | PASS |
| Global counts and cross-scope leaks | Organization/Catalog/Acquisition/Telemetry/Audit adapters apply principal and scope predicates before count, sort, search, and paging; T058/T066 prove zero out-of-scope rows/counts | PASS |
| Client-authoritative role/scope claims | Server resolves principal and role; Web URL/selection values are requests only; mutation/activation rechecks scope and versions | PASS |
| Server redaction and fail-closed errors | Audit recursively redacts sensitive keys; unauthorized/mismatch responses are safe 401/403/404; dependency/runtime errors do not fabricate data | PASS |
| Read-only Dashboard/Audit | Dashboard and Audit use query ports/GET-style reads; no read-side mutation or Simulator auto-start is present; phase read snapshots show zero side effects | PASS |
| Auto-start, savings, AI, control/writeback, Modbus | Feature scope and static source review found no energy-savings claims/calculation, AI recommendation, equipment control/writeback, Modbus, or automatic Run start; Dashboard is operational-only | PASS |
| Audit/credential emission | Audit before/after values are server-redacted; correlation ID is Administrator-only; no secrets are sent to Web or written to evidence | PASS |

## Static review commands

The following searches are read-only and are recorded for audit reproducibility. Matches in the
authoritative rules, historical evidence, and this checklist are expected policy vocabulary; a
match is not itself a vulnerability and was reviewed in context.

```powershell
rg -n --glob '!specs/003-operational-configuration-workspace/checklists/*' 'Password=|IUMP_DB_PASSWORD|ConnectionStrings__|api[_-]?key|secret|token|credential|5432|Docker|docker|SQLite|InMemory|Testcontainers|Modbus|savings|energy savings|AI|writeback' src tests scripts .github
```

The repository policy, architecture, and observability verification scripts are the authoritative
automated gates; their fresh exit codes are recorded in `final-verification.md`. No password or
secret value is included in command output, Markdown, JSON, screenshots, or Git.

## Scope conclusion

Feature 003 remains limited to server-authorized operational configuration, Simulator operation,
Latest/Health observation, Dashboard navigation, and Audit review. The audit found no Critical or
High security/scope defect and no authorization to open Spec 004, Rule/Alert/CSV/Reporting, savings,
AI, control, or any other post-T080 capability.
