# Data Model: Asset Simulator Latest

**Feature**: `002-asset-simulator-latest`  
**Database**: PostgreSQL, module-owned schemas  
**Time convention**: UTC `timestamptz`; display conversion occurs at the boundary

## Global Conventions

- Primary keys are application-generated UUIDs. Mutable aggregates carry `version bigint` for
  optimistic concurrency; commands supply the expected version.
- Codes use normalized uppercase comparison while preserving display text. Uniqueness follows the
  business scope and ignores soft-inactive history only where explicitly stated.
- Configuration rows contain `created_at`, `created_by`, `updated_at`, and `updated_by`.
- Cross-schema identifiers are logical references validated through owner contracts; one module
  never writes another module's tables.
- Audit stores immutable subject/value snapshots rather than restrictive foreign keys to mutable
  business rows.
- Effective intervals are half-open `[effective_from, effective_to)`.

## Entity Governance Matrix

This matrix makes the storage obligations explicit; field-level rules and relationships follow.

| Entity | Owner/schema | PK / business key | Required constraints and indexes | Concurrency / effective date | Delete, decommission, audit |
|---|---|---|---|---|---|
| `user_account` | IAM/`iam` | UUID / normalized username | unique username; status and credential checks; username/status indexes | `version`; n/a | no delete after reference; disable; audit status/role/scope |
| `role` | IAM/`iam` | UUID / role code | unique fixed role code | seed-versioned; n/a | seeded reference, no runtime delete; audit seed/version |
| `user_role` | IAM/`iam` | `(user_id,role_id)` / same | composite uniqueness; user and role indexes | assignment version; n/a | revoke association; audit before/after |
| `user_site_scope` | IAM/`iam` | `(user_id,site_id)` / same | composite uniqueness; Site/user indexes | assignment version; n/a | revoke association; audit |
| `user_area_scope` | IAM/`iam` | `(user_id,area_id)` / same | composite uniqueness; Area/Site/user indexes | assignment version; n/a | revoke association; audit |
| `metric` | Catalog/`catalog` | UUID / code | unique normalized code; status index | `version`; n/a | inactivate after reference; audit |
| `unit` | Catalog/`catalog` | UUID / code | unique normalized code; symbol lookup | `version`; n/a | inactivate after reference; audit |
| `metric_unit_compatibility` | Catalog/`catalog` | `(metric_id,unit_id)` / same | unique pair; one canonical Unit per Metric | assignment version; n/a | remove only if unused; audit |
| `data_source` | Catalog/`catalog` | UUID / code | unique normalized code; type/status indexes | `version`; n/a | Draft-unused delete; otherwise suspend/decommission; audit snapshot |
| `source_point_mapping` | Catalog/`catalog` | UUID / Point+effective period | no overlapping Active period per Point; source/Point/time indexes | `version`; `[from,to)` | Draft-unused delete; otherwise inactive/superseded; audit snapshot |
| `site` | Organization/`organization` | UUID / global code | unique normalized code; status index | `version`; n/a | reject child/history delete; inactivate; audit |
| `area` | Organization/`organization` | UUID / Site+code | unique normalized `(site,code)`; parent/status indexes | `version`; n/a | reject child/history delete; inactivate; audit |
| `asset` | Organization/`organization` | UUID / Area+code | unique normalized `(area,code)`; parent/status indexes | `version`; n/a | reject active child/history delete; terminal decommission; audit |
| `measurement_point` | Organization/`organization` | UUID / Site+code | unique normalized `(site,code)` forever; parent/status/owner indexes; interval checks | `version`; n/a | no code reuse; terminal decommission; audit |
| `simulator_configuration` | Acquisition/`acquisition` | Source UUID / source+config version | scenario/min/max/interval checks; source index | `version`; applies to new Run | retained after Run; audit config change |
| `simulator_run` | Acquisition/`acquisition` | UUID / source+run identity | one non-Stopped Run/source; status/due/lease indexes; nonnegative counters | `version`; run interval | never delete after generation; stop; audit controls |
| `simulator_run_point_state` | Acquisition/`acquisition` | `(run_id,point_id)` / same | nonnegative sequence; due/lease indexes | `version`; Run lifetime | retained with Run evidence; system audit/log |
| `measurement_identity` | Telemetry/`telemetry` | Measurement UUID / same | global duplicate uniqueness; source/sequence lookup | immutable; event time | immutable evidence; ingestion trace |
| `measurement_raw` | Telemetry/`telemetry` | Measurement UUID / same | quality/reason checks; Point/source/time and correlation indexes | immutable; source time | retained per policy; ingestion trace |
| `point_latest` | Telemetry/`telemetry` | Point UUID / same | referenced measurement unique as applicable; ordering tuple index | atomic compare/version; current | projection rebuild/update only; correlated trace |
| `point_source_health` | Telemetry/`telemetry` | Point UUID / same | state checks; source/state/next-evaluation indexes | atomic version; evaluated-at | projection update; state-change evidence |
| `audit_event` | Audit/`audit` | UUID / event identity | event uniqueness; object/actor/time/correlation/scope indexes | immutable; occurred-at | database-protected append-only |
| `outbox_message` / `inbox_message` | Integration/`integration` | existing R0 IDs / event ID | existing dedup/delivery indexes, extended only if necessary | existing claim/version time | retention by operations policy; delivery trace |
| `job` | Operations/`operations` | existing R0 ID / job key | unique job key; due/status/lease indexes | lease token/version; next due | disable/retain execution history; operational audit |

