# Feature Specification: Asset Simulator Latest

**Feature Branch**: `002-asset-simulator-latest`

**Created**: 2026-07-23

**Status**: Draft

**Input**: User description: R1/VS-01 observable-data vertical slice — hierarchy, simulator, latest value

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Configure monitored hierarchy (Priority: P1)

As an Engineer, I want to create and activate a hierarchy of Site, Area, Asset, and Measurement Point records, so that the system knows what physical or logical location I am monitoring and what metric I am collecting.

**Why this priority**: Every later capability (ingestion, observation, rules, alerts) depends on an explicit and valid hierarchy and point configuration.

**Independent Test**: An Engineer with appropriate scope creates a Site, adds an Area, registers an Asset, and configures a Measurement Point with a Metric and Unit. The hierarchy is visible to authorized viewers. Activation respects parent relationships.

**Acceptance Scenarios**:
1. **Given** an authorized Engineer, **When** they create a Site with a unique code and name, **Then** the Site is created in Draft state and visible to users assigned to that Site.
2. **Given** an active Site, **When** the Engineer creates an Area with a unique code, **Then** the Area is created in Draft state under that Site.
3. **Given** an active Area, **When** the Engineer creates an Asset with a unique code, **Then** the Asset is created in Draft state under that Area.
4. **Given** an active Asset and existing Metric and Unit records, **When** the Engineer creates a Measurement Point with a unique code, **Then** the Point is created in Draft state under that Asset.
5. **Given** a complete hierarchy with valid configuration, **When** the Engineer activates each entity bottom-up, **Then** each entity transitions from Draft to Active without error.
6. **Given** a Measurement Point whose parent Asset is Draft, **When** the Engineer attempts to activate the Point, **Then** activation fails with a message citing the inactive parent.
7. **Given** an entity with dependent measurement history, **When** a user attempts to delete it, **Then** the system rejects hard deletion.
8. **Given** duplicate codes within the same scope, **When** the Engineer attempts to create the second entity, **Then** creation fails with a unique-code constraint error.

---

### User Story 2 — Configure and operate a Simulator source (Priority: P1)

As an Engineer, I want to create a Simulator Data Source, associate it with a Measurement Point, and control its run cycle (start, pause, resume, stop), so that the application can produce and ingest realistic measurement data without physical equipment.

**Why this priority**: The Simulator decouples MVP-1 development and testing from real hardware. It is the primary ingestion driver for VS-01.

**Independent Test**: An Engineer configures a Simulator for an active Point, starts it, observes generated measurements appearing in the ingestion pipeline, pauses it, and verifies that the source eventually reports missing data.

**Acceptance Scenarios**:
1. **Given** an active Measurement Point, **When** the Engineer creates a Simulator Data Source with an interval, min/max range, and deterministic seed, **Then** the Simulator is configured but not yet running.
2. **Given** a configured Simulator for an active Point, **When** the Engineer starts the Simulator, **Then** it begins producing measurements at the configured interval.
3. **Given** a running Simulator, **When** the Engineer pauses it, **Then** measurement production stops and the run status reflects Paused.
4. **Given** a paused Simulator, **When** the Engineer resumes it, **Then** measurement production continues without gap in sequence.
5. **Given** a running or paused Simulator, **When** the Engineer stops it, **Then** the run is terminated and status reflects Stopped.
6. **Given** a deterministic seed and identical configuration, **When** two separate runs are started, **Then** they produce the same sequence of values.
7. **Given** a running Simulator, **When** the system restarts (Worker process), **Then** the Simulator resumes producing measurements automatically.

---

### User Story 3 — Observe latest measurement and source health (Priority: P1)

As an Operator, I want to view the latest accepted measurement value, unit, timestamps, data quality, and source health for points within my assigned scope, so that I can understand the current observed condition at a glance.

**Why this priority**: The primary operational value of VS-01 is the ability to see what is happening right now. This is the first observable outcome for end users.

