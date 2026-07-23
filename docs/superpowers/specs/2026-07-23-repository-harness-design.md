# IUMP Repository Harness Design

**Date:** 2026-07-23
**Status:** Approved
**Scope:** A version-controlled, long-lived repository harness for developers, Codex, and OpenCode.

## Purpose

The harness gives every engineering worker one reliable entry point for discovering project
knowledge, selecting the active Spec Kit feature, running the appropriate checks, and reporting
evidence. It extends the existing IUMP verification foundation rather than introducing another
specification, planning, or task system.

The harness must remain useful after R0. Permanent product and security constraints remain enforced,
while feature scope comes from the active Spec Kit feature instead of a repository-wide rule that
rejects all post-R0 behavior.

## Design Principles

- The repository is the system of record. `AGENTS.md` is a short map, not an encyclopedia.
- Spec Kit owns feature specification, planning, tasks, analysis, and convergence.
- Business documents, the source register, `CONTEXT.md`, and ADRs remain authoritative according to
  their documented precedence.
- One command is the interface; existing build, test, and verification scripts remain internal
  implementations behind it.
- Verification produces machine-readable evidence and never converts an environmental blocker into
  a pass.
- No public package source, public CI action, container workflow, or real credential is introduced.
- Existing uncommitted feature 002 and OpenCode integration changes are preserved.

## User Interface

The harness exposes one PowerShell command:

```powershell
& .\scripts\harness.ps1 -Mode Fast
& .\scripts\harness.ps1 -Mode Full
& .\scripts\harness.ps1 -Mode Full -Feature 002-asset-simulator-latest
```

`Mode` is mandatory and accepts `Fast` or `Full`. `Feature` is optional and accepts either a feature
directory name or its repository-relative path under `specs/`.

The command returns:

- exit code `0` when every mandatory executed check passes;
- exit code `1` when at least one mandatory check fails;
- exit code `20` when there is no failure but at least one mandatory check is blocked;
- structured results in the existing root `verification-results.json`.

## Active Feature Resolution

The harness resolves the active feature in this order:

1. The explicit `-Feature` argument.
2. The `feature_directory` value in `.specify/feature.json`.
3. A Git branch name that exactly starts with a directory name under `specs/`.

Resolution must reject paths outside `specs/`, missing directories, and ambiguous branch matches.
Failure to resolve a feature is a failure for `Full` mode. `Fast` mode may run repository-only checks
without a feature, but records the feature check as `NOT_RUN`.

## Knowledge Map

`docs/repository-harness.md` becomes the human-readable table of contents. It tells workers what to
read for each kind of change:

| Change type | Required context |
|---|---|
| Domain language or behavior | `CONTEXT.md`, `docs/source-register.md`, relevant Business Docs, active `spec.md` |
| Architecture or dependencies | Relevant ADRs, `docs/architecture/`, active `plan.md` |
| Implementation | Active `spec.md`, `plan.md`, and `tasks.md` |
| Database or integration | DOC-05, DOC-06, relevant ADRs and runbooks |
| Completion claim | Active artifacts plus a fresh Full harness result |

`AGENTS.md` links to this map and states the non-negotiable entry and completion rules. It continues
to document the combined Spec Kit and engineering-skill workflow.

## Harness Components

### Command Orchestrator

`scripts/harness.ps1` validates arguments, resolves the feature, selects checks for the requested
mode, executes each check independently, writes results, prints a concise summary, and returns the
aggregate exit code.

### Shared Harness Functions

`scripts/common/Harness.ps1` contains the small reusable interface used by the orchestrator and its
tests:

- `Resolve-HarnessFeature`
- `Test-FeatureArtifacts`
- `Invoke-HarnessCheck`
- `Get-HarnessExitCode`

These functions return data instead of terminating the process. Only `scripts/harness.ps1` writes
the final file or exits.

### Feature Artifact Check

The artifact check recognizes four lifecycle stages:

| Stage | Required artifacts |
|---|---|
| Specify | `spec.md` |
| Plan | `spec.md`, `plan.md` |
| Tasks | `spec.md`, `plan.md`, `tasks.md` |
| Implement/complete | `spec.md`, `plan.md`, `tasks.md` |

The harness infers the highest available stage and reports missing prerequisites. A Full completion
run requires all three canonical files. Checklists and analysis remain workflow quality tools but
are not universal completion prerequisites because their necessity is feature-dependent.

