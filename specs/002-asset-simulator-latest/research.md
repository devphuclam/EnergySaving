# Research: Asset Simulator Latest (Targeted Repair)

**Scope**: R1 / VS-01 only | **Authority**: updated `spec.md` and its Clarifications

## Findings and decisions

### Decision: Administrator-rooted bootstrap

The root Site is created by Administrator only. The Administrator then assigns an Engineer Site
scope. The Engineer can create/manage lower hierarchy, Catalog-owned Source/Mapping and Acquisition
Simulator within that scope. An Engineer without a Site scope has no global bypass and receives a
specific permission denial for root Site creation. Seeds create fixed roles and a bootstrap Admin
identity first; they do not insert a scope row referencing a Site that does not exist.

Alternatives rejected: pre-seeding Engineer scope to a future Site (invalid logical reference),
granting Engineer a global bootstrap exception, or allowing UI claims to override server scope.

### Decision: Catalog owns Source and Mapping

Catalog owns Data Source, Source-Point Mapping, lifecycle, effective periods, overlap protection and
mapping eligibility. Organization Point activation synchronously asks IAM for Data Owner eligibility,
Catalog for Metric/Unit compatibility and exactly one Active Mapping, and Organization for its own
ancestor/interval checks. Organization never asks Acquisition whether a Mapping exists.

Alternatives rejected: placing Mapping in Acquisition (contradicts authoritative module ownership),
or direct cross-schema writes.

### Decision: Correct dependency order

The design uses ten checkpoints: IAM/bootstrap; Catalog primitives; Draft Organization; Simulator
configuration/mapping; Point activation; Run/Worker; canonical Telemetry; Latest/Health; API/Web;
acceptance hardening. A Draft Point may have an Active non-producing Mapping, eliminating the former
activation circularity.

### Decision: Measurement identity ownership

Acquisition generates the stable Simulator Measurement ID before calling Telemetry. The
generated-measurement request sent to Telemetry MUST contain `measurement_id` plus all identity,
value and provenance fields. Telemetry MUST NOT generate a replacement Measurement ID. Telemetry
validates that `measurement_id` is present, correctly formed (UUIDv5 under the repository namespace),
and unique. Duplicate requests return the original stable classification and Measurement outcome.
The namespace UUID is `02e993bb-c767-5ff6-963f-530e1dfdff6b` (UUIDv5 of DNS namespace +
"iump.idea-technology.com").

Alternatives rejected: Telemetry generating its own measurement ID (would break deterministic retry
identity and crash recovery), Acquisition generating a random ID (would lose deterministic
reproducibility).

### Decision: Production-attempt checkpoint

Acquisition owns `acquisition.simulator_production_attempt` with fields for run, point, sequence,
measurement ID, mapping/config/algorithm snapshots, status (Pending/Completed), Telemetry outcome
(Accepted/Rejected/Duplicate), final classification, persisted time/value/unit and safe
producer/correlation/lineage references, error code and timestamps. The primary
identity is `(simulator_run_id, point_id, source_sequence)`. Worker inserts a Pending attempt in
the same Acquisition transaction as one new-slot state/sequence/Generated advancement, then calls
Telemetry, then finalizes the attempt and Accepted/Rejected counter atomically.

Before invoking the generator, Worker loads any existing Pending attempt. Pending is the
authoritative retry payload: reuse its persisted identity, snapshots, time, value, unit and
provenance; invoke no generator; deserialize/advance no PRNG state; change neither
`next_source_sequence` nor Generated. A new reservation uses
`source_sequence = next_source_sequence`; after the Pending insert succeeds,
`next_source_sequence = source_sequence + 1`. Constant and Normal both reserve and advance exactly
one slot. Pause/Resume continues; new Start creates new per-Point state at zero.

Alternatives rejected: in-memory checkpoint (lost on crash), letting Telemetry own the checkpoint
(violates module ownership), no checkpoint at all (crash between state advance and Telemetry
persistence would lose a value and break sequence continuity).

