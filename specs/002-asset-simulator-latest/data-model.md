# Data Model: Asset Simulator Latest

**Feature**: `002-asset-simulator-latest` | **Database**: PostgreSQL | **Time**: UTC `timestamptz`

## Storage conventions

- UUID primary keys, normalized uppercase codes, `version bigint` optimistic concurrency, and
  created/updated actor timestamps apply to mutable aggregates.
- Effective periods are half-open `[effective_from,effective_to)`.
- Cross-module IDs are logical references with no cross-schema foreign keys unless separately
  architecture-reviewed. The owning contract validates existence, status, scope and version.
- Audit stores immutable subject/type/value snapshots and deliberately has no restrictive FK to a
  deleted Draft object.

## Entity obligations

| Entity | Owner/schema | PK / business key | Required fields, constraints and indexes | Concurrency/effective dates | Delete/decommission/audit |
|---|---|---|---|---|---|
| `user_account` | IAM/`iam` | `user_id` / normalized username | username, credential hash, Active/Disabled; unique username; user/status index | `version`; n/a | disable after reference; auth audit |
| `role` | IAM/`iam` | `role_id` / fixed code | exactly Administrator, Engineer, Operator, Manager, Viewer | seed version; n/a | no runtime delete; seed audit |
| `user_role` | IAM/`iam` | `(user_id,role_id)` | composite unique and lookup indexes | assignment version; n/a | revoke and audit |
| `user_scope` | IAM/`iam` | `(user_id,scope_type,scope_id)` | `scope_type` SITE/AREA, logical `scope_id`, access level, effective dates; unique/indexed | assignment version; `[from,to)` | revoke and audit; Site must exist before Engineer row |
| `user_session` | IAM/`iam` | `session_id` / session hash | hashed server session, user, issue/idle/absolute expiry, revocation, status; unique hash/expiry indexes | immutable issue + revocation version | logout/expiry/revoke; no credential exposure |
| `capability` | IAM/`iam` | `capability_id` / code | fixed seeded code: `AUDIT_READ`; name; unique code | seed version; n/a | no runtime delete |
| `user_capability` | IAM/`iam` | `(user_id,capability_id)` / active assignment | user, capability, assigned_by, assigned_at, revoked_at nullable, version; unique active index where revoked_at IS NULL | assignment version; n/a | revoke set revoked_at, audit |
| `metric` | Catalog/`catalog` | `metric_id` / code | code/name/status; unique code/status index | `version`; n/a | inactivate when safe; audit |
| `unit` | Catalog/`catalog` | `unit_id` / code | code/symbol/status; unique code lookup | `version`; n/a | inactivate when safe; audit |
| `metric_unit_compatibility` | Catalog/`catalog` | `(metric_id,unit_id)` | unique pair; at most one canonical unit per Metric; pair indexes | assignment version; n/a | remove only if unused; audit |
| `data_source` | Catalog/`catalog` | `source_id` / code | Simulator type, Draft/Active/Suspended/Decommissioned; unique code/status index | `version`; n/a | Draft-unused delete; operational history blocks; audit snapshot |
| `source_point_mapping` | Catalog/`catalog` | `mapping_id` / Point + period | source, logical Point, lifecycle, effective dates; overlap exclusion/transaction lock and source/Point/time indexes | `version`; `[from,to)` | Draft-unused delete; otherwise Inactive/Superseded; audit snapshot |
| `site` | Organization/`organization` | `site_id` / global code | code/name/timezone/status Draft/Active/Inactive; unique code | `version`; n/a | root commands Admin-only; inactivate, not destructive delete; audit |
| `area` | Organization/`organization` | `area_id` / Site+code | logical Site, code/name/status; unique `(site,normalized_code)` | `version`; n/a | child/history guards; audit |
| `asset` | Organization/`organization` | `asset_id` / Area+code | logical Area/Site, code/name/status incl Decommissioned; unique `(area,normalized_code)` | `version`; n/a | decommission terminal, atomic, no Active Point child; audit |
| `measurement_point` | Organization/`organization` | `point_id` / Site+code | logical hierarchy/catalog/user IDs, intervals, status; unique `(site,normalized_code)` forever; interval checks; parent/status indexes | `version`; n/a | Point code never reused; decommission terminal, no Running Run; audit |
| `point_lifecycle_history` | Organization/`organization` | history ID / Point+version | append-only old/new status, actor, reason; Point/time index | immutable; occurred-at | never delete; lifecycle audit |
| `simulator_configuration` | Acquisition/`acquisition` | config ID / source aggregate | source ID, current version, metadata; source/current index | `version`; head only | retain; audit version change |
| `simulator_configuration_version` | Acquisition/`acquisition` | `(config_id,configuration_version)` | scenario, interval, min/max, seed, `algorithm_id`, `algorithm_version`; Constant min=max, Normal min<max | immutable; version identity | never delete if Run references; audit |
| `simulator_run` | Acquisition/`acquisition` | `run_id` / source+run | source/config/version, Running/Paused/Stopped, counters, error, lease; status/lease indexes | `version`; run lifetime | retain after production; stop terminal; audit |
| `simulator_run_point_state` | Acquisition/`acquisition` | `(run_id,point_id)` | pinned snapshot plus zero-based `next_source_sequence`, serialized PCG/spare state and next_due_at | version; Run lifetime | retain with Run; operational evidence |
| `simulator_production_attempt` | Acquisition/`acquisition` | `(run_id,point_id,source_sequence)` | authoritative persisted Telemetry payload; identity/mapping/config/algorithm/value/provenance snapshots; Pending/Completed; response disposition and final classification | immutable payload + versioned terminal fields; unique sequence and measurement_id | never delete |
| `measurement_identity` | Telemetry/`telemetry` | `measurement_id` / terminal dedup identity | immutable request identity/fingerprint plus Accepted/Rejected terminal result, persistence flag/reference, quality/reason/rejection, Latest result, completed time and safe lineage | immutable; unique Run+Point+sequence | retain at least through retry/Measurement retention |
| `measurement_raw` | Telemetry/`telemetry` | `measurement_id` / same | point/source/mapping, source/received/processing time, value/unit/quality/reason/lineage; time partition + Point/time index | immutable; source time | retention policy; trace |
| `point_latest` | Telemetry/`telemetry` | `point_id` / same | value/unit/timestamps/quality/reason and full ordering tuple; Point PK | atomic compare/version; current | projection rebuild/update; trace |
| `point_source_status` | Telemetry/`telemetry` | `(point_id,source_id)` / same | run status, last received, counters, Online/Stale/NoData/Suspended/Decommissioned; state/evaluation indexes | atomic version; evaluated-at | projection update; real-change event |
| `audit_event` | Audit/`audit` | `audit_event_id` / unique `source_event_id` | event type/version/producer/aggregate, actor snapshot, object/action, redacted before/after JSON, summary, correlation/causation, scope snapshots; query indexes | immutable; occurred/recorded-at | append-only database permissions |
| `command_idempotency` | Integration/`integration` | `command_idempotency_id` / `(caller_user_id,operation_code,idempotency_key)` | 32-byte request fingerprint, target scope/aggregate, Pending/Completed, lease/attempt, original HTTP/safe result or reference, resource ID/version, error code, timestamps/expiry; unique identity and status-shape checks; Pending/expiry/target indexes | optimistic `version`; 30s Pending lease; 24h Completed retention | cleanup after expiry; never stores raw request, credentials, tokens, or secrets |
| `outbox_event` | Integration/`integration` | existing R0 `event_id` | reuse existing payload/status/attempt/lease columns; add only approved nullable metadata | existing claim/lease | operations retention |
| `inbox_message` | Integration/`integration` | existing `(consumer_name,event_id)` | reuse hash/status/error/dedup; 0011 adds lease owner/until, attempts, next-attempt and claim index | additive claim/recovery state | operations retention |
| `job` | Operations/`operations` | existing job ID / `(job_type,idempotency_key)` | existing payload/status/availability/lease/attempt fields | existing lease/version | retain execution history |

