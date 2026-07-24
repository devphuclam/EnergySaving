# Implementation Plan: Asset Simulator Latest (Targeted Repair)

**Branch context**: `002-asset-simulator-latest` | **Date**: 2026-07-24 | **Spec**: [spec.md](spec.md)

**Boundary**: This repair updates planning/design artifacts only. It does not implement source
code, create migrations or tests, or create `tasks.md`. Scope remains R1 / VS-01.

## Authoritative scope and counts

- Functional requirements: **68** (`FR-001..039`, `FR-AP-001..005`, `FR-IAM-001..008`,
  `FR-DS-001..004`, `FR-CAT-001..004`, `FR-DO-001..003`, `FR-DC-001..005`).
- User stories: **5**.
- Success criteria: **9** (`SC-001..SC-009`).
- Included: Administrator-bootstrap Site, scoped Engineer hierarchy/configuration, minimal five-role
  IAM, Metric/Unit, Catalog-owned Simulator Source/Mapping, versioned Simulator configuration,
  deterministic Constant/Normal generation, production-attempt checkpoint, pinned Run-Point snapshot,
  canonical internal Telemetry, Latest, Source Health, server-side scope authorization, capability
  model for AuditReview, decommission guards, and immutable audit.
- Excluded: external REST/CSV ingestion, history explorer, aggregates, rules, alerts, notifications,
  reports, Modbus, Edge Collector, write-back, AI/ML, SSO, directory integration, password reset,
  complete user administration, business/measurement reprocessing or replay product capability,
  historical Measurement replay UI, regenerating or resubmitting arbitrary historical telemetry as
  a user feature, ramp/spike/advanced-noise/seasonal/ML scenarios, and any R2 capability.

Required technical replay remains in scope: exact API command-result replay through command
idempotency, Duplicate replay of the stored Telemetry terminal result, and operator-authorized
outbox/inbox/Audit delivery replay that preserves original event, correlation, and causation IDs.

## Technical context

**Runtime**: .NET SDK 10.0.300; Node 24.16.0; ASP.NET Core API; React + TypeScript + Vite Web;
PostgreSQL only. API and Worker remain separate composition roots in the modular monolith.

**Architecture constraints**: CQRS-lite, module-owned schemas and writes, public module contracts,
transactional outbox/inbox, database-backed jobs/leases, structured JSON logs, correlation/causation
IDs, optimistic concurrency, deterministic global lock order for strict cross-module invariants,
restricted offline package policy, and no Docker/Redis/broker/alternate database/public download.

**Environment classification**: Domain, authorization-policy, architecture and contract-shape tests
are `RUNNABLE_NOW`. PostgreSQL migration/constraint/transaction/dedup/lease/E2E checks are
`BLOCKED_BY_DATABASE_ACCESS` when approved PostgreSQL is unavailable; missing approved package
sources are `BLOCKED_BY_PACKAGE_POLICY`; missing required local tools are
`BLOCKED_BY_MISSING_TOOL`; company CI/deployment is `BLOCKED_BY_COMPANY_APPROVAL`.

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
the same state. `next_source_sequence` starts at zero; every successfully inserted new Constant or
Normal Pending attempt uses the current cursor and advances it exactly once. Retrying an existing
Pending attempt never advances sequence or Generated. Worker leases prevent concurrent production
for one Run.

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
configuration. See the normative specification in `contracts/simulator.md` for the exact PCG32
parameters, canonical UTF-8/FNV-1a initialization pseudocode, RXS-M-XS output, Box-Muller transform,
numeric precision, rounding, clamping, state
serialization, UUID namespace (`02e993bb-c767-5ff6-963f-530e1dfdff6b`), measurement identity
derivation, and three literal golden vectors. Their attempt sequences are `0,0,1`, outputs are
`12.5000,11.6519,17.9149`, and stored next sequences are `1,1,2`.

### P-013 - Stable Measurement identity

