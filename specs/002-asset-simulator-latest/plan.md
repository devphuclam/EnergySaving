# Implementation Plan: Asset Simulator Latest

**Branch**: `002-asset-simulator-latest` | **Date**: 2026-07-23 | **Spec**: [spec.md](spec.md)

**Input**: R1 / VS-01 feature specification and its Clarifications, with DOC-04 v0.2, DOC-05
v0.2, DOC-06 v0.1, DOC-07 v0.2, `CONTEXT.md`, accepted ADRs, and the R0 repository baseline as
supporting sources.

**Boundary**: This command produces planning and design artifacts only. It does not implement source
code and does not create `tasks.md`.

## Summary

Deliver the first observable-data vertical slice without recreating R0: minimal local IAM and
scoped authorization; Metric/Unit and Simulator source catalog; Site/Area/Asset/Measurement Point
hierarchy with top-down activation; deterministic Constant/Normal Simulator operation in the
Worker; canonical Telemetry validation and duplicate protection; monotonic Point Latest; derived
Source Health; configuration/control audit; and a React UI over versioned ASP.NET Core APIs.

The central application remains a PostgreSQL-backed modular monolith with separate API and Worker
composition roots. Each module owns its writes. Bounded synchronous contracts answer validation and
query questions that must complete in-request; asynchronous cross-module effects use the existing
transactional outbox/inbox and database job/lease foundations.

## Authoritative Scope and Counts

- Functional requirements: **61**.
- User stories: **5**.
- Success criteria: **8** (`SC-001..SC-008`). The planning request says six in several places, but
  `spec.md` is higher priority and contains eight; this plan covers all eight.
- Included release: **R1 / VS-01 only**.
- Excluded: CSV/external REST ingestion, history explorer, aggregates, Rules, Alerts, Notifications,
  Reports, Modbus, Edge Collector, equipment control/write-back, AI/ML, Improvement Action, SSO,
  enterprise directory, complete user administration, and advanced Simulator scenarios.

## Technical Context

**Language/Version**: C# on .NET SDK 10.0.300; TypeScript/React on Node 24.16.0.

**Primary Dependencies**: ASP.NET Core; existing React + TypeScript + Vite baseline; PostgreSQL
driver/ORM only from approved locked package sources. No new public download is permitted.

**Storage**: PostgreSQL only, with module-owned schemas. No SQLite, in-memory database substitute,
Redis, external broker, or time-series database.

**Testing**: Installed PowerShell verification/architecture harness; framework-only C# tests now;
PostgreSQL integration, migration, and end-to-end tests when approved packages and database access
exist.

**Target Platform**: Restricted non-containerized internal workstation and approved on-premise
Windows/service hosts. Web static assets, ASP.NET Core API publish output, and .NET Worker publish
output are separate deployables.

**Project Type**: Web application with React SPA, Application API, Background Worker, shared
modular-monolith modules, and PostgreSQL.

**Performance Goals**:

- `SC-002`: first generated/accepted Simulator Measurement visible within two minutes of start.
- `SC-006`: configuration/control audit visible within five seconds.
- Source Health changes within one evaluation cycle after a threshold.
- R1 POC baseline: 8-20 Points at a 60-second interval; correctness and traceability precede
  throughput tuning.

**Constraints**:

- Restricted offline policy; no Docker/container artifacts or public package sources.
- Server-side role and object-scope authorization on every command/query.
- Optimistic concurrency on mutable configuration and run state.
- Structured JSON logs and correlation/causation propagation.
- At-least-once delivery with effectively-once business effects.
- PostgreSQL work remains blocked when approved packages/database are unavailable.

**Scale/Scope**: One internal product, one database, 13 existing module/schema ownership entries,
five user stories, eight delivery phases, 61 FRs, and eight measurable success criteria.

## Plan-Time Decisions

### P-001 — Latest eligibility by quality