### Decision: Run-point input snapshot

`simulator_run_point_state` pins the exact identities at Start: `point_id`, `point_version_at_start`,
`mapping_id`, `mapping_version`, `metric_id`, `unit_id`, `unit_code`, `source_version`,
`next_source_sequence`, PCG32 state and `next_due_at`. Changing, superseding or inactivating a
Mapping MUST NOT change the identity of an already reserved production attempt. Future production
stops safely when an owner state change is detected; the Run faults and does not silently switch
Mapping. A replacement Mapping requires an explicit new Start/new Run.

### Decision: Normative deterministic algorithm (IUMP-DETERMINISTIC-V1)

`contracts/simulator.md` is the single normative definition. Canonical seed material is exact UTF-8
over seed-as-16-hex, lowercase dashed Point/configuration UUIDs and decimal configuration/algorithm
versions. FNV-1a-64 mixing uses literal offset/prime and unsigned overflow. Standard PCG seeding,
state transition, RXS-M-XS output multiplier/projection and every intermediate truncation are
literal pseudocode. State/spare serialization is 25-byte little-endian.

Constant emits min=max with no draws/state change but advances the reserved source sequence. A fresh
standard trigonometric Box-Muller pair consumes two draws and returns z0; the next value may consume
cached z1 with zero draws. Computation is IEEE 754 float64; one bounding policy applies: ties-to-even
round to four decimals, then clamp. The three literal vectors use attempt sequences `0,0,1`, outputs
`12.5000,11.6519,17.9149`, and stored next sequences `1,1,2`, with exact initial/result state hex
and spare values in the contract.

Alternatives rejected: System.Random (not deterministic across platforms), third-party library
(violates restriction policy), implementation-chosen hash/seeding or expected vectors (not
portable), and rejection sampling without spare (unbounded worst-case draws).

### Decision: Stable Telemetry terminal outcome registry

Telemetry extends `telemetry.measurement_identity` as its immutable terminal-result registry,
preserving Telemetry schema ownership and the existing global dedup entity. It stores request
identity/fingerprint, Accepted/Rejected final classification, Measurement-persisted flag/reference,
quality/reason/rejection, Latest result, completion time and safe correlation/lineage references.
Accepted result and raw Measurement commit atomically. An identity-addressable Rejected result
commits atomically without a raw Measurement. Duplicate returns the exact stored original result;
Rejected is never reconstructed from raw history. Same ID with a different fingerprint is an
idempotency conflict.

Trusted-producer and well-formed/recomputed Measurement-ID checks happen before identity reservation.
An immutable non-locking registry preflight may return Duplicate; new results retain the established
Organization -> Catalog -> Telemetry -> Integration lock order. Acquisition applies stored original
classification exactly once during the first Pending -> Completed transition.

Alternatives rejected: reconstructing Rejected from absent raw history, storing the registry in
Acquisition (violates ownership), mutable/in-progress registry rows, or revalidating changed owner
state before returning an already terminal Duplicate.

### Decision: Fixed UUID namespace

`IUMP_NAMESPACE_UUID = 02e993bb-c767-5ff6-963f-530e1dfdff6b` derived from UUIDv5(DNS namespace,
"iump.idea-technology.com"). This literal constant is used for all deterministic UUIDv5 derivations
in the repository. The namespace and serialization version `V1` are fixed contract constants.

### Decision: Cross-module transaction and lock protocol

The composition root owns the unit-of-work boundary. Provider rows required for strict invariants are
locked in a deterministic global order: IAM → Organization → Catalog → Acquisition → Telemetry →
Integration. Isolation level REPEATABLE READ, `SELECT FOR UPDATE` in the exact flow order. Up to
3 retries with exponential backoff on serialization/deadlock (50ms, 150ms, 450ms). After exhaustion,
return HTTP 503 with `TRANSIENT_DATABASE_CONFLICT` (never `PRECONDITION_FAILED`).
`PRECONDITION_FAILED` is reserved for business-state failures only. Each applied flow
(Point activation, Mapping activation, Simulator Start, Point decommission, Asset decommission,
Telemetry ingestion, Source/Mapping hard delete) follows its specific required order documented in
`contracts/README.md`. `lock_timeout` is 2 seconds.

