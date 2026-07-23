# Implementation Plan: Asset Simulator Latest (Targeted Repair)

**Branch context**: `002-asset-simulator-latest` | **Date**: 2026-07-23 | **Spec**: [spec.md](spec.md)

**Boundary**: This repair updates planning/design artifacts only. It does not implement source
code, create migrations or tests, or create `tasks.md`. Scope remains R1 / VS-01.

## Authoritative scope and counts

- Functional requirements: **68** (`FR-001..039`, `FR-AP-001..005`, `FR-IAM-001..008`,
  `FR-DS-001..004`, `FR-CAT-001..004`, `FR-DO-001..003`, `FR-DC-001..005`).
- User stories: **5**.
- Success criteria: **9** (`SC-001..SC-009`).
- Included: Administrator-bootstrap Site, scoped Engineer hierarchy/configuration, minimal five-role
  IAM, Metric/Unit, Catalog-owned Simulator Source/Mapping, versioned Simulator configuration,
  deterministic Constant/Normal generation, canonical internal Telemetry, Latest, Source Health,
  server-side scope authorization, decommission guards, and immutable audit.
- Excluded: external REST/CSV ingestion, history explorer, aggregates, rules, alerts, notifications,
  reports, Modbus, Edge Collector, write-back, AI/ML, SSO, directory integration, password reset,
  complete user administration, replay, ramp/spike/advanced-noise/seasonal/ML scenarios, and any
  R2 capability.

## Technical context

**Runtime**: .NET SDK 10.0.300; Node 24.16.0; ASP.NET Core API; React + TypeScript + Vite Web;
PostgreSQL only. API and Worker remain separate composition roots in the modular monolith.

**Architecture constraints**: CQRS-lite, module-owned schemas and writes, public module contracts,
transactional outbox/inbox, database-backed jobs/leases, structured JSON logs, correlation/causation
IDs, optimistic concurrency, restricted offline package policy, and no Docker/Redis/broker/alternate
database/public download.

**Environment classification**: Domain, authorization-policy, architecture and contract-shape tests
are `RUNNABLE_NOW`. PostgreSQL migration/constraint/transaction/dedup/lease/E2E evidence is
`REQUIRES_APPROVED_POSTGRESQL`; missing approved package sources are `BLOCKED_BY_PACKAGE_POLICY`;
missing local services/tools are `BLOCKED_BY_ENVIRONMENT`; company CI/deployment is
`REQUIRES_COMPANY_APPROVAL`.

## Plan-time decisions

### P-001 - Latest eligibility

Good and Uncertain Measurements may advance Point Latest and the projection preserves quality and
reason. Bad Measurements may be stored for traceability but never advance Latest. No Data is derived,
never a Measurement or synthetic zero. A previous Latest value may remain visible beside current
No Data, with distinct API/UI fields.

### P-002 - Clock skew

`clock_skew_threshold_seconds` is configurable and defaults to 300 seconds. A safely parsed source
timestamp beyond the configured future threshold is preserved as Uncertain with
`SOURCE_TIMESTAMP_FUTURE`, remains Latest-eligible, and is not hard-rejected by a second hidden
threshold.

### P-003 - Latest ordering

Latest advances only for an eligible Measurement whose tuple is strictly greater: source_timestamp,
then sequence_number when supplied and resolving an equal timestamp, then processing_timestamp, then
measurement_id. Simulator always supplies sequence. The losing tie remains history.
Distinct older/out-of-order history is stored without regression. Duplicate identity is a separate
idempotent outcome with no second storage or counter increment.

### P-004 - Draft mapping bootstrap

A Draft Point may have an Active Catalog mapping. The mapping is configuration-ready but produces no
Measurement until Source, Point and all ancestors are Active. Point activation requires exactly one
effective Active mapping; Simulator Start repeats all readiness checks.

### P-005 - Validation outcomes

Parseable out-of-range values are accepted into history as Bad with `VALUE_OUT_OF_RANGE`, increment
accepted, and do not advance Latest. Malformed/unattributable/unauthorized input is Rejected. A
duplicate is neither a second acceptance nor a rejection.

### P-006 - Health precedence

Decommissioned overrides Suspended, and both override elapsed Online/Stale/No Data calculation.
Otherwise Online is elapsed <= expected interval, Stale is above expected and <= no-data threshold,
and No Data is above the threshold.

### P-007 - Run-point determinism

Generator state and sequence are per Run + Point. A new Start creates a new Run; Resume continues
the same state. Worker leases prevent concurrent production for one Run.

