# Repository Harness

This document is the entry map for developers, Codex, and OpenCode. It points to the repository's
sources of truth and defines the verification evidence required before work is described as
complete.

## Commands

Use Fast mode while iterating:

```powershell
& .\scripts\harness.ps1 -Mode Fast
& .\scripts\harness.ps1 -Mode Fast -Feature 002-asset-simulator-latest
```

Use Full mode before claiming completion:

```powershell
& .\scripts\harness.ps1 -Mode Full
& .\scripts\harness.ps1 -Mode Full -Feature 002-asset-simulator-latest
```

`scripts/verify.ps1` remains a compatibility wrapper for Full mode.
The stable command surfaces are `scripts/harness.ps1 -Mode Fast` and
`scripts/harness.ps1 -Mode Full`; the PowerShell examples above use Windows path separators.

## Active Feature Resolution

The harness selects the active Spec Kit feature in this order:

1. The explicit `-Feature` argument.
2. `.specify/feature.json`.
3. The current Git branch when it starts with exactly one directory name under `specs/`.

Fast mode may perform repository-only checks when no feature resolves. Full mode fails when it
cannot resolve a feature or when the feature lacks `spec.md`, `plan.md`, or `tasks.md`.

## Required Context

| Change | Read before changing code |
|---|---|
| Domain language or behavior | `CONTEXT.md`, `docs/source-register.md`, relevant files under `Business Docs/`, active `spec.md` |
| Architecture or dependencies | Relevant ADRs under `docs/adr/`, `docs/architecture/`, active `plan.md` |
| Implementation | Active `spec.md`, `plan.md`, and `tasks.md` |
| Database or integration | DOC-05, DOC-06, relevant ADRs, and `docs/runbooks/` |
| Completion claim | Active Spec Kit artifacts and a fresh Full harness result |

Follow progressive disclosure: begin with this map, then read only the sources required by the
change. Do not load every Business Docs file into context when the active feature cites a smaller
authoritative set.

## Source Precedence

When sources disagree, use this exact order:

1. DOC-01 Product Vision and Scope.
2. DOC-03 Business Requirements.
3. DOC-04 Software Requirements Specification.
4. DOC-05 Software Architecture Document.
5. DOC-06 Data and Integration Specification.
6. DOC-07 MVP Roadmap and Delivery Plan.
7. DOC-08 UI/UX Design Specification.
8. Repository ADRs.
9. Active Spec Kit feature artifacts.
10. `CONTEXT.md`.
11. Source code and automated tests.

DOC-02 Feasibility Assessment is supporting evidence for feasibility and environment constraints; it
does not override the ordered sources above. Repository ADRs implement but do not override the
Business Docs; active Spec Kit artifacts provide feature-level delivery detail. Raise a
contradiction instead of silently choosing a lower-priority source, and keep
`docs/source-register.md` synchronized with this order.

## Readiness States

- **Planning-ready** means the specification, authoritative source registration, design inputs,
  ownership/composition-root decisions, and required planning artifacts are complete enough that
  planning artifacts may continue. It does not authorize green implementation or release.
- **Implementation-ready** means the clean cross-artifact analysis has zero Critical/High
  findings, constitution impact is resolved, required amendments/templates/guidance are applied,
  and the final Phase 0 checkpoint permits one implementation phase. It does not imply release.
- **Release-ready** means caller-visible functionality and acceptance evidence pass, Fast and
  mandatory Full/environment verification pass, no mandatory blocker remains, and the final
  release checkpoint permits release.

These states are distinct and must be recorded explicitly; Planning-ready and
Implementation-ready do not imply Release-ready.

## Delivery Workflow

Spec Kit owns the canonical delivery artifacts:

```text
source registration -> specify -> clarify -> plan/design -> tasks -> analyze -> resolve Critical/High -> constitution-impact -> amendment -> template/guidance sync -> Phase 0 checkpoint -> one implementation phase -> Standards/Spec review -> Fast -> Full -> acceptance/release
```

Engineering skills such as domain modeling, codebase design, TDD, diagnosis, and code review are
methods used inside that lifecycle. They must not create a competing specification, plan, or task
list. Spec Kit artifacts remain canonical. Each `/speckit.implement` invocation executes exactly
one phase, reaches its checkpoint, and stops; continuation requires the next explicit invocation.
The Phase 0 checkpoint must pass before green application-source work begins.

## Evidence and Blocker Model

The harness writes machine-readable evidence to `verification-results.json`.

| Exit | Meaning |
|---:|---|
| `0` | Every mandatory check passed |
| `1` | At least one mandatory check failed |
| `20` | No mandatory check failed, but at least one was blocked or not run |

Evidence status and executability classification are separate fields. Statuses are `PASS`,
`FAIL`, `BLOCKED`, and `NOT_RUN`; classifications are `RUNNABLE_NOW`,
`BLOCKED_BY_DATABASE_ACCESS`, `BLOCKED_BY_PACKAGE_POLICY`, `BLOCKED_BY_MISSING_TOOL`, and
`BLOCKED_BY_COMPANY_APPROVAL`.

