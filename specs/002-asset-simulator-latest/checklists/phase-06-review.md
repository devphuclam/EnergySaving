# Phase 6 Corrective Standards and Specification Review (T129)

Repository: `devphuclam/EnergySaving`

Parent baseline: `89b32b7595f03fc90145f993a8ad77a61343433d`

Review surface: `git diff 89b32b7595f03fc90145f993a8ad77a61343433d --`, including the
corrective working-tree delta. Standards and Spec axes were reviewed independently.

## Corrective findings

| ID | Severity | Source/test evidence | Resolution | State |
|---|---|---|---|---|
| P6C-A | High | `RunCommands.cs`; micro-RED 01 | Start now requires algorithm version exactly `1`; the complete snapshot is validated and generator initialization is safely converted to `CONFIGURATION_INVALID` before Run ID/transaction/mutation. | CLOSED |
| P6C-B | High | `RunCommands.cs`; T110 unknown-scenario case; micro-RED 02 | Only `Constant` and `Normal` are accepted; unknown enum values fail before Run creation. | CLOSED |
| P6C-C | High | `RunControlTests.cs`; T110 `50` scenarios / `150` assertions | Added the complete identity, duplicate, readiness, provider-version, authorization, atomic multi-Point, lifecycle, scoped-Engineer and stable-repeat matrix. Every rejected case proves no Run, Run-Point, event, active transaction or PRNG initialization. | CLOSED |
| P6C-D | High | `ProductionAttemptService.cs`, fake repositories, T112 and T124 race scenarios; micro-RED 05/11 | Pending insertion is attempted first. The simulated competing winner independently commits Pending plus PRNG/cursor/due/Generated/version state atomically; loser rollback performs no second advance. | CLOSED |
| P6C-E | High | `SimulatorRunPointReservationTransition`; T124 pinned mutation scenarios; micro-RED 06 | `StageReservationAsync` accepts only mutable transition fields and validates expected Run/Point/cursor state. T124 submits each pinned-field mutation through the provider test contract and observes `PINNED_STATE_IMMUTABLE` with no change. | CLOSED |
| P6C-F | High | `TelemetryDispatchResultValidator`; T112/T124 invalid terminal matrices; micro-RED 07-09 | Stable `TERMINAL_RESULT_INVALID` validation now rejects mismatched/unknown/malformed terminal results before transaction staging; Pending/version/counters remain unchanged. | CLOSED |
| P6C-G | High | `0007_acquisition_run.sql`; T128; micro-RED 10 | Added all pinned Run-Point immutability checks and NULL-safe Accepted/Rejected/Duplicate terminal-pair constraints. Migration remains source-only and unexecuted. | CLOSED |
| P6C-H | High | `RunAttemptRepositoryTests.cs`; T124 `37` scenarios / `55` assertions | Expanded interface-only coverage for race atomicity/loser behavior, every pinned mutation, immutable payload, terminal pairs, Duplicate Accepted/Rejected metadata, commit rollback and optimistic conflict. | CLOSED |
| P6C-I | High | T108-T113 runtime output; T128; micro-RED 12 | Removed positive constant assignments. Each suite increments `TestCount` once at the executed scenario boundary and increments `CheckCount` only in assertion helpers. | CLOSED |
| P6C-J | High | `architecture.tests.ps1`, this review and T130 | T128 now fails on every rejected negative shape; unsupported prior PASS/progression claims were replaced with measured commands, counts and capability classifications. | CLOSED |

## Independent review axes

### Standards

No documented-standard violation remains. The change preserves Acquisition write ownership,
provider-neutral public ports, API/Worker separation, package/secret/database/container restrictions
and the unexecuted-migration boundary.

Two Low judgement-call smells remain accepted:

- repeated small counter/assertion helpers across the six dependency-free runners;
- a localized tuple data clump in the table-driven T110 prerequisites.

Neither is Critical/High or warrants broader test-framework work in this narrow correction.

### Specification

The initial Spec review found four High issues (provider-version error code, reserve/stage ordering,
scenario counter semantics and incomplete mutation execution) plus one Medium absence-of-Point
proof. All were corrected before this final review. No Phase 7 work or other scope creep was found.

## Gate

- Standards: Critical `0`, High `0`; Low judgement calls `2`.
- Specification: Critical `0`, High `0`; scope-creep findings `0`.
- T129: **PASS** because unresolved Critical and High findings are both zero.