### Verification Profiles

Fast mode executes:

- tool availability needed by the selected checks;
- repository policy and credential checks;
- active feature resolution and artifact checks;
- repository and module architecture checks;
- focused unit verification that does not require database access.

Full mode executes every Fast check plus:

- backend Release build and complete backend tests;
- frontend lint and production build using the existing dependency tree;
- database connectivity and database-backed checks when approved configuration exists;
- environment and company-approval checks already represented by the verification contract.

Existing scripts remain callable by developers. The harness orchestrates them and normalizes their
results rather than duplicating their implementation.

## Scope Enforcement Beyond R0

`tests/Verification/repository-scope.tests.ps1` stops treating all R1 behavior as prohibited. It is
split conceptually into:

1. **Permanent invariants:** no equipment control or write-back, no real credentials, no unapproved
   container/public-CI artifacts, and no technology that violates an accepted ADR.
2. **Feature evidence:** the active `spec.md` must contain `Scope and Evidence Boundaries`, including
   included capability, explicit exclusions, dependencies, and evidence classifications.

The harness does not attempt to infer arbitrary source-code violations from natural-language
exclusions. Spec compliance is handled by Spec Kit analysis/convergence and code review. Mechanical
repository tests enforce only stable, objectively detectable invariants.

For feature 002 this permits Simulator, hierarchy, ingestion, and latest-value work while permanent
read-only OT and credential constraints continue to apply.

## Result Model and Error Handling

The existing verification result contract is generalized from R0 naming to project-wide IUMP usage
without changing its classification vocabulary:

- `PASS`
- `FAIL`
- `NOT_RUN`
- `BLOCKED_BY_MISSING_TOOL`
- `BLOCKED_BY_PACKAGE_POLICY`
- `BLOCKED_BY_DATABASE_ACCESS`
- `BLOCKED_BY_COMPANY_APPROVAL`

Every result contains a check ID, command, UTC timestamp, mandatory flag, classification, exit code
when available, sanitized evidence, and blocker ID when applicable.

Checks run independently where safe. A failed architecture test does not prevent the harness from
reporting a missing database. Exceptions are converted to `FAIL` results with sanitized messages.
No credential value is printed or written to evidence.

## Testing Strategy

PowerShell contract tests use temporary fixture repositories and exercise real harness functions
without network, package installation, containers, or database credentials.

Required cases:

- explicit, state-file, and branch-based feature resolution;
- rejection of traversal, missing features, and ambiguous feature selection;
- Full mode failure when canonical artifacts are missing;
- Fast mode repository-only behavior when no feature is resolvable;
- correct aggregation of pass, failure, and blocker exit codes;
- Fast profile exclusion of Full-only build, frontend, and database checks;
- permanent scope invariant detection;
- machine-readable results conforming to the generalized verification schema.

The existing architecture red fixture remains a required proof that architecture enforcement can
detect a real violation.

## Planned Repository Changes

- Create `scripts/harness.ps1`.
- Create `scripts/common/Harness.ps1`.
- Create `tests/Verification/repository-harness.tests.ps1`.
- Create `docs/repository-harness.md`.
- Update `AGENTS.md` with harness entry and completion rules.
- Update `README.md` from an R0-only landing page to a project landing page with current release
  status and harness commands.
- Update `scripts/verify.ps1` to compose with the harness without recursive execution.
- Update `tests/Verification/repository-scope.tests.ps1` to enforce permanent invariants.
- Generalize the verification schema and related documentation from R0-only naming.
- Update the aggregate verification contract test to cover harness classifications and exit codes.

## Non-Goals

- No autonomous task queue or multi-agent orchestrator.
- No GitHub Actions workflow until a company-approved runner and templates exist.
- No containers or per-worktree runtime stack.
- No replacement for Spec Kit, ADRs, Business Docs, or code review.
- No automatic semantic judgment that implementation satisfies every natural-language requirement.
- No database credential provisioning.

## Acceptance Criteria

The design is complete when:

1. A developer or agent can discover required context from one short repository map.
2. Fast and Full modes run through one stable command.
3. Feature 002 is recognized without weakening permanent safety constraints.
4. Missing artifacts and code/test failures are reported as failures.
5. Environmental restrictions are reported as blockers, never passes.
6. Results are readable by humans and machine consumers such as OpenCode.
7. All harness contract tests pass without network, container, or credential access.