- Good and Uncertain Measurements are eligible to advance Point Latest.
- Latest preserves `quality=Uncertain` and its reason code.
- Bad Measurements are persisted when safely interpretable but never advance Latest.
- No Data is derived health state, never a Measurement and never a synthetic zero.
- UI/API show the last observed value separately from current health, so an older Latest may remain
  visible while health is No Data.

### P-002 — Configurable future timestamp threshold

- `clock_skew_threshold_seconds=300` is a configurable VS-01 Working Default.
- `source_timestamp > received_timestamp + threshold` produces Uncertain quality and
  `SOURCE_TIMESTAMP_FUTURE`; the Measurement is preserved and remains Latest-eligible.
- No additional hard-rejection threshold is introduced.

### P-003 — Deterministic Latest ordering

Eligible Measurements compare by:

1. `source_timestamp`;
2. `sequence_number` when both values are present and source timestamps tie;
3. `processing_timestamp` when sequence does not resolve the tie;
4. `measurement_id` as the final deterministic tie-breaker.

A duplicate has the same Measurement identity and is not stored or counted twice. An out-of-order
Measurement has a distinct identity, may be stored, but loses the ordering comparison and cannot
regress Latest.

### Additional resolved design decisions

- **P-004 Activation bootstrap**: Draft Points may have Draft Simulator configuration and a current
  Active mapping. Mapping activation validates an activation-ready Draft/Active Point but does not
  permit production while the Point is not Active. Point activation then closes the bootstrap
  cycle. Starting a Simulator still requires Active source, mapping, and Point.
- **P-005 Bad outcome**: a parseable, attributable out-of-range value is stored as an Accepted
  Measurement with `quality=Bad` and `VALUE_OUT_OF_RANGE`; it increments accepted, not rejected,
  and never advances Latest. Rejected means the record cannot safely enter canonical history.
- **P-006 Health precedence**: Decommissioned overrides Suspended; Suspended overrides elapsed-time
  Online/Stale/No Data. For operational sources, accepted data sets Online and schedules the next
  check.
- **P-007 Determinism**: PRNG state and sequence are per Simulator Run and Point. A new Start from
  Stopped creates a new Run and resets deterministic state; Resume continues the same Run without
  a sequence gap.
- **P-008 Delete/audit**: immutable audit entries do not by themselves prevent conditional deletion
  of a Draft unused source/mapping. Operational history (run, Measurement, mapping use, dependent
  reference) prevents hard deletion; audit retains a tombstone/snapshot.
- **P-009 Lifecycle**: Inactive entities may reactivate only through the same current activation
  checks. Decommissioned is terminal. Asset decommission is blocked while any child Point remains
  Active.
- **P-010 Mapping overlap**: active Simulator mapping uniqueness is enforced per Point in the
  Catalog owner with normalized half-open effective intervals `[effective_from,effective_to)`.
  Future mappings remain Draft until their activation boundary; historical rows become
  Inactive/Superseded.

Full rationale and rejected alternatives are in [research.md](research.md).

## Constitution Check

*GATE: evaluated before research and re-evaluated after Phase 1 design.*

| Gate | Pre-design | Post-design evidence |
|---|---|---|
| Product boundary / requested release | PASS | R1/VS-01 is explicit; every R2+ capability is excluded |
| Source-of-truth / traceability | PASS | 61 FR, 5 stories, and 8 SC map to owners, phases, contracts, and tests |
| Deep modules / ownership | PASS | Eight owning/support modules, small public contracts, no cross-schema write |
| Test-first evidence | PASS | Each phase has a TDD checkpoint and truthful environment classification |
| Restricted secure execution | PASS | No container, public download, secret, alternate DB, or external broker |
| Operability | PASS | Jobs/leases, outbox/inbox, health, logs, metrics, correlation, restart paths planned |

