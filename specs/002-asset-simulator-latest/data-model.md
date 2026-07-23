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
| `simulator_run` | Acquisition/`acquisition` | `run_id` / source+run | source/config/version, Running/Paused/Stopped, counters, error, lease; status/lease indexes | `version`, idempotency; run lifetime | retain after production; stop terminal; audit |
| `simulator_run_point_state` | Acquisition/`acquisition` | `(run_id,point_id)` | pinned snapshot: point_id, point_version_at_start, mapping_id, mapping_version, metric_id, unit_id, unit_code, source_version, next_source_sequence, prng_state (bytea 25), next_due_at; nonnegative/indexed | version; Run lifetime | retain with Run; operational evidence |
| `simulator_production_attempt` | Acquisition/`acquisition` | `(run_id,point_id,source_sequence)` | measurement_id, mapping/config/algorithm snapshots, source_timestamp, numeric_value, unit_code, status Pending/Completed, telemetry_outcome Accepted/Rejected/Duplicate, original_classification, latest_advanced, error_code, created_at, completed_at, version | version; unique per sequence | immutable record; never delete |
| `measurement_identity` | Telemetry/`telemetry` | `measurement_id` / dedup identity | stable identity, source/run/point/mapping/sequence/algorithm; PK + dedup unique/index | immutable | never delete within retention; trace |
| `measurement_raw` | Telemetry/`telemetry` | `measurement_id` / same | point/source/mapping, source/received/processing time, value/unit/quality/reason/lineage; time partition + Point/time index | immutable; source time | retention policy; trace |
| `point_latest` | Telemetry/`telemetry` | `point_id` / same | value/unit/timestamps/quality/reason and full ordering tuple; Point PK | atomic compare/version; current | projection rebuild/update; trace |
| `point_source_status` | Telemetry/`telemetry` | `(point_id,source_id)` / same | run status, last received, counters, Online/Stale/NoData/Suspended/Decommissioned; state/evaluation indexes | atomic version; evaluated-at | projection update; real-change event |
| `audit_event` | Audit/`audit` | `audit_event_id` / event ID | actor snapshot, object type/ID, action, before/after JSON, summary, correlation, scope snapshots; query indexes | immutable; occurred-at | append-only database permissions |
| `outbox_event` | Integration/`integration` | existing R0 `event_id` | reuse existing payload/status/attempt/lease columns; add only approved nullable metadata | existing claim/lease | operations retention |
| `inbox_message` | Integration/`integration` | existing `(consumer_name,event_id)` | existing hash/status/error/dedup columns | existing claim state | operations retention |
| `job` | Operations/`operations` | existing job ID / `(job_type,idempotency_key)` | existing payload/status/availability/lease/attempt fields | existing lease/version | retain execution history |

`metric_unit_compatibility`, `simulator_configuration*`, `simulator_run_point_state`,
`simulator_production_attempt`, `point_lifecycle_history`, `capability`, and `user_capability`
are explicit VS-01 additions where DOC-06 does not provide an equivalent; they do not replace R0
structures.

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
BLOCKED_BY_ENVIRONMENT. The application must not request elevation or alter system ACLs.

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

Algorithm contract: `IUMP-DETERMINISTIC-V1`, with fixed `algorithm_version`. PCG32
(multiplier `6364136223846793005`, increment `1442695040888963407`, unsigned 64-bit overflow,
`pcg_output_rxs_m_xs_64_32`). Initial state derived from seed, stable Point ID, configuration
ID/version and algorithm version. Constant emits exact min=max value. Normal uses Box-Muller polar
form, midpoint mean, range/6 sigma, IEEE 754 float64, 4-decimal rounding, deterministic clamping
to bounds, cached spare. State serialized as 25 bytes (state, increment, spare_valid, spare_value).
No platform-default Random, third-party statistics package, system randomness or current-time seed
is allowed. Three golden test vectors defined (Constant, first Normal, restart Normal).

Simulator Measurement identity is UUIDv5 under the repository namespace UUID
`02e993bb-c767-5ff6-963f-530e1dfdff6b` (derived from DNS namespace + "iump.idea-technology.com")
using UTF-8 canonical bytes:
`IUMP:SIMULATOR:V1|source_id|run_id|point_id|mapping_id|source_sequence|algorithm_version`.
UUIDs are lowercase canonical strings with dashes, sequence is decimal, separators are literal `|`,
and no received/processing time is included. Same slot retries to the same ID; different
Run/Point/Mapping/sequence differs. Namespace and serialization versions are contract constants.

### Production-attempt checkpoint

`acquisition.simulator_production_attempt` provides a durable checkpoint for each production slot
`(simulator_run_id, point_id, source_sequence)`. The Worker inserts a Pending row in the same
Acquisition transaction as PRNG state advancement, calls Telemetry outside that transaction, then
finalizes idempotently. Crash recovery uses Duplicate detection to finalize without double-counting.
The entity stores measurement_id, mapping/config/algorithm snapshots, source_timestamp, numeric_value,
unit_code, status, telemetry_outcome, original_classification (populated on Duplicate), error_code,
created_at, completed_at and version. No in-memory checkpoint is used.

### Run-point pinned snapshot

`simulator_run_point_state` is extended to include:
- `point_version_at_start` — concurrency version at Start
- `mapping_id` / `mapping_version` — the active Catalog mapping
- `metric_id` / `unit_id` / `unit_code` — resolved Metric/Unit
- `source_version` — Catalog Data Source version at Start
- `next_source_sequence` — first sequence number
- `prng_state` (bytea 25 bytes) — PCG32 state and cached spare
- `next_due_at` — calculated next due timestamp

These pinned identities bind the Run to the exact Catalog and Organization state at creation.
Changing or inactivating a mapping does not change already-reserved production attempt identity.
A replacement Mapping requires an explicit new Run.

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
| Production attempt Pending -> Completed | Worker (system) | Pending row, stable Telemetry outcome | idempotent finalize; Duplicate returns original_classification | state conflict |

## Existing R0 infrastructure

Reuse exactly `integration.outbox_event`, `integration.inbox_message`, and `operations.job` from
`0001_r0_foundation.sql`. Add only backward-compatible nullable metadata or payload envelope fields
through an explicit expand migration if a proven VS-01 need exists. Do not recreate delivery/job
tables.

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
7. `0008_telemetry_measurement.sql` — identity registry and raw Measurement
8. `0009_telemetry_latest_status.sql` — Latest and physical point_source_status projections/indexes
9. `0010_audit_event.sql` — append-only audit storage and permissions
10. `0011_r1_infrastructure_expand.sql` — only approved additive changes to R0 tables (if proven
    necessary; no recreation)
11. `0012_r1_idempotent_seeds.sql` — deterministic POC users/scopes after Site bootstrap strategy
    and catalog seeds. Does NOT claim to create impossible pre-Site scope rows.
12. `0013_r1_validation_reconciliation.sql` — reconciliation and validation queries

Each has owner, checksum/ledger evidence, forward behavior, lock/time estimate, clean/N-1 validation,
and forward-fix notes. Mapping is placed after the Point table exists even though Catalog owns it;
the Catalog contract supports a fake Organization readiness port in Phase 2 and the real port in
Phases 3-4.
