# Feature 003 Final Governance Corrective Report

Date: 2026-08-03
Starting merged-main SHA: `045f3981f3ba6bb87425009ee8f8cf0e6cf4e56a`
Corrective branch: `fix/003-final-governance-corrective`
Scope: Post-Phase-6 governance, traceability, evidence, regression, and source-precedence closure
only. No Phase 7, Feature 004, Rule, Alert, CSV, Reporting, or merge.

## 1. Baseline and source-of-truth gate

- Entry gate: PASS. Working tree was clean before branch creation; `main` was at
  `045f3981f3ba6bb87425009ee8f8cf0e6cf4e56a`; that SHA is an ancestor of the corrective branch.
- Required repository guidance was read: `AGENTS.md`, `CONTEXT.md`, constitution 1.1.0,
  `docs/source-register.md`, `docs/repository-harness.md`, relevant ADRs, Feature 003 artifacts,
  and current checklists.
- DOC-05 v0.2 and DOC-07 v0.2 were reviewed from the Business Docs source. DOC-05 v0.2 is the
  higher-priority source for architecture/deployment and defines MVP-1 as restricted
  non-containerized host/service deployment.
- The approved PostgreSQL runtime remains `127.0.0.1:5433/iump_dev`; no command in this closure
  targeted port `5432`, and no secret value was printed or persisted.

## 2. SpecKit workflow

| Operation | Result | Evidence |
|---|---|---|
| `speckit-converge` | `NOT_RUN` | SpecKit provider command unavailable in this runtime |
| `speckit-analyze` | `NOT_RUN` | SpecKit provider command unavailable in this runtime |
| Direct artifact comparison | PASS | 87 unique task IDs; T081–T087 additive corrective ledger; no duplicate IDs; direct source comparison recorded here and never promoted to provider PASS |

## 3. Corrective task ledger

The canonical `tasks.md` now contains T081–T087 under “Post-Phase-6 Corrective Closure”. T081 is
explicitly retrospective and registers Phase 5 corrective commit
`986b3dca8673b455710835bc252cd17980f9cac5` and merge
`f93c2da8bcd71c0436c38d502ddd7a770c35e621`; it is not historical task execution or historical TDD
proof. T082–T086 are complete. T087 is complete only after the independent reviews and this final
stop record are attached to the corrective commit.

Historical accessibility RED evidence is `NOT_AVAILABLE`; no fabricated historical red run is
claimed. The new regression is post-merge static contract protection.

## 4. Finding disposition

| Finding | Disposition |
|---|---|
| FINDING-01: Phase 5 corrective work absent from canonical task ledger | Resolved by retrospective T081 registration with commit/merge mapping and disclosure. |
| FINDING-02: AC-005 PASS lacked API/Web host restart evidence | Resolved honestly: AC-005 is `PARTIAL`; no approved authenticated browser/process-control runner could be claimed. Refresh/logout-login evidence is retained but not promoted to PASS. |
| FINDING-03: Phase 6 accessibility change lacked direct regression coverage | Resolved with `tests/Verification/app-shell-accessibility.tests.ps1`; post-merge static regression PASS. Browser behavior remains unexercised. |
| FINDING-04: `spec.md` remained Draft | Resolved with `Status: Implemented — Release Evidence Blocked`; never marked Released. |
| FINDING-05: DOC-05/ADR-010/harness/blocker deployment conflict | Historical Phase 6 disposition used superseded wording; the final closure corrects ADR-010 to `Accepted for MVP-1 architecture; deployment approval pending`. The harness no longer emits BLK-ENV-004 and uses BLK-ENV-005 for concrete non-containerized target-host approval. |
| FINDING-06: Phase 6 direct-to-main review boundary | Resolved for this corrective work by dedicated branch, fresh independent Standards/Specification reviews, and prepared-but-unmerged PR boundary. Historical Phase 6 merge is not rewritten. |

## 5. AC-005 host-restart evidence

Status: **PARTIAL**.

Required exact journey: login, observe persisted setup progress, stop/restart approved API host,
stop/restart separate Web host when applicable, login again, reload status, verify completed/next
step, and verify no duplicate/auto-start. No approved authenticated browser/process-control runner
was available: the in-app browser session could not claim a controllable local tab, and the available
Chrome tab surface contained no local IUMP tab. Therefore the exact journey was not run. No database
cleanup/seed, port 5432, alternate database, or fake PASS was used. The missing capability is
recorded as `BLOCKED_BY_MISSING_TOOL`; AC-005 remains PARTIAL rather than PASS.

## 6. Accessibility regression

`app-shell-accessibility.tests.ps1` PASS (exit 0) checks the existing AppShell source seam for:

- visible Vietnamese labels and `htmlFor`/`id` pairs for username and password;
- invalid-credential `sign-in-error` alert and `aria-describedby` association on both inputs;
- Vietnamese navigation and authentication region names.

This is a static contract result, not browser behavior. Historical accessibility RED evidence is
`NOT_AVAILABLE`; the post-merge regression result is `PASS`.

## 7. Deployment decision reconciliation