`metric_unit_compatibility`, `simulator_configuration*`, `simulator_run_point_state`,
`simulator_production_attempt`, `point_lifecycle_history`, `capability`, `user_capability`, and
`command_idempotency`
are explicit VS-01 additions where DOC-06 does not provide an equivalent; they do not replace R0
structures.

### Command-idempotency invariants

The unique command scope is `(caller_user_id, operation_code, idempotency_key)`, without a
cross-schema FK to IAM. `request_fingerprint` is exactly 32 bytes and follows the canonical V1
algorithm in `contracts/README.md`. `status` is only Pending or Completed. Pending requires no
original response and may have `pending_owner`, `pending_until`, and nonnegative `attempt_count`.
Completed requires `original_http_status` in 100..599, a safe result payload or stable reference,
`completed_at`, cleared lease fields, and positive `version`. `created_at <= completed_at <=
expires_at` where applicable.

Required indexes are the unique command scope, partial `(pending_until,created_at)` for Pending
recovery, `expires_at` for cleanup, and partial target aggregate lookup. Same fingerprint replays
the stored status/result; a different fingerprint conflicts. The short Pending registration and
the owner-mutation/outbox/Completed transaction boundaries are defined in
`contracts/integration.md`.

## IAM and session model

`user_scope.scope_id` is a logical Site/Area reference. IAM asks Organization to validate existence,
ancestry and current state; it does not write Organization. Bootstrap is ordered: fixed roles,
Administrator credential injected from protected environment, Administrator creates Site, then the
Administrator assigns Engineer scope. Deterministic seeds never insert a scope to a nonexistent Site.

