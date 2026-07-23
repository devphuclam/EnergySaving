# Telemetry Contracts

## `IngestMeasurement`

Input contains stable `measurementId`, Point/Source/Mapping identity, optional source sequence,
source and received timestamps, numeric value, unit code, trusted internal producer identity,
correlation ID, and lineage/run ID.

Validation order:

1. authenticate trusted internal producer;
2. confirm Active Source and effective Active Mapping;
3. confirm Point and hierarchy operational eligibility;
4. confirm Active Metric/Unit compatibility;
5. validate schema, timestamp, identity, lineage, and duplicate status;
6. classify value/range and clock skew;
7. persist identity and Measurement atomically;
8. atomically advance Latest if eligible and newer;
9. return a stable outcome.

Outcomes:

- `Accepted`: persisted Good/Uncertain/Bad row, quality/reason, and whether Latest advanced.
- `Duplicate`: the identity already exists; no second row or counter effect.
- `Rejected`: safe reason code; no Measurement persisted.

P-001: Good and Uncertain are Latest-eligible; Bad is persisted but not eligible. No Data is never a
Measurement. P-002: future skew is configurable and defaults to 300 seconds; a safely interpreted
row beyond it is Uncertain with `SOURCE_TIMESTAMP_FUTURE`. Out-of-range is accepted Bad with
`VALUE_OUT_OF_RANGE`.

P-003: Latest uses the strict tuple
`(sourceTimestamp, normalizedSourceSequence, processingTimestamp, measurementId)`. Missing sequence
sorts below supplied sequence at equal source time. Compare-and-update is atomic, so retry, late
arrival, and concurrency cannot regress Latest.

## HTTP read surface

- `GET /api/v1/points/{pointId}/latest`
- `GET /api/v1/points/{pointId}/source-health`
- `GET /api/v1/sites/{siteId}/points/current`

Responses contain Point identity, value, unit, source/received timestamps, quality, elapsed time,
run status, last received time, generated/accepted/rejected counts, and health. IAM scope is applied
before lookup/list paging. A Point with no accepted observation returns Latest absent plus health
NoData, never numeric zero.

## Health evaluation

The evaluator uses server time and last accepted received time:
Online at elapsed <= expected interval; Stale above expected and <= no-data threshold; NoData above
the threshold. Decommissioned overrides Suspended, which overrides elapsed-time status. Repeated
evaluation is idempotent. Administrative and Point/run change events trigger early evaluation in
addition to a leased periodic job.

## Events

`MeasurementAccepted` contains identifiers, quality/reason, timestamps, and lineage, not a mutable
entity graph. `PointLatestAdvanced` includes old/new measurement IDs and ordering tuple.
`PointSourceHealthChanged` is emitted only on a real state transition. All are outbox-backed and
consumer-deduplicated.