### P-008 - Conditional deletion

An audit snapshot alone does not block deletion of a Draft-unused Source/Mapping. Operational or
business dependency (mapping use, Run, Measurement, projection, scheduled job, or other business
reference) blocks hard delete. Audit retains immutable snapshots without restrictive foreign keys.

### P-009 - Terminal decommission

Asset decommission fails atomically while any child Point is Active and never cascades child state.
Point decommission fails while its Simulator Run is Running, requires explicit stop/inactivation,
triggers health reconciliation, is audited, and is terminal. Inactive records may reactivate through
current readiness checks; Decommissioned records cannot.

### P-010 - Effective mapping

Catalog enforces at most one Active mapping effective for a Point at an instant using half-open
`[effective_from,effective_to)` periods and transactional overlap protection. Historical mappings
remain Inactive/Superseded; future configuration may remain Draft.

### P-011 - Configuration version identity

`simulator_configuration` is the aggregate head with `current_version`; immutable
`simulator_configuration_version` rows hold executable parameters and algorithm identity. A Run
stores the exact aggregate/version pair. Editing creates a new version for future Starts and never
mutates a version referenced by Running, Paused or historical Runs.

### P-012 - Deterministic algorithm

`algorithm_id=IUMP-DETERMINISTIC-V1`, with an explicit `algorithm_version`, is part of the immutable
configuration. Constant returns exactly `minimum_value` and consumes no PRNG state. Normal uses a
repository-owned PCG32 integer PRNG with a documented Box-Muller normal-like transformation, seeded
from the canonical configuration seed and a stable Point identity; each source sequence derives the
bounded Box-Muller normal-like value with midpoint mean and range/6 sigma, clamped to
`[minimum_value,maximum_value]`. Algorithm ID/version, seed, Point ID, configuration version and
source sequence are the complete deterministic inputs; Worker restart, scheduling order, process and
future implementation revisions cannot silently change the sequence.

### P-013 - Stable Measurement identity

UUIDv5-style SHA-1 name derivation uses a repository-owned namespace UUID and UTF-8 canonical material:
`IUMP:SIMULATOR:V1|source_id|run_id|point_id|mapping_id|source_sequence|algorithm_version`.
IDs and fields are lowercase canonical UUID strings, decimal sequence, and `|` separators with no
whitespace. The namespace and serialization version are fixed contract constants. Same production
slot retries to the same ID; different Run, Point, Mapping or sequence yields a different name.
Received/processing time is never input. Collision risk is the UUIDv5 namespace assumption and is
covered by identity tests.

### P-014 - Local POC session

IAM uses ASP.NET Core PasswordHasher with framework-managed iteration/version metadata and a
version metadata), a server-issued encrypted Secure/HttpOnly/SameSite cookie backed by revocable iam.user_session.
The session has absolute and idle expiry, revocation/logout, server-side lookup and Disabled-user
invalidation. Antiforgery is required for state-changing cookie requests and tokens never appear in
query strings. Login errors are non-enumerating, failed attempts have bounded framework-backed rate
limiting, and success/failure/revocation are auditable. Seed credentials are delivered through
protected local environment instructions, never committed.

## Constitution gate

Core modular-monolith, security, traceability, test-first and restricted-execution principles pass
for planning. Planning and task generation may proceed. The implementation governance gate is
BLOCKED by the historical R0-only constitution wording; after task analysis the governed next step
is to amend the constitution for an active feature/release lifecycle. This is separate from
PostgreSQL and package blockers.

## Module ownership and contracts

| Module / schema | Owns in VS-01 |
|---|---|
| IAM / `iam` | users, five base roles, sessions, Site/Area scopes, principal and authorization contracts |
| Organization / `organization` | Site, Area, Asset, Point, hierarchy lifecycle, code uniqueness, interval checks |
| Catalog / `catalog` | Metric, Unit, compatibility, Data Source, Source-Point Mapping, effective periods and overlap |
| Acquisition / `acquisition` | Simulator configuration head/versions, Runs, per-Point state, generation and counters |
| Telemetry / `telemetry` | trusted internal ingestion, identity, raw Measurements, Latest, Source Health |
| Audit / `audit` | immutable audit events and authorized audit query |
| Integration / `integration` | existing `outbox_event` and `inbox_message` delivery infrastructure |
| Operations / `operations` | existing `job`, Worker leases, scheduling and health evaluation |