ASP.NET Core `PasswordHasher<T>` stores framework-versioned hashes. On login, a 256-bit opaque
random session token is generated, SHA-256(token) is stored in `iam.user_session`, and the raw token
is placed in a Secure/HttpOnly/SameSite Lax cookie (`.IUMP.Auth`). The cookie is not an ASP.NET
encrypted identity ticket; it contains the raw opaque token. The server resolves `user_session` and
Active user/roles/scopes on every request by hashing the cookie value. State-changing cookie requests
require antiforgery (`.IUMP.Xsrf` cookie, `X-XSRF-TOKEN` header).
Logout/revocation, expiry, Disabled status and scope revocation invalidate access. The raw token
never appears in response JSON or query strings. Successful/
failed authentication is auditable with non-enumerating errors and a bounded framework-backed rate
limit (5 attempts per 15-second window). Data Protection keys (required for antiforgery and
framework-protected values, not for reconstructing the session token) reside in a pre-provisioned
directory writable by the API service account. Development may use an approved user-writable local
path (e.g. `%LOCALAPPDATA%/IUMP/DataProtection-Keys/`). Unavailable storage is
BLOCKED_BY_COMPANY_APPROVAL when required provisioning/approval is absent. The application must not
request elevation or alter system ACLs.

Base roles are exactly Administrator, Engineer, Operator, Manager and Viewer. `AUDIT_READ` is a
capability seeded into `iam.capability` and assigned via `iam.user_capability`. Administrator has
implicit `AUDIT_READ` without a row. Viewer does not receive `AUDIT_READ` automatically. Manager
does not receive it automatically unless explicitly chosen by the seeded POC policy. Data Owner
assignment grants no permission.

## Catalog and Organization rules

Administrator alone creates, updates, activates and inactivates root Site. Engineer scope is required
for all lower hierarchy, Catalog Source/Mapping and Simulator mutations. Area/Asset/Point Draft rows
may be prepared beneath an existing Draft or Active parent. Activation is top-down. Point activation
asks IAM for Data Owner eligibility, Catalog for Metric/Unit compatibility and exactly one effective
Active Mapping, and Organization for ancestors/intervals. It never asks Acquisition for Mapping.

Point activation uses a REPEATABLE READ transaction with row locks in global order (IAM user,
Organization Point/ancestor, Catalog Metric/Unit/Mapping). Activation MUST NOT commit if Data Owner,
Metric, Unit or Mapping can change between validation and commit — the transaction rechecks provider
version snapshots before commit and rolls back on mismatch.

Asset decommission is rejected atomically if any child Point is Active; no child cascade occurs.
Point decommission asks Acquisition for Running Simulator status (with Organization Point locked
first, then Acquisition Run locked in global order), fails while any mapped Run is Running, requires
explicit stop/inactivation, triggers Health reconciliation, and is terminal.

## Simulator version and algorithm

`simulator_configuration` is an aggregate head. Every edit inserts an immutable
`simulator_configuration_version`; a Run stores `simulator_configuration_id` and exact
`configuration_version`. Running/Paused/historical Runs never change when a future version is made.

Algorithm contract: `IUMP-DETERMINISTIC-V1`, with fixed `algorithm_version`. The normative
initialization, FNV-1a-64 mixing, PCG transition/RXS-M-XS 32-bit projection, UTF-8/UUID
normalization, overflow/truncation, standard trigonometric Box-Muller, binary64 spare
serialization and round-then-clamp policy are defined only in `contracts/simulator.md`.
Constant consumes no draws/state but reserves a sequence slot. A fresh Normal pair consumes two
draws and its cached next value consumes zero. The same contract contains the three literal golden
vectors: attempt sequences `0,0,1`, outputs `12.5000,11.6519,17.9149`, and stored next sequences
`1,1,2`. No platform-default Random, third-party statistics package, system randomness or
current-time seed is allowed.

Simulator Measurement identity is UUIDv5 under the repository namespace UUID
`02e993bb-c767-5ff6-963f-530e1dfdff6b` (derived from DNS namespace + "iump.idea-technology.com")
using UTF-8 canonical bytes:
`IUMP:SIMULATOR:V1|source_id|run_id|point_id|mapping_id|source_sequence|algorithm_version`.
UUIDs are lowercase canonical strings with dashes, sequence is decimal, separators are literal `|`,
and no received/processing time is included. Same slot retries to the same ID; different
Run/Point/Mapping/sequence differs. Namespace and serialization versions are contract constants.