The constitution's “R0 Delivery Constraints” and feature `001` workflow describe the completed R0
release. This command only plans R1 and therefore does not violate the R0 prohibition on implementing
R1 inside R0. The permanent Core Principles apply unchanged. Before `/speckit.implement`, the
constitution must be amended through its governed workflow to replace the historical
single-feature/R0-only wording with an active-feature lifecycle. `/speckit.tasks` may proceed because
it creates planning artifacts, not R1 source behavior.

## Architecture

### Module ownership

| Module / schema | Owned behavior and writes in VS-01 |
|---|---|
| IAM / `iam` | Internal users, Active/Disabled status, roles, Site/Area scopes, principal and authorization contracts |
| Organization / `organization` | Site, Area, Asset, Measurement Point, hierarchy lifecycle, scoped codes, Data Owner reference |
| Catalog / `catalog` | Metric, Unit, compatibility, Data Source, Source-Point Mapping and effective lifecycle |
| Acquisition / `acquisition` | Simulator Configuration, Run, per-Point deterministic state, controls, counters/error |
| Telemetry / `telemetry` | Canonical validation result, identity, raw history, Latest, Source Health |
| Audit / `audit` | Immutable append/query model |
| Integration / `integration` | Existing outbox/inbox storage and dispatch contracts |
| Operations / `operations` | Existing jobs plus Simulator tick and health-evaluation leases/checkpoints |

Rules, Alerts, Notifications, Reporting, and Files receive no VS-01 product behavior.

### Cross-module rules

- A module never writes another module's schema.
- API and Worker are composition roots; they adapt consumer-owned ports to provider public
  contracts because current architecture tests forbid module-to-module project references.
- Synchronous calls are shallow and return validation snapshots with version/concurrency evidence:
  IAM user/scope eligibility; Catalog Metric/Unit and mapping eligibility; Organization Point
  readiness; Telemetry ingestion result.
- Side effects that do not need an immediate answer are versioned events through local
  state+outbox transactions and consumer inboxes.
- CQRS-lite separates commands and read DTOs; current tables remain source of truth. No event
  sourcing.

### Synchronous contract catalogue

| Consumer | Provider contract | Purpose |
|---|---|---|
| API/Organization | IAM principal, scope, and active-user eligibility | Authorize object action and validate Data Owner |
| Organization | Catalog Metric/Unit compatibility snapshot | Point activation |
| Organization | Catalog active Simulator mapping eligibility | Point activation |
| Catalog | Organization Point mapping-readiness snapshot | Activate mapping against Draft/Active activation-ready Point |
| Acquisition | Catalog source/mapping snapshot | Validate run command and enumerate mapped Points |
| Acquisition | Organization Point operational snapshot | Prevent production to non-Active Point |
| Acquisition/Worker | Telemetry `IngestGeneratedMeasurement` | Canonical accepted/duplicate/rejected result per Point |
| API | Organization/Catalog/Acquisition/Telemetry query DTOs | Bounded UI composition for hierarchy, run, Latest, Health |
| Owner modules | Audit append command or committed integration event | Immutable audit evidence |

Detailed interfaces are under [contracts/](contracts/).

### Required integration events

All envelopes contain `eventId`, `eventType`, `schemaVersion`, `producerModule`, `occurredAt`,
`correlationId`, `causationId`, aggregate identity, and payload.

| Event | Producer | Consumer / effect |
|---|---|---|
| `ConfigurationChanged.v1` | IAM, Organization, Catalog | Audit append |
| `SimulatorRunStateChanged.v1` | Acquisition | Operations schedule/cancel; Audit append |
| `MeasurementAccepted.v1` | Telemetry | Source-health evaluation and Audit/diagnostic projection where required |
| `SourceHealthChanged.v1` | Telemetry | API read projection/Audit |

Duplicate Measurement identity produces no second `MeasurementAccepted`. Rejection is returned
synchronously to Acquisition for counters/error; a separate rejection event is not introduced
without a consumer.

## Project Structure

