# Quickstart: Validate Asset Simulator Latest

This is a validation guide for the planned R1/VS-01 slice. It does not claim implementation and
does not replace tasks.md.

## Prerequisites

- .NET SDK 10.0.300, Node 24.16.0 and approved PostgreSQL.
- Locked package sources and protected local bootstrap credential; no public download or container.
- Current R0 migration 0001 applied. R1 migrations are applied in documented order only after tasks
  and implementation approval.

## Fast and Full checks

From the repository root:

    .\scripts\harness.ps1 -Mode Fast -Feature 002-asset-simulator-latest

Fast covers artifacts, policy, architecture and tests that need no database.

    .\scripts\harness.ps1 -Mode Full -Feature 002-asset-simulator-latest

Full additionally requires PostgreSQL/package-backed migration, constraints, transaction,
deduplication, lease, API/Worker and acceptance evidence. A targeted tasks repair is still required;
the current tasks artifact must not be treated as implementation-ready for these repaired paths.

## Executable business journey

### Foundation (database seed A)

1. Seed fixed roles, capability (`AUDIT_READ`), bootstrap Administrator identity, and deterministic
   Engineer/Operator/Manager/Viewer users (no Site scope). Do not seed an Engineer scope row.
2. Seed Metric/Unit records and compatibility.

### Bootstrap (application command B)

3. Login as Administrator; verify session cookie (`.IUMP.Auth`), antiforgery token
   (`.IUMP.Xsrf`/`X-XSRF-TOKEN`), and GET /me.
4. Administrator creates the root Site.
5. Administrator assigns Engineer, Operator, Manager and Viewer to the Site scope.
6. Optionally grant AUDIT_READ to Manager per seeded POC policy.
7. Login as Engineer; verify an Engineer without scope cannot create a Site and has no global bypass.

### API command idempotency

For one representative create and one update/lifecycle command:

- repeat the same caller/operation/key and canonical request; verify the stored original HTTP
  status/body/resource version/Location/ETag and original correlation ID replay after API restart;
- reuse the key with a different canonical field or If-Match; expect `IDEMPOTENCY_CONFLICT`;
- issue concurrent identical requests; verify one owner mutation/outbox event and either bounded
  winner replay or `IDEMPOTENCY_IN_PROGRESS` plus `Retry-After`;
- crash after committed Pending registration but before owner mutation; after the 30-second lease,
  retry the same request and verify optimistic reclaim without a duplicate owner mutation;
- verify Completed retention/cleanup at 24 hours and Pending reconciliation before removal;
- inspect evidence to confirm no raw request, cookie, authorization/antiforgery/session material,
  credential, hash, or secret is stored.

### Hierarchy and configuration

8. Engineer creates Draft Area, Draft Asset and Draft Measurement Point in the assigned Site.
9. Administrator activates the Site; Engineer or Administrator activates Area and Asset top-down.
10. Create or verify idempotent Metric/Unit seeds and compatibility.
11. Create a Catalog-owned Simulator Data Source and make it Active.
12. Create an immutable Acquisition configuration version (scenario, interval, min/max, seed).
13. Create and activate exactly one effective Catalog Mapping for the Draft Point.
14. Confirm the Active Mapping is configuration only and produces no Measurement yet.
15. Assign an eligible Active Data Owner and activate the Point (REPEATABLE READ, global lock order).

### Simulator Run production

16. Start the Simulator. Start must fail if Source, Mapping, Point or any ancestor is inactive.
17. Observe the production-attempt checkpoint (`simulator_production_attempt`) being created as
    Pending before each Telemetry call. Verify a new Constant or Normal slot uses current
    `next_source_sequence`, then advances it once after Pending insert; an existing Pending retry
    advances neither sequence nor Generated.
18. Observe Accepted Measurements with stable identity (UUIDv5 under `02e993bb-c767-5ff6-963f-530e1dfdff6b`),
    counters and the immutable Run configuration version.
19. Verify pinned Run-Point snapshot identity: inactivating the Mapping does not change already-
    reserved production-attempt measurement_id.
20. Pause and cross Online -> Stale -> NoData; verify NoData is never numeric zero and last value
    remains distinct.
21. Resume; verify per-Run+Point deterministic continuation and recovery to Online.
22. Stop the Run.

### Telemetry edge cases

23. Submit duplicate, older, equal-time, future-skew and out-of-range internal records; verify
    Duplicate/Accepted/Rejected outcomes, P-001/P-002/P-003 and no Latest regression.
24. Verify an Accepted terminal registry result commits with its raw Measurement, a Rejected
    terminal registry result commits without raw Measurement, and Duplicate returns the exact stored
    `original_result` for both classifications.

### Query and authorization

25. Query Latest and Source Status as an in-scope Operator; query as out-of-scope users and verify
    no data leakage.
26. Verify AUDIT_READ gating: Administrator sees audit events; user without AUDIT_READ assignment
    does not; Manager with AUDIT_READ assigned does.

### Decommission

27. Attempt Asset decommission with an Active child Point: expect ACTIVE_CHILD_POINT, no cascade and
    no partial change. Stop/inactivate the source, explicitly handle the Point, then decommission.
28. Attempt Point decommission while its Run is Running: expect RUNNING_SIMULATOR. After explicit
    stop, decommission is terminal and triggers Source Status reconciliation.
29. Inspect AuditReview results for bootstrap, scope, configuration, capability, mapping, Run
    controls, auth and decommission.