Alternatives rejected: distributed transaction (not available in single PostgreSQL database),
optimistic-only for strict invariants (retry overhead makes crash-recovery paths fragile),
cross-schema writes (violates module ownership), PRECONDITION_FAILED for database contention
(confuses infrastructure and business errors).

### Decision: POC seed and bootstrap model

Split into (A) database foundation seed: five fixed roles; bootstrap Administrator identity;
deterministic Engineer, Operator, Manager and Viewer users without Site scope; Metric/Unit catalog
records. (B) Post-Site POC fixture: authenticated Administrator creates/locates the test Site;
assigns users to Site scope; optionally grants AUDIT_READ to Manager; uses application/IAM commands;
idempotent; disabled outside development/POC. Migration 0012 does not claim to create impossible
pre-Site scope rows.

Alternatives rejected: single monolithic seed with pre-Site scope rows (creates invalid FK reference),
manual step without fixture (unreliable for E2E), direct cross-schema SQL in fixture (violates
module ownership).

### Decision: AuditReview capability model

`iam.capability` with `AUDIT_READ` code seeded once. `iam.user_capability` assigns capabilities to
users. Administrator has implicit `AUDIT_READ` without a row. Viewer does not receive it
automatically. Manager does not receive it automatically unless explicitly chosen by seeded POC
policy. Data Owner never gains it implicitly. No full permission-management UI; minimal
Administrator assign/revoke commands suffice for POC.

Alternatives rejected: role-based (`AuditReview` as a sixth role — contradicts spec which says
Reviewer is not a base role), no capability model at all (AUDIT_READ would be ungovernable),
permission-based approach (too complex for VS-01).

### Decision: API concurrency semantics

Create: Idempotency-Key required, no If-Match (no aggregate version exists). Update/lifecycle/delete:
both Idempotency-Key and If-Match required; stale version returns VERSION_CONFLICT. Login: neither
If-Match nor Idempotency-Key. Logout: no If-Match, antiforgery required. Query: neither If-Match
nor Idempotency-Key. All contracts updated to reflect this split.

### Decision: Persistent API command-idempotency registry

Integration owns `integration.command_idempotency` and `ICommandIdempotencyStore`. Its unique
identity is caller user + versioned operation code + Idempotency-Key. A SHA-256 fingerprint uses a
versioned, typed, length-prefixed canonical encoding of caller, operation, target, If-Match version,
and operation-specific command fields. It excludes transport order/whitespace, correlation/retry
metadata, credentials, cookies, session/antiforgery material, and the key itself.

A same-fingerprint Completed request replays the exact original HTTP status/result/resource
version. A live Pending duplicate waits briefly then returns `IDEMPOTENCY_IN_PROGRESS`; an expired
Pending row can be reclaimed optimistically by the same fingerprint. A different fingerprint
returns `IDEMPOTENCY_CONFLICT`. Registration is a short transaction so Pending survives a crash;
the owner mutation, outbox, and Completed result share the host-coordinated transaction with
Integration last. Completed retention is 24 hours and the Pending lease is 30 seconds.

Alternatives rejected: no persistent record (cannot replay after restart/concurrency),
`integration.inbox_message` (consumer-event identity has different ownership and key), Acquisition
production attempts (generated-slot checkpoint), Telemetry measurement identity (ingestion result),
and raw-request storage (secret/canonicalization risk).

### Decision: Session implementation details

