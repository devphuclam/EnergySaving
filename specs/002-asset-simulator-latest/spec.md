# Feature Specification: Asset Simulator Latest

**Feature Branch**: `002-asset-simulator-latest`

**Created**: 2026-07-23

**Status**: Draft

**Input**: User description: R1/VS-01 observable-data vertical slice — hierarchy, simulator, latest value

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Configure monitored hierarchy (Priority: P1)

As an Administrator or scoped Engineer, I want to create and activate a hierarchy of Site, Area, Asset, and Measurement Point records, so that the system knows what physical or logical location I am monitoring and what metric I am collecting. The Administrator creates the root Site and assigns Engineer scope; the Engineer then prepares and activates the lower levels.

**Why this priority**: Every later capability (ingestion, observation, rules, alerts) depends on an explicit and valid hierarchy and point configuration.

**Independent Test**: An Administrator creates a Site and assigns an Engineer to its scope. The Engineer creates Draft Area, Asset, and Measurement Point, completes Simulator configuration, then activates the hierarchy top-down. Activation respects parent-child preconditions; the Engineer cannot create a root Site.

**Acceptance Scenarios**:
1. **Given** an Administrator, **When** they create a Site with a unique code and name, **Then** the Site is created in Draft state and visible only to the Administrator and users assigned to that Site.
2. **Given** an Administrator who has created a Site, **When** they assign an Engineer to that Site scope, **Then** the Engineer sees the Site and may operate within it.
3. **Given** an Engineer who is not assigned to any Site scope, **When** they attempt to create a new root Site, **Then** the request fails with a permission error.
4. **Given** a scoped Engineer and an existing Site (Draft or Active), **When** they create an Area with a unique code, **Then** the Area is created in Draft state under that Site.
5. **Given** an existing Area (Draft or Active), **When** the Engineer creates an Asset with a unique code, **Then** the Asset is created in Draft state under that Area.
6. **Given** an existing Asset (Draft or Active) and existing Metric and Unit records, **When** the Engineer creates a Measurement Point with a unique code, **Then** the Point is created in Draft state under that Asset.
7. **Given** a Draft Site with valid required fields, **When** the Administrator activates it, **Then** the Site transitions to Active.
8. **Given** a Draft Area whose parent Site is Active, **When** the Administrator or scoped Engineer activates it, **Then** the Area transitions to Active.
9. **Given** a Draft Asset whose parent Area is Active, **When** the Administrator or scoped Engineer activates it, **Then** the Asset transitions to Active.
10. **Given** a Draft Measurement Point whose parent Asset is Active, an active Metric, an active Unit compatible with the Metric, expected_interval_seconds set to a positive value, no_data_after_seconds set to a value greater than expected_interval_seconds, a Data Owner assigned, and exactly one active Simulator mapping, **When** the Administrator or scoped Engineer activates it, **Then** the Point transitions to Active.
11. **Given** a Draft Measurement Point missing any activation prerequisite, **When** the user attempts to activate it, **Then** activation fails with a specific reason.
12. **Given** an entity with dependent measurement history, alert history, or dependent child entities, **When** a user attempts to hard delete it, **Then** the system rejects hard deletion.
13. **Given** duplicate codes within the same scope, **When** a user attempts to create the second entity, **Then** creation fails with a unique-code constraint error.

---

### User Story 2 — Configure and operate a Simulator source (Priority: P1)

As an Engineer, I want to create a Simulator Data Source and Source-Point Mapping, associate it with one or more Measurement Points (even while they are Draft), and control its run cycle (start, pause, resume, stop), so that the application can produce and ingest realistic measurement data without physical equipment.

**Why this priority**: The Simulator decouples MVP-1 development and testing from real hardware. It is the primary ingestion driver for VS-01.

**Independent Test**: An Engineer creates a Simulator for a Draft Point, creates and activates a mapping, activates the Point, then starts the Simulator. Starting the Simulator fails while the Point remains Draft. A second overlapping mapping for the same Point is rejected. Once active, measurements flow through the ingestion pipeline.