**Independent Test**: An Operator opens the point detail or overview screen within their site/area scope and sees the most recent valid value, its quality, the last received time, and whether the source is healthy, stale, or missing.

**Acceptance Scenarios**:
1. **Given** an active Point with a running Simulator, **When** the Operator views the point, **Then** they see the latest accepted value, unit, source timestamp, received timestamp, and a data quality indicator.
2. **Given** an active Point whose Simulator has been paused, **When** the pause duration exceeds the expected interval plus grace period, **Then** the point shows a Missing/No Data status and the source shows Stale or Offline.
3. **Given** a Point in Missing state, **When** the Operator views the latest value display, **Then** the status reads Missing/No Data and is never shown as a numeric zero.
4. **Given** a Point whose Simulator has been resumed, **When** a new valid measurement is ingested, **Then** the source health returns to Online and the latest value updates.
5. **Given** an active Point with the Simulator running, **When** the Operator views the source health indicator, **Then** they see a count of generated, accepted, and rejected measurements.

---

### User Story 4 — Enforce role and site/area scope (Priority: P2)

As a Product Owner or Administrator, I want every command and query to enforce the user's role and assigned Site/Area scope server-side, so that internal access remains controlled and traceable.

**Why this priority**: Security is a product requirement from day one. UI-only enforcement is never sufficient.

**Independent Test**: A user assigned to Site A attempts to read or modify objects in Site B. Every request is rejected server-side regardless of the UI state.

**Acceptance Scenarios**:
1. **Given** an Operator assigned only to Site A, **When** they attempt to view a Point in Site B, **Then** the server returns a permission-denied error.
2. **Given** an Engineer assigned only to Site A, **When** they attempt to create an Asset in Site B, **Then** the server rejects the request.
3. **Given** a Viewer outside the Site scope, **When** they call any hierarchy or latest-value API, **Then** results are scoped to their authorized Sites only.
4. **Given** an Administrator, **When** they access any Site, **Then** they are permitted by virtue of the Admin role.

---

### User Story 5 — Audit trail for configuration changes (Priority: P2)

As a Reviewer or Administrator, I want important configuration changes and simulator commands to appear in the audit history, so that I can trace who changed what and when.

**Why this priority**: Internal accountability requires that sensitive operations are logged and reviewable.

**Independent Test**: An Engineer creates an Asset, then starts and stops a Simulator. A Reviewer inspects the audit log and sees each event with actor identity, timestamp, object type, action, and summary.

**Acceptance Scenarios**:
1. **Given** an Engineer creates, updates, or changes the status of a Site, Area, Asset, or Measurement Point, **When** the action completes, **Then** an audit entry is created with actor, timestamp, object type, action, and summary.
2. **Given** an Engineer creates, modifies, or deletes a Point-Source mapping, **When** the mapping change is saved, **Then** an audit entry records the change.
3. **Given** an Engineer starts, pauses, resumes, or stops a Simulator, **When** the command executes, **Then** an audit entry records the command and the Simulator identity.

### Edge Cases

- An Engineer creates a Site but never activates it; no Points can be created under it.
- An Asset is decommissioned while its Measurement Points are still Active; the system must define the transition behavior (points become Inactive or surface a warning).
- A Simulator is stopped and restarted with a different seed; the measurement sequence changes.
- Two Simulators are configured for the same Point; the system must define whether this is allowed and how latest value is resolved.
- A Simulator is configured for multiple Points; a failure on one Point measurement must not block the others.
- The Worker process crashes mid-simulation; on restart the Simulator resumes only if its persisted run state was Running before the crash.
- A measurement arrives with a source timestamp far in the future; the system defines a clock-skew boundary.
- The Simulator produces a value outside the configured min/max; the measurement is rejected as Invalid.

## Requirements *(mandatory)*

### Functional Requirements

#### Hierarchy and Configuration