Working defaults defined for cookie name (`.IUMP.Auth`), opaque 256-bit CSPRNG token (not an ASP.NET
encrypted identity ticket), SHA-256 hash stored in `user_session`, Secure/SameSite Lax/HttpOnly,
idle 20m, absolute 8h, new opaque token after login (old session remains valid — multiple sessions
allowed), logout revokes current session by `revoked_at`, revocation is `revoked_at IS NOT NULL`
(idle/absolute expiry or Disabled user also invalid). Multiple sessions per user. Administrator
revoke-all sets `revoked_at` on all sessions. Antiforgery cookie/header (`.IUMP.Xsrf`/
`X-XSRF-TOKEN`), same-origin only, HTTPS with Development override. Data Protection keys in a
pre-provisioned directory writable by the service account (no elevation). Development may use
`%LOCALAPPDATA%/IUMP/DataProtection-Keys/`. Missing required key-storage provisioning/approval is
BLOCKED_BY_COMPANY_APPROVAL.
Rate-limit 5 attempts per 15s window.

### Decision: Immutable configuration versions

Acquisition has a configuration head plus immutable version rows. A Run stores the exact version used.
Editing creates a new version for future Starts; Running, Paused and historical Runs are unchanged.
This avoids a mutable row whose historical meaning changes.

### Decision: Reproducible Normal algorithm

(Consolidated into IUMP-DETERMINISTIC-V1 normative specification above.)

### Decision: Stable Measurement identity

UUIDv5-style SHA-1 name derivation under the repository namespace UUID
(`02e993bb-c767-5ff6-963f-530e1dfdff6b`) over canonical UTF-8 bytes:
`IUMP:SIMULATOR:V1|source_id|run_id|point_id|mapping_id|source_sequence|algorithm_version`.
Lowercase canonical UUIDs, decimal sequence and literal separators are normative. Time is excluded.
Retries of the same slot share identity; different Run/Point/Mapping/sequence do not. UUID namespace
collision assumptions are tested as a contract.

### Decision: Local authentication/session

Use approved ASP.NET Core `PasswordHasher<T>` and an opaque 256-bit CSPRNG session token in a
Secure/HttpOnly/SameSite Lax cookie (`.IUMP.Auth`). The cookie is not an ASP.NET encrypted identity
ticket; it contains the raw opaque token. The server stores SHA-256(token) in revocable
`iam.user_session`. Login does not use Idempotency-Key: each login creates a new independent session.
Multiple sessions per user are allowed. Logout revokes only the current session; Administrator
revoke-all revokes all sessions for a user. Every request validates Active user, roles and scopes;
Disabled user status takes effect immediately. State-changing cookie requests require antiforgery.
No token appears in response JSON or query strings. Login errors are non-enumerating with bounded
framework-backed rate limiting. Bootstrap credentials are injected from protected environment state,
never committed.

### Decision: Five roles and audit review capability

Base roles are exactly Administrator, Engineer, Operator, Manager and Viewer. Manager and Viewer are
distinct. `AuditReview` is governed by the capability model (`iam.capability`/`iam.user_capability`),
not a sixth role and not automatic for Viewer. Data Owner assignment grants no audit-review or
elevated permission.

### Decision: Decommission and delete

Asset decommission fails atomically while an Active child Point exists; it never cascades. Point
decommission fails while a mapped Simulator Run is Running, requires explicit stop/inactivation,
triggers Health reconciliation and is terminal. An Audit snapshot alone does not block deleting a
Draft-unused Source/Mapping; Run, Mapping use, Measurement, projection, scheduled job or other
business dependency does. Audit remains append-only with no restrictive target FK.

### Decision: R0 infrastructure and cross-schema references

R0 supplies only `integration.outbox_event`, `integration.inbox_message` and `operations.job` for this
slice. Existing structures are reused. R1 migration 0011 adds the separate required
`integration.command_idempotency` table and nullable inbox lease/retry fields needed for crash-safe
Audit consumption; it does not recreate R0 tables. Logical
cross-schema IDs have no FK, are validated by versioned public contracts, carry snapshots in evidence,
and are checked by reconciliation. No module writes another schema. Audit receives committed events
through outbox/inbox; originating modules do not insert Audit rows into another schema transaction.

