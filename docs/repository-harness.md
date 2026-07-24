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
- Release is claimed only from Release-ready evidence with no mandatory blocker.
- `verification-results.json` contains no credential values.
