# Catalog Contracts

## HTTP surface

- GET|POST /api/v1/metrics; GET|PUT /api/v1/metrics/{id}
- GET|POST /api/v1/units; GET|PUT /api/v1/units/{id}
- GET|PUT /api/v1/metrics/{metricId}/compatible-units
- GET|POST /api/v1/data-sources; GET|PUT|DELETE /api/v1/data-sources/{id}
- GET|POST /api/v1/source-point-mappings; GET|PUT|DELETE
  /api/v1/source-point-mappings/{id}; activate, inactivate and supersede commands

Administrator or Engineer with an existing Site scope may mutate. Create commands require
Idempotency-Key but no If-Match. Update/lifecycle/delete commands require both Idempotency-Key
and If-Match. Source and Mapping are Catalog-owned; no Catalog command writes Organization tables.

## Compatibility and mapping ports

IMetricUnitCompatibility(MetricId, UnitId) returns existence/status, compatibility, canonical flag
and provider version. Organization uses it for Point activation; Telemetry uses it for ingestion.

IActiveSimulatorMappingEligibility(PointId, at) returns exactly-one Active effective mapping or a
specific missing/multiple reason. ISourceMappingSnapshot returns Source/Mapping status, effective
period, Point ID and provider version for Acquisition/Telemetry validation. Mapping activation asks
Organization for Point readiness and scope through a public port. Mapping activation executes inside
a REPEATABLE READ transaction with lock order: Organization Point → Catalog Source/Mapping and
overlap rows → Integration outbox. Phase 2 uses a fake readiness port; real Draft Point readiness
is integrated in Phases 3-4.

## Invariants and lifecycle

Metric and Unit codes are globally unique. At most one canonical Unit exists per Metric. Idempotent
seeds include Electric Power/kW and Electrical Energy/kWh; seed presence is not Point approval.

Data Source lifecycle is Draft, Active, Suspended, Decommissioned. Mapping lifecycle is Draft, Active,
Inactive, Superseded. A Draft Point may have an Active Mapping but it is non-producing until the
Point and all ancestors are Active. Active Mapping periods are half-open and cannot overlap per
Point. Future/historical mappings may coexist.

Draft-unused Source or Mapping deletion is allowed only after owner dependency checks find no mapping
use, Run, Measurement, projection, scheduled job or other business reference. An Audit snapshot alone
does not block deletion. Operational dependency returns DEPENDENT_HISTORY.

## Events

MetricStatusChanged.v1, UnitStatusChanged.v1, MetricUnitCompatibilityChanged.v1,
DataSourceStatusChanged.v1 and SourcePointMappingChanged.v1 are emitted only when a real consumer
requires reconciliation. Audit consumes committed events through inbox_message.