### Documentation (this feature)

```text
specs/002-asset-simulator-latest/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── checklists/
└── contracts/
    ├── README.md
    ├── iam-authorization.md
    ├── organization.md
    ├── catalog.md
    ├── simulator.md
    ├── telemetry.md
    └── audit-events.md
```

`tasks.md` is intentionally absent until `/speckit.tasks`.

### Source code (existing layout extended in place)

```text
src/
├── Api/                         # HTTP composition root, auth middleware, endpoint adapters
├── Worker/                      # job/lease, Simulator tick, health and outbox composition
├── BuildingBlocks/              # stable technical primitives only
├── Modules/
│   ├── IAM/{Contracts,Domain,Application,Infrastructure}/
│   ├── Organization/{Contracts,Domain,Application,Infrastructure}/
│   ├── Catalog/{Contracts,Domain,Application,Infrastructure}/
│   ├── Acquisition/{Contracts,Domain,Application,Infrastructure}/
│   ├── Telemetry/{Contracts,Domain,Application,Infrastructure}/
│   ├── Audit/{Contracts,Domain,Application,Infrastructure}/
│   ├── Integration/{Contracts,Domain,Application,Infrastructure}/
│   └── Operations/{Contracts,Domain,Application,Infrastructure}/
└── Web/src/
    ├── features/auth/
    ├── features/hierarchy/
    ├── features/catalog/
    ├── features/simulator/
    ├── features/points/
    └── shared/

database/
├── migrations/                  # monotonic, module/capability ordered SQL
└── seeds/                       # idempotent development/POC configuration

tests/
├── Unit/                        # pure Domain/Application seams
├── Integration/                 # approved PostgreSQL only
├── Architecture/                # module/reference/schema/forbidden-surface checks
└── Verification/                # repository and environment evidence
```

**Structure Decision**: extend the accepted repository layout rather than relocating to the DOC-05
reference `src/Hosts/...`. Each module stays one assembly initially, with internal
Domain/Application/Infrastructure namespaces and a small public `Contracts` surface. API and Worker
reference module assemblies only as composition roots; architecture tests must prohibit access to
module internals and direct cross-schema writes.

## Dependency-Ordered Implementation Phases

No phase is complete only because source exists. Each checkpoint requires runnable evidence plus
explicit blocker classification for PostgreSQL/CI work.

### Phase 1 — Minimal IAM foundation

- Internal user, Active/Disabled, roles, Site and optional Area scope.
- Principal resolution and centralized role/object-scope authorization contracts.
- Deterministic development users and assignments; no SSO, reset workflow, or full admin UI.
- TDD checkpoint: user lifecycle, disabled-user denial, role matrix, Site/Area ancestry, Data Owner
  eligibility, object-ID enumeration negatives.

### Phase 2 — Catalog foundation

- Metric, Unit, Metric-Unit compatibility and Active/Inactive lifecycle.
- Idempotent Electric Power/kW and Electrical Energy/kWh POC seeds.
- Data Source and Source-Point Mapping lifecycle/effective-period foundation.
- TDD checkpoint: catalog uniqueness, compatibility, seed idempotency, conditional hard delete,
  overlap conflict and mapping activation-ready Point contract.

### Phase 3 — Organization hierarchy

- Site, Area, Asset, Measurement Point create/read/update/list.
- Draft children under Draft/Active parents; top-down activation.
- Scoped normalized codes, Data Owner user reference, interval checks, hard-delete restrictions,
  concurrency and audit.
- TDD checkpoint: every activation precondition/failure reason, parent state, reactivation,
  decommission child guard, scope denial, audit before/after.

### Phase 4 — Simulator configuration and lifecycle