Organization Point activation asks IAM for Data Owner eligibility, Catalog for Metric/Unit
compatibility and exactly-one mapping eligibility, and Organization for ancestor/interval checks. It
never asks Acquisition whether a mapping exists. Acquisition asks Catalog for Source/Mapping snapshots
and Organization for Point operational eligibility before invoking Telemetry. No module writes another
module's tables.

Point decommission synchronously asks Acquisition whether any mapped Run is Running; the command
fails before mutation when it is. Business modules publish committed events to Audit through the
existing outbox/inbox; they never insert Audit rows across schemas.

### Synchronous contracts

- IAM: `ICallerContext`, `IAuthorizationDecision`, `IActiveUserEligibility`, session validation.
- Organization: `IPointOperationalEligibility`, hierarchy lifecycle and scope-filtered read DTOs.
- Catalog: `IMetricUnitCompatibility`, `IActiveSimulatorMappingEligibility`, source/mapping snapshot.
- Acquisition: configuration version, Run control/status, generated-measurement producer adapter.
- Telemetry: `IngestMeasurement` accepted/rejected/duplicate result, Latest and Health query DTOs.
- Audit: `AppendAuditEvent` and authorized audit query.

### Asynchronous events

Outbox-backed envelopes carry `eventId`, type/schema version, producer, aggregate/version,
occurredAt, correlationId and causationId. Required events are `UserScopeChanged`,
`OrganizationStatusChanged`, `CatalogSourceMappingChanged`, `SimulatorRunStateChanged`,
`MeasurementAccepted`, `PointLatestAdvanced`, `PointSourceHealthChanged`, and audit-consumed control
events. Consumers deduplicate through `inbox_message`; event delivery is at-least-once.

## Dependency-ordered phases

### Phase 1 - Minimal IAM and bootstrap

Fixed roles, internal users, Active/Disabled, Administrator bootstrap/session, Site/Area scopes,
principal resolution and server-side authorization. **Checkpoint**: fixed roles and Administrator
exist; no pre-created Engineer Site-scope row is required; Administrator can authenticate and create
the root Site; out-of-scope Engineer has no global bypass.

### Phase 2 - Catalog primitives

Metric, Unit, compatibility, idempotent Electric Power/kW and Electrical Energy/kWh seeds, Data
Source and Mapping foundations. **Checkpoint**: compatible catalog seed and mapping overlap/delete
policy pass without Point activation.

### Phase 3 - Draft organization hierarchy

Administrator creates Site and assigns Engineer Site scope. Administrator activates Site; Engineer
creates Draft Area, Asset and Point, then Engineer or Administrator activates Area/Asset top-down;
Point remains Draft. **Checkpoint**: no Engineer root-site
creation, correct scope, no premature Point activation or measurement.

### Phase 4 - Simulator source, configuration and mapping

Create Catalog Source, immutable Acquisition configuration version, and one effective Active Mapping
for the Draft Point. **Checkpoint**: mapping is Active configuration but cannot produce until Source,
Point and ancestors are Active; overlap conflict is atomic.

### Phase 5 - Measurement Point activation

Validate Active ancestors, Metric/Unit, Data Owner, intervals and exactly one mapping; activate Point
with optimistic concurrency and audit. **Checkpoint**: every failed prerequisite returns a specific
error; an Active child prevents Asset decommission, and a Running source prevents Point
decommission.

### Phase 6 - Simulator Run and Worker execution

Start/Pause/Resume/Stop, immutable configuration reference, leases, restart, counters and
deterministic generation. **Checkpoint**: Start fails unless Source/Mapping/Point/ancestors Active;
Running-only restart recovery and per-Run+Point repeatability pass.

### Phase 7 - Canonical Telemetry ingestion

Validate trusted producer, identity, mapping/Point/catalog, timestamps, quality and lineage; persist
accepted/rejected/duplicate outcomes and update Latest. **Checkpoint**: P-001/P-002 behavior,
out-of-order storage, stable identity and counter semantics pass.

### Phase 8 - Latest and Source Health

P-003 ordering, Online/Stale/No Data, Suspended/Decommissioned, thresholds, restart-safe evaluation
and recovery. **Checkpoint**: no synthetic zero and no Latest regression under ties/concurrency.

### Phase 9 - API and Web integration

Versioned configuration journey, Simulator controls, Latest/Health, audit review, authorization,
safe errors, loading/empty/blocked states. **Checkpoint**: SC-001..SC-009 journey works with Admin
bootstrap and Engineer scoped access.

### Phase 10 - Acceptance hardening