**Acceptance Scenarios**:
1. **Given** a Draft Measurement Point, **When** the Engineer creates a Simulator Data Source with an interval, min/max range, scenario type, and deterministic seed, and creates an active Source-Point Mapping for that Point, **Then** the Simulator is configured but not yet running and the mapping exists in Active state.
2. **Given** a configured Simulator with an active mapping for a still-Draft Point, **When** the Engineer attempts to start the Simulator, **Then** start fails with a specific error citing the inactive Point.
3. **Given** the Measurement Point has been activated top-down and the Simulator mapping is Active, **When** the Engineer starts the Simulator, **Then** it begins producing measurements at the configured interval.
4. **Given** a running Simulator, **When** the Engineer pauses it, **Then** measurement production stops and the run status reflects Paused.
5. **Given** a paused Simulator, **When** the Engineer resumes it, **Then** measurement production continues without gap in sequence.
6. **Given** a running or paused Simulator, **When** the Engineer stops it, **Then** the run is terminated and status reflects Stopped.
7. **Given** a deterministic seed and identical configuration (scenario, min, max), **When** two separate runs are started, **Then** they produce the same sequence of values.
8. **Given** a running Simulator, **When** the system restarts (Worker process), **Then** the Simulator resumes producing measurements only if its run state was Running before the restart.
9. **Given** a Point that already has one active Simulator mapping, **When** the Engineer attempts to activate a second overlapping Simulator mapping for that same Point, **Then** activation fails with a domain conflict error.
10. **Given** a Simulator configured for multiple Points, **When** a measurement for one Point fails validation, **Then** measurements for the other Points are not affected.

---

### User Story 3 — Observe latest measurement and source health (Priority: P1)

As an Operator, I want to view the latest accepted measurement value, unit, timestamps, data quality, and source health for points within my assigned scope, so that I can understand the current observed condition at a glance.

**Why this priority**: The primary operational value of VS-01 is the ability to see what is happening right now. This is the first observable outcome for end users.

**Independent Test**: An Operator opens the point detail or overview screen within their site/area scope and sees the most recent value, its quality (Good/Uncertain/Bad), the last received time, and whether the source is Online, Stale, No Data, Suspended, or Decommissioned.

**Acceptance Scenarios**:
1. **Given** an active Point with a running Simulator, **When** the Operator views the point, **Then** they see the latest accepted value, unit, source timestamp, received timestamp, and a data quality indicator.
2. **Given** an active Point whose Simulator has been paused, **When** the elapsed time since the last received measurement exceeds expected_interval_seconds but is within no_data_after_seconds, **Then** the source health shows Stale.
3. **Given** an active Point whose Simulator has been paused, **When** the elapsed time exceeds no_data_after_seconds, **Then** the source health shows No Data and the display shows "No Data" — never a numeric zero.
4. **Given** a Point in No Data state, **When** a new valid measurement is ingested, **Then** the source health returns to Online and the latest value updates.
5. **Given** a draft or deactivated child entity, **When** the Operator views its source health, **Then** it must not appear as operationally Online.

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
5. **Given** an Engineer with no Site scope, **When** they attempt to create a root Site, **Then** the server rejects the request with a permission error.

---

### User Story 5 — Audit trail for configuration changes (Priority: P2)

As an Administrator or an authorized user with audit-review capability, I want important configuration changes and simulator commands to appear in the audit history, so that I can trace who changed what and when.

**Why this priority**: Internal accountability requires that sensitive operations are logged and reviewable.

**Independent Test**: An Engineer creates an Asset, then starts and stops a Simulator. An Administrator inspects the audit log and sees each event with actor identity, timestamp, object type, action, and before/after values.

**Acceptance Scenarios**:
1. **Given** an Engineer creates, updates, or changes the status of a Site, Area, Asset, or Measurement Point, **When** the action completes, **Then** an audit entry is created with actor, timestamp, object type, action, and before/after values of changed fields.
2. **Given** an Engineer creates, modifies, or deletes a Point-Source mapping, **When** the mapping change is saved, **Then** an audit entry records the change.
3. **Given** an Engineer starts, pauses, resumes, or stops a Simulator, **When** the command executes, **Then** an audit entry records the command and the Simulator identity.

### Edge Cases

