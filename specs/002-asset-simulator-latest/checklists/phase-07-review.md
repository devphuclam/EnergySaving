# Phase 7 Standards and Specification Review — Atomic-Race and Compatibility-Lock Closure

- Baseline: `d5c71ed42a45c6fee189c3a67580b0cf096c9bf6`
- Review target: corrective convergence for T131–T151 only.
- Review date: 2026-07-29.
- Evidence: T131–T135, T145, T149, T150 focused run; Fast harness; source-only migration and
  boundary checks. PostgreSQL execution remains blocked by missing approved tooling.

## Findings and resolution

| ID | Severity | Finding | Resolution / evidence | State |
|---|---|---|---|---|
| A | High | `PublishRaceWinner` mutates committed terminal state before validating the complete winner fixture (defect A). | Refactored into `ValidateRaceWinnerFixture` (phase A, no mutation) then atomic clone-and-replace (phase B). T133 accepted/rejected race scenarios verify zero partial publication. | Resolved |
| B | Critical | An invalid Accepted or Rejected fixture with incomplete/mismatched raw/latest/event leaves partially committed state (defect B). | Phase A validates exactly before any dictionary assignment; phase B clones, constructs, and replaces atomically. | Resolved |
| C | High | `TelemetryFlowLockTarget` had no `CatalogCompatibility` member (defect C). | Added `CatalogCompatibility = 9` after `CatalogUnit`. Lock trace tests updated to include it. | Resolved |
| D | High | `AcquireOwnerLocksAsync` did not lock the Compatibility row; drift after recheck was unchecked (defect D). | Compatibility lock acquired after `CatalogUnit`, keyed by `CompatibilityIdentity`. Fake lock injection range extended. | Resolved |
| E | High | `ITelemetryRaceWinnerProbe` exposed only `LatestCount` without the actual committed Latest candidate (defect E). | Added `GetCommittedLatestAsync(Guid pointId, CancellationToken)` to the probe interface and `FakeTelemetryRepositories`. | Resolved |
| F | Medium | T145 had no exact Rejected race-winner scenario (defect F). | Added `exact Rejected race winner` scenario: StageRaceWinner with terminal only, verifies zero raw/latest/event, Duplicate replay. | Resolved |
| G | High | `MeasurementAcceptedEventFactory.Create` used unverified `provider.SiteId`/`provider.AreaId` (defect G). | Factory accepts optional `eventSiteId`/`eventAreaId`; production callers pass `provider.TrustedSiteId`/`provider.TrustedAreaId`. `ValidateTrustedScope` enforces `TrustedSiteId == SiteId`. | Resolved |
| H | High | `PROVIDER_SCOPE_MISMATCH` error code was absent; scope validation was missing from `PersistAcceptedAsync` (defect G). | Added `ValidateTrustedScope` method and `PROVIDER_SCOPE_MISMATCH` error. Test data updated to match. | Resolved |
| I | Medium | T149 architecture checks did not detect atomic publication, compatibility lock, or trusted scope defects (defect H). | Architecture script now inspects `PublishRaceWinner` order, `CatalogCompatibility` existence, lock acquisition, probe interface, and `PROVIDER_SCOPE_MISMATCH`. | Resolved |
| J | Medium | `Phase7ReviewCheck` lacked checks for atomic-race, compatibility lock, Latest probe, Rejected fixture, and trusted scope. | Added 8 new review checks covering all defects A–G. | Resolved |

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
Phase7ReviewCheck executes 24 checks, ArchitectureVerification executes 52 checks.

## Historical baseline reference

Previous corrective closure at `b6b2510820f5ab8f0af5569a2fc18b4ee4b2f892` (T151) covered canonical
validation, exact fixture equality, and provider recheck facts. This T150 review builds on that
work, adding atomic-race publication, compatibility lock, trusted scope, and extended T145
coverage.