### Decision: Complete Audit delivery

The mandatory path is owner command/event, transactional outbox, leased Worker dispatch, Audit
inbox claim/dedup, `IAuditEventConsumer`, append-only Audit repository, filtered Audit query
repository/service, then authorized API/UI. Audit append and inbox completion share a transaction
with Integration last. Inbox identity plus unique Audit source-event ID provides at-most-one Audit
row per event for the Audit consumer over at-least-once delivery.

The dispatcher/inbox/Audit adapters implement 30-second leases, maximum 10 attempts, bounded
backoff, redacted Failed poison state, reconciliation, and operator-authorized identity-preserving
replay. `operations.job` schedules wakeups/reconciliation and is not a second event store.
Correlation, causation, and source event IDs survive every retry. Administrator queries globally;
another active user needs both `AUDIT_READ` and applicable Site/Area scope.

Alternatives rejected: synchronous producer writes to Audit (cross-schema ownership violation),
payload construction as completion (no durable/queryable evidence), endpoint without consumer or
repositories, and best-effort delivery without leases/reconciliation.

### Decision: Runtime persistence adapter inventory

IAM, Organization, Catalog, Acquisition, Telemetry, Audit, Integration, and Operations each require
public repository ports, provider-owned PostgreSQL adapters, host composition registration,
transaction responsibility, and real-PostgreSQL verification as inventoried in
`contracts/persistence-adapters.md`. SQL migration files create storage and tests verify behavior;
neither implements the runtime adapter.

### Decision: Execution evidence semantics

PASS means executed and verified. FAIL means verification executed and failed. BLOCKED means
runnable source/evidence was produced where possible, the unavailable external dependency and
classification are recorded, execution could not occur, and the behavior is not passing. NOT_RUN
means unattempted. A phase may close only for incomplete planning/development progression when all
runnable work passes, blockers are external/classified, no runnable dependent remains, and the
checkpoint explicitly says the capability is incomplete. Mandatory Full/release gates cannot pass
with BLOCKED or NOT_RUN evidence.

### Decision: Migration sequence update

Migrations are updated to include:
- IAM capability and user_capability tables (inserted after role/user but before scopes).
- Acquisition production-attempt checkpoint (inserted after Run and run-point-state tables).
- Extended run-point-state columns for pinned snapshot.
- Session/index additions in the IAM migration.
- Required 0011 Integration expand creates command idempotency and adds inbox recovery metadata;
  existing outbox/inbox/job tables are not recreated.

## Controlled source differences

- The updated feature has 68 FRs and 9 SCs; all earlier planning counts are obsolete.
- DOC-07 broader Simulator scenarios and replay are deferred because the clarified feature permits
  Constant and Normal only.
- DOC-07/R0 assumptions about IAM, Catalog and Site seeds are not true of the repository baseline;
  R1 supplies minimal IAM/Catalog and the Admin bootstrap journey without recreating R0.
- DOC-06 physical names are respected: `iam.user_scope`, `telemetry.point_source_status`,
  `integration.outbox_event`, `integration.inbox_message`, `operations.job`. VS-01 compatibility,
  version, run-point-state, production-attempt and lifecycle-history tables are explicit extensions.
- DOC-04 grace-period wording is narrowed to the clarified total `no_data_after_seconds` threshold.

## Clarification status

All repair points have a documented decision. Unresolved questions: **0**. This repair does not
request a constitution amendment. PostgreSQL/package approvals and targeted task repair are
execution gates, not product questions.

The repaired planning artifacts contain no remaining command-idempotency, Audit-delivery,
persistence-adapter, evidence-state, sequence, retry, outcome-persistence, or generator
contradiction. Requirements coverage is 68/68, stories 5/5 and success criteria 9/9.