### Production-attempt checkpoint

`acquisition.simulator_production_attempt` provides a durable checkpoint for each production slot
`(simulator_run_id, point_id, source_sequence)`. Before generation, the Worker loads an existing
Pending row. Existing Pending is the authoritative retry payload: it reuses measurement ID,
mapping/configuration/algorithm snapshots, source timestamp, numeric value, unit, producer,
correlation and lineage fields without generator invocation, PRNG deserialization/state change,
sequence advancement or a second Generated increment.

For a new slot, `source_sequence = next_source_sequence`. Only after the complete Pending insert
succeeds does the same Acquisition transaction persist generator state, set
`next_source_sequence = source_sequence + 1`, and increment Generated once. This applies equally
to Constant and Normal. Finalization atomically performs the first Pending -> Completed transition
and exactly one Accepted/Rejected counter increment based on Telemetry's stored
`final_classification`; replay of the same completed result is a no-op.

### Run-point pinned snapshot

`simulator_run_point_state` is extended to include:
- `point_version_at_start` — concurrency version at Start
- `mapping_id` / `mapping_version` — the active Catalog mapping
- `metric_id` / `unit_id` / `unit_code` — resolved Metric/Unit
- `source_version` — Catalog Data Source version at Start
- `next_source_sequence` - zero-based cursor for the next new production slot; starts at 0
- `prng_state` (bytea 25 bytes) — PCG32 state and cached spare
- `next_due_at` — calculated next due timestamp

These pinned identities bind the Run to the exact Catalog and Organization state at creation.
Changing or inactivating a mapping does not change already-reserved production attempt identity.
A replacement Mapping requires an explicit new Run. Pause/Resume continues the cursor; a new
Start creates new per-Point state at zero.

### Telemetry terminal ingestion result

`telemetry.measurement_identity` is both identity registry and immutable terminal-result registry.
It stores the request identity and SHA-256 fingerprint; `final_classification`
(Accepted/Rejected); `measurement_persisted`; nullable persisted Measurement reference, quality,
reason, rejection code and `latest_advanced`; `completed_at`; and safe correlation/lineage
references. Accepted requires a raw Measurement reference and quality. Rejected requires no raw
Measurement reference and a rejection code. `(simulator_run_id, point_id, source_sequence)` is
unique in addition to the Measurement ID.

A valid trusted Simulator identity receives one terminal result. Accepted registry result and raw
Measurement commit atomically. Identity-addressable Rejected registry result commits atomically
without a raw row. Duplicate returns the exact stored original result; it never reconstructs a
Rejected outcome. Missing/malformed identity and untrusted producer failures occur before registry
reservation.

## Telemetry quality, Latest and status

- Good and Uncertain are Latest-eligible; Bad is stored but not Latest; No Data is derived only.
- Future skew beyond configurable 300 seconds is Uncertain + `SOURCE_TIMESTAMP_FUTURE` and preserved.
- Out-of-range safe values are accepted Bad + `VALUE_OUT_OF_RANGE`; malformed records are Rejected.
- `point_latest` compares source timestamp, sequence when supplied and resolving the tie, processing
  timestamp, then measurement ID. Missing sequence falls through to the next tie-breaker; Simulator
  always supplies sequence. Atomic compare-and-set prevents regression.
- `point_source_status` derives Online (elapsed <= expected), Stale (above expected <= no-data), or
  NoData (above no-data), with Decommissioned > Suspended precedence. No Data never creates a raw row
  or numeric zero.

## Logical-reference consistency

Each logical reference has no cross-schema FK, owner validation, lookup index, version/snapshot and
reconciliation path:

- IAM user -> Point Data Owner: IAM Active/scope contract; Organization stores ID and owner snapshot.
- Site/Area scope -> Organization: IAM asks Organization ancestry; each command rechecks scope.
- Point -> Metric/Unit: Catalog status/compatibility version is rechecked at activation/ingestion.
- Mapping -> Point: Catalog asks Organization readiness; overlap is protected in Catalog transaction.
- Run -> Data Source: Acquisition asks Catalog source status before Start and stores source/version.
- Measurement -> Point/Source/Mapping: Telemetry validates all owner snapshots before atomic identity,
  raw and projection work; drift is reconciled, never repaired by cross-schema write.

## State transitions