UUIDv5-style SHA-1 name derivation uses the repository namespace UUID
`02e993bb-c767-5ff6-963f-530e1dfdff6b` and UTF-8 canonical material:
`IUMP:SIMULATOR:V1|source_id|run_id|point_id|mapping_id|source_sequence|algorithm_version`.
IDs and fields are lowercase canonical UUID strings, decimal sequence, and `|` separators with no
whitespace. The namespace and serialization version are fixed contract constants. Same production
slot retries to the same ID; different Run, Point, Mapping or sequence yields a different name.
Received/processing time is never input. Collision risk is the UUIDv5 namespace assumption and is
covered by identity tests.

### P-014 - Local POC session

IAM uses ASP.NET Core PasswordHasher with framework-managed iteration/version metadata. On login,
a 256-bit opaque random session token is generated, SHA-256(token) is stored in `iam.user_session`,
and the raw opaque token is placed in a Secure/HttpOnly/SameSite=Lax cookie (`.IUMP.Auth`). The
cookie is not an ASP.NET encrypted identity ticket; it contains the raw opaque token. The session has absolute (8h) and idle (20m) expiry, revocation/logout,
server-side lookup and Disabled-user invalidation. Session token is 256-bit CSPRNG, stored as
SHA-256 hash. Antiforgery (`.IUMP.Xsrf` cookie, `X-XSRF-TOKEN` header) is required for
state-changing cookie requests and tokens never appear in query strings. Login errors are
non-enumerating, failed attempts have bounded framework-backed rate limiting (5 per 15s window),
and success/failure/revocation are auditable. Seed credentials are delivered through protected
local environment instructions, never committed. Data Protection keys are stored in a pre-provisioned directory writable by the API service account
using DPAPI `ProtectKeysWithDpapi()`. The application must not request elevation or alter system
ACLs. Development may use an approved user-writable local path configured outside the repository
(e.g. `%LOCALAPPDATA%/IUMP/DataProtection-Keys/`). Unavailable/unapproved storage is
BLOCKED_BY_COMPANY_APPROVAL when required provisioning/approval is absent. No keys are committed.

### P-015 - Production-attempt checkpoint

Acquisition owns `acquisition.simulator_production_attempt` with primary key
`(simulator_run_id, point_id, source_sequence)`. The Worker inserts a Pending row in the same
transaction as one new-slot PRNG/sequence/Generated advancement. Before generation it loads any
existing Pending attempt. That row is the authoritative retry payload: retry invokes no generator,
does not deserialize/advance PRNG state, and changes neither sequence nor Generated. Telemetry is
called outside the reservation transaction. Finalization atomically completes the attempt and
increments exactly one Accepted/Rejected counter from the stable original classification. No
in-memory checkpoint is used. See `contracts/simulator.md` and `data-model.md`.

### P-015A - Stable Telemetry terminal result

Telemetry extends `telemetry.measurement_identity` as its immutable terminal-result registry.
A valid trusted Simulator identity commits one Accepted or Rejected result. Accepted result and raw
Measurement commit atomically; identity-addressable Rejected result commits without a raw row.
Duplicate returns the exact stored original result, while same ID/different fingerprint is an
idempotency conflict. Missing/malformed identity and untrusted producer failures happen before
identity reservation. The immutable duplicate preflight is non-locking; new-result writes retain
Organization -> Catalog -> Telemetry -> Integration lock order.

### P-016 - Cross-module lock order

Strict cross-module invariants use a deterministic global lock order inside REPEATABLE READ
transactions: IAM → Organization → Catalog → Acquisition → Telemetry → Integration.
`SELECT FOR UPDATE` in this order. Retry on serialization/deadlock: up to 3 immediate retries with
exponential backoff (50ms, 150ms, 450ms). After exhaustion, return HTTP 503 with
`TRANSIENT_DATABASE_CONFLICT` (never `PRECONDITION_FAILED`). `lock_timeout` set to 2 seconds.
Each applied flow follows its specific required order; see `contracts/README.md` for the exact
seven flow orders. `PRECONDITION_FAILED` remains for business-state failures only (inactive parent,
missing Mapping, incompatible Unit, Running Simulator).

### P-017 - Capability model

`iam.capability` seeds the fixed code `AUDIT_READ`. `iam.user_capability` assigns capabilities to
users. Administrator has implicit `AUDIT_READ`. Viewer does not receive it automatically. Manager
does not receive it automatically unless explicitly chosen by seeded POC policy. Data Owner never
gains it implicitly. No full permission-management UI; minimal Administrator assign/revoke commands
suffice. See `contracts/iam-authorization.md`.

