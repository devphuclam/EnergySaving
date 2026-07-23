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
(Accepted/Rejected/Duplicate), original classification, error code and timestamps. The primary
identity is `(simulator_run_id, point_id, source_sequence)`. Worker inserts a Pending attempt in
the same Acquisition transaction as state advancement, then calls Telemetry, then finalizes
idempotently. Crash recovery uses Duplicate detection to finalize without double-counting.

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

PCG32 with fixed multiplier `6364136223846793005`, increment `1442695040888963407`, unsigned 64-bit
overflow, `pcg_output_rxs_m_xs_64_32` output function. Initial state derived from seed, stable
Point ID, configuration ID/version and algorithm version. UUIDs normalized as canonical lowercase
dashed strings in UTF-8. Constant emits min=max without PRNG. Normal uses Box-Muller polar form with
two uint32 draws, open-interval `(0,1]` conversion, midpoint mean, range/6 sigma, cached spare, IEEE
754 float64 precision, 4-decimal rounding, deterministic clamping to bounds. State serialized as 25
bytes. Three golden vectors (Constant, first Normal, restart Normal) specified with exact inputs.
No platform-default Random, current-time seed, system randomness or third-party statistics dependency
is permitted.

Alternatives rejected: System.Random (not deterministic across platforms), third-party library
(violates restriction policy), rejection sampling without spare (unbounded worst-case draws).

### Decision: Fixed UUID namespace

`IUMP_NAMESPACE_UUID = 02e993bb-c767-5ff6-963f-530e1dfdff6b` derived from UUIDv5(DNS namespace,
"iump.idea-technology.com"). This literal constant is used for all deterministic UUIDv5 derivations
in the repository. The namespace and serialization version `V1` are fixed contract constants.

### Decision: Cross-module transaction and lock protocol

The composition root owns the unit-of-work boundary. Provider rows required for strict invariants are
locked in a deterministic global order: (1) IAM user, (2) Organization hierarchy, (3) Catalog
Metric/Unit/Source/Mapping, (4) Acquisition Run/dependency, (5) Telemetry identity/projection,
(6) Integration outbox. Isolation level REPEATABLE READ, `SELECT FOR UPDATE` in lock order, up to
3 retries with exponential backoff on serialization/deadlock, 2-second lock_timeout. Applied to
Point activation, Mapping activation, Simulator Start, Point decommission vs Start, Asset
decommission, Point decommission, canonical ingestion and Source/Mapping delete.

Alternatives rejected: distributed transaction (not available in single PostgreSQL database),
optimistic-only for strict invariants (retry overhead makes crash-recovery paths fragile),
cross-schema writes (violates module ownership).

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
both Idempotency-Key and If-Match required; stale version returns VERSION_CONFLICT. Login/logout/
query: no aggregate If-Match. All contracts updated to reflect this split.

### Decision: Session implementation details

Working defaults defined for cookie name (`.IUMP.Auth`), Secure/SameSite Lax/HttpOnly, idle 20m,
absolute 8h, 256-bit CSPRNG token, SHA-256 hash, rotation on login, logout revocation, antiforgery
cookie/header (`.IUMP.Xsrf`/`X-XSRF-TOKEN`), same-origin only, HTTPS with Development override,
persistent Data Protection keys under `%ProgramData%/IUMP/DataProtection-Keys/` with DPAPI, and
rate-limit 5 attempts per 15s window.

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

Use approved ASP.NET Core `PasswordHasher<T>` and a server-issued encrypted Secure/HttpOnly/SameSite
Lax cookie (`.IUMP.Auth`) backed by revocable `iam.user_session`. Every request validates Active
user, roles and scopes; Disable/revoke takes effect immediately. Logout clears/revokes the session,
expiry is idle plus absolute, state-changing cookie requests require antiforgery, and no token/hash
is returned or placed in a query string. Login errors are non-enumerating with bounded framework-backed
rate limiting. Bootstrap credentials are injected from protected environment state, never committed.

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
slice. Existing structures are reused; only minimal approved additive changes are possible. Logical
cross-schema IDs have no FK, are validated by versioned public contracts, carry snapshots in evidence,
and are checked by reconciliation. No module writes another schema. Audit receives committed events
through outbox/inbox; originating modules do not insert Audit rows into another schema transaction.

### Decision: Migration sequence update

Migrations are updated to include:
- IAM capability and user_capability tables (inserted after role/user but before scopes).
- Acquisition production-attempt checkpoint (inserted after Run and run-point-state tables).
- Extended run-point-state columns for pinned snapshot.
- Session/index additions in the IAM migration.
- Additive R0 outbox/job changes are included only when proven necessary; no recreation of existing
  tables.

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

All repair points have a documented decision. Unresolved questions: **0**. The constitution's
historical R0-only implementation restriction and missing PostgreSQL/package approvals are execution
gates, not product questions.
