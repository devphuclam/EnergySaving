# Acquisition Simulator Contracts

## HTTP surface

- POST /api/v1/simulators/{sourceId}/start|pause|resume|stop
- GET /api/v1/simulators/{sourceId}/run
- Configuration version commands are scoped to the Catalog Source and Acquisition configuration
  aggregate.

Engineer with Site scope or Administrator may mutate. Commands require If-Match, correlation ID and
idempotency key. Invalid transitions return PRECONDITION_FAILED. Start asks Catalog for Source and
Mapping status and Organization for Point/ancestor readiness; a Draft Point never produces.

## Immutable configuration and algorithm

simulator_configuration is a head with current_version. Every edit creates an immutable
simulator_configuration_version row. A Run stores the exact configuration ID/version and later
edits affect only future Start commands.

Constant requires min=max and emits that exact value without PRNG state. Normal requires min<max and
uses IUMP-DETERMINISTIC-V1: repository-owned PCG32 plus Box-Muller normal-like transformation,
midpoint mean, range/6 sigma, cached spare and rejection/clamping to bounds. Algorithm ID/version,
seed, stable Point ID, configuration version and source sequence are complete deterministic inputs.
No platform Random, current time seed, system randomness or third-party statistics.

## Run lifecycle and Worker

Start creates a new Running Run and resets independent Run+Point state. Running -> Paused/Stopped;
Paused -> Running/Stopped; Stopped is terminal for that Run. Resume continues sequence and generator
state. Only persisted Running Runs auto-recover after Worker restart. Lease/version prevents two
workers producing one slot.

For each due Run+Point, canonical identity derives from:
IUMP:SIMULATOR:V1|source_id|run_id|point_id|mapping_id|source_sequence|algorithm_version
using a repository namespace and UTF-8 canonical bytes. Received/processing time is excluded.

Acquisition creates a unique production-attempt checkpoint for (Run, Point, Sequence) before calling
Telemetry. The Telemetry result is Accepted, Rejected or Duplicate. The checkpoint finalizes
Generated/Accepted/Rejected counters once, so a crash between transactions cannot double-count or
lose an attempt. Audit is event-driven, not a direct cross-schema write.

## Events

SimulatorRunStateChanged.v1 is consumed by Operations, Audit and Source Status evaluation.
Catalog supplies DataSourceStatusChanged.v1 and SourcePointMappingChanged.v1. Events use outbox_event
and are deduplicated by inbox_message.