- Constant and Normal only; deterministic per-Run/Point generator.
- One Simulator source to many Points; at most one current Active Simulator mapping per Point.
- Start/Pause/Resume/Stop, persisted Run state, restart behavior, counters/latest error.
- Idempotent Operations job/lease/checkpoint and duplicate-command behavior.
- TDD checkpoint: scenario constraints, deterministic sequence, per-Point failure isolation, state
  matrix, optimistic concurrency, lease reclaim and Running-only restart.

### Phase 5 — Canonical Measurement and Telemetry

- Trusted internal producer; source/mapping/Point/catalog validation.
- Stable deterministic identity and sequence; timestamps, lineage and correlation.
- Good/Uncertain/Bad with reason codes and P-002 clock skew.
- Atomic identity/raw/Latest/outbox owner transaction; accepted/duplicate/rejected outcomes.
- TDD checkpoint: identity, duplicate no-op, Bad acceptance without Latest, Uncertain clock skew,
  mapping/catalog errors, out-of-order storage, counter semantics.

### Phase 6 — Latest and Source Health

- P-001 eligibility and P-003 total ordering.
- Online/Stale/No Data boundaries with defaults 60/300 and administrative precedence.
- Worker restart-safe due evaluation using `next_check_at`; accepted-data recovery.
- Last observed value is not erased by No Data and no synthetic zero exists.
- TDD checkpoint: every ordering tie-break, boundary equality, delayed evaluation, override status,
  recovery, duplicate/out-of-order behavior.

### Phase 7 — API and Web vertical integration

- Versioned command/query endpoints and standard safe error envelope.
- Hierarchy, Metric/Unit, source/mapping configuration, Simulator controls/status.
- Latest and Health in one scope-aware view; audit query for this slice.
- Loading, empty, validation, conflict, blocked and stale states; optimistic ETag forwarding.
- TDD checkpoint: contract tests, server-side role/scope negatives, cross-scope enumeration, UI
  distinction of last value vs current No Data, conflict and concurrency feedback.

### Phase 8 — Acceptance hardening

- Authorization negatives, concurrency, audit completeness, correlation/logging.
- Worker/database failure, lease recovery and restart behavior.
- Clean/N-1 migration, seed, constraints and atomicity when PostgreSQL is approved.
- Trace every story/FR/SC and run R1 demo evidence.
- Checkpoint: no Critical/High integrity, authorization, lifecycle, audit, or observability defect;
  blocked evidence is reported, never passed.

## Migration Sequence

Do not put the feature into one migration. Applied migrations are immutable and expand-contract.

| Order | Owner | Capability |
|---:|---|---|
| `0002` | IAM | `user_account`, `role`, `user_role`, `user_site_scope`, `user_area_scope` |
| `0003` | Catalog | `metric`, `unit`, compatibility, `data_source`, `source_point_mapping`, effective dating and overlap protection |
| `0004` | Organization | `site`, `area`, `asset`, `measurement_point`, scoped uniqueness and lifecycle |
| `0005` | Acquisition | Simulator configuration, Run, per-Point state and lease support |
| `0006` | Telemetry | identity registry and canonical Measurement persistence |
| `0007` | Telemetry | Latest, Source Health, ordering and evaluation indexes |
| `0008` | Audit | append-only audit event, permissions and query indexes |
| `0009` | Owners | cross-cutting R1 constraints, indexes, grants and validation/reconciliation queries |
| `0010` | IAM/Catalog | idempotent deterministic test users/scopes and POC Metric/Unit seeds |
| `0011` | Owners | forward validation/reconciliation checks; no destructive contraction |

Every migration records owner, forward behavior, rollback/roll-forward note, estimated lock/time,
clean and N-1 upgrade checks, validation query, and blocker classification. Long backfills run as
idempotent Operations jobs outside startup transactions.

## Verification Strategy and Environment Classification