### P-018 - API concurrency semantics

Create commands: Idempotency-Key required, no If-Match. Update/lifecycle/delete commands: both
Idempotency-Key and If-Match required; stale version returns VERSION_CONFLICT. Login: neither
If-Match nor Idempotency-Key. Logout: no If-Match, antiforgery required. Query: neither If-Match
nor Idempotency-Key. See `contracts/README.md`.

### P-019 - Bootstrap split

Foundation seed (A) creates fixed roles, five deterministic users (no Site scope), and catalog
records. Post-Site fixture (B) uses application commands to assign scopes after Site exists, and
optionally grants AUDIT_READ. Fixture is idempotent and disabled outside development/POC.
See `contracts/iam-authorization.md`.

### P-020 - Persistent API command idempotency

All API create/update/lifecycle/delete commands covered by P-018 use the Integration-owned
`integration.command_idempotency` registry through `ICommandIdempotencyStore`; they do not use
`integration.inbox_message`. A short registration transaction commits a unique Pending record
before business execution. The host-coordinated mutation transaction then locks owner rows in the
global order, calls the Integration PostgreSQL adapter last, and atomically commits the owner
mutation, owner event/outbox row, and Completed original result. Concurrent same-fingerprint
requests wait for the unique-key winner for a bounded interval, replay Completed, or return
`IDEMPOTENCY_IN_PROGRESS` with `Retry-After`; a different fingerprint returns
`IDEMPOTENCY_CONFLICT`. An expired Pending lease is reclaimed only by a retry carrying the same
canonical request. See `contracts/integration.md`.

### P-021 - Complete Audit delivery path

Audit functionality is the mandatory executable chain: business command -> owner-module event ->
`integration.outbox_event` -> outbox dispatcher -> Audit `integration.inbox_message` reservation ->
Audit event consumer -> immutable `audit.audit_event` append -> filtered Audit query service ->
authorized API/UI. Delivery is at-least-once. Audit append and inbox completion share a
host-coordinated transaction with Integration last, and a unique source-event constraint makes one
producer event create at most one Audit row for the Audit consumer. Retry, poison, reconciliation,
and operator-authorized outbox/inbox/Audit delivery replay reuse Integration/Operations infrastructure;
all replay preserves the original event, correlation, and causation IDs. Payload construction or
outbox insertion alone is not complete Audit functionality. See `contracts/audit-events.md`,
`contracts/integration.md`, and `contracts/operations.md`.

### P-022 - State-owning persistence adapters

Every state-owning module has a public repository port and a module-owned PostgreSQL adapter.
Application/composition code depends on ports, not SQL or another module's schema. Migrations create
storage; tests verify behavior; neither substitutes for implementing the adapter. The required
adapter inventory, transaction participation, environment classification, and PostgreSQL evidence
are normative in `contracts/persistence-adapters.md`.

### P-023 - Execution evidence vocabulary

`PASS` means behavior executed and verified; `FAIL` means executable verification ran and failed;
`BLOCKED` means runnable source/evidence was produced where possible, the exact external dependency
was recorded, execution could not occur, and the behavior is not passing; `NOT_RUN` means no
attempt. A phase may close only for planning/development progression with BLOCKED evidence when all
RUNNABLE_NOW work is complete, blockers are external/classified, no runnable dependent needs the
unavailable behavior, and the checkpoint explicitly remains capability-incomplete. Full/release
gates never pass with mandatory blockers.

## Constitution gate

Core modular-monolith, security, traceability, test-first and restricted-execution principles pass
for planning. This targeted repair neither amends nor requests amendment of the constitution.
Planning and targeted task repair may proceed. Implementation remains not ready until the repaired
tasks are analyzed and all applicable execution gates are satisfied.

## Module ownership and contracts

