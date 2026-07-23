# Acquisition Simulator Contracts

## HTTP surface

- Simulator control: `POST /api/v1/simulators/{sourceId}/start|pause|resume|stop`.
- Run status: `GET /api/v1/simulators/{sourceId}/run`.

Mutation requires Engineer in object scope or Administrator, expected version, correlation ID, and
an idempotency key for control commands. Invalid state changes return `PRECONDITION_FAILED`.
Source and mapping commands are owned by the Catalog contract.

## Configuration and lifecycle

Constant requires min=max. Normal requires min<max and produces deterministic values within bounds.
Only these scenarios are valid. One source may map to many Points, but a Point has at most one
effective Active Simulator mapping. A Draft Point may be configured with a mapping; production still
requires an Active Point and ancestors.

Start creates a new Run and per-Point deterministic state. Pause persists state. Resume continues
the same Run. Stop is terminal for that Run; another Start creates another Run. The Worker
reacquires only persisted Running runs after restart. A lease and expected version prevent two
workers from generating the same slot.

## Worker production contract

For every due Run+Point, Acquisition constructs a canonical request with stable measurement ID,
source/run/point/mapping IDs, source sequence, source/received timestamps, value/unit, trusted
producer identity, and correlation/lineage. It calls Telemetry ingestion and records exactly one
Generated outcome, then increments Accepted or Rejected according to the returned classification.
Duplicate retries do not double-increment counters.

## Events

`SimulatorRunStarted`, `SimulatorRunPaused`, `SimulatorRunResumed`, `SimulatorRunStopped`, and
optionally `SimulatorRunFaulted`. Catalog supplies `DataSourceStatusChanged` and
`SourcePointMappingChanged`. Control events drive immutable audit; status facts allow health
re-evaluation.