| Evidence | Classification now | Rule |
|---|---|---|
| Domain/application lifecycle, generation, ordering, health, authorization tests | `RUNNABLE_NOW` | Framework-only, deterministic clock/ID/PRNG adapters |
| Repository, architecture and contract tests | `RUNNABLE_NOW` | Existing harness; add module/public-contract/schema checks |
| PostgreSQL migrations, constraints, partitions, atomicity, dedup, leases | `REQUIRES_APPROVED_POSTGRESQL` | No substitute database |
| ORM/driver compilation if approved locked packages absent | `BLOCKED_BY_PACKAGE_POLICY` | No public restore/download |
| Worker/API/Web local smoke without required service/tool | `BLOCKED_BY_ENVIRONMENT` | Report exact missing prerequisite |
| Company CI, TEST promotion, target deployment | `REQUIRES_COMPANY_APPROVAL` | Company runner/templates/hosts only |

TDD is mandatory for lifecycle, activation, uniqueness, Metric/Unit compatibility, Data Owner
validity, mapping cardinality, deterministic generation, Run transitions, Measurement identity,
quality, Latest ordering, Source Health, authorization, audit and concurrency.

## Requirement Traceability

### Functional requirements — all 61

| FRs | Count | Design owner | Phase | Primary evidence |
|---|---:|---|---:|---|
| `FR-IAM-001..008` | 8 | IAM | 1, 7, 8 | user/role/scope/principal domain + API negative tests |
| `FR-CAT-001..004` | 4 | Catalog | 2, 7 | catalog lifecycle, compatibility, seed idempotency |
| `FR-DS-001..004` | 4 | Catalog | 2, 7, 8 | source/mapping lifecycle and conditional delete |
| `FR-001..007` | 7 | Organization | 3, 7 | CRUD/list, scoped uniqueness, lifecycle, delete guards |
| `FR-AP-001..005` | 5 | Organization | 3 | Draft-child and activation-precondition matrix |
| `FR-DO-001..003` | 3 | Organization + IAM contract | 1, 3 | active/scope-valid user reference; no privilege grant |
| `FR-008..015` | 8 | Acquisition + Catalog | 2, 4 | scenarios, controls, restart, counters, cardinality |
| `FR-016..020` | 5 | Telemetry | 5 | identity, validation, duplicate, Bad/future quality |
| `FR-021..026` | 6 | Telemetry | 6, 7 | Latest ordering/view and Health thresholds/view |
| `FR-027..032` | 6 | IAM policy + every handler | 1, 7, 8 | role/scope matrix and enumeration negatives |
| `FR-033..037` | 5 | Audit + event producers | 3, 4, 7, 8 | append-only entries, before/after, query visibility |
| **Total** | **61** | | | |

**Explicit coverage index**:

- IAM: `FR-IAM-001`, `FR-IAM-002`, `FR-IAM-003`, `FR-IAM-004`, `FR-IAM-005`,
  `FR-IAM-006`, `FR-IAM-007`, `FR-IAM-008`.
- Catalog: `FR-CAT-001`, `FR-CAT-002`, `FR-CAT-003`, `FR-CAT-004`.
- Data Source/mapping lifecycle: `FR-DS-001`, `FR-DS-002`, `FR-DS-003`, `FR-DS-004`.
- Hierarchy: `FR-001`, `FR-002`, `FR-003`, `FR-004`, `FR-005`, `FR-006`, `FR-007`.
- Activation preconditions: `FR-AP-001`, `FR-AP-002`, `FR-AP-003`, `FR-AP-004`, `FR-AP-005`.
- Data Owner: `FR-DO-001`, `FR-DO-002`, `FR-DO-003`.
- Simulator: `FR-008`, `FR-009`, `FR-010`, `FR-011`, `FR-012`, `FR-013`, `FR-014`,
  `FR-015`.
- Ingestion: `FR-016`, `FR-017`, `FR-018`, `FR-019`, `FR-020`.
- Latest/Health: `FR-021`, `FR-022`, `FR-023`, `FR-024`, `FR-025`, `FR-026`.
- Authorization/scope: `FR-027`, `FR-028`, `FR-029`, `FR-030`, `FR-031`, `FR-032`.
- Audit: `FR-033`, `FR-034`, `FR-035`, `FR-036`, `FR-037`.