| Module / schema | Owns in VS-01 |
|---|---|
| IAM / `iam` | users, five base roles, capabilities, user_capability, sessions, Site/Area scopes, principal and authorization contracts |
| Organization / `organization` | Site, Area, Asset, Point, hierarchy lifecycle, code uniqueness, interval checks |
| Catalog / `catalog` | Metric, Unit, compatibility, Data Source, Source-Point Mapping, effective periods and overlap |
| Acquisition / `acquisition` | Simulator configuration head/versions, Runs, per-Point state (pinned snapshot), production-attempt checkpoint, generation and counters |
| Telemetry / `telemetry` | trusted internal ingestion, immutable Accepted/Rejected identity result registry, Accepted raw Measurements, Latest, Source Health |
| Audit / `audit` | immutable audit events and authorized audit query (AUDIT_READ capability) |
| Integration / `integration` | API command-idempotency registry plus existing `outbox_event` and `inbox_message` delivery infrastructure |
| Operations / `operations` | existing `job`, Worker leases, scheduling and health evaluation |

Organization Point activation asks IAM for Data Owner eligibility, Catalog for Metric/Unit
compatibility and exactly-one mapping eligibility, and Organization for ancestor/interval checks. It
never asks Acquisition whether a mapping exists. Acquisition asks Catalog for Source/Mapping snapshots
and Organization for Point operational eligibility before invoking Telemetry. No module writes another
module's tables.

Point decommission synchronously asks Acquisition whether any mapped Run is Running; the command
fails before mutation when it is. Both locks (Organization Point, then Acquisition Run) are acquired
in global order. Business modules publish committed events to Audit through the existing outbox/inbox;
they never insert Audit rows across schemas.

### Synchronous contracts

- IAM: `ICallerContext`, `IAuthorizationDecision`, `IActiveUserEligibility`, session validation.
- Organization: `IPointOperationalEligibility`, hierarchy lifecycle and scope-filtered read DTOs.
- Catalog: `IMetricUnitCompatibility`, `IActiveSimulatorMappingEligibility`, source/mapping snapshot.
- Acquisition: configuration version, Run control/status, generated-measurement producer adapter.
- Telemetry: `IngestMeasurement` accepted/rejected/duplicate result, Latest and Health query DTOs.
- Audit: `IAuditEventConsumer`, `IAuditAppendRepository`, and scope/capability-filtered
  `IAuditQueryRepository`.
- Integration: `ICommandIdempotencyStore`, `ITransactionalOutboxWriter`,
  `IOutboxClaimRepository`, and `IInboxDeduplicationRepository`.
- Operations: `IDurableJobScheduler` and `IJobClaimRepository` for scheduling, leases, retries,
  poison handling, reconciliation, and replay.

### Asynchronous events

Outbox-backed envelopes carry `eventId`, type/schema version, producer, aggregate/version,
occurredAt, correlationId and causationId. Required events are `UserScopeChanged`,
`UserCapabilitiesChanged`, `OrganizationStatusChanged`, `CatalogSourceMappingChanged`,
`SimulatorRunStateChanged`, `MeasurementAccepted`, `PointLatestAdvanced`,
`PointSourceHealthChanged`, and audit-consumed control events. Consumers deduplicate through
`inbox_message`; event delivery is at-least-once. The Worker dispatcher claims leased outbox rows,
propagates the immutable envelope, and marks publish/reschedule/poison outcomes. The Audit consumer
reserves its inbox identity, appends through the Audit adapter, then completes the inbox record in
one transaction; `(consumer_name,event_id)` and unique `audit_event.source_event_id` make retries
idempotent. Operations jobs wake dispatch/reconciliation/replay work but do not replace the outbox
or inbox source of truth.

## Dependency-ordered phases

### Phase 1 - Minimal IAM and bootstrap

Fixed roles, internal users, Active/Disabled, Administrator bootstrap/session, Site/Area scopes,
principal resolution, capability model (iam.capability + user_capability), and server-side
authorization. **Checkpoint**: fixed roles and Administrator exist; no pre-created Engineer
Site-scope row is required; Administrator can authenticate and create the root Site; out-of-scope
Engineer has no global bypass.

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
with optimistic concurrency, REPEATABLE READ transaction, global lock order and audit.
**Checkpoint**: every failed prerequisite returns a specific
error; an Active child prevents Asset decommission, and a Running source prevents Point
decommission.

