# Catalog Contracts

## HTTP surface

- `GET|POST /api/v1/metrics`, `GET|PUT /api/v1/metrics/{id}`
- `GET|POST /api/v1/units`, `GET|PUT /api/v1/units/{id}`
- `GET|PUT /api/v1/metrics/{metricId}/compatible-units`
- `GET|POST /api/v1/data-sources`, `GET|PUT|DELETE /api/v1/data-sources/{id}`
- `GET|POST /api/v1/source-point-mappings`,
  `PUT|DELETE /api/v1/source-point-mappings/{id}`, plus `activate`, `inactivate`, and `supersede`

Mutations require Engineer in scope or Administrator; POC seed application is idempotent.

## `IMetricUnitCompatibility`

Input: Metric ID and Unit ID. Output: both existence/status values, compatibility, canonical Unit
indicator, and provider version. Organization calls it for Point activation; Telemetry calls it for
canonical ingestion. A missing/inactive/incompatible pair returns a reason code, not a partial
business row.

## Invariants

Metric and Unit codes are globally unique. At most one canonical Unit exists per Metric. Seeds
include Electric Power/kW and Electrical Energy/kWh and can be applied repeatedly without
duplicates. Seed presence is not approval of any Point.

Data Source lifecycle is Draft/Active/Suspended/Decommissioned; Mapping lifecycle is
Draft/Active/Inactive/Superseded. A Point has at most one effective Active mapping, enforced with
half-open periods and transactional overlap protection. Draft-unused deletion is conditional; once
operational history exists it returns `DEPENDENT_HISTORY`.

`IActiveSimulatorMappingEligibility` answers Point activation. `ISourceMappingSnapshot` answers
Acquisition/Telemetry validation without permitting cross-schema writes. Mapping activation asks
Organization for Point readiness and scope.

## Events

`MetricStatusChanged`, `UnitStatusChanged`, `MetricUnitCompatibilityChanged`,
`DataSourceStatusChanged`, and `SourcePointMappingChanged` are published only when a real consumer
requires reconciliation.