For one control event, trace the complete Audit path: owner mutation/event and outbox commit;
leased Worker dispatch; `Audit.v1` inbox claim; Audit consumer; unique `source_event_id` append;
inbox completion; filtered query. Crash before and after append to prove at-least-once delivery
produces at most one Audit row. Force retries through attempt 10, verify bounded scheduling and
redacted Failed poison state, correct the fault, then replay with unchanged event/correlation/
causation identities. Reconciliation must find expired leases and Published-without-Audit gaps.
Verify Administrator global query and `AUDIT_READ` **plus** Site/Area scope for every non-Admin;
global/unscoped events remain Administrator-only.

### Deletion rules

30. Audit-only reference must not block Draft-unused Source/Mapping deletion; business dependency
    must return DEPENDENT_HISTORY.

### Crash recovery

31. Simulate Worker crash mid-production: verify the Pending production-attempt row exists.
32. On restart, verify Worker loads the Pending authoritative payload, calls Telemetry with its
    persisted fields, and never invokes the generator, deserializes/advances PRNG, increments
    `next_source_sequence`, or increments Generated again.
33. Test both crash positions: before Telemetry, and after Telemetry terminal persistence but before
    Acquisition finalization. The latter receives Duplicate with exact stored original result.
    Finalization transitions once and increments exactly one Accepted/Rejected counter; replay is
    a no-op.

### Normative generator vectors

34. Execute the three literal vectors from `contracts/simulator.md`; do not substitute generated
    expectations.

| Vector | Attempt sequence | Output | Result state/spare | Stored next sequence |
|---|---:|---:|---|---:|
| Constant first slot | 0 | `12.5000` | initial state unchanged; spare invalid | 1 |
| Normal first slot | 0 | `11.6519` | `ed99faae39338fb74f8167f77e7b0514013f80c23bc5fbfb3f`; spare valid | 1 |
| Normal persisted-state restart | 1 | `17.9149` | `ed99faae39338fb74f8167f77e7b0514000000000000000000`; spare invalid | 2 |

All use seed 42, Point `11111111-2222-4333-8444-555555555555`, configuration
`aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee`, configuration version 7 and algorithm version 1. Verify
Constant consumes zero draws/state change but advances sequence; Vector 3 uses cached z1 and
consumes zero new draws.

## Phase checkpoints and expected criteria

| Phase | Independent checkpoint |
|---|---|
| 1 IAM/bootstrap | Admin-only root Site, cookie session, five roles, capability model, no pre-Site Engineer scope |
| 2 Catalog | idempotent seeds, compatibility, Source/Mapping lifecycle and overlap policy |
| 3 Draft Organization | scoped Draft hierarchy, top-down Site/Area/Asset activation, no cascade |
| 4 Simulator mapping | immutable configuration version, one effective Mapping, non-producing Draft Point |
| 5 Point activation | IAM/Catalog/Organization checks, REPEATABLE READ + global lock order, specific failures |
| 6 Run/Worker | Start/Pause/Resume/Stop, lease/restart/counters, deterministic values, production-attempt checkpoint, pinned snapshot |
| 7 Telemetry | immutable Accepted/Rejected result, exact Duplicate replay, Accepted raw history, Latest |
| 8 Latest/Status | ordering, Online/Stale/NoData, administrative precedence/recovery |
| 9 API/Web | supported journey, scope-safe reads, AUDIT_READ capability, errors/empty/blocked states |
| 10 Hardening | all 68 FRs, five stories and nine criteria with truthful blocker evidence |

SC-001 is the timed Admin bootstrap plus Engineer journey. SC-005 includes no-scope Engineer root
Site denial and cross-scope no-leakage. SC-006 requires capability-based audit review (AUDIT_READ).
SC-009 verifies Active-child Asset rejection, no cascade, atomicity and successful decommission audit.

## Test classification

- RUNNABLE_NOW: pure domain, session policy, authorization, generator, identity, contract and
  architecture tests, including every repository port, fake, and composition registration.
- BLOCKED_BY_DATABASE_ACCESS when approved PostgreSQL is unavailable: migrations, constraints,
  exclusion/overlap, atomic Latest, command registry/replay, outbox/inbox/Audit delivery,
  production-attempt checkpoint, repository adapters, leases, concurrency, and query plans.
- BLOCKED_BY_PACKAGE_POLICY: ORM/driver compilation if locked packages are unavailable.
- BLOCKED_BY_MISSING_TOOL: missing required local tool/service other than PostgreSQL for
  API/Worker/Web smoke.
- BLOCKED_BY_COMPANY_APPROVAL: CI, promotion and target deployment.

Evidence results are:

| Result | Meaning |
|---|---|
| PASS | Behavior executed and the expected result was verified with fresh evidence |
| FAIL | Executable verification ran and failed |
| BLOCKED | Runnable artifact/test source was produced where possible; exact external dependency, check ID, blocker ID, classification, and evidence are recorded; verification could not execute; behavior is not passing |
| NOT_RUN | The task/check has not been attempted |

A phase may close for planning/development progression with BLOCKED evidence only if every
RUNNABLE_NOW item is PASS, every blocker is external and classified, no runnable dependent requires
the unavailable behavior, and the checkpoint states the end-to-end capability remains incomplete.
FAIL or runnable NOT_RUN prevents progression. Full/release cannot PASS while any mandatory
BLOCKED or NOT_RUN remains; the harness result is nonzero (blocked/not-run exit 20). No alternate
database is permitted.