- **FR-001**: The system MUST support create, read, update, and list operations for Site entities with attributes including at minimum: unique code, name, description, timezone, status (Draft, Active, Inactive).
- **FR-002**: The system MUST support create, read, update, and list operations for Area entities with attributes including at minimum: unique code (within parent Site), name, description, status (Draft, Active, Inactive), and parent Site reference.
- **FR-003**: The system MUST support create, read, update, and list operations for Asset entities with attributes including at minimum: unique code (within parent Area), name, description, status (Draft, Active, Inactive, Decommissioned), and parent Area reference.
- **FR-004**: The system MUST support create, read, update, and list operations for Measurement Point entities with attributes including at minimum: unique code (within parent Site), description, status (Draft, Active, Inactive, Decommissioned), expected interval, grace period, Metric reference, Unit reference, parent Asset reference, and Data Owner reference.
- **FR-005**: A Measurement Point MUST NOT transition to Active unless its parent Asset is Active, a Metric is selected, a Unit is selected, the expected interval is configured and greater than zero, a Data Owner is assigned, and at least one valid Data Source mapping exists.
- **FR-006**: The system MUST reject hard deletion of any entity that has associated measurement history, alert history, or dependent child entities.
- **FR-007**: Entity codes MUST be unique within their defined scope (Site code globally unique; Area code unique within Site; Asset code unique within Area; Point code unique within Site, not reassigned to a different meaning after decommission).

#### Simulator Data Source

- **FR-008**: The system MUST support create, read, update, and delete operations for Simulator Data Source configuration with attributes including at minimum: code, description, one or more Measurement Point references, interval in seconds, minimum value, maximum value, deterministic seed (numeric), and scenario type (at minimum constant/normal; ramp, noise, spike, and advanced scenario types deferred beyond VS-01).
- **FR-009**: The Simulator MUST expose commands: start, pause, resume, and stop, each transitioning the run state accordingly.
- **FR-010**: The Simulator run state MUST be persisted so that it survives Worker process restarts. On startup, the Simulator MUST automatically resume production only if its persisted run state was Running prior to the process stop; if Paused or Stopped it MUST NOT auto-resume.
- **FR-011**: The Simulator MUST produce a deterministic sequence of values when configured with the same seed and min/max range.
- **FR-012**: The Simulator MUST NOT produce a measurement when it is in Stopped or Paused state.
- **FR-013**: The Simulator MUST expose observable run metrics: current status, total generated count, accepted count, rejected count, and latest error message when present.

#### Ingestion and Measurement

- **FR-014**: Each produced measurement MUST include: a stable unique measurement identity, Measurement Point identity, Data Source identity, source timestamp, received timestamp, processing timestamp, numeric value, unit code, data quality value, and a correlation or lineage identifier.
- **FR-015**: The system MUST reject duplicate measurements based on measurement identity such that the same identity is never stored more than once.
- **FR-016**: The system MUST validate each measurement through the canonical ingestion pipeline: source authentication, mapping resolution, schema validation, duplicate check, quality assessment, and storage.
- **FR-017**: Measurements with a value outside the configured min/max range MUST be marked as Invalid quality and excluded from latest value projection.
- **FR-018**: A measurement whose source timestamp exceeds a defined clock-skew threshold relative to the received timestamp MUST be flagged as Uncertain.

#### Latest Value and Source Health

- **FR-019**: The system MUST maintain a latest accepted measurement projection for each active Measurement Point, updated only when a new accepted measurement has a source timestamp later than the current latest. When source timestamps are equal, the system MUST use the measurement sequence number or processing timestamp as the tie-breaker to determine which measurement is newer.
- **FR-020**: The latest value view MUST expose: point identity, numeric value, unit code, source timestamp, received timestamp, data quality value, and the time since last received measurement.
- **FR-021**: The system MUST compute source health based on the elapsed time since the last received measurement relative to the configured expected interval plus grace period. Health states MUST include at minimum: Online (within interval), Stale (exceeded interval but within grace), No Data (exceeded interval plus grace period). Suspended and Decommissioned states MUST also be supported for sources that are administratively suspended or decommissioned.
- **FR-022**: Missing/No Data status MUST be represented as an absence of value, never as a numeric zero.
- **FR-023**: The source health view MUST expose: run status (Running, Paused, Stopped), last received timestamp, generated count, accepted count, rejected count, online/stale/missing status.