## IAM (`iam`)

### `user_account`

Fields: `user_id` PK, `username`, `normalized_username`, `credential_hash`, `status`
(`Active|Disabled`), `version`, audit timestamps/actors. Unique `normalized_username`; credential
material is never returned. A Disabled user cannot authenticate, own a newly activated Point, or
authorize a command. Existing evidence retains its actor/user ID.

### `role` and `user_role`

`role(role_id, code, name)` has seeded unique codes `Administrator`, `Engineer`, `Operator`,
`Manager`, `Viewer`. `user_role(user_id, role_id)` has a composite PK and idempotent assignment.
Roles grant capabilities but never bypass object scope except Administrator.

### `user_site_scope` and `user_area_scope`

Composite keys prevent duplicate assignments. Area scope includes a logical Site ID for efficient
consistency validation. An Area assignment must belong to an assigned Site unless the user is an
Administrator. Scope removal uses expected-version authorization and does not rewrite audit history.

## Catalog (`catalog`)

### `metric`

Fields: `metric_id`, globally unique `code`, `name`, `status` (`Active|Inactive`), `version`.
Inactivation is blocked only for new/activation references; historical Points retain the ID.

### `unit`

Fields: `unit_id`, globally unique `code`, unique `symbol` where appropriate, `name`, `status`
(`Active|Inactive`), `version`.

### `metric_unit_compatibility`

Composite PK `(metric_id, unit_id)`, `is_canonical`, timestamps. At most one canonical Unit per
Metric. POC idempotent seeds create Electric Power/kW and Electrical Energy/kWh. Compatibility must
exist and both records must be Active when a Point activates.

### `data_source`

Fields: `source_id`, globally unique `code`, `source_type` fixed to `Simulator` for VS-01,
`description`, `status` (`Draft|Active|Suspended|Decommissioned`), `version`. Draft-unused deletion
is allowed only if no mapping/run/measurement operational history exists. Audit snapshots do not
create a restrictive FK; otherwise history requires suspend/decommission.

### `source_point_mapping`

Fields: `mapping_id`, `source_id`, logical `point_id`, `status`
(`Draft|Active|Inactive|Superseded`), `effective_from`, nullable `effective_to`, `version`.
PostgreSQL exclusion/transactional locking prevents overlapping Active intervals per Point.
Historical mappings are retained. A Draft mapping may target a Draft Point, but cannot produce data
until the Point is Active.

## Organization (`organization`)

### `site`

Fields: `site_id`, globally unique `code`, `name`, `description`, `timezone` default
`Asia/Ho_Chi_Minh`, `status` (`Draft|Active|Inactive`), `version`. Required code/name/timezone are
validated before activation.

### `area`

Fields: `area_id`, logical `site_id`, `code`, `name`, `description`, `status`
(`Draft|Active|Inactive`), `version`. Unique `(site_id, code)`. Draft creation is allowed beneath a
Draft or Active Site; activation requires an Active Site.

### `asset`

Fields: `asset_id`, logical `site_id`, logical `area_id`, `code`, `name`, `description`, `status`
(`Draft|Active|Inactive|Decommissioned`), `version`. Unique `(area_id, code)`. Decommissioned is
terminal. Decommission is rejected while an Active Point remains.

