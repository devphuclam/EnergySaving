# Telemetry Contracts

## Internal generated-measurement contract

Input contains sourceId, pointId, mappingId/mapping version, stable producer externalId,
sequenceNumber, sourceTimestamp, numeric value, unitCode, correlationId and lineage/runId.
Trusted internal producer identity is contextual, not an external transport credential. Telemetry
generates receivedAt, processingAt, quality/reason and canonical measurementId.

Validation sequence:

1. confirm trusted internal producer context;
2. ask Catalog for Active Source and effective Active Mapping;
3. ask Organization for Active Point/ancestor operational eligibility;
4. ask Catalog for Metric/Unit compatibility;
5. validate schema, timestamp, lineage and duplicate identity;
6. classify range and clock skew;
7. persist identity/raw Measurement atomically;
8. compare-and-set Point Latest only when eligible and newer;
9. return stable outcome.

## Result

- Accepted: Measurement persisted as Good, Uncertain or Bad, with reason and Latest-advanced flag.
- Duplicate: same identity returns original measurement/classification; no second row or counter.
- Rejected: safe error code; no Measurement row.

P-001: Good and Uncertain are Latest-eligible; Bad is persisted but not eligible; No Data is never a
Measurement. P-002: future skew beyond configurable 300 seconds is preserved Uncertain with
SOURCE_TIMESTAMP_FUTURE and remains eligible. Out-of-range is Accepted Bad with VALUE_OUT_OF_RANGE.

P-003: compare sourceTimestamp, then sequence when supplied and resolving an equal timestamp, then
processingTimestamp, then measurementId. Simulator always supplies sequence. Older/out-of-order
history remains stored without Latest regression. Duplicate identity is not out-of-order data.

## Query surface

- GET /api/v1/points/{pointId}/latest
- GET /api/v1/points/{pointId}/source-health
- GET /api/v1/sites/{siteId}/points/current

Responses contain Point identity, value, unit, source/received timestamps, quality/reason, elapsed
time, Run status, last received time, Generated/Accepted/Rejected counts and Source Status. Scope is
applied before lookup/paging. No accepted observation returns Latest absent plus NoData, never zero.

## Source Status

Physical projection is telemetry.point_source_status. Worker uses server time and last accepted
received time: Online when elapsed <= expected interval; Stale when above expected and <= no-data
threshold; NoData when above threshold. Decommissioned overrides Suspended, which overrides elapsed
status. Evaluation is idempotent and validates owner snapshot/version before update. A real status
transition emits an event; repeated evaluation does not.

## Events

MeasurementAccepted.v1 contains identifiers, quality/reason, timestamps and lineage. PointLatestAdvanced.v1
contains old/new IDs and the ordering tuple. PointSourceHealthChanged.v1 is emitted only on a real
status transition. All use integration.outbox_event and inbox_message deduplication.
