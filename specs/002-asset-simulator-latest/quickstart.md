# Quickstart: Validate Asset Simulator Latest

This is a validation guide for the planned R1/VS-01 slice. It does not claim implementation and
does not replace tasks.md.

## Prerequisites

- .NET SDK 10.0.300, Node 24.16.0 and approved PostgreSQL.
- Locked package sources and protected local bootstrap credential; no public download or container.
- Current R0 migration 0001 applied. R1 migrations are applied in documented order only after tasks
  and implementation approval.
- Constitution R0-only implementation wording amended through its governed process.

## Fast and Full checks

From the repository root:

    .\scripts\harness.ps1 -Mode Fast -Feature 002-asset-simulator-latest

Fast covers artifacts, policy, architecture and tests that need no database.

    .\scripts\harness.ps1 -Mode Full -Feature 002-asset-simulator-latest

Full additionally requires PostgreSQL/package-backed migration, constraints, transaction,
deduplication, lease, API/Worker and acceptance evidence. Until tasks.md exists, a missing-task
artifact result is expected and must not be reported as an end-to-end pass.

## Executable business journey

1. Seed fixed roles and inject a protected Administrator credential. Do not seed an Engineer scope.
2. Login as Administrator; verify session cookie, antiforgery token, and GET /me.
3. Administrator creates the root Site.
4. Administrator assigns the Engineer to the Site scope.
5. Login as Engineer; verify an Engineer without scope cannot create a Site and has no global bypass.
6. Engineer creates Draft Area, Draft Asset and Draft Measurement Point in the assigned Site.
7. Administrator activates the Site; Engineer or Administrator activates Area and Asset top-down.
8. Create or verify idempotent Metric/Unit seeds and compatibility.
9. Create a Catalog-owned Simulator Data Source and make it Active.
10. Create an immutable Acquisition configuration version.
11. Create and activate exactly one effective Catalog Mapping for the Draft Point.
12. Confirm the Active Mapping is configuration only and produces no Measurement yet.
13. Assign an eligible Active Data Owner and activate the Point.
14. Start the Simulator. Start must fail if Source, Mapping, Point or any ancestor is inactive.
15. Observe Accepted Measurements, stable identity, counters and the immutable Run configuration
    version.
16. Pause and cross Online -> Stale -> NoData; verify NoData is never numeric zero and last value
    remains distinct.
17. Resume; verify per-Run+Point deterministic continuation and recovery to Online.
18. Submit duplicate, older, equal-time, future-skew and out-of-range internal records; verify
    Duplicate/Accepted/Rejected outcomes, P-001/P-002/P-003 and no Latest regression.
19. Query Latest and Source Status as an in-scope Operator; query as out-of-scope users and verify
    no data leakage.
20. Attempt Asset decommission with an Active child Point: expect ACTIVE_CHILD_POINT, no cascade and
    no partial change. Stop/inactivate the source, explicitly handle the Point, then decommission.
21. Attempt Point decommission while its Run is Running: expect RUNNING_SIMULATOR. After explicit
    stop, decommission is terminal and triggers Source Status reconciliation.
22. Inspect AuditReview results for bootstrap, scope, configuration, mapping, Run controls, auth and
    decommission. Audit-only reference must not block Draft-unused Source/Mapping deletion; business
    dependency must return DEPENDENT_HISTORY.

## Phase checkpoints and expected criteria

| Phase | Independent checkpoint |
|---|---|
| 1 IAM/bootstrap | Admin-only root Site, cookie session, five roles, no pre-Site Engineer scope |
| 2 Catalog | idempotent seeds, compatibility, Source/Mapping lifecycle and overlap policy |
| 3 Draft Organization | scoped Draft hierarchy, top-down Site/Area/Asset activation, no cascade |
| 4 Simulator mapping | immutable configuration version, one effective Mapping, non-producing Draft Point |
| 5 Point activation | IAM/Catalog/Organization checks and specific failures |
| 6 Run/Worker | Start/Pause/Resume/Stop, lease/restart/counters, deterministic values |
| 7 Telemetry | identity, quality, duplicate, accepted/rejected, raw history and Latest |
| 8 Latest/Status | ordering, Online/Stale/NoData, administrative precedence/recovery |
| 9 API/Web | supported journey, scope-safe reads, errors/empty/blocked states, AuditReview |
| 10 Hardening | all 68 FRs, five stories and nine criteria with truthful blocker evidence |

SC-001 is the timed Admin bootstrap plus Engineer journey. SC-005 includes no-scope Engineer root
Site denial and cross-scope no-leakage. SC-009 verifies Active-child Asset rejection, no cascade,
atomicity and successful decommission audit.

## Test classification

- RUNNABLE_NOW: pure domain, session policy, authorization, generator, identity, contract and
  architecture tests.
- REQUIRES_APPROVED_POSTGRESQL: migrations, constraints, exclusion/overlap, atomic Latest, dedup,
  outbox/inbox, production-attempt checkpoint and leases.
- BLOCKED_BY_PACKAGE_POLICY: ORM/driver compilation if locked packages are unavailable.
- BLOCKED_BY_ENVIRONMENT: missing database/service/tool for API/Worker/Web smoke.
- REQUIRES_COMPANY_APPROVAL: CI, promotion and target deployment.

No alternate database is permitted and blocked evidence is never called complete.