- An Administrator creates a Site but never activates it; Draft child entities may be prepared but cannot become Active and must not appear operationally Online.
- An Engineer without an assigned Site scope cannot create a root Site; every request to do so is rejected with a permission error.
- A Draft child under an inactive parent may be edited but cannot become Active, cannot receive Simulator measurements, and must not appear operationally Online.
- Asset decommission fails while any child Measurement Point remains Active. No automatic cascade. The Engineer or Administrator must explicitly inactivate or decommission child Points first. Decommissioned states are terminal. Failed decommission returns a specific domain error and must not partially change child state.
- A Measurement Point decommission fails while its Simulator source is running; the Simulator must be stopped first.
- A Draft and unused Data Source or Source-Point Mapping may be physically deleted only when it has no operational or referential dependency such as active or historical mapping usage, Simulator Run, Measurement, current projection, scheduled job, or other business reference. An immutable Audit Entry by itself MUST NOT prevent deletion because Audit stores identity, type, before/after values, and a human-readable snapshot without a restrictive foreign key requiring the deleted Draft object to continue existing. Once operational history or another business dependency exists, hard delete is prohibited; use Inactive, Superseded, Suspended, or Decommissioned lifecycle behavior instead. Audit append-only immutability is not affected.
- Historical, inactive, superseded, or future-effective Source-Point mappings may coexist without conflicting; only the active mapping governs production.
- A Draft Point MAY have an Active Simulator mapping, but the mapping must not produce Measurements while the Point or any ancestor is not Active. Simulator Start must fail until Source, Mapping, Point, and ancestors are Active.
- A Simulator configured for multiple Points must not allow a validation failure on one Point to block measurements for others.
- The Worker process crashes mid-simulation; on restart the Simulator resumes only if its persisted run state was Running before the crash.
- A measurement arrives with a source timestamp far in the future relative to the received timestamp; the quality is Uncertain with reason SOURCE_TIMESTAMP_FUTURE.
- The Simulator produces a value outside the configured min/max range; the quality is Bad with reason VALUE_OUT_OF_RANGE and the Measurement does not advance Point Latest.
- No Data after the configured threshold is a derived status, not a stored Measurement with value zero.
- The bounded IAM and Catalog seed data are development/POC configuration and do not represent production measurement-point approval.
- Successful decommission of an Asset or Measurement Point is audited.

## Requirements *(mandatory)*

### Functional Requirements

#### Hierarchy and Configuration

- **FR-001**: The system MUST support create, read, update, and list operations for Site entities with attributes including at minimum: unique code, name, description, timezone, status (Draft, Active, Inactive).
- **FR-002**: The system MUST support create, read, update, and list operations for Area entities with attributes including at minimum: unique code (within parent Site), name, description, status (Draft, Active, Inactive), and parent Site reference.
- **FR-003**: The system MUST support create, read, update, and list operations for Asset entities with attributes including at minimum: unique code (within parent Area), name, description, status (Draft, Active, Inactive, Decommissioned), and parent Area reference.
- **FR-004**: The system MUST support create, read, update, and list operations for Measurement Point entities with attributes including at minimum: unique code (within parent Site), description, status (Draft, Active, Inactive, Decommissioned), expected_interval_seconds, no_data_after_seconds, Metric reference, Unit reference, parent Asset reference, and data_owner_user_id.
- **FR-005**: Activation MUST be top-down. A Site may transition Draft to Active when its required fields are valid. An Area may transition Draft to Active only when its parent Site is Active. An Asset may transition Draft to Active only when its parent Area is Active. A Measurement Point may transition Draft to Active only when all of the following hold: parent Asset is Active; Metric exists and is active; Unit exists, is active, and is compatible with the Metric; expected_interval_seconds is greater than zero; no_data_after_seconds is greater than expected_interval_seconds; a Data Owner (data_owner_user_id) is assigned to an existing Active user; and exactly one valid active Simulator mapping exists.
- **FR-006**: The system MUST reject hard deletion of any entity that has associated measurement history, alert history, dependent child entities, Simulator Run, current projection, scheduled job, or other operational business reference. An immutable Audit Entry by itself MUST NOT prevent deletion because Audit stores identity, type, before/after values, and a human-readable snapshot without a restrictive foreign key requiring the deleted object to continue existing.
- **FR-007**: Entity codes MUST be unique within their defined scope (Site code globally unique; Area code unique within Site; Asset code unique within Area; Point code unique within Site, not reassigned to a different meaning after decommission).