### `measurement_point`

Fields: `point_id`, logical `site_id`, logical `area_id`, logical `asset_id`, `code`,
`description`, `status` (`Draft|Active|Inactive|Decommissioned`), logical `metric_id`, logical
`unit_id`, logical `data_owner_user_id`, `expected_interval_seconds` default 60,
`no_data_after_seconds` default 300, `version`.

Constraints: unique `(site_id, code)` across all history; both intervals positive and
`no_data_after_seconds > expected_interval_seconds`. Activation validates Active ancestors,
Metric/Unit compatibility, an Active and appropriately scoped Data Owner, and exactly one effective
Active Simulator mapping. A Point under an inactive ancestor cannot activate, receive data, or
appear Online. Decommissioned is terminal and its code is never reassigned.

## Acquisition (`acquisition`)

### `simulator_configuration`

One-to-one subtype keyed by `source_id`; fields: `interval_seconds`, `minimum_value`,
`maximum_value`, `deterministic_seed bigint`, `scenario_type` (`Constant|Normal`), `version`.
Interval is positive. Constant requires min=max; Normal requires min<max. Configuration becomes
immutable for an existing Run; edits create a new version used by the next Start.

### `simulator_run`

Fields: `run_id`, `source_id`, `configuration_version`, `status`
(`Running|Paused|Stopped`), `started_at`, `paused_at`, `stopped_at`, `generated_count`,
`accepted_count`, `rejected_count`, `latest_error_code`, `latest_error_message`, `lease_owner`,
`lease_until`, `version`. Only one non-Stopped Run per source. A Worker restart reacquires Running
runs; Paused/Stopped runs do not auto-resume.

### `simulator_run_point_state`

Composite PK `(run_id, point_id)`; fields: `next_sequence bigint`, serialized deterministic
`generator_state`, `next_due_at`, `version`. State is independent per Point. A new Start/new Run
resets sequence and generator state; Resume continues the same state.

## Telemetry (`telemetry`)

### `measurement_identity`

Fields: `measurement_id` PK, `producer_identity`, `source_id`, `source_sequence`, `correlation_id`,
`lineage_id`, `first_seen_at`. The primary key is the canonical idempotency key. Repeated submission
returns Duplicate without creating another raw row or incrementing accepted/rejected twice.

### `measurement_raw`

Fields: `measurement_id` PK, logical `point_id`, `source_id`, `mapping_id`, `source_timestamp`,
`received_timestamp`, `processing_timestamp`, nullable `source_sequence`, `numeric_value`,
`unit_code`, `quality` (`Good|Uncertain|Bad`), nullable `reason_code`, `correlation_id`, `lineage_id`,
`ingested_at`. Indexes support `(point_id, source_timestamp desc)` and source/run investigation.
Partitioning is introduced only at the documented Telemetry migration boundary.

Quality policy:

- P-001: Good and Uncertain are Latest-eligible; Bad is stored but ineligible; No Data is never a
  Measurement.
- P-002: configurable clock skew defaults to 300 seconds. Beyond it, a safe row is Uncertain with
  `SOURCE_TIMESTAMP_FUTURE`, not rejected.
- Out-of-range safe values are Bad with `VALUE_OUT_OF_RANGE`, counted accepted, and never Latest.

### `point_latest`

One row per Point: `point_id` PK, `measurement_id`, value/unit/timestamps/quality snapshot,
`source_sequence`, and `version`. P-003 compares
`(source_timestamp, normalized_source_sequence, processing_timestamp, measurement_id)` atomically;
only a strictly greater tuple replaces the row. A missing sequence sorts below a supplied sequence
for the same timestamp. Duplicate and late arrival cannot regress Latest.

### `point_source_health`

One row per Point: `point_id`, `source_id`, optional `run_id`, `run_status`, nullable
`last_received_at`, counts, `health_state`
(`Online|Stale|NoData|Suspended|Decommissioned`), `evaluated_at`, `version`. Precedence is
Decommissioned, then Suspended, then elapsed-time derivation:
Online when elapsed <= expected interval; Stale when expected interval < elapsed <= no-data
threshold; NoData when elapsed > threshold. Re-evaluation is idempotent and does not create a
Measurement.

