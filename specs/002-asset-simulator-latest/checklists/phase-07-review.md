# Phase 7 Standards and Specification Review — Concurrency-and-Scope Closure

- Baseline: `8261074a2c77f34a7988d4b9a0d04df5565d8deb`
- Review target: concurrency-and-scope corrective convergence for T131–T151 only.
- Review date: 2026-07-29.
- Evidence: T131–T135, T145, T149, T150 focused run; Fast harness; source-only migration and
  boundary checks. PostgreSQL execution remains blocked by missing approved tooling.

## Findings and resolution

| ID | Severity | Finding | Resolution / evidence | State |
|---|---|---|---|---|
| A | Critical | `PublishRaceWinner` and `CommitAsync` access `_committedState` without synchronization; concurrent fake transactions can interleave. | Added `_committedGate` lock; `PublishRaceWinner`, `CommitAsync` state mutation, and `CommittedState` getter all lock on `_committedGate`. | Resolved |
| B | Critical | `CommitAsync` has no commit-time recheck — staged terminals could overwrite a concurrently committed terminal without detection. | Inside `_committedGate` lock, `CommitAsync` rechecks Measurement-ID and Run+Point+sequence uniqueness against latest `_committedState`; throws `TelemetryUniqueRaceException` on conflict. | Resolved |
| C | Critical | `PublishRaceWinner` no-op checks only terminal fingerprint and fields; committed Raw, Latest, and Event are not verified. | Expanded no-op to compare `Raw.Equals(fixture.Raw)`, `Latest.Equals(fixture.Latest)` (when applicable), and `Event.EventId` membership. | Resolved |
| D | Critical | `CommittedState` property and `ListCommittedAsync` expose mutable internal dictionary references; callers could mutate committed state. | `CommittedState` getter returns deep-copy snapshot with fresh dictionaries inside the lock. `ListCommittedAsync` returns deep-copied events with new Before/After dictionaries. | Resolved |
| E | Critical | Trusted-scope check (`CheckTrustedScope`) is only inside `PersistAcceptedAsync`; `PersistRejectedAsync` with provider has no scope guard. | Added `CheckTrustedScope` call in `IngestMeasurement.ExecuteAsync` after provider snapshot, before `ValidateProvider` and any `PersistAccepted`/`PersistRejected` call. | Resolved |
| F | Critical | Event factory has nullable `eventAreaId` and no nonblank validation; blank scope IDs could reach the event envelope. | Changed `eventAreaId` to non-nullable `string`; added `EVENT_SCOPE_ID_BLANK` validation for both `eventSiteId` and `eventAreaId`. Updated `EventMatchesWinner` to check nonblank AreaId. | Resolved |
| G | High | T145 has no pre-existing state proof or Rejected fixture matrix with multiple rejection codes. | Added "Rejected fixture preserves pre-existing Accepted state" and "Rejected fixture with multiple rejection codes" scenarios (5 codes). | Resolved |
| H | High | T145 lacks direct fixture/slot conflict probe tests using `ReplayProbe`. | Added "direct fixture conflict probe" (conflicting terminal returns TERMINAL_RESULT_CONFLICT) and "direct slot conflict probe" (slot loser raises TelemetryUniqueRaceException, winner preserved). | Resolved |
| I | Medium | T135 has no factory blank or mismatched scope tests. | Added 4 factory tests: blank eventSiteId, blank eventAreaId, mismatched trusted site, mismatched trusted area. | Resolved |
| J | Medium | Corrective RED uses stale baseline `f8521159` and does not cover the new A–F defects. | RED now uses `8261074a` baseline; documents 5 active assertion failures and 15+ T149 structural failures covering all new defects. | Resolved |

## Standards result

- Unresolved Critical: `0`
- Unresolved High: `0`
- Unresolved Medium: `0`
- Scope creep: `0`
- T146 remains `BLOCKED_BY_PACKAGE_POLICY`.
- T147 remains `BLOCKED_BY_PACKAGE_POLICY`.
- T148 remains `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE`.

## T150 decision

PASS — all review findings A–J are resolved, with zero unresolved Critical or High findings.
Phase7ReviewCheck executes 41 checks, ArchitectureVerification executes 52 checks.

## Historical baselines

Previous corrective closures at `f8521159802fd39732c4cfa24605aed912c18419` (atomic-evidence
closure), `d5c71ed42a45c6fee189c3a67580b0cf096c9bf6` (atomic-race and compatibility-lock),
and `b6b2510820f5ab8f0af5569a2fc18b4ee4b2f892` (exact-result). This T150 review is the
concurrency-and-scope closure at `8261074a2c77f34a7988d4b9a0d04df5565d8deb`.