DOC-05 v0.2 §19/§19.2/§19.3 and DOC-07 v0.2 §17/§22.2 govern. MVP-1 uses Web static files, an API
executable/approved host, a Worker service, and an internal PostgreSQL service. Docker, Compose,
Podman, image promotion, and downloaded runtime/package tooling are not the target topology. ADR-010
now records that its earlier container-target wording is superseded. Historical records remain
historical; current harness output uses `deployment-target` and `BLK-ENV-005`.

`BLK-ENV-005` remains a real company-approval blocker because no concrete TEST/UAT/PROD host,
service-manager, lifecycle, rollback, and Infrastructure/Security approval evidence is available.
This closure did not deploy or mutate services.

## 8. Verification evidence

| Check | Result |
|---|---|
| AppShell static accessibility regression | PASS, exit 0 |
| Unit suite | PASS, exit 0 (Full harness run) |
| PostgreSQL Integration/database target | PASS, exit 0 against `127.0.0.1:5433/iump_dev` (Full harness run) |
| API/backend build | PASS, exit 0 (Full harness run) |
| Web lint/build | PASS, exit 0 (Full harness run) |
| Architecture/repository policy/scope | PASS, exit 0 |
| Fast harness | PASS=12, exit 0 |
| Full harness | exit 20; PASS=15, `BLOCKED_BY_COMPANY_APPROVAL=2` (`BLK-ENV-003`, `BLK-ENV-005`); no mandatory FAIL |

Full exit 20 is non-passing by repository contract. Frontend behavior remains separately
`BLOCKED_BY_PACKAGE_POLICY`, and authenticated browser automation remains `BLOCKED_BY_MISSING_TOOL`.

## 9. Independent review

Independent reviews against the corrective branch and fixed baseline completed with no unresolved
Critical, High, or actionable Medium finding on either axis:

| Axis | Initial finding(s) | Final |
|---|---|---:|
| Standards | Active security-scope and deployment-evidence wording still referenced the old container check; corrected to `BLK-ENV-005` and non-containerized target-host/service language. | C0 / H0 / M0 / L0 |
| Specification | T087/open-task and stale 80-task comparison in the release checkpoint; synchronized to 87 total tasks, T034 as the only historical unchecked task, and T081–T087 complete. | C0 / H0 / M0 / L0 |

The reviews confirm AC-005 PARTIAL, provider statuses `NOT_RUN`, and release blockers are honestly
represented. No review finding remains that blocks the corrective stop.

## 10. Readiness and blockers

| State | Result |
|---|---|
| Planning-ready | YES (historical and synchronized) |
| Implementation-ready | YES for bounded Feature 003 implementation; corrective governance artifacts are synchronized |
| Feature-implementation-complete | YES, bounded; AC-005 is PARTIAL only at the host-restart evidence boundary |
| Release-ready | NO |

Remaining blockers: `BLK-ENV-003` company CI approval, `BLK-ENV-005` approved non-containerized
target-host/service approval, frontend behavior runner `BLOCKED_BY_PACKAGE_POLICY`, and approved
authenticated browser/process-control capability for AC-005. These are not database-access
blockers; the approved database target is available.

## Final documentation and deployment-gate closure

The `fix/003-doc05-deployment-gate` closure from baseline `6dbfaf3bcbc95f2d262ddeacf174232d9d746bd7`
is historical evidence. The current final corrective branch is `fix/003-trusted-deployment-approval`
from baseline `6b77256f29775bb2a777ddcb555d868d7e671243`.

`scripts/common/DeploymentTarget.ps1` now enforces a trust-bounded fail-closed contract: approved
company CI context (`CI=true` plus `IUMP_COMPANY_CI_APPROVED=true`), `IUMP_DEPLOYMENT_TARGET_APPROVED=true`,
a trusted evidence root, manifest path containment, reparse-escape rejection, and SHA-256
attestation. Missing or untrusted approval is `BLOCKED_BY_COMPANY_APPROVAL` (`BLK-ENV-005`);
malformed, unsafe, or attestation-failed evidence is `FAIL`. No bypass variables exist. The new
contract test passes blocked, trust-boundary, attestation, redaction, Fast/Full-plan, and
exit-code cases. Provider Analyze/Converge statuses are recorded honestly when available and
`NOT_RUN` otherwise. AC-005 and AC-011 remain PARTIAL; Release-ready remains NO. This is the final
Feature 003 stop and does not authorize Phase 7, Spec 004, or a merge. The current ledger
comparison is `task_count=109`, `unique_task_count=109`, with T098-T109 as the active corrective
tasks, historical T034 as the only pre-existing unchecked task, and no duplicate task IDs.

## Historical Git and PR boundary (T081-T087)

Corrective branch: `fix/003-final-governance-corrective`. A PR title/body and reviewer request are
prepared for this branch, but no PR is created or merged by this closure. No force push, branch
deletion, or main update is performed. The explicit Feature 003 stop is recorded here: do not start
Phase 7, Feature 004, Rule, Alert, CSV, Reporting, or any other post-Phase-6 capability.