### Phase 6 - Simulator Run and Worker execution

Start/Pause/Resume/Stop, immutable configuration reference, leases, restart, counters,
deterministic generation, production-attempt checkpoint, and pinned Run-Point input snapshot.
**Checkpoint**: Start fails unless Source/Mapping/Point/ancestors Active;
Running-only restart recovery, per-Run+Point repeatability, and crash recovery via production-attempt
checkpoint pass. Command retries also prove persistent Pending/Completed reservation, exact original
result replay, conflict rejection, and concurrent duplicate behavior.

### Phase 7 - Canonical Telemetry ingestion

Validate trusted producer, Acquisition-provided identity, mapping/Point/catalog, timestamps,
quality and lineage; persist one Accepted/Rejected terminal result, persist Accepted raw history,
return Duplicate by exact registry replay, and update Latest.
**Checkpoint**: P-001/P-002 behavior, out-of-order storage, stable identity and counter semantics,
Rejected-without-raw durability, and Duplicate returning the exact stored original result pass.

### Phase 8 - Latest and Source Health

P-003 ordering, Online/Stale/No Data, Suspended/Decommissioned, thresholds, restart-safe evaluation
and recovery. **Checkpoint**: no synthetic zero and no Latest regression under ties/concurrency.

### Phase 9 - API and Web integration

Versioned configuration journey, Simulator controls, Latest/Health, audit review (AUDIT_READ
capability-gated), authorization, safe errors, loading/empty/blocked states. **Checkpoint**:
SC-001..SC-009 journey works with Admin bootstrap and Engineer scoped access; audit evidence is
query-visible only after dispatcher, inbox deduplication, Audit append, and authorized query.

### Phase 10 - Acceptance hardening

All 68 FRs, five stories and nine criteria; PostgreSQL migration, concurrency and E2E evidence where
available. **Checkpoint**: no Critical/High authorization, lifecycle, audit, identity, integrity or
observability defect; blocked evidence is reported, never passed.

## Project structure

```text
specs/002-asset-simulator-latest/
  spec.md  plan.md  research.md  data-model.md  quickstart.md  contracts/
src/Api/  src/Worker/  src/BuildingBlocks/  src/Modules/{IAM,Organization,Catalog,Acquisition,Telemetry,Audit,Integration,Operations}/
src/Web/src/  database/migrations/  database/seeds/  tests/{Unit,Integration,Architecture,Verification}/
```

`tasks.md` is maintained as the implementation graph. The repaired graph remains subject to final
`/speckit.analyze`; implementation remains gated by clean analysis and Phase 0 constitution
governance.

## Migration sequence

Migrations are immutable, forward-fix and dependency ordered:

| ID | Owner | Capability |
|---|---|---|
| 0002 | IAM | identity, fixed roles, capability, user_capability, `user_session`, session/index additions |
| 0003 | Catalog | Metric/Unit/compatibility and Data Source |
| 0004 | Organization | Site/Area/Asset/Draft Point and hierarchy indexes |
| 0005 | Acquisition | configuration head and immutable configuration versions |
| 0006 | Catalog | Source-Point Mapping after Point exists, effective periods and overlap |
| 0007 | Acquisition | Run, per-Point state (extended with pinned snapshot), production-attempt checkpoint and leases |
| 0008 | Telemetry | immutable terminal identity/result registry and Accepted raw Measurement |
| 0009 | Telemetry | Latest and physical point_source_status projections/indexes |
| 0010 | Audit | append-only audit storage and permissions |
| 0011 | Integration | create `integration.command_idempotency`; add proven nullable lease/retry metadata and indexes to existing `integration.inbox_message`; never recreate `outbox_event`, `inbox_message`, or `job` |
| 0012 | IAM/Catalog | deterministic POC users/scopes after Site bootstrap strategy and catalog seeds (does not create impossible pre-Site scope rows) |
| 0013 | Owners | reconciliation and validation queries |

