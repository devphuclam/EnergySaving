# R0 Engineering Foundation — Final Report

Date: 2026-07-23  
Status: **R0_IMPLEMENTED_WITH_ENVIRONMENT_BLOCKERS**  
Scope stop: R0 only; R1/VS-01 was not started.

## 1. Executive summary

The repository now has a truthful R0 engineering foundation: one canonical Spec Kit feature,
project-local Matt Pocock engineering skills, a modular-monolith contract skeleton with separate API
and Worker processes, an R0 Web shell, PostgreSQL-only migration source, offline-safe local commands,
architecture/TDD evidence, blocker and approval requests, and two-axis review. Safe local checks pass.
PostgreSQL execution, hosted CI, clean frontend installation, and target container deployment remain
explicitly blocked and are not reported as passed.

## 2. Sources read

DOC-01 v0.3, DOC-02 v0.1, DOC-03 v0.1, DOC-04 v0.2, DOC-05 v0.1, DOC-06 v0.1,
and DOC-07 v0.1 were read in full. Their source hierarchy is recorded in
`docs/source-register.md`. Business source documents were not edited. DOC-08 was present but was not
used to widen the explicitly requested R0 scope.

## 3. Environment inventory

| Capability | Exact evidence | Classification |
|---|---|---|
| Git | `2.54.0.windows.1` | available |
| .NET SDK | `10.0.300` | available; pinned by `global.json` |
| Node.js | `v24.16.0` | available |
| npm | `11.13.0` | available; public configured registry prohibited for install |
| Spec Kit | `0.13.2` | project-local workflow available |
| PostgreSQL client | `psql` missing | `BLOCKED_BY_MISSING_TOOL` |
| PostgreSQL endpoint | none approved/supplied | `BLOCKED_BY_DATABASE_ACCESS` |
| Hosted CI | no approved runner/template | `BLOCKED_BY_COMPANY_APPROVAL` |

The repository `NuGet.Config` clears every source. The current 17-project R0 graph has zero
`PackageReference` entries and restores from installed framework packs only. No package version was
changed to fit cache. The existing Web `node_modules` tree was used without install; its clean
offline reproducibility is not certified.

## 4. How Spec Kit and Matt skills work together

Spec Kit owns delivery state and traceability:

`constitution → specify → clarify → plan → checklist → tasks → analyze → implement → converge`

Matt skills supply the engineering method inside those stages:

- `domain-modeling` produced the ubiquitous language in `CONTEXT.md` and kept technical choices in
  ADRs.
- `codebase-design` shaped deep verification/host seams and explicit module ownership.
- `tdd` drove red/green checks for result contracts, policy, scope, architecture, and correlation.
- `diagnosing-bugs` was used for the Worker build issue and terminal exit-code investigation.
- `code-review` ran independent Standards and Specification reviews around remediation.

The combination is deliberate: Spec Kit answers “what is required and what remains”; the Matt
skills answer “how to design, test, diagnose, and review it.”

## 5. Spec Kit outcome

The constitution, spec, clarification pass, plan, research, data model, contracts, two requirement
checklists, dependency-ordered tasks, analysis, implementation, and convergence assessment are
present under `.specify/` and `specs/001-r0-engineering-foundation/`. Clarification found no critical
ambiguity requiring a user question. Analysis found no unresolved Critical conflict.

Convergence checked 16 FRs, 6 buildable SCs, 6 acceptance scenarios, 5 plan decisions, and 6
constitution principles. It found no actionable missing, partial, contradictory, or unrequested
implementation gap, so no Convergence phase/task was appended. Environment-gated evidence remains a
blocker by design, not an invitation to substitute infrastructure.

## 6. Domain and architecture decisions

- One deployable product boundary implemented as a modular monolith.
- Separate API and Worker composition roots; Web is an R0 shell only.
- Thirteen module owners and PostgreSQL schemas are canonical in
  `docs/architecture/module-ownership.json`.
- Integration owns outbox/inbox contracts and tables; Operations owns durable jobs.
- BuildingBlocks contains only stable technical primitives and has no package/project dependency.
- No cross-module implementation references, host-to-internal namespace access, or command/write-back
  contracts are permitted.
- PostgreSQL is the only database target; no SQLite/InMemory substitute exists.
- Target container deployment from DOC-05 remains proposed/deferred for controlled infrastructure
  review; this workstation contains no container artifact.

## 7. ADR state