All 68 FRs, five stories and nine criteria; PostgreSQL migration, concurrency and E2E evidence where
available. **Checkpoint**: no Critical/High authorization, lifecycle, audit, identity, integrity or
observability defect; blocked evidence is reported, never passed.

## Project structure

```text
specs/002-asset-simulator-latest/
  spec.md  plan.md  research.md  data-model.md  quickstart.md  contracts/
src/Api/  src/Worker/  src/BuildingBlocks/  src/Modules/{IAM,Organization,Catalog,Acquisition,Telemetry,Audit,Integration,Operations}/
Web/src/  database/migrations/  database/seeds/  tests/{Unit,Integration,Architecture,Verification}/
```

`tasks.md` is intentionally absent.

## Migration sequence

Migrations are immutable, forward-fix and dependency ordered:

| ID | Owner | Capability |
|---|---|---|
| 0002 | IAM | identity, fixed roles, `user_session`, bootstrap/session indexes |
| 0003 | Catalog | Metric/Unit/compatibility and Data Source |
| 0004 | Organization | Site/Area/Asset/Draft Point and hierarchy indexes |
| 0005 | Acquisition | configuration head and immutable configuration versions |
| 0006 | Catalog | Source-Point Mapping after Point exists, effective periods and overlap |
| 0007 | Acquisition | Run, per-Point state, counters and leases |
| 0008 | Telemetry | identity registry and raw Measurement |
| 0009 | Telemetry | Latest and physical point_source_status projections/indexes |
| 0010 | Audit | append-only audit storage and permissions |
| 0011 | Integration/Operations | additive R1 changes to existing `integration.outbox_event`, `integration.inbox_message`, `operations.job` |
| 0012 | IAM/Catalog | deterministic POC users/scopes after Site bootstrap strategy and catalog seeds |
| 0013 | Owners | reconciliation and validation queries |

No duplicate outbox table is introduced. The deterministic seed process creates fixed roles and an
Administrator first; Site and Engineer scope are created by the bootstrap journey, not by a seed FK
that points to a nonexistent Site.

## Traceability

| Phase / requirement group | Count | Owner / design phase | Evidence |
|---|---:|---|---|
| Phase 1: FR-IAM-001..008 | 8 | IAM / 1 | sessions, fixed roles, scopes, bootstrap negatives |
| Phase 2: FR-CAT-001..004, FR-DS-001..004 | 8 | Catalog / 2 | compatibility, seeds, mapping lifecycle/overlap/delete |
| Phase 3: FR-001..007, FR-DC-001..005 | 12 | Organization / 3 | Draft hierarchy, codes, terminal non-cascade decommission |
| Phase 4: FR-008..016 | 9 | Acquisition + Catalog / 4 | source, versioned configuration, mapping and Run readiness |
| Phase 5: FR-AP-001..005, FR-DO-001..003 | 8 | Organization + IAM / 5 | Point activation and Data Owner eligibility |
| Phase 7: FR-017..021 | 5 | Telemetry / 7 | identity, validation, quality, duplicate outcome |
| Phase 8: FR-022..027 | 6 | Telemetry + Operations / 8 | Latest, Health, thresholds and recovery |
| Phase 9: FR-028..039 | 12 | IAM, API, Audit / 9 | authorization/scope and append-only audit |
| **Total** | **68** | | |

**Explicit FR index**: `FR-001` through `FR-039`; `FR-AP-001` through `FR-AP-005`;
`FR-IAM-001` through `FR-IAM-008`; `FR-DS-001` through `FR-DS-004`; `FR-CAT-001` through
`FR-CAT-004`; `FR-DO-001` through `FR-DO-003`; `FR-DC-001` through `FR-DC-005`.

Explicit IDs: FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, FR-007, FR-008, FR-009, FR-010,
FR-011, FR-012, FR-013, FR-014, FR-015, FR-016, FR-017, FR-018, FR-019, FR-020, FR-021, FR-022,
FR-023, FR-024, FR-025, FR-026, FR-027, FR-028, FR-029, FR-030, FR-031, FR-032, FR-033, FR-034,
FR-035, FR-036, FR-037, FR-038, FR-039, FR-AP-001, FR-AP-002, FR-AP-003, FR-AP-004, FR-AP-005,
FR-IAM-001, FR-IAM-002, FR-IAM-003, FR-IAM-004, FR-IAM-005, FR-IAM-006, FR-IAM-007, FR-IAM-008,
FR-DS-001, FR-DS-002, FR-DS-003, FR-DS-004, FR-CAT-001, FR-CAT-002, FR-CAT-003, FR-CAT-004,
FR-DO-001, FR-DO-002, FR-DO-003, FR-DC-001, FR-DC-002, FR-DC-003, FR-DC-004, FR-DC-005.

