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
| `simulator_run_point_state` | Acquisition/`acquisition` | `(run_id,point_id)` | next sequence, PRNG state, next due; nonnegative/indexed | version; Run lifetime | retain with Run; operational evidence |
| `measurement_identity` | Telemetry/`telemetry` | `measurement_id` / dedup identity | stable identity, source/run/point/mapping/sequence/algorithm; PK + dedup unique/index | immutable | never delete within retention; trace |
| `measurement_raw` | Telemetry/`telemetry` | `measurement_id` / same | point/source/mapping, source/received/processing time, value/unit/quality/reason/lineage; time partition + Point/time index | immutable; source time | retention policy; trace |
| `point_latest` | Telemetry/`telemetry` | `point_id` / same | value/unit/timestamps/quality/reason and full ordering tuple; Point PK | atomic compare/version; current | projection rebuild/update; trace |
| `point_source_status` | Telemetry/`telemetry` | `(point_id,source_id)` / same | run status, last received, counters, Online/Stale/NoData/Suspended/Decommissioned; state/evaluation indexes | atomic version; evaluated-at | projection update; real-change event |
| `audit_event` | Audit/`audit` | `audit_event_id` / event ID | actor snapshot, object type/ID, action, before/after JSON, summary, correlation, scope snapshots; query indexes | immutable; occurred-at | append-only database permissions |
| `outbox_event` | Integration/`integration` | existing R0 `event_id` | reuse existing payload/status/attempt/lease columns; add only approved nullable metadata | existing claim/lease | operations retention |
| `inbox_message` | Integration/`integration` | existing `(consumer_name,event_id)` | existing hash/status/error/dedup columns | existing claim state | operations retention |
| `job` | Operations/`operations` | existing job ID / `(job_type,idempotency_key)` | existing payload/status/availability/lease/attempt fields | existing lease/version | retain execution history |

`metric_unit_compatibility`, `simulator_configuration*`, `simulator_run_point_state`, and
`point_lifecycle_history` are explicit VS-01 additions where DOC-06 does not provide an equivalent;
they do not replace R0 structures.

## IAM and session model

`user_scope.scope_id` is a logical Site/Area reference. IAM asks Organization to validate existence,
ancestry and current state; it does not write Organization. Bootstrap is ordered: fixed roles,
Administrator credential injected from protected environment, Administrator creates Site, then the
Administrator assigns Engineer scope. Deterministic seeds never insert a scope to a nonexistent Site.

ASP.NET Core `PasswordHasher<T>` stores framework-versioned hashes. Login sets a server-issued
encrypted Secure/HttpOnly/SameSite cookie; the server resolves `user_session` and Active user/roles/
scopes on every request. State-changing cookie requests require antiforgery. Logout/revocation,
expiry, Disabled status and scope revocation invalidate access. No token/hash is returned or placed in
a query string. Successful/failed authentication is auditable with non-enumerating errors and a
bounded framework-backed rate limit.

Base roles are exactly Administrator, Engineer, Operator, Manager and Viewer. `AuditReview` is a
capability/responsibility assignable under approved policy; it is not a role and is not automatic for
Viewer. Data Owner assignment grants no permission.

## Catalog and Organization rules

Administrator alone creates, updates, activates and inactivates root Site. Engineer scope is required
for all lower hierarchy, Catalog Source/Mapping and Simulator mutations. Area/Asset/Point Draft rows
may be prepared beneath an existing Draft or Active parent. Activation is top-down. Point activation
asks IAM for Data Owner eligibility, Catalog for Metric/Unit compatibility and exactly one effective
Active Mapping, and Organization for ancestors/intervals. It never asks Acquisition for Mapping.

Asset decommission is rejected atomically if any child Point is Active; no child cascade occurs.
Point decommission asks Acquisition for Running Simulator status, fails while any mapped Run is
Running, requires explicit stop/inactivation, triggers Health reconciliation, and is terminal.

## Simulator version and algorithm

`simulator_configuration` is an aggregate head. Every edit inserts an immutable
`simulator_configuration_version`; a Run stores `simulator_configuration_id` and exact
`configuration_version`. Running/Paused/historical Runs never change when a future version is made.

Algorithm contract: `IUMP-DETERMINISTIC-V1`, with fixed `algorithm_version`. Constant emits the exact
minimum/maximum value and consumes no PRNG state. Normal uses repository-owned PCG32 state and a
Box-Muller normal-like transformation with mean at the range midpoint, sigma equal to range/6,
rejection/clamping into `[minimum_value,maximum_value]`, and a persisted cached spare value. The
complete stream input is algorithm version, seed, stable Point ID, configuration version and source
sequence; state is independent per Run+Point. New Start resets state, Resume continues it. No
platform-default Random, third-party statistics package, system randomness or current-time seed is
allowed.

Simulator Measurement identity is a UUIDv5-style SHA-1 name under a repository namespace UUID using
UTF-8 canonical bytes:
`IUMP:SIMULATOR:V1|source_id|run_id|point_id|mapping_id|source_sequence|algorithm_version`.
UUIDs are lowercase canonical strings, sequence is decimal, separators are literal `|`, and no
received/processing time is included. Same slot retries to the same ID; different Run/Point/Mapping/
sequence differs. Namespace and serialization versions are contract constants.

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
| Point Draft -> Active; Active <-> Inactive | Admin/scoped Engineer | all IAM/Catalog/Organization checks | expected version; health reconcile; audit | specific prerequisite |
| Point nonterminal -> Decommissioned | Admin/scoped Engineer | no Running mapped Run | Acquisition status contract; terminal audit/health | `RUNNING_SIMULATOR` |
| Source Draft -> Active; Active <-> Suspended; nonterminal -> Decommissioned | Admin/scoped Engineer | config/scope checks | expected version; schedule/health event + audit | lifecycle/history conflict |
| Mapping Draft -> Active; Active -> Inactive/Superseded | Admin/scoped Engineer | Point readiness port, no overlap | exclusion/serializable lock; Catalog event + audit | mapping conflict |
| Run Start/Pause/Resume/Stop | scoped Engineer/Admin | Start requires active Source/Mapping/Point/ancestors; idempotency key | expected version; new Run or continued state, lease/job/event/audit | invalid transition |
| Source status evaluation | Worker/System | owner snapshot/version still valid | idempotent projection; event only on real change | stale snapshot ignored |

## Existing R0 infrastructure

Reuse exactly `integration.outbox_event`, `integration.inbox_message`, and `operations.job` from
`0001_r0_foundation.sql`. Add only backward-compatible nullable metadata or payload envelope fields
through an explicit expand migration if a proven VS-01 need exists. Do not recreate delivery/job
tables.

## Ordered migration design

1. `0002_iam_foundation.sql`
2. `0003_catalog_foundation.sql`
3. `0004_organization_hierarchy.sql`
4. `0005_acquisition_configuration.sql`
5. `0006_catalog_source_mapping.sql`
6. `0007_acquisition_run.sql`
7. `0008_telemetry_measurement.sql`
8. `0009_telemetry_latest_status.sql`
9. `0010_audit_event.sql`
10. `0011_r1_infrastructure_expand.sql` (only approved additive changes to R0 tables)
11. `0012_r1_idempotent_seeds.sql`
12. `0013_r1_validation_reconciliation.sql`

Each has owner, checksum/ledger evidence, forward behavior, lock/time estimate, clean/N-1 validation,
and forward-fix notes. Mapping is placed after the Point table exists even though Catalog owns it;
the Catalog contract supports a fake Organization readiness port in Phase 2 and the real port in
Phases 3-4.