ADRs 001–011 plus ADR-015 and ADR-016 are present. The key reconciled decisions cover modular
monolith, installed technology baseline, separate processes, DB-backed jobs/outbox/inbox,
module/schema ownership, observability, migration source, restricted workstation verification, and
company-runner readiness. ADR-010 is Proposed/Deferred/Needs Infrastructure Review rather than a
false local deployment claim.

## 8. Source and configuration delivered

- `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `NuGet.Config`, and `IUMP.slnx`.
- Package-free .NET projects for API, Worker, BuildingBlocks, 13 module contracts, and unit checks.
- React/TypeScript R0 readiness shell with no business route/page.
- JSON console logging, correlation ID validation/echo, liveness, blocked-readiness semantics, and
  Worker blocked-dependency lifecycle.
- Ordered PostgreSQL source `database/migrations/0001_r0_foundation.sql` for integration outbox/inbox
  and operations jobs.
- Non-installing PowerShell build, test, verify, start, migrate, and seed entry points.
- Verification-result JSON schema and architecture/policy/scope negative fixtures.

## 9. TDD evidence

Recorded red/green cycles include: missing verification interface; hard-coded credential detection;
over-broad scope matching; generated `obj` false positives; deliberate module dependency; invalid
correlation characters/length; aggregate exit handling; and missing Worker hosting reference.
Architecture now proves four negative fixtures: module-to-module, foundation dependency,
host-to-module-internal, and command/write-back contract.

## 10. Fresh verification evidence

| Command | Result |
|---|---|
| `dotnet nuget list source --configfile .\NuGet.Config` | PASS — `No sources found.` |
| `dotnet restore .\IUMP.slnx --configfile .\NuGet.Config --no-cache --force-evaluate` | PASS — 17 projects |
| `scripts/test.ps1` | PASS — 6 suites/checks, exit 0 |
| `scripts/build.ps1` | PASS — 17 projects, 0 warnings, 0 errors |
| `npm run lint` | PASS using existing tree; no install |
| `npm run build` | PASS; Vite production output built |
| API `/health/live` with correlation | PASS — HTTP 200; `r0-final-smoke` echoed |
| API `/health/ready` | PASS — HTTP 503 and `BLOCKED_BY_DATABASE_ACCESS` |
| Worker start/stop | PASS — structured JSON and `BLOCKED_BY_DATABASE_ACCESS`; clean shutdown log |
| `scripts/verify.ps1` in child `powershell.exe` | expected non-zero; child exit 20 |

The desktop terminal wrapper represented a direct `exit 20` invocation as exit 1. A controlled child
PowerShell reproduction returned `CODE=20`, `CHILD_EXIT=20`, and `VERIFY_CHILD_EXIT=20`, proving the
script contract is correct; both are non-zero and prevent false completion.

Aggregate results: 12 PASS (four tools, five static/architecture checks, correlation unit check,
backend build, frontend), one `BLOCKED_BY_MISSING_TOOL` (database), and two
`BLOCKED_BY_COMPANY_APPROVAL` (CI and target deployment). No mandatory blocked check is PASS.

## 11. Database status

Migration source exists but was not executed. `psql` is absent and no approved endpoint, TLS policy,
or least-privilege credential was supplied. Clean migration, seed idempotency, DB readiness,
outbox/inbox duplicate behavior, N-1 upgrade, and backup/restore remain unverified under BLK-R0-002.
The actionable request is `docs/database-access-request.md`.

## 12. CI and deployment status

No public GitHub action, hosted public runner, PostgreSQL container, image, Dockerfile, Compose file,
Podman/Buildah configuration, or `.devcontainer` was created. Company CI is blocked under BLK-R0-003;
target deployment verification is blocked under BLK-R0-004. Requirements are in
`docs/ci-readiness.md`.

## 13. Code review

The repository has no `HEAD`, so the normal fixed-point diff review was impossible. Two independent
reviewers assessed the full initial worktree snapshot instead. Initial Standards review found 1
Critical/5 High; initial Spec review found 4 High/1 Medium. After remediation, both final reviews
reported **0 Critical and 0 High**. Lower hardening observations are retained in
`docs/code-review.md` and do not invalidate current R0 behavior.

## 14. Gate G1

Gate G1 is **NOT PASSED**. Requirements, architecture/data, backlog, and risk ownership are partial;
team capacity, DEV/TEST/CI/migration environment, PO/pilot calendar, accepted DoR/DoD, and named
Ops/Security ownership remain unmet or draft. This R0 foundation does not authorize MVP-1 commitment.

## 15. Required company approvals

- Product Owner/Sponsor baseline and scope acceptance.
- Tech Lead, QA, DevOps/Operations, and Security acceptance of DoR/DoD and environments.
- Database owner provision of approved PostgreSQL, TLS route, and least-privilege profiles.
- CI owner provision of company runner/templates and internal NuGet/npm mirrors.
- Infrastructure/Security decision for TEST/UAT/PROD deployment topology.
- Named pilot reviewers and schedule.

## 16. Deviations and constraints

- Full-snapshot review replaced fixed-point review because Git has no commit/HEAD.
- Database execution used Mode C (blocked); no substitute or fake integration result.
- Existing Web dependencies were used directly; no `npm install`/`npm ci` occurred.
- Repository no-source restore was permitted only after the graph had zero package references.
- Target container architecture was deferred; workstation policy took precedence for execution, not
  as an undocumented architecture rewrite.
- One earlier combined hidden-process smoke command was rejected before execution; foreground session
  smoke supplied the evidence instead.
- Direct terminal non-zero mapping was diagnosed with a child PowerShell process as described above.

## 17. Removed prohibited/out-of-scope material

Removed: `docker-compose.yml`, `src/Api/Dockerfile`, `src/Worker/Dockerfile`, the public/container CI
workflow `.github/workflows/ci.yml`, unsafe database setup SQL/credentials, duplicate BuildingBlocks
project, pre-existing R1 business models/migrations/API wiring/Web pages, empty/fake tests, and Worker
placeholder behavior. These items were untracked and the repository has no commit, so Git cannot
recover them. Source DOC-01..DOC-08 files were not removed or edited.

## 18. Changed-file inventory

Because the repository has no `HEAD`, Git reports the entire tree as untracked and cannot distinguish
pre-existing files from this R0 work. The R0-controlled file set is:

- Root: `.editorconfig`, `.gitignore`, `AGENTS.md`, `CONTEXT.md`, `Directory.Build.props`,
  `Directory.Packages.props`, `global.json`, `IUMP.slnx`, `NuGet.Config`, `README.md`, and
  `_project_docs/README.md`.
- Skills/workflow: all files under `.agents/skills/{code-review,codebase-design,diagnosing-bugs,
  domain-modeling,grill-with-docs,improve-codebase-architecture,setup-matt-pocock-skills,
  speckit-*,tdd}/` and all current scaffold/config/template/workflow files under `.specify/`.
- Feature: every file under `specs/001-r0-engineering-foundation/`.
- Documentation: `docs/source-register.md`, `decision-log.md`, `environment-inventory.md`,
  `database-access-request.md`, `ci-readiness.md`, `blocker-report.md`, `gate-g1-status.md`,
  `verification-report.md`, `code-review.md`, this report, `docs/architecture/*`, `docs/contracts/*`,
  `docs/runbooks/*`, `docs/agents/*`, and `docs/adr/ADR-{001..011,015,016}-*.md`.
- Database/scripts: `database/migrations/0001_r0_foundation.sql`, `database/seeds/README.md`, and every
  current file under `scripts/`.
- Backend: every current file under `src/Api/`, `src/Worker/`, `src/BuildingBlocks/`, and
  `src/Modules/{IAM,Organization,Catalog,Acquisition,Telemetry,Rules,Alerts,Notifications,Reporting,
  Audit,Integration,Operations,Files}/`.
- Frontend: current tracked-source candidates under `src/Web/` (configuration, lock/manifest,
  `index.html`, and `src/`); `node_modules` and `dist` are ignored/generated.
- Tests: every current file under `tests/Unit/`, `tests/Verification/`, and
  `tests/Architecture/fixtures/`.

The raw final command `git status --short --untracked-files=all` enumerates these paths plus unchanged
Business Docs, because Git has no baseline.

## 19. Final Git status

Branch name reports `master`, but `git rev-parse --verify HEAD` fails with `Needed a single revision`.
There are no commits, remotes, staged files, or Codex-created branch/PR. All visible files are
untracked from Git's perspective; ignored build outputs, dependency trees, and
`verification-results.json` are not candidates for commit.

## 20. Final disposition

R0 is implemented to the extent allowed by the approved workstation and available dependencies.
It is not `FULLY_COMPLETE`: mandatory external evidence remains blocked. The correct status is
`R0_IMPLEMENTED_WITH_ENVIRONMENT_BLOCKERS`, Gate G1 remains not passed, and work stops before R1.

## 21. Lowest-risk next step

Have the named company owners approve/provision PostgreSQL, internal package mirrors, and the CI
runner/template. Then rerun the exact documented migration, integration, frontend clean-install, and
aggregate verification commands, record real evidence, and reassess Gate G1. Do not begin R1/VS-01
until those blockers and approvals are resolved.