| Story | Phase coverage | Checkpoint |
|---|---|---|
| US1 hierarchy | 1-5, 9 | Admin Site/bootstrap then Engineer scoped journey |
| US2 Simulator | 2,4,6,7 | versioned configuration, mapping and deterministic Run |
| US3 Latest/Health | 7-9 | scoped view and status recovery |
| US4 authorization | 1,3,9,10 | no Engineer global bypass/no leakage |
| US5 audit | 1,4-6,9 | capability-based audit review |

| Criterion | Verification |
|---|---|
| SC-001 | five-minute Admin bootstrap, scope assignment and Engineer hierarchy journey |
| SC-002 | two-minute mapping, Point activation, Start and visible Measurement journey |
| SC-003 | one scoped current view with Latest and Health fields |
| SC-004 | paused source crosses threshold to No Data, never zero |
| SC-005 | every cross-scope request denied; no-scope Engineer cannot create Site |
| SC-006 | configuration/control audit visible within five seconds |
| SC-007 | concurrent/serial overlapping Mapping activation conflict |
| SC-008 | operational dependency blocks Source/Mapping delete; audit-only snapshot does not |
| SC-009 | Active-child Asset decommission fails without cascade; successful decommission audited |

## Cross-schema references and consistency rules

Logical references have no database foreign key across module schemas. The owner validates them using
versioned contracts; lookup indexes exist on the referenced ID and scope. Before commit, the owner
rechecks the version/active state inside its transaction or rejects stale evidence. Events carry the
validated snapshot/version; reconciliation jobs compare references and report drift. This applies to:

- IAM user -> Point Data Owner: IAM validates Active status/scope; Organization stores user ID plus
  owner version snapshot; disabled/deleted users cannot be newly assigned.
- Site/Area scope -> Organization: IAM resolves scope; Organization supplies Site/Area ancestry
  snapshot; every command rechecks scope.
- Point -> Metric/Unit: Catalog returns compatibility/status version; Organization stores IDs and
  snapshot; activation revalidates.
- Mapping -> Point: Catalog asks Organization readiness/scope; overlap transaction protects the
  effective period; deleted Points invalidate mapping activation.
- Run -> Data Source: Acquisition stores source ID/version and asks Catalog before Start; deactivated
  source blocks new work and reconciliation stops stale schedules.
- Measurement -> Point/Source/Mapping: Telemetry stores IDs and validation snapshots; ingestion
  atomically checks the current owner contracts before persistence; reconciliation flags drift without
  cross-schema writes.

## TDD and verification

TDD covers lifecycle, bootstrap, sessions, role/scope, uniqueness, compatibility, Data Owner,
configuration version immutability, mapping cardinality, deterministic algorithm, stable identity,
Run state, decommission, quality, Latest ordering, Health, audit, concurrency, deduplication and
cross-scope enumeration. Fast runs pure/architecture/contract checks. Full adds approved PostgreSQL
migrations, atomicity, partitions/indexes, outbox/inbox, leases, API/Worker integration and E2E.
When PostgreSQL/packages are unavailable, create no substitute database and do not claim end-to-end
completion.

## Contradictions and resolution record

- Updated spec counts (68/9) replace the previous planning counts (61/8).
- Administrator Site bootstrap resolves the root-scope circularity.
- Draft Point + Active non-producing Mapping resolves Point/Mapping ordering.
- Catalog owns Mapping; Organization never queries Acquisition for mapping eligibility.
- Immutable configuration versions replace a mutable configuration row.
- Exact algorithm and deterministic Measurement ID are normative contracts.
- Reviewer is a capability/policy, not a base role; Manager and Viewer remain separate.
- Audit snapshots do not block Draft-unused delete; operational dependencies do.
- Asset/Point decommission is terminal, explicit, atomic and non-cascading.
- R0 infrastructure names are `outbox_event`, `inbox_message`, and `job`.

## Readiness

- Research/design repair: complete after artifact verification.
- Unresolved questions: **0**.
- Contradictions: **0** after the resolutions above.
- Ready for `/speckit.tasks`: **YES**.
- Ready for `/speckit.implement`: **NO** until constitution amendment, approved packages/PostgreSQL,
  and task analysis are complete.