#### Measurement Point Activation Preconditions

- **FR-AP-001**: Draft child entities MAY be created under an existing parent regardless of the parent's status (Draft or Active).
- **FR-AP-002**: A Draft child under an inactive parent MAY be edited but MUST NOT become Active, MUST NOT receive Simulator measurements, and MUST NOT appear operationally Online.
- **FR-AP-003**: A Metric MUST exist with an Active status before it can be referenced by an activating Measurement Point.
- **FR-AP-004**: A Unit MUST exist, be Active, and be compatible with the referenced Metric before it can be referenced by an activating Measurement Point.
- **FR-AP-005**: Measurement Point activation MUST fail with a specific reason when any prerequisite is unmet.

#### Simulator Data Source

- **FR-008**: The system MUST support create, read, update, and delete operations for a Catalog-owned Simulator Data Source and its Acquisition-owned Simulator Configuration aggregate. The Data Source includes at minimum code, description, and lifecycle; each immutable configuration version includes interval in seconds, minimum value, maximum value, deterministic seed (numeric), and scenario type (constant or normal); Point associations are represented by Source-Point Mappings. A constant scenario requires minimum_value equals maximum_value; a normal scenario requires minimum_value less than maximum_value. Ramp, spike, advanced noise, seasonal, and ML scenarios are outside VS-01 scope.
- **FR-009**: The Simulator MUST expose commands: start, pause, resume, and stop, each transitioning the run state accordingly.
- **FR-010**: The Simulator run state MUST be persisted so that it survives Worker process restarts. On startup, the Simulator MUST automatically resume production only if its persisted run state was Running prior to the process stop; if Paused or Stopped it MUST NOT auto-resume.
- **FR-011**: The Simulator MUST produce a deterministic sequence of values when configured with the same seed, scenario type, and min/max range.
- **FR-012**: The Simulator MUST NOT produce a measurement when it is in Stopped or Paused state.
- **FR-013**: The Simulator MUST expose observable run metrics: current status, total generated count, accepted count, rejected count, and latest error message when present.
- **FR-014**: One Simulator Data Source MAY map to one or more Measurement Points.
- **FR-015**: One Measurement Point MUST have at most one active Simulator mapping at a time. Historical, inactive, superseded, or future-effective mappings may coexist. Activating a second overlapping Simulator mapping for the same Point MUST fail with a domain conflict error.
- **FR-016**: Simulator Start MUST fail while the source, mapping, Measurement Point, or any ancestor is not Active. A Draft Point MAY have an Active Simulator mapping, but the mapping MUST NOT produce Measurements until the Point and all ancestors are Active.

#### Ingestion and Measurement

- **FR-017**: Each produced measurement MUST include: a stable unique measurement identity, Measurement Point identity, Data Source identity, source timestamp, received timestamp, processing timestamp, numeric value, unit code, data quality value, and a correlation or lineage identifier.
- **FR-018**: A valid repeated Measurement with the same `measurement_id` and the same canonical request fingerprint MUST be classified as **Duplicate**. Duplicate MUST return the exact stored original terminal result, MUST NOT create a second `measurement_identity` result or `measurement_raw` row, and MUST NOT increment Accepted or Rejected counters again. Rejected is reserved for the validation, trust, and business failures defined by the Telemetry contract. The same `measurement_id` with a different fingerprint MUST return `IDEMPOTENCY_CONFLICT`. The same measurement identity MUST never be stored more than once.
- **FR-019**: The canonical ingestion path for Simulator-produced Measurements MUST validate: trusted internal producer identity (not external transport credential); active Simulator Data Source; active effective Source-Point Mapping; active Measurement Point; Metric and Unit compatibility; schema and timestamp; measurement identity and duplicate status; value and range quality; lineage or correlation; and persistence result. External source authentication is outside this feature.
- **FR-020**: A measurement whose value is outside the configured min/max range MUST be classified as Bad quality with reason code VALUE_OUT_OF_RANGE and MUST NOT advance the Point Latest projection.
- **FR-021**: A measurement whose source timestamp exceeds a defined clock-skew threshold relative to the received timestamp MUST be classified as Uncertain quality with reason code SOURCE_TIMESTAMP_FUTURE.

#### Latest Value and Source Health

