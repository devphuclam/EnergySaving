# Phase 6 Final Scope-and-Isolation Review (T129)

Repository: `devphuclam/EnergySaving`

Parent baseline: `651c22c9db04f9cb01091f9873558f5be50530a8`

Review surface: `git diff 651c22c9db04f9cb01091f9873558f5be50530a8 --`. Standards and
Specification axes were reviewed independently.

## Required findings

| ID | Severity | Source/test evidence | Resolution | State |
|---|---|---|---|---|
| ISO-01 | High | `spec.md` multi-Point isolation acceptance; prior `contracts/simulator.md` owner-state section | Reconciled only the conflicting contract section: `SOURCE_INACTIVE` is Run-wide, while Mapping/Point/ancestor failures are Point-specific and preserve unrelated production. The feature spec was not changed. | CLOSED |
| ISO-02 | High | `ProductionAttemptService.cs`; T111 Point-specific two-Point scenario | Point-specific failures no longer invoke global Stop. Point A reports its exact error, performs no generation/reservation/dispatch/finalization and releases its lease; Point B completes independently; the Run stays Running with only Point B counters and no Stop event. Separate multi-Point `SOURCE_INACTIVE` coverage proves one global Stop event and zero production. | CLOSED |
| ISO-03 | High | `RunCommands.cs`; T110 changed-current-scope scenario | Existing nonterminal Run lookup now precedes current snapshot resolution. Authorization loads distinct pinned Run-Point Site IDs. A current-Site-only Engineer receives `NOT_FOUND`; pinned-Site Engineer and Administrator receive the stable existing Run without snapshot, PRNG, transaction or event work. | CLOSED |
| ISO-04 | High | `RunCommands.cs`; T110 Source-mismatch scenario | New Start checks `snapshot.SourceId == command.SourceId`; mismatch consistently returns `NOT_FOUND` before authorization-dependent creation, PRNG, recheck or transaction and creates state for neither Source. | CLOSED |
| ISO-05 | High | T110 `63` scenarios / `189` assertions; T111 `9` scenarios / `38` assertions; T128 | Added missing/null/non-Simulator/interval/bounds/nonfinite/Source-mismatch/existing-scope/Paused cases and both owner-isolation outcomes. T128 now guards their absence, the reconciled contract, existing-run-first order, pinned Site authorization, Source equality and all canonical T131+ Phase 7 paths. | CLOSED |

## Independent axes

### Standards

No documented-standard violation was found. The diff preserves Acquisition ownership,
API/Worker separation, scoped authorization, contract-to-test traceability, and all restricted
execution rules. String error codes and scenario-specific test setup were reviewed as low-risk
judgement calls, not actionable findings in this narrow closure.

### Specification

The first Spec review correctly identified three High checkpoint/static findings: stale T129,
stale T130, and incomplete T131+ Phase 7 path guards. All three were corrected before the final
review. Production behavior and the narrow contract reconciliation matched the authoritative
scope-and-isolation decision; no scope creep was found.

## Gate

- Standards: unresolved Critical `0`, High `0`.
- Specification: unresolved Critical `0`, High `0`; scope creep `0`.
- T129: **PASS**.