`0011_r1_infrastructure_expand.sql` is a required additive expand migration: it creates the command
registry after Integration exists and before commands depend on it, and extends the R0 inbox for
crash-safe consumer leasing. It does not recreate or rename R0 delivery/job tables. `0010` continues
to own Audit storage. Applied migrations are immutable; later corrections use a higher-numbered
forward-fix migration in dependency order. No duplicate outbox table is introduced. The deterministic seed process creates fixed roles and an
Administrator first; Site and Engineer scope are created by the bootstrap journey, not by a seed FK
that points to a nonexistent Site. Capability seeds (`AUDIT_READ`) and user_capability assignments
are part of 0002. Post-Site POC scope fixture is a separate application command, not part of any
migration.

## Traceability

| Phase / requirement group | Count | Owner / design phase | Evidence |
|---|---:|---|---|
| Phase 1: FR-IAM-001..008 | 8 | IAM / 1 | sessions, fixed roles, scopes, capabilities, bootstrap negatives |
| Phase 2: FR-CAT-001..004, FR-DS-001..004 | 8 | Catalog / 2 | compatibility, seeds, mapping lifecycle/overlap/delete |
| Phase 3: FR-001..007, FR-DC-001..005 | 12 | Organization / 3 | Draft hierarchy, codes, terminal non-cascade decommission |
| Phase 4: FR-008..016 | 9 | Acquisition + Catalog / 4 | source, versioned configuration, mapping and Run readiness |
| Phase 5: FR-AP-001..005, FR-DO-001..003 | 8 | Organization + IAM / 5 | Point activation and Data Owner eligibility |
| Phase 7: FR-017..021 | 5 | Telemetry / 7 | identity, validation, quality, duplicate outcome |
| Phase 8: FR-022..027 | 6 | Telemetry + Operations / 8 | Latest, Health, thresholds and recovery |
| Phase 9: FR-028..039 | 12 | IAM, API, Audit / 9 | authorization/scope and append-only audit (AUDIT_READ) |
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
| US5 audit | 1,4-6,9 | capability-based audit review (AUDIT_READ) |

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
rechecks the version/active state inside its REPEATABLE READ transaction using deterministic global
lock order, or rejects stale evidence. Events carry the validated snapshot/version; reconciliation
jobs compare references and report drift. This applies to:

- IAM user -> Point Data Owner: IAM validates Active status/scope; Organization stores user ID plus
  owner version snapshot; disabled/deleted users cannot be newly assigned.
- Site/Area scope -> Organization: IAM resolves scope; Organization supplies Site/Area ancestry
  snapshot; every command rechecks scope.
- Point -> Metric/Unit: Catalog returns compatibility/status version; Organization stores IDs and
  snapshot; activation revalidates inside locked transaction.
- Mapping -> Point: Catalog asks Organization readiness/scope; overlap transaction protects the
  effective period; deleted Points invalidate mapping activation.
- Run -> Data Source: Acquisition stores source ID/version and asks Catalog before Start; deactivated
  source blocks new work and reconciliation stops stale schedules.
- Measurement -> Point/Source/Mapping: Telemetry stores IDs and validation snapshots; ingestion
  atomically checks the current owner contracts before persistence; reconciliation flags drift without
  cross-schema writes.

## TDD and verification

TDD covers lifecycle, bootstrap, sessions, role/scope, capabilities, uniqueness, compatibility, Data
Owner, configuration version immutability, mapping cardinality, deterministic algorithm (PCG32 +
Box-Muller and the three literal vectors in `contracts/simulator.md`), stable identity (UUIDv5
namespace `02e993bb-c767-5ff6-963f-530e1dfdff6b`), Run state,
production-attempt checkpoint, command-idempotency fingerprint/replay/conflict/Pending recovery,
decommission, quality, Latest ordering, Health, complete Audit delivery/query, concurrency,
global lock order, deduplication and cross-scope enumeration. Fast runs pure/architecture/contract
checks. Full adds approved PostgreSQL migrations, atomicity, partitions/indexes, outbox/inbox,
leases, API/Worker integration and E2E. When PostgreSQL/packages are unavailable, create no
substitute database and do not claim end-to-end completion.

## Contradictions and resolution record