`PASS` and `FAIL` mean the check completed with that outcome. `NOT_RUN` means it was not
attempted. `BLOCKED` means the required capability could not execute and the evidence includes the
exact blocker. A blocker is never a pass: report its check ID, `status: BLOCKED`, classification,
blocker ID, and evidence. A blocked classification such as `BLOCKED_BY_DATABASE_ACCESS` is not a
replacement for the `BLOCKED` status. Serialized values such as
`BLOCKED_BY_DATABASE_ACCESS` represent evidence status `BLOCKED` plus the corresponding blocker
classification.

The machine-readable interpretation remains stable: exit code `0` requires all mandatory checks
to pass; exit code `1` reports a mandatory failure; exit code `20` reports no mandatory failure but
at least one blocked or not-run check. Do not change the scripts or exit-code interface to hide a
blocker. Mandatory blocked or `NOT_RUN` evidence prevents Full verification and release from
passing.

## Permanent Restrictions

- Do not introduce equipment control, setpoints, actuation, or write-back.
- Do not introduce Modbus until its documented conditional gate is approved.
- Do not expose real credentials in source, console output, or evidence.
- Do not use public package sources, public CI actions, or container workflows on the restricted
  workstation.
- DOC-05 v0.2 defines the target as restricted non-containerized host/service deployment; a
  concrete TEST/UAT/PROD host and service-manager approval is still required before release.
- Do not substitute another database for PostgreSQL.
- Work only inside the included scope of the active Spec Kit feature.

## Completion Checklist

- The implemented behavior is represented by the active `spec.md`, `plan.md`, and `tasks.md`.
- Relevant domain documents and ADRs were read.
- The governing constitution version and Planning-ready, Implementation-ready, and Release-ready
  states are recorded distinctly.
- The Phase 0 governance checkpoint passed before green implementation work.
- Exactly one `/speckit.implement` phase was executed and its checkpoint was followed by an
  explicit stop.
- Standards and Spec-compliance review completed with Critical/High findings resolved.
- Fast mode passed during iteration.
- A fresh Full mode was run after the final change.
- Full mode is mandatory evidence for a release claim; it is not optional.
- Every failure was fixed or reported, and every blocked or not-run capability is explicitly
  classified.
- Every blocker was reported as blocked, not passed.
- Release is claimed only from Release-ready evidence with no mandatory blocker. The Full
  `deployment-target` check represents approved non-containerized target-host/service evidence;
  `BLK-ENV-004` is obsolete and must not be emitted.
- The deployment-target contract is trust-bounded and fail-closed: it requires an approved company
  CI context (`CI=true` plus `IUMP_COMPANY_CI_APPROVED=true`), `IUMP_DEPLOYMENT_TARGET_APPROVED=true`,
  the company-provided `IUMP_DEPLOYMENT_TRUSTED_ROOT`, manifest path containment inside that root,
  reparse-point escape rejection, and protected `IUMP_DEPLOYMENT_EVIDENCE_SHA256` attestation for
  a sanitized manifest. Missing or untrusted approval is
  `BLOCKED_BY_COMPANY_APPROVAL`/`BLK-ENV-005`; malformed, unsafe, or attestation-failed manifest
  evidence is `FAIL`. A developer-created manifest is never treated as company approval and no
  bypass variables exist.
- Repository-wide harness checks (`deployment-target-contract`, `deployment-signature`,
  `doc05-architecture`) are registered before Feature-scoped checks so they run for every relevant
  Feature and are never silently skipped. The `doc05-architecture` check structurally verifies
  DOC-05 v0.2 restricted non-containerized wording, the corrected date, deployment components, and
  ADR AR-11; it verifies Open XML package integrity (required entries, relationship XML,
  office-document target, no traversal), copies a locked document to a temporary path, never writes
  into the repository, and its text-level PASS is never promoted to a visual PASS. Approved visual
  DOCX rendering is a documented `NOT_RUN` limitation (not a release blocker and not an unenforced
  mandatory gate); see `docs/decision-log.md` for the source-of-truth decision.
- `verification-results.json` contains no credential values.

## Current Feature 003 handle-bound trust closure guidance

The current Feature 003 corrective phase starts from merged `main` `4b4713cb42b1a03270a2688b344988d2945bab2c`
on `fix/003-handle-bound-trust-closure`. The deployment policy verifier must read policy bytes,
file identity, security descriptor, and authorization evidence from the same opened Windows handle;
pathname-based ACL reads are not production authority. Windows `AccessCheck` (or an approved built-in
equivalent) is required for effective access, and unavailable capability is `BLOCKED_BY_MISSING_TOOL`,
never a pathname-only PASS. A started verifier process that exits without one valid structured
protocol result is `FAIL`; only pre-start command/project/runtime/process-start failures are missing-tool
blockers. Provider-native Spec Kit commands remain `NOT_RUN` when unavailable, and direct artifact
comparison is not provider PASS. This guidance does not alter the product boundary or release gate.
