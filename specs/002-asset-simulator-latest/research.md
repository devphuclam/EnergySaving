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

### Decision P-001: Latest eligibility

Good and Uncertain may advance Latest; Bad may be retained but never advances; No Data is derived and
does not replace Latest or produce numeric zero. The API separates last observed value from current
status. This preserves evidence while avoiding an invalid operational current value.

### Decision P-002: Future timestamp

`clock_skew_threshold_seconds` is configurable with 300 seconds as the VS-01 working default. A
safely parsed future value beyond the threshold is retained as Uncertain with
`SOURCE_TIMESTAMP_FUTURE`, remains Latest-eligible and is not subject to an extra hidden rejection
threshold. Server time is used for the comparison.

### Decision P-003: Latest ordering

Compare source timestamp, then supplied sequence when it resolves an equal timestamp, then processing
timestamp, then measurement ID. Simulator always supplies sequence. A distinct out-of-order row may
be stored but cannot regress Latest; duplicate identity returns the original outcome and does not
increment counters again.

### Decision: Immutable configuration versions

Acquisition has a configuration head plus immutable version rows. A Run stores the exact version used.
Editing creates a new version for future Starts; Running, Paused and historical Runs are unchanged.
This avoids a mutable row whose historical meaning changes.

### Decision: Reproducible Normal algorithm

Use repository-owned `IUMP-DETERMINISTIC-V1` with a fixed algorithm version, PCG32 integer PRNG and a
Box-Muller normal-like transformation (mean midpoint, sigma range/6, cached spare, rejection/
clamping to bounds). State is per Run+Point and includes seed/state/spare. Constant emits exact fixed
value without PRNG. Inputs are algorithm version, seed, stable Point ID, configuration version and
source sequence. No platform-default Random, system randomness, current-time seed or third-party
statistics dependency is permitted.

This survives Worker restart because state is persisted; scheduling order because each Point stream is
independent; process changes because the algorithm is repository-owned; and future revisions because
algorithm ID/version is part of the immutable configuration.

### Decision: Stable Measurement identity

Use UUIDv5-style name derivation under a repository namespace over canonical UTF-8 bytes:
`IUMP:SIMULATOR:V1|source_id|run_id|point_id|mapping_id|source_sequence|algorithm_version`.
Lowercase canonical UUIDs, decimal sequence and literal separators are normative. Time is excluded.
Retries of the same slot share identity; different Run/Point/Mapping/sequence do not. UUID namespace
collision assumptions are tested as a contract.

### Decision: Local authentication/session

Use approved ASP.NET Core `PasswordHasher<T>` and a server-issued encrypted Secure/HttpOnly/SameSite
cookie backed by revocable `iam.user_session`. Every request validates Active user, roles and scopes;
Disable/revoke takes effect immediately. Logout clears/revokes the session, expiry is idle plus
absolute, state-changing cookie requests require antiforgery, and no token/hash is returned or placed
in a query string. Login errors are non-enumerating with bounded framework-backed rate limiting.
Bootstrap credentials are injected from protected environment state, never committed.

### Decision: Five roles and audit review capability

Base roles are exactly Administrator, Engineer, Operator, Manager and Viewer. Manager and Viewer are
distinct. `AuditReview` is a capability/responsibility assigned under policy, not a sixth role and not
automatic for Viewer. Data Owner assignment grants no audit-review or elevated permission.

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

## Controlled source differences

- The updated feature has 68 FRs and 9 SCs; all earlier planning counts are obsolete.
- DOC-07 broader Simulator scenarios and replay are deferred because the clarified feature permits
  Constant and Normal only.
- DOC-07/R0 assumptions about IAM, Catalog and Site seeds are not true of the repository baseline;
  R1 supplies minimal IAM/Catalog and the Admin bootstrap journey without recreating R0.
- DOC-06 physical names are respected: `iam.user_scope`, `telemetry.point_source_status`,
  `integration.outbox_event`, `integration.inbox_message`, `operations.job`. VS-01 compatibility,
  version, run-point-state and lifecycle-history tables are explicit extensions.
- DOC-04 grace-period wording is narrowed to the clarified total `no_data_after_seconds` threshold.

## Clarification status

All repair points have a documented decision. Unresolved questions: **0**. The constitution's
historical R0-only implementation restriction and missing PostgreSQL/package approvals are execution
gates, not product questions.