- **FR-022**: The system MUST maintain a latest accepted measurement projection for each active Measurement Point. Good and Uncertain Measurements are Latest-eligible; Bad Measurements MUST NOT advance Latest. Latest updates only when an eligible Measurement has a source timestamp later than the current latest. When source timestamps are equal, the system MUST use the measurement sequence number, processing timestamp, and measurement identity as deterministic tie-breakers.
- **FR-023**: The latest value view MUST expose: point identity, numeric value, unit code, source timestamp, received timestamp, data quality value, and the time since last received measurement.
- **FR-024**: The system MUST compute source health based on elapsed time since the last received accepted measurement. Health states: Online (elapsed ≤ expected_interval_seconds); Stale (expected_interval_seconds < elapsed ≤ no_data_after_seconds); No Data (elapsed > no_data_after_seconds). Suspended and Decommissioned states MUST also be supported for sources that are administratively suspended or decommissioned.
- **FR-025**: expected_interval_seconds MUST be greater than zero. no_data_after_seconds MUST be greater than expected_interval_seconds. POC defaults: expected_interval_seconds = 60, no_data_after_seconds = 300.
- **FR-026**: No Data is a derived status and MUST be displayed as "No Data". It MUST NOT be stored as a Measurement and MUST NOT be represented by a numeric zero.
- **FR-027**: The source health view MUST expose: run status (Running, Paused, Stopped), last received timestamp, generated count, accepted count, rejected count, and current health state.

#### Authorization and Scope

- **FR-028**: Every API endpoint and command handler MUST validate the caller's role and assigned Site/Area scope against the target entity before executing.
- **FR-029**: Read queries MUST return only entities within the caller's authorized Site/Area scope.
- **FR-030**: The Administrator role MUST be authorized for all Sites and Areas.
- **FR-031**: The Engineer role MUST be authorized to create, update, and manage hierarchy (Area, Asset, Measurement Point), Simulator configuration, and mappings within assigned scope. An Engineer MUST NOT create a root Site. The Administrator role MUST also be authorized to create and modify hierarchy entities and Sites across all scopes.
- **FR-032**: The Operator role MUST be authorized to view hierarchy, latest values, and source health within assigned scope but MUST NOT modify any configuration.
- **FR-033**: The Manager role MUST be authorized to view hierarchy and current status within assigned scope but MUST NOT create or modify any entity.
- **FR-034**: The Viewer role MUST be authorized to view hierarchy and current status within assigned scope but MUST NOT create or modify any entity. Viewer is a read-only permission set distinct from Manager.

#### Asset and Point Decommission

- **FR-DC-001**: Asset decommission MUST fail while any child Measurement Point remains Active. The system MUST NOT automatically cascade Active child Points to Inactive or Decommissioned.
- **FR-DC-002**: Measurement Point decommission MUST fail while the Simulator source is running; the Simulator must be stopped first.
- **FR-DC-003**: Decommissioned Asset and Measurement Point states are terminal. A decommissioned entity MUST NOT be reactivated.
- **FR-DC-004**: Failed decommission MUST return a specific domain error and MUST NOT partially change child entity state.
- **FR-DC-005**: Successful decommission MUST be audited.

#### Audit

- **FR-035**: The system MUST record an audit entry for every create, update, and status change operation on Site, Area, Asset, and Measurement Point entities.
- **FR-036**: The system MUST record an audit entry for every create, update, and delete operation on a Point-Data Source mapping.
- **FR-037**: The system MUST record an audit entry for every Simulator start, pause, resume, and stop command.
- **FR-038**: Each audit entry MUST include at minimum: actor identity, UTC timestamp, object type, object identity, action, before and after values of changed important fields, and a human-readable summary.
- **FR-039**: Audit entries MUST be append-only and immutable after creation.

#### Minimal IAM Foundation

