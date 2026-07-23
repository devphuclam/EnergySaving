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

When sources disagree, use this order:

1. DOC-01 through DOC-07 according to `docs/source-register.md`.
2. Accepted repository ADRs, which implement but do not override the Business Docs.
3. The active Spec Kit feature's `spec.md`, `plan.md`, and `tasks.md`.
4. `CONTEXT.md` for ubiquitous language.
5. Source code and automated tests as executable evidence.

Raise a contradiction instead of silently choosing a lower-priority source.

## Delivery Workflow

Spec Kit owns the canonical delivery artifacts:

```text
spec.md -> plan.md -> tasks.md -> implementation -> analysis/convergence
```

Engineering skills such as domain modeling, codebase design, TDD, diagnosis, and code review are
methods used inside that lifecycle. They must not create a competing specification, plan, or task
list.

## Verification Meaning

The harness writes machine-readable evidence to `verification-results.json`.

| Exit | Meaning |
|---:|---|
| `0` | Every mandatory check passed |
| `1` | At least one mandatory check failed |
| `20` | No mandatory check failed, but at least one was blocked or not run |

Classifications are `PASS`, `FAIL`, `NOT_RUN`, `BLOCKED_BY_MISSING_TOOL`,
`BLOCKED_BY_PACKAGE_POLICY`, `BLOCKED_BY_DATABASE_ACCESS`, and
`BLOCKED_BY_COMPANY_APPROVAL`.

A blocker is not a pass. Report its check ID, classification, blocker ID, and evidence.

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
- Fast mode passed during iteration.
- A fresh Full mode was run after the final change.
- Every failure was fixed or reported.
- Every blocker was reported as blocked, not passed.
- `verification-results.json` contains no credential values.