- Updated spec counts (68/9) replace the previous planning counts (61/8).
- Administrator Site bootstrap resolves the root-scope circularity.
- Draft Point + Active non-producing Mapping resolves Point/Mapping ordering.
- Catalog owns Mapping; Organization never queries Acquisition for mapping eligibility.
- Immutable configuration versions replace a mutable configuration row.
- Exact algorithm (IUMP-DETERMINISTIC-V1) and deterministic Measurement ID are normative contracts.
- UUID namespace `02e993bb-c767-5ff6-963f-530e1dfdff6b` is recorded as a literal constant.
- Production-attempt checkpoint (`acquisition.simulator_production_attempt`) resolves crash-recovery
  gap between Acquisition state advancement and Telemetry persistence.
- Pinned Run-Point input snapshot prevents silent Mapping/identity drift during Running Run.
- Cross-module transaction and global lock order resolve stale-checks race between Point activation,
  decommission and Simulator Start.
- POC seed/bootstrap split into foundation seed (no Site scope) and post-Site application fixture
  resolves FR-IAM-006 scope circularity. Migration 0012 does not claim pre-Site scope rows.
- AuditReview capability model (`iam.capability`/`iam.user_capability`) resolves the missing
  capability mechanism for AUDIT_READ.
- API concurrency semantics split (create no If-Match, update requires If-Match) is applied to all
  contracts.
- Session working defaults are concrete (opaque 256-bit CSPRNG token, SHA-256 hash, multiple sessions
  allowed, revoked_at IS NOT NULL means revoked) and P-014 malformed sentence fixed.
- Reviewer is a capability/policy, not a base role; Manager and Viewer remain separate.
- Audit snapshots do not block Draft-unused delete; operational dependencies do.
- Asset/Point decommission is terminal, explicit, atomic and non-cascading.
- R0 infrastructure names are `outbox_event`, `inbox_message`, and `job`.
- source_sequence alone is never sufficient without the persisted PCG32 state/spare for
  reproducibility; all retries use the production-attempt value and do not consume PRNG state again.
- source_sequence is zero-based per Run + Point: new Constant and Normal slots both advance exactly
  once; first Constant and first Normal use 0, and resumed Normal uses 1.
- Telemetry's immutable terminal-result registry makes both Accepted and Rejected duplicates
  replayable without reconstruction from raw Measurement.
- Exact generator initialization and the three literal golden vectors are normative in
  `contracts/simulator.md`; implementation does not select expected outputs.
- No "rejection/clamping" ambiguity: deterministic clamping on rounded float64 is the only
  bounding policy.
- No encrypted identity ticket cookie: `.IUMP.Auth` contains an opaque random session token;
  ASP.NET Data Protection is for antiforgery and framework-protected values, not for reconstructing
  the session token.
- Login does not use Idempotency-Key; each login creates a new independent session.
- Data Protection key directory must be pre-provisioned and writable by the API service account;
  application must not request elevation. Missing required provisioning/approval is
  BLOCKED_BY_COMPANY_APPROVAL.
- Deadlock, lock timeout and serialization exhaustion return 503 TRANSIENT_DATABASE_CONFLICT
  (not PRECONDITION_FAILED). PRECONDITION_FAILED reserved for business-state failures only.
- Every applied lock flow follows the correct order starting at Organization or IAM; no
  Catalog-before-Organization or Acquisition-before-Organization flows exist.
- Telemetry ingestion lock order is Organization → Catalog → Telemetry → Integration (not
  Telemetry-first).
- API Idempotency-Key semantics are backed by `integration.command_idempotency`; inbox rows remain
  consumer-event deduplication only.
- Audit completion means dispatch, inbox reservation, idempotent Audit append, and authorized query;
  an event payload or endpoint shape alone is insufficient.
- Every state owner requires a public repository port plus PostgreSQL adapter; a migration and its
  integration test do not implement runtime persistence.
- BLOCKED is non-passing evidence and may permit only explicitly incomplete development progression;
  it can never satisfy a mandatory Full/release gate.

## Readiness

- Research/design repair: complete after artifact verification.
- Unresolved questions: **0**.
- Contradictions: **0** after the resolutions above.
- Ready for targeted `/speckit.tasks` repair: **YES**.
- Ready for constitution amendment: **NO**.
- Ready for `/speckit.implement`: **NO** until targeted task repair/analysis and applicable
  package/PostgreSQL gates are complete.
