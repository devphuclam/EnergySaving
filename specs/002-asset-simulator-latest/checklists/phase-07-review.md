# Phase 7 Standards and Specification Review — Concurrency-and-Scope Closure

- Baseline: `0710ba158e9616262a94120a3800988884a8d7c7`
- Review target: concurrency-and-scope corrective convergence for T131–T151 only.
- Review date: 2026-07-29.
- Evidence: T131–T135, T145, T149, T150 focused run; Fast harness; source-only migration and
  boundary checks. PostgreSQL execution remains blocked by missing approved tooling.

## Findings and resolution

| ID | Severity | Finding | Resolution / evidence | State |
|---|---|---|---|---|
| K | Critical | T132 error precedence — blank SiteId/AreaId returns PROVIDER_SCOPE_MISMATCH not PROVIDER_ID_MISSING; 2 runnable failures | Adopted precedence rule: blank SiteId/AreaId with nonblank TrustedSiteId/TrustedAreaId → PROVIDER_SCOPE_MISMATCH; AssetId/MetricId/UnitId blank → PROVIDER_ID_MISSING. Added 7 scope no-transaction evidence cases (TrustedSiteId blank, TrustedAreaId blank, Site mismatch, Area mismatch, mismatch+Point inactive, mismatch+Source inactive, mismatch+invalid version) all proving BeginCount=0, Rechecks=0, zero state. | Resolved |
| L | Critical | PublishRaceWinner existing-state branch checks Event equality by EventId only, not complete fields | Replaced `Events.Any(e => e.EventId == fixture.Event.EventId)` with `EventEqualsComplete(storedEvent, fixture.Event)` comparing all 18 fields including complete Before/After dictionaries. Added LatestAdvanced=false check: fixture.Latest must be null. | Resolved |
| M | Critical | No BeginCount on fake unit of work for scope no-transaction evidence | Added `BeginCount` to `FakeTelemetryRepositories`; `BeginRepeatableReadAsync` increments counter. | Resolved |
| N | Critical | No direct PublishRaceWinner existing-state tests | Added 6 direct tests: exact Accepted no-op, exact Rejected no-op, changed EventId conflict, changed After conflict, changed fingerprint conflict, changed SiteId conflict — all through StageRaceWinner exercising actual PublishRaceWinner existing-state branch. | Resolved |
| O | Critical | No direct race-winner slot conflict test through PublishRaceWinner | Added direct slot test: different MeasurementId, same Run+Point+sequence → RACE_WINNER_SLOT_CONFLICT; original winner unchanged. | Resolved |
| P | Critical | Valid Rejected invalid-fixture matrix uses Accepted-terminal mutation not Data(rejected:true) | Replaced with `TelemetryTestData.Terminal(request, TelemetryFinalClassification.Rejected)` which produces complete Rejected shape (MeasurementPersisted=false, PersistedMeasurementId=null, LatestAdvanced=null, RejectionCode nonnull). Each attaches Raw/Latest/Event independently → RACE_WINNER_FIXTURE_INVALID. | Resolved |
| Q | Critical | CommittedState getter does not deep-copy terminal via terminal.Copy() | Changed `new Dictionary<Guid, TelemetryTerminalResult>(currentTerminals)` to `currentTerminals.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Copy())`. Raw and Latest also deep-copied with `with { }`. | Resolved |
| R | High | No fingerprint mutation isolation test | Added test: mutate CommittedState snapshot terminal fingerprint byte[0] → re-read terminal → internal fingerprint unchanged. | Resolved |
| S | High | No event dictionary mutation isolation test | Added test: modify returned event After dictionary → re-read → original unchanged. | Resolved |
| T | High | No same-ID commit-time concurrency test | Added test: Tx A and Tx B both stage same MeasurementId; commit A, commit B → B throws TelemetryUniqueRaceException, B publishes zero state, A winner preserved. | Resolved |
| U | High | No same-slot commit-time concurrency test | Added test: Tx A and Tx B stage different IDs with same Run+Point+sequence; commit A, commit B → B throws, A winner preserved. | Resolved |
| V | High | No independent-slot no-lost-update test | Added test: Tx A and Tx B stage different valid slots; both commit → final state contains both complete publications, no lost update. | Resolved |
| W | Medium | Scope tests omit BeginCount, Rechecks, raw/Latest/event proof | Added all 7 scope cases with complete assertions: BeginCount=0, Rechecks=0, terminals=0, raw=0, Latest=0, events=0. | Resolved |
| X | Medium | T149 baseline hash and T151 reference use old f852/8261074a | Updated to 0710ba1; all three checkpoints updated. | Resolved |
| Y | Medium | Phase7ReviewCheck and ArchitectureVerification need updates | Phase7ReviewCheck updated for BeginCount, event equality, direct tests; T149 architecture checks updated for EventEqualsComplete and 0710ba1 baseline. | Resolved |

## Standards result

- Unresolved Critical: `0`
- Unresolved High: `0`
- Unresolved Medium: `0`
- Scope creep: `0`
- T146 remains `BLOCKED_BY_PACKAGE_POLICY`.
- T147 remains `BLOCKED_BY_PACKAGE_POLICY`.
- T148 remains `BLOCKED_BY_PACKAGE_POLICY_TRANSITIVE`.

## T150 decision

PASS — all review findings K–Y are resolved, with zero unresolved Critical or High findings.
Phase7ReviewCheck executes checks, ArchitectureVerification executes checks.

## Historical baselines

Previous corrective closures at `8261074a2c77f34a7988d4b9a0d04df5565d8deb` (concurrency-and-scope
closure), `f8521159802fd39732c4cfa24605aed912c18419` (atomic-evidence closure),
`d5c71ed42a45c6fee189c3a67580b0cf096c9bf6` (atomic-race and compatibility-lock),
and `b6b2510820f5ab8f0af5569a2fc18b4ee4b2f892` (exact-result). This T150 review is the
truth-and-concurrency closure at `0710ba158e9616262a94120a3800988884a8d7c7`.