#### Authorization and Scope

- **FR-024**: Every API endpoint and command handler MUST validate the caller's role and assigned Site/Area scope against the target entity before executing.
- **FR-025**: Read queries MUST return only entities within the caller's authorized Site/Area scope.
- **FR-026**: The Administrator role MUST be authorized for all Sites and Areas.
- **FR-027**: The Engineering role MUST be authorized to create, update, and manage hierarchy, Simulator configuration, and mappings within assigned scope. The Administrator role MUST also be authorized to create and modify hierarchy entities across all scopes.
- **FR-028**: The Operator role MUST be authorized to view hierarchy, latest values, and source health within assigned scope but MUST NOT modify any configuration.
- **FR-029**: The Manager and Viewer roles MUST be authorized to view hierarchy and current status within assigned scope but MUST NOT create or modify any entity.

#### Audit

- **FR-030**: The system MUST record an audit entry for every create, update, and status change operation on Site, Area, Asset, and Measurement Point entities.
- **FR-031**: The system MUST record an audit entry for every create, update, and delete operation on a Point-Data Source mapping.
- **FR-032**: The system MUST record an audit entry for every Simulator start, pause, resume, and stop command.
- **FR-033**: Each audit entry MUST include at minimum: actor identity, UTC timestamp, object type, object identity, action, before and after values of changed important fields, and a human-readable summary.
- **FR-034**: Audit entries MUST be append-only and immutable after creation.

### Key Entities

- **Site**: A physical or logical location being monitored. Root of the asset hierarchy. Owns Areas. Has lifecycle: Draft, Active, Inactive.
- **Area**: A sub-division within a Site, such as a building, floor, or zone. Owns Assets. Has lifecycle: Draft, Active, Inactive.
- **Asset**: A monitored piece of equipment, system, or functional unit within an Area. Owns Measurement Points. Has lifecycle: Draft, Active, Inactive, Decommissioned.
- **Measurement Point**: A single configured observation point for one metric and canonical unit. Represents what is being measured and how. Has lifecycle: Draft, Active, Inactive, Decommissioned.
- **Metric**: A stable catalog entry defining the type of measurement (e.g., Power, Temperature, Pressure).
- **Unit**: A stable catalog entry defining the unit of measure (e.g., kW, degC, bar). Related to a Metric.
- **Data Source**: A configured source of measurement data (e.g., Simulator, CSV Import, REST API). Has lifecycle: Draft, Active, Suspended, Decommissioned. A Simulator Source is a subtype with specific configuration.
- **Simulator Configuration**: A Data Source subtype with interval, min/max range, deterministic seed, scenario type, run state (Running, Paused, Stopped), and association with one or more Measurement Points.
- **Measurement**: A single data observation with identity, timestamps, value, unit, quality, and correlation reference.
- **Latest Value Projection**: A maintained view of the most recent accepted measurement per active Point.
- **Source Health**: Derived status (Online, Stale, No Data, Suspended, Decommissioned) based on expected interval, grace period, last-received time, and administrative state.
- **Audit Entry**: An immutable record of a configuration or command event with actor, timestamp, object, action, and summary.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An Engineer with assigned scope can create a complete hierarchy (Site → Area → Asset → Measurement Point) and activate all levels in under five minutes without documentation.
- **SC-002**: An Engineer can configure a Simulator, start it, and observe measurements flowing into the system within two minutes of activation.
- **SC-003**: An Operator assigned to a Site can view the latest value, unit, timestamps, quality, and source health for every active Point in that Site within a single screen or page load.
- **SC-004**: When a Simulator is paused for longer than the expected interval plus grace period, the affected Point reports Missing/No Data within one evaluation cycle after the grace period expires.
- **SC-005**: A user outside an object's Site scope receives a permission-denied response for every unauthorized API request, with no data leakage across scope boundaries.
- **SC-006**: Every configuration change and simulator control command appears in the audit log within five seconds of execution, with correct actor, timestamp, and action details.