## Audit (`audit`)

### `audit_event`

Fields: `audit_event_id`, `occurred_at`, `actor_user_id`, `actor_username_snapshot`,
`correlation_id`, `object_type`, `object_id`, `action`, `before_json`, `after_json`, `summary`,
`site_id_snapshot`, `area_id_snapshot`. Insert-only database permissions and application contracts
prohibit update/delete. It records configuration lifecycle, mapping changes, Simulator commands,
and required authorization decisions. Snapshot references allow valid deletion of Draft-unused
configuration without erasing evidence.

## Existing Infrastructure Reused

- `integration.outbox_message` publishes committed cross-module facts.
- `integration.inbox_message` deduplicates consumer effects.
- `operations.job` and lease fields coordinate Simulator generation and health evaluation.
- No R1 business table is added to `rules`, `alerts`, `notifications`, `reporting`, or `files`.

## Relationships and Ownership Boundaries

```text
IAM user ──logical owner──> Organization Point
Site ──> Area ──> Asset ──> Point
Catalog Metric + Unit ──logical compatibility──> Point
Acquisition DataSource ──> Mapping ──logical target──> Point
DataSource ──> SimulatorConfiguration ──> SimulatorRun ──> RunPointState
SimulatorRun ──contract──> Telemetry Measurement ──> PointLatest
Measurement/Run evidence ──derived──> PointSourceHealth
All control/config changes ──outbox/contract──> AuditEvent
```

## State Transitions

| Aggregate | Transition | Actor | Preconditions and scope | Concurrency / side effects | Failure |
|---|---|---|---|---|---|
| User | Active -> Disabled; Disabled -> Active | Administrator | Global Admin | Expected version; invalidates future authorization; audit | conflict/not found |
| Site | Draft -> Active; Active <-> Inactive | Engineer scoped or Admin | Required fields; reactivation checks | Expected version; audit/outbox | validation/scope/conflict |
| Area | Draft -> Active; Active <-> Inactive | Engineer scoped or Admin | Parent Site Active for activation | Expected version; audit/outbox | parent inactive |
| Asset | Draft -> Active; Active <-> Inactive; any nonterminal -> Decommissioned | Engineer scoped or Admin | Parent Area Active; no Active Point for decommission | Expected version; terminal event/audit | active child/conflict |
| Measurement Point | Draft -> Active; Active <-> Inactive; nonterminal -> Decommissioned | Engineer scoped or Admin | Full activation checklist; code never reused | Expected version; health reevaluation/audit | specific prerequisite |
| Data Source | Draft -> Active; Active <-> Suspended; nonterminal -> Decommissioned | Engineer scoped or Admin | Valid Simulator config and mapped scope | Expected version; generation/health side effects; audit | history/delete conflict |
| Mapping | Draft -> Active; Active -> Inactive/Superseded | Engineer scoped or Admin | Compatible Point; no effective overlap | Serializable lock/exclusion; audit | domain overlap conflict |
| Simulator Run | Stopped/new -> Running; Running -> Paused/Stopped; Paused -> Running/Stopped | Engineer scoped or Admin | Active source, effective mapping, Active Points | Expected version + lease; counters/event/audit | invalid transition |
| Source Health | elapsed evaluation; any -> Suspended/Decommissioned | Worker/System | Owner snapshots and thresholds | Idempotent compare/update; optional event | stale input ignored |

Hard deletion is not a lifecycle transition. It is available only for Draft-unused Data Source or
Mapping records after owner-side dependency checks; hierarchy deletion additionally obeys FR-006.

## Ordered Migration Set

1. `0002_iam_local_identity_and_scope.sql`
2. `0003_catalog_metric_unit_source_mapping.sql`
3. `0004_organization_hierarchy.sql`
4. `0005_acquisition_simulator_run.sql`
5. `0006_telemetry_measurement.sql`
6. `0007_telemetry_latest_health.sql`
7. `0008_audit_event.sql`
8. `0009_r1_constraints_indexes_and_permissions.sql`
9. `0010_r1_idempotent_poc_seeds.sql`
10. `0011_r1_validation_and_reconciliation.sql`

Every migration has a forward verification query and is exercised from a clean database and from
the current `0001_r0_foundation` baseline. Data production is enabled only after 0002-0009 succeed;
seeds are rerunnable and environment-scoped.