- **FR-IAM-001**: The system MUST support local internal user identity with fields including at minimum: username, hashed credential, status (Active, Disabled).
- **FR-IAM-002**: The system MUST support role assignment per user for the following base roles: Administrator, Engineer, Operator, Manager, Viewer. Reviewer is a responsibility or permission assigned to an appropriate Manager or other authorized user and MUST NOT be a sixth base role.
- **FR-IAM-003**: The system MUST support Site scope assignment per user, and optionally Area scope assignment. An Engineer without an assigned Site scope MUST NOT create a root Site.
- **FR-IAM-004**: The system MUST resolve the caller's principal, roles, and scopes server-side on every authenticated request.
- **FR-IAM-005**: The system MUST enforce server-side role authorization and server-side object-scope authorization for every command and query.
- **FR-IAM-006**: The system MUST provide deterministic POC test-user and scope seeds for development and acceptance testing. At minimum: one Administrator with global scope; one Engineer assigned to a test Site; one Operator assigned to a test Site; one Manager assigned to a test Site; one Viewer assigned to a test Site.
- **FR-IAM-007**: Authorization audit MUST be recorded where required by this specification.
- **FR-IAM-008**: SSO, password-reset workflow, enterprise directory integration, complete user-administration UI, and Teams or other integrations are outside VS-01 scope.

#### Data Source and Mapping Lifecycle

- **FR-DS-001**: Data Source lifecycle MUST be: Draft, Active, Suspended, Decommissioned.
- **FR-DS-002**: Source-Point Mapping lifecycle MUST be: Draft, Active, Inactive, Superseded.
- **FR-DS-003**: A Draft and unused Data Source or Source-Point Mapping MAY be physically deleted only when it has no operational or referential dependency such as active or historical mapping usage, Simulator Run, Measurement, current projection, scheduled job, or other business reference. An immutable Audit Entry by itself MUST NOT prevent deletion because Audit stores identity, type, before/after values, and a human-readable snapshot without a restrictive foreign key requiring the deleted Draft object to continue existing. Once operational history or another business dependency exists, hard delete is prohibited; deactivate, supersede, suspend, or decommission instead.
- **FR-DS-004**: Attempted hard delete of a Data Source or mapping with dependent operational history MUST fail with a specific message. Audit-only references do not constitute operational history for the purpose of blocking deletion.

#### Catalog (Metric and Unit)

- **FR-CAT-001**: The system MUST support minimal Metric persistence with at minimum: unique code, name, status (Active, Inactive).
- **FR-CAT-002**: The system MUST support minimal Unit persistence with at minimum: unique code, symbol, status (Active, Inactive), and Metric compatibility reference.
- **FR-CAT-003**: The system MUST support Metric-to-Unit compatibility association.
- **FR-CAT-004**: The system MUST provide idempotent development and POC seed data for at minimum: Metric "Electric Power" with canonical Unit "kW"; Metric "Electrical Energy" with canonical Unit "kWh". Seeds are development/POC configuration and do not constitute production measurement-point approval.

#### Data Owner

- **FR-DO-001**: The Data Owner of a Measurement Point MUST reference a stable internal user identity via data_owner_user_id.
- **FR-DO-002**: The referenced user MUST exist in the IAM system, MUST be Active, and MUST be within an appropriate organizational scope.
- **FR-DO-003**: Being assigned as a Data Owner MUST NOT automatically grant Reviewer or Administrator permission or audit-review capability.

### Key Entities