### User stories

| Story | Phases | Independent checkpoint |
|---|---|---|
| US1 Configure hierarchy | 1-3, 7 | Draft tree then top-down activation under Engineer scope |
| US2 Operate Simulator | 2, 4-6, 7 | Multi-Point deterministic Run; pause to No Data; overlap rejected |
| US3 Observe Latest/Health | 5-7 | Scoped Latest/quality plus independent current Health |
| US4 Role and scope | 1, 7, 8 | Site A actor cannot read/write/enumerate Site B |
| US5 Audit trail | 3, 4, 7, 8 | Reviewer/Admin sees immutable changes and controls within five seconds |

### Success criteria

| Criterion | Verification |
|---|---|
| SC-001 | Timed hierarchy creation/top-down activation acceptance run |
| SC-002 | Timed Simulator configure/start-to-visible Measurement run |
| SC-003 | One scoped view returns all active-Point Latest/quality/Health fields |
| SC-004 | Controlled clock crosses `no_data_after_seconds`; next evaluation shows No Data, never zero |
| SC-005 | Parameterized cross-scope API negative suite with no existence leakage |
| SC-006 | Correlated configuration/control event reaches audit query within five seconds |
| SC-007 | Concurrent/serial second mapping activation returns domain conflict |
| SC-008 | Source/mapping with operational history returns specific hard-delete rejection |

## Controlled Source Differences and Resolutions

| Difference | Resolution |
|---|---|
| DOC-04/DOC-07 include ramp/noise/spike/missing/replay scenarios | Clarified feature is authoritative: Constant and Normal only in VS-01 |
| DOC-07 implies IAM/catalog seeds in R0; repository has none | Deliver real minimal IAM and catalog in Phases 1-2; do not pretend R0 supplied them |
| DOC-05 uses some shortened schema/layout names | Enforced manifest/ADR-007 and DOC-06 exact schemas win; retain current `src/Api`, `src/Worker` |
| DOC-04 “interval + grace” wording | Clarified `no_data_after_seconds=300` is total elapsed threshold; no separate grace column |
| Feature says Simulator setup for Active Point while Point activation needs mapping | P-004 bootstrap through activation-ready Draft Point and non-producing Active mapping |
| Create audit versus Draft-unused delete | P-008 excludes immutable audit reference alone from operational dependency |
| Feature text calls Simulator CRUD | Delete is conditional under `FR-DS-003/004`, never unconditional |
| DOC-06 latest eligibility is catalog-driven | P-001 fixes VS-01 Good/Uncertain eligibility; code/reason catalog records the policy |
| Planning instruction says six SC | Spec contains eight; trace all eight and report the mismatch |

## Complexity Tracking

| Item | Why needed | Simpler alternative rejected because |
|---|---|---|
| Eight modules participate | Ownership already exists and each owns real behavior/data | Combining feature into one module would create cross-domain writes and a shallow god module |
| Outbox/inbox for selected effects | Audit/operations effects cross owner transactions and must survive restart | Direct cross-schema writes violate constitution and lose retry/idempotency |
| Per-Run/Point state | Multi-Point reproducibility and failure isolation | One shared PRNG makes Point order affect values and restart behavior |

No microservice, broker, cache, event store, alternative database, or speculative framework is
introduced.

## Readiness

- Phase 0 research: complete; no unresolved clarification item.
- Phase 1 design/contracts: complete in the sibling artifacts.
- Ready for `/speckit.tasks`: **YES**, using all 61 FRs, 5 stories and 8 SC.
- Ready for `/speckit.implement`: **NO** until the constitution's historical R0-only workflow text
  is amended and approved, approved dependencies/PostgreSQL are available for persistence work, and
  task analysis has no Critical conflict.