| Aggregate transition | Actor and scope | Preconditions | Concurrency/effects/audit | Failure |
|---|---|---|---|---|
| User Active <-> Disabled | Administrator global | expected current status | expected version; invalidate sessions; Audit event | version/not found |
| Site Draft -> Active; Active <-> Inactive | Administrator only | required fields; Site scope context | expected version; outbox then Audit; no Engineer bypass | forbidden/validation |
| Area/Asset Draft -> Active; Active <-> Inactive | Admin or scoped Engineer | Active parent for activation | expected version; lifecycle/audit | parent/scope conflict |
| Asset nonterminal -> Decommissioned | Admin/scoped Engineer | no Active child Point | atomic expected version; no cascade; audit | `ACTIVE_CHILD_POINT` |
| Point Draft -> Active; Active <-> Inactive | Admin/scoped Engineer | all IAM/Catalog/Organization checks, REPEATABLE READ + global lock order | expected version; health reconcile; audit | specific prerequisite |
| Point nonterminal -> Decommissioned | Admin/scoped Engineer | no Running mapped Run | lock order Organization Point → Acquisition Run → Integration outbox; audit | `RUNNING_SIMULATOR` |
| Source Draft -> Active; Active <-> Suspended; nonterminal -> Decommissioned | Admin/scoped Engineer | config/scope checks | expected version; schedule/health event + audit | lifecycle/history conflict |
| Mapping Draft -> Active; Active -> Inactive/Superseded | Admin/scoped Engineer | Point readiness port, no overlap | exclusion/serializable lock; Catalog event + audit | mapping conflict |
| Run Start/Pause/Resume/Stop | scoped Engineer/Admin | Start requires active Source/Mapping/Point/ancestors; idempotency key; global lock order | expected version; new Run or continued state, lease/job/event/audit | invalid transition |
| Source status evaluation | Worker/System | owner snapshot/version still valid | idempotent projection; event only on real change | stale snapshot ignored |
| Capability assign/revoke | Administrator | user exists, capability exists | user_capability version; audit | not found |
| Production attempt Pending -> Completed | Worker (system) | authoritative Pending payload, stable Telemetry original result | atomic first transition + exactly one final-classification counter increment; identical replay no-op | differing terminal result conflict |

## Existing R0 infrastructure

Reuse exactly `integration.outbox_event`, `integration.inbox_message`, and `operations.job` from
`0001_r0_foundation.sql`. The proven crash-safe Audit consumer need requires backward-compatible
nullable inbox lease/retry fields in 0011. Outbox and job already provide claim/lease/attempt
fields. Do not recreate delivery/job tables. The R1 `command_idempotency` table is a separate API
command mechanism, not an R0-table replacement.

## Ordered migration design

1. `0002_iam_foundation.sql` — identity, fixed roles, capability, user_capability, `user_session`,
   session/index additions
2. `0003_catalog_foundation.sql` — Metric/Unit/compatibility and Data Source
3. `0004_organization_hierarchy.sql` — Site/Area/Asset/Draft Point and hierarchy indexes
4. `0005_acquisition_configuration.sql` — configuration head and immutable configuration versions
5. `0006_catalog_source_mapping.sql` — Source-Point Mapping after Point exists, effective periods
   and overlap
6. `0007_acquisition_run.sql` — Run, per-Point state (extended with pinned snapshot), production
   attempt checkpoint and leases
7. `0008_telemetry_measurement.sql` - immutable terminal identity/result registry and Accepted raw Measurement
8. `0009_telemetry_latest_status.sql` — Latest and physical point_source_status projections/indexes
9. `0010_audit_event.sql` — append-only audit storage, unique source event and permissions
10. `0011_r1_infrastructure_expand.sql` — create `integration.command_idempotency`; add nullable
    inbox lease/retry fields and claim index; never recreate `outbox_event`, `inbox_message`, or `job`
11. `0012_r1_idempotent_seeds.sql` — deterministic POC users/scopes after Site bootstrap strategy
    and catalog seeds. Does NOT claim to create impossible pre-Site scope rows.
12. `0013_r1_validation_reconciliation.sql` — reconciliation and validation queries

Each has owner, checksum/ledger evidence, forward behavior, lock/time estimate, clean/N-1 validation,
and forward-fix notes. Mapping is placed after the Point table exists even though Catalog owns it;
the Catalog contract supports a fake Organization readiness port in Phase 2 and the real port in
Phases 3-4.

Applied migrations remain immutable. Any correction after 0011 deployment uses a new,
higher-numbered forward-fix migration; Audit storage remains owned by 0010. Repository ports and
PostgreSQL adapters in `contracts/persistence-adapters.md` remain required runtime work—migration
SQL and database tests are not substitutes.
