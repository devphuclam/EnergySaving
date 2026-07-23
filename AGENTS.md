# IUMP Agent Instructions

## Repository harness

Read `docs/repository-harness.md` before making changes. Use Fast mode while iterating and run a
fresh Full mode before claiming completion. A blocked check must be reported as blocked and must
never be described as passing.

## Agent skills

### Issue tracker

Use local Markdown files under `.scratch/` for investigation issues and backlog items that are not already represented by a Spec Kit feature artifact. See `docs/agents/issue-tracker.md`.

### Domain docs

This is a single-context repository. Read the root `CONTEXT.md` and relevant ADRs under `docs/adr/` before changing domain language or architecture. See `docs/agents/domain.md`.

## Spec Kit and Matt Pocock workflow

Spec Kit owns the delivery lifecycle and its canonical artifacts:

- `.specify/memory/constitution.md`
- `specs/<feature>/spec.md`
- `specs/<feature>/plan.md`
- `specs/<feature>/checklists/`
- `specs/<feature>/tasks.md`
- Spec Kit analysis and convergence results

Matt Pocock skills provide engineering methods inside that lifecycle. They must enrich the canonical Spec Kit artifacts and repository documentation, not create a competing specification, plan, or task system.

Use the combined workflow in this order:

1. `$grill-with-docs` and `$domain-modeling` validate terminology and unresolved decisions against source documents, `CONTEXT.md`, and ADRs. Do not reopen documented product decisions.
2. `$speckit-constitution` establishes project-wide engineering principles.
3. `$speckit-specify` defines WHAT and WHY in the canonical feature specification.
4. `$speckit-clarify` resolves only material ambiguities not answered by source documents, ADRs, or the installed environment.
5. `$codebase-design` evaluates module depth, interfaces, seams, adapters, project references, composition roots, data ownership, and test surfaces while `$speckit-plan` creates the canonical implementation plan.
6. `$speckit-checklist` validates requirements quality; `$speckit-tasks` creates the canonical executable task list.
7. `$speckit-analyze` checks cross-artifact consistency. Do not implement while Critical conflicts remain.
8. `$speckit-implement` drives the canonical tasks. Apply `$tdd` at each agreed behavioral seam using red, green, and refactor. When a build or test fails unexpectedly, use `$diagnosing-bugs` before changing production code.
9. `$code-review` performs separate Standards and Spec-compliance reviews. Resolve Critical and High findings before completion.
10. `$speckit-converge` compares the implementation with the canonical artifacts and appends genuinely missing work to `tasks.md`.

Do not use Matt skills to publish duplicate specs or tickets for work already represented under `specs/`. Do not use Spec Kit artifacts to override the documented source-of-truth hierarchy.