## Clarifications

### Session 2026-07-23

The following clarifications were resolved from source documents (DOC-03, DOC-04, DOC-05, DOC-06) during the `/speckit-clarify` review:

- Q: What is the uniqueness scope for Point codes? → A: Point codes are unique within the Site, not within the Asset (DOC-03 BRULE-003, DOC-03 §8.2). Updated FR-007.
- Q: Can a Simulator serve multiple Measurement Points? → A: Yes, a Simulator can be associated with one or more Points (DOC-04 FR-SIM-001). Updated FR-008.
- Q: What happens when two measurements have the same source timestamp for latest value? → A: Use sequence number or processing timestamp as tie-breaker (DOC-06 §16.2). Updated FR-019.
- Q: Should audit entries include before/after field values? → A: Yes, for important fields (DOC-04 FR-ASSET-012). Updated FR-033.
- Q: What are the complete source health states? → A: Online / Stale / No Data / Suspended / Decommissioned (DOC-04 FR-TEL-012). The Stale intermediate state between Online and No Data is retained as a spec addition. Updated FR-021.
- Q: Can an Administrator also create/modify hierarchy? → A: Yes, per the DOC-04 §5.1 permission matrix. Updated FR-027.
- Q: Should the Simulator auto-resume on Worker restart even if it was Paused or Stopped? → A: No, only if the run state was Running before the process stopped (DOC-05 §21.2-21.3). Updated FR-010.
- Q: What minimum scenario type must the Simulator support in VS-01? → A: At minimum constant/normal scenario. Ramp, noise, spike, and other scenario types are deferred beyond VS-01 (DOC-04 FR-SIM-002). Updated FR-008.

## Assumptions

- The Metric and Unit catalogs already exist from the R0 foundation and can be seeded with initial data.
- The default expected interval for a Measurement Point is 60 seconds, matching the DOC-01 POC default.
- The default grace period is 300 seconds (5 minutes), matching the DOC-01 POC default.
- The default timezone for Sites is Asia/Ho_Chi_Minh, matching the DOC-04 working default.
- The Simulator runs as a background job within the existing Worker process using the durable job framework established in R0.
- The measurement ingestion pipeline reuses the outbox/inbox and idempotency patterns from R0.
- The authorization system provides an API to resolve the caller's roles and scoped Site/Area membership server-side.
- The audit system provides an append-only store with a documented contract for writing entries.
- The canonical unit for electricity measurements is kW and for energy is kWh, matching DOC-06 defaults.
- Clock-skew threshold for measurement timestamps is 300 seconds (5 minutes).

## Scope and Evidence Boundaries *(mandatory)*

- **Included release/capability**: R1 / VS-01 Observable Data hierarchy entities (Site, Area, Asset, Measurement Point), Simulator Data Source and engine, latest value projection, source health derivation, server-side role and scope enforcement, configuration audit trail.
- **Explicitly excluded**: CSV import or REST ingestion product capability; full telemetry history explorer; aggregates and downsampling; threshold rules; no-data rules; alerts; notifications; reports; Modbus; Edge Collector; write-back or device control; AI or machine learning; Improvement Action; Teams, ERP, MES, or CMMS integrations.
- **External approvals/dependencies**: Approved PostgreSQL development access for migration execution and integration tests. Named Data Owner for point/source mapping decisions if extending beyond Simulator. Company CI runner for automated verification.
- **Evidence classification**: PASS / FAIL / NOT_RUN / BLOCKED_BY_MISSING_TOOL / BLOCKED_BY_PACKAGE_POLICY / BLOCKED_BY_DATABASE_ACCESS / BLOCKED_BY_COMPANY_APPROVAL.