- **Site**: A physical or logical location being monitored. Root of the asset hierarchy. Owns Areas. Has lifecycle: Draft, Active, Inactive.
- **Area**: A sub-division within a Site, such as a building, floor, or zone. Owns Assets. Has lifecycle: Draft, Active, Inactive.
- **Asset**: A monitored piece of equipment, system, or functional unit within an Area. Owns Measurement Points. Has lifecycle: Draft, Active, Inactive, Decommissioned (terminal).
- **Measurement Point**: A single configured observation location for one metric and canonical unit. Has lifecycle: Draft, Active, Inactive, Decommissioned (terminal). References a Metric, a Unit, an Asset, a Data Owner (internal user identity), expected_interval_seconds, and no_data_after_seconds.
- **Metric**: A stable catalog entry defining the type of measurement (e.g., Electric Power, Electrical Energy). Has lifecycle: Active, Inactive.
- **Unit**: A stable catalog entry defining the unit of measure (e.g., kW, kWh). Related to a Metric via compatibility. Has lifecycle: Active, Inactive.
- **Data Source**: A Catalog-owned measurement origin (e.g., Simulator) with lifecycle Draft, Active, Suspended, or Decommissioned. It does not own executable generator parameters, Run state, or direct Point associations.
- **Source-Point Mapping**: A Catalog-owned effective-dated, approved association from a Data Source to a Measurement Point. It owns mapping lifecycle Draft, Active, Inactive, or Superseded and the effective-period overlap rule.
- **Simulator Configuration**: An Acquisition-owned configuration aggregate that references the Catalog Data Source and contains immutable versions. A version contains interval, min/max range, deterministic seed, scenario type, algorithm ID, and algorithm version; it does not own Run state or direct Point associations. Scenario type is either constant (min equals max) or normal (min less than max).
- **Simulator Run**: An Acquisition-owned execution instance that references one immutable Simulator Configuration version and owns Running, Paused, or Stopped state, counters, lease, and per-Point pinned state.
- **Measurement**: A single data observation with stable identity, timestamps, numeric value, unit, quality (Good, Uncertain, or Bad), reason code, and correlation reference. No Data is a derived source status, not a stored Measurement.
- **Latest Value Projection**: A maintained view of the most recent accepted Measurement per active Point, updated by source timestamp with tie-breaker.
- **Source Health**: Derived status (Online, Stale, No Data, Suspended, Decommissioned) computed from expected_interval_seconds, no_data_after_seconds, and last-received time.
- **Audit Entry**: An immutable record of a configuration or command event with actor, timestamp, object, action, before/after values, and summary. Stores identity and type without a restrictive foreign key that would block Draft-object deletion.
- **Internal User**: A local identity record with username, hashed credential, status (Active, Disabled), role assignment (Administrator, Engineer, Operator, Manager, or Viewer — base roles only; Reviewer is a permission), and Site/Area scope assignment.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An Administrator can create a root Site, assign an Engineer to its scope, after which the Engineer can prepare Draft Area, Asset, Measurement Point, complete Simulator configuration, and activate the hierarchy top-down — all within five minutes without documentation.
- **SC-002**: An Engineer can configure a Simulator for a Draft Point, create and activate the mapping, activate the Point, start the Simulator, and observe measurements flowing into the system within two minutes of Point activation.
- **SC-003**: An Operator assigned to a Site can view the latest value, unit, timestamps, quality, and source health for every active Point in that Site within a single screen or page load.
- **SC-004**: When a Simulator is paused for longer than no_data_after_seconds, the affected Point reports No Data within one evaluation cycle after the threshold is crossed. No Data is never displayed as zero.
- **SC-005**: A user outside an object's Site scope receives a permission-denied response for every unauthorized API request, with no data leakage across scope boundaries. An Engineer without a Site scope cannot create a root Site.
- **SC-006**: Every configuration change and simulator control command appears in the audit log within five seconds of execution, with correct actor, timestamp, action, and before/after values.
- **SC-007**: An Engineer cannot activate a second overlapping Simulator mapping for the same Point; the system returns a domain conflict error.
- **SC-008**: An Engineer cannot hard-delete a Data Source or mapping that has dependent operational history; the system rejects with a specific message. An Audit Entry by itself does not block deletion.
- **SC-009**: Asset decommission fails while any child Measurement Point remains Active. The system does not automatically cascade. Successful decommission is audited.

## Clarifications

### Session 2026-07-23

The first pass resolved 8 items from source documents (DOC-03, DOC-04, DOC-05, DOC-06).

The second pass (targeted hardening) applied 12 decision sets covering hierarchy activation, cardinality, quality terminology, source health, Metric/Unit scope, IAM scope, Data Owner, scenario semantics, lifecycle, ingestion, and consistency cleanup.

The third pass (authoritative correction) applied the following decisions:

- Q: Who creates the root Site? → A: Only the Administrator. An Engineer without an assigned Site scope MUST NOT create a root Site. The Administrator creates the Site and assigns scope. Updated US1, FR-031, FR-IAM-003, SC-001, all acceptance scenarios, edge cases. (Correction item 1)
- Q: What happens when an Asset with Active child Points is decommissioned? → A: Decommission fails while any child MP is Active. No automatic cascade. Children must be explicitly handled first. Decommissioned states are terminal. Failed decommission returns a specific domain error with no partial state change. Updated edge cases, added FR-DC-001..005. (Correction item 2)
- Q: Does an Audit entry by itself prevent Draft-object deletion? → A: No. Audit stores identity/type/values/snapshot without a restrictive FK. Once operational history (run, Measurement, projection, job, etc.) exists, hard delete is prohibited. Updated FR-006, FR-DS-003, FR-DS-004, SC-008, edge cases. Audit append-only immutability is not weakened. (Correction item 3)
- Q: What is the correct Point activation and configuration order? → A: Draft Point may have an Active Simulator mapping, but the mapping must not produce Measurements until Point and ancestors are Active. Simulator Start must fail until Source, Mapping, Point, and ancestors are Active. Point activation requires exactly one Active mapping. Updated US2 scenarios 1-3, added FR-016. (Correction item 4)
- Q: Is Reviewer a base role? → A: No. Base roles are Administrator, Engineer, Operator, Manager, Viewer. Reviewer is a responsibility or permission assigned to an authorized user. Data Owner assignment does not grant audit-review. Manager and Viewer remain distinct. Updated FR-IAM-002, US5 description, Key Entities. (Correction item 5)
- Q: Are Manager and Viewer the same or distinct? → A: Distinct. Manager and Viewer remain separate permission sets even when upstream documents group them. Updated FR-033, FR-034, Key Entities. (Correction item 5)

## Assumptions

- The durable job framework established in R0 provides the foundation for the Simulator background engine and outbox/inbox processing.
- The measurement ingestion pipeline reuses the transactional outbox/inbox and idempotency patterns from R0.
- The audit system provides an append-only store with a documented contract for writing entries. Audit entries store identity, type, before/after values, and a human-readable snapshot without a restrictive foreign key requiring the deleted object to continue existing.
- The default timezone for Sites is Asia/Ho_Chi_Minh, matching the DOC-04 working default.
- `clock_skew_threshold_seconds` is configurable and defaults to 300 seconds. A safely parsed future-skewed Measurement is Uncertain with reason `SOURCE_TIMESTAMP_FUTURE` (P-002).
- Good and Uncertain Measurements are Latest-eligible; Bad Measurements do not advance Latest. No Data is derived and is never represented by numeric zero (P-001).
- IAM persistence and end-to-end authorization tests remain BLOCKED_BY_DATABASE_ACCESS if PostgreSQL execution is unavailable; domain and application authorization logic may still be tested independently.
- Catalog seed data is development/POC configuration and does not represent production measurement-point approval.

## Scope and Evidence Boundaries *(mandatory)*

- **Included release/capability**: R1 / VS-01 Observable Data: hierarchy entities (Site, Area, Asset, Measurement Point) with top-down activation; Administrator-bootstrapped root Site with Engineer scope assignment; minimal Metric and Unit persistence with seed data; minimal IAM foundation with local user, five base roles (Administrator, Engineer, Operator, Manager, Viewer), Site/Area scope, and Reviewer as responsibility; Simulator Data Source and engine with constant and normal scenarios; Source-Point Mapping lifecycle; latest value projection; source health derivation (Online/Stale/No Data/Suspended/Decommissioned); server-side role and scope enforcement; Asset and Point decommission rules (cascade prohibited, terminal state); Data Owner user reference; configuration and mapping lifecycle management (Audit does not block Draft deletion); configuration audit trail.
- **Explicitly excluded**: CSV import or REST ingestion product capability; full telemetry history explorer; aggregates and downsampling; threshold rules; no-data rules; alerts; notifications; reports; Modbus; Edge Collector; write-back or device control; AI or machine learning; Improvement Action; Teams, ERP, MES, or CMMS integrations; SSO; password-reset workflow; enterprise directory integration; complete user-administration UI; ramp, spike, advanced noise, seasonal, or ML Simulator scenarios; external transport credential authentication; Reviewer as a sixth base role; automatic child state cascade on decommission.
- **External approvals/dependencies**: Approved PostgreSQL development access for migration execution and integration tests. Named Data Owner for point/source mapping decisions if extending beyond Simulator. Company CI runner for automated verification.
- **Evidence classification**: PASS / FAIL / NOT_RUN / BLOCKED_BY_MISSING_TOOL / BLOCKED_BY_PACKAGE_POLICY / BLOCKED_BY_DATABASE_ACCESS / BLOCKED_BY_COMPANY_APPROVAL.
