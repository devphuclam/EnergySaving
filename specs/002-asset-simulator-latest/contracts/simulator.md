# Acquisition Simulator Contracts

## HTTP surface

- POST /api/v1/simulators/{sourceId}/start|pause|resume|stop
- GET /api/v1/simulators/{sourceId}/run
- Configuration version commands are scoped to the Catalog Source and Acquisition configuration
  aggregate.

Engineer with Site scope or Administrator may mutate. Create commands require Idempotency-Key
but no If-Match. Update/lifecycle commands require both Idempotency-Key and If-Match.
Invalid transitions return PRECONDITION_FAILED. Start asks Catalog for Source and Mapping status
and Organization for Point/ancestor readiness; a Draft Point never produces. Start acquires
deterministic locks in the required flow order: Organization Point/ancestors → Catalog
Source/Mapping → Acquisition Run/Run-Point rows → Integration outbox.

## Immutable configuration and algorithm

simulator_configuration is a head with current_version. Every edit creates an immutable
simulator_configuration_version row. A Run stores the exact configuration ID/version and later
edits affect only future Start commands.

### IUMP-DETERMINISTIC-V1 normative specification

Algorithm ID: `IUMP-DETERMINISTIC-V1`. Every Run stores the exact `algorithm_version` as part
of the immutable configuration.

#### PRNG core

Use PCG32 (pcg_output_rxs_m_xs_64_32) with:

- **Multiplier**: `6364136223846793005` (0x5851F42D4C957F2D)
- **Increment/stream**: `1442695040888963407` (0x14057B7EF767814F) for stream 0; the stream
  selection is part of the serialized state.
- **Arithmetic**: Unsigned 64-bit with well-defined silent overflow (truncation to 64 bits).
- **Output function**: `pcg_output_rxs_m_xs_64_32` — XOR high 32 bits of (state XOR (state >>
  22)) with (state >> 32), then multiply by 0xDA442D24 and XOR with (result >> 32).

#### State derivation

Initial state is derived from five immutable inputs:

1. `deterministic_seed` (uint64 from configuration, zero-padded if smaller)
2. `stable_point_id` (UUID as 16 bytes, interpreted as two 64-bit integers mixed via the PRNG
   advance function)
3. `configuration_id` (UUID, mixed into state via 4 advance calls)
4. `configuration_version` (bigint, mixed via 2 advance calls)
5. `algorithm_version` (bigint, mixed via 2 advance calls)

The derivation calls `pcg_advance` after absorbing each input. The resulting state is the
initial generator state for the Run+Point stream.

#### UUID byte/string normalization and encoding

All UUIDs in state derivation use canonical lowercase hex format with dashes as 36-character
UTF-8 strings. No byte swapping is performed on UUID fields — the standard string
representation is used for all hash/PRNG-mix inputs.

#### PRNG draws and Normal conversion

Each Normal value requires exactly two PRNG draws (uint32) and uses a cached spare for
efficiency via the Box-Muller polar form:

1. Draw `u1`, `u2` from PCG32.
2. Convert each to `(0,1]` open-interval float: `(draw + 1.0) / 4294967296.0` (maps [0, 2^32)
   to (0, 1]).
3. Apply Box-Muller: `z0 = sqrt(-2 * ln(u1)) * cos(2 * pi * u2)`;
   `z1 = sqrt(-2 * ln(u1)) * sin(2 * pi * u2)`.
4. `z0` is returned, `z1` is cached as spare for the next call.
5. Scale and shift: `value = midpoint + z0 * sigma`, where:
   - `midpoint = (maximum_value + minimum_value) / 2`
   - `sigma = (maximum_value - minimum_value) / 6`
6. Clamp to `[minimum_value, maximum_value]`.
7. On the next call, if spare is populated, return the cached spare (consume 0 fresh draws).

#### Constant scenario

Constant emits exactly `minimum_value` (which equals `maximum_value`). No PRNG state is
consumed. source_sequence does not advance.

#### State and spare serialization

The serialized state (`simulator_run_point_state.prng_state`) is an opaque byte array
containing:

- `state` (uint64, 8 bytes, little-endian)
- `increment` (uint64, 8 bytes, little-endian)
- `spare_valid` (bool, 1 byte)
- `spare_value` (float64, 8 bytes, IEEE 754 little-endian)

Total: 25 bytes.

#### Numeric precision and rounding

All internal Normal computation uses IEEE 754 float64 (double precision). The final value is
rounded to 4 decimal places via `Math.Round(value, 4, MidpointRounding.ToEven)`. The rounded
value is the production output.

#### Output clamp behavior

After rounding, if `value < minimum_value`, output = `minimum_value`. If `value > maximum_value`,
output = `maximum_value`. This is deterministic clamping — no rejection sampling on the output.
Bounds check uses `value >= min && value <= max` on the rounded float64; rejection only protects
against float64 rounding excursions beyond bounds.

#### Restart behavior

On Worker restart, the persisted `simulator_run_point_state.prng_state` is deserialized and
used as the current generator state. The Run+Point stream continues from the next
source_sequence. No initial-state derivation is repeated. Retries use the persisted
production-attempt value and do not consume PRNG state again.

#### Golden test vectors

Three normative golden vectors are required during implementation preparation. Each vector
specifies:

- **Vector 1 — Constant**: `algorithm_id`, `algorithm_version`, `seed`, `point_id`,
  `configuration_id`, `configuration_version`, `scenario=Constant`, `minimum_value`,
  `maximum_value`. Output: exactly `minimum_value`, source_sequence=0.
- **Vector 2 — First Normal value**: Same identity inputs with `scenario=Normal`,
  `minimum_value < maximum_value`. Output: the first Normal value rounded to 4 decimal places,
  source_sequence=1.
- **Vector 3 — Restart/resume Normal value**: Same configuration, after persisting state at
  source_sequence=1, restart the generator from the persisted state. Output: the second Normal
  value (cached spare or fresh), source_sequence=2.

Inputs and expected outputs are recorded in `/tests/GoldenVectors/` during implementation.

### Fixed UUID namespace

Repository namespace UUID for deterministic identity derivation:

```
IUMP_NAMESPACE_UUID = 02e993bb-c767-5ff6-963f-530e1dfdff6b
```

Derivation: UUIDv5(DNS namespace `6ba7b810-9dad-11d1-80b4-00c04fd430c8`,
name = `"iump.idea-technology.com"`).

### Measurement identity derivation

```
namespace = IUMP_NAMESPACE_UUID (02e993bb-c767-5ff6-963f-530e1dfdff6b)
name = "IUMP:SIMULATOR:V1|source_id|run_id|point_id|mapping_id|source_sequence|algorithm_version"
```

Rules:
- All UUIDs in the name string are lowercase canonical format with dashes.
- `source_sequence` is decimal (no leading zeros unless zero itself).
- Separator is literal `|` (U+007C, no escaping needed because the fields never contain `|`).
- UTF-8 encoding of the full name string.
- SHA-1 hash, UUIDv5 byte-order rules per RFC 4122.
- Algorithm and configuration serialization version `V1` is fixed for this contract.

Acquisition generates `measurement_id` before calling Telemetry. Telemetry MUST NOT generate a
replacement measurement_id.

## Run lifecycle and Worker

Start creates a new Running Run and resets independent Run+Point state. Running -> Paused/Stopped;
Paused -> Running/Stopped; Stopped is terminal for that Run. Resume continues sequence and
generator state. Only persisted Running Runs auto-recover after Worker restart. Lease/version
prevents two workers producing one slot.

### Input snapshot at Start

At Start, `simulator_run_point_state` pins:

- `point_id` — the Measurement Point identity.
- `point_version_at_start` — optimistic concurrency version at Start.
- `mapping_id` — the active Catalog mapping identity.
- `mapping_version` — mapping version at Start.
- `metric_id` — resolved Metric identity.
- `unit_id` — resolved Unit identity.
- `unit_code` — resolved Unit code.
- `source_version` — Catalog Data Source version at Start.
- `next_source_sequence` — first sequence number for this Run+Point (starts at 0).
- `prng_state` — initial PCG32 state and cached spare (25 bytes).
- `next_due_at` — calculated next due timestamp.

These pinned identities bind the Run to the exact Catalog and Organization state at creation.
Changing, superseding or inactivating a Mapping after Start MUST NOT change the identity of an
already reserved production attempt.

### Owner state change behavior during Running Run

- Already-reserved Pending production attempts retain their pinned Mapping identity, value
  and measurement_id.
- Future production stops safely when a non-terminal owner change is detected: if Source,
  Mapping, Point or any ancestor is no longer Active, the Worker faults the next production
  cycle with a specific error (`SOURCE_INACTIVE`, `MAPPING_INACTIVE`, `POINT_INACTIVE`,
  `ANCESTOR_INACTIVE`).
- The Run does NOT silently switch to a new Mapping.
- Using a replacement Mapping requires an explicit new Start (new Run).
- Resulting Run status is `Stopped` with error code, audited.
- Reconciliation job flags stale pinned-identity references for operator review.

## Production-attempt checkpoint

Acquisition owns `acquisition.simulator_production_attempt`. For each due Run+Point, the Worker:

1. Acquires the Run+Point lease (prevents concurrent production).
2. Derives the deterministic value and `measurement_id` from the persisted state.
3. Opens an Acquisition transaction (REPEATABLE READ):
   - INSERT `simulator_production_attempt` with status `Pending` if absent for
     `(run_id, point_id, source_sequence)`. (Idempotent — if exists, skip insert.)
   - Persist the next PRNG state and increment `next_source_sequence` in
     `simulator_run_point_state`.
   - Record `Generated` exactly once.
   - COMMIT.
4. Calls Telemetry `IngestMeasurement` outside the transaction with the full payload:
   `measurement_id, source_id, simulator_run_id, point_id, mapping_id, mapping_version,
   source_sequence, algorithm_id, algorithm_version, simulator_configuration_id,
   configuration_version, source_timestamp, numeric_value, unit_code, producer_identity,
   correlation_id, lineage_id`.
5. Finalizes the same attempt idempotently:
   - If Telemetry returned `Accepted`: set `status=Completed`, `telemetry_outcome=Accepted`,
     increment `Accepted` counter.
   - If `Rejected`: set `status=Completed`, `telemetry_outcome=Rejected`, increment `Rejected`
     counter.
   - If `Duplicate`: set `telemetry_outcome=Duplicate`, `original_classification` from
     Telemetry response (Accepted or Rejected), and apply that original classification for
     counters.

### Crash recovery

- Crash after Telemetry persistence but before finalization: retry returns `Duplicate` plus
  original classification, allowing the `Pending` attempt to finalize correctly.
- Crash before Telemetry: `Pending` attempt is retried with the same value, mapping snapshot
  and `measurement_id`. The deterministic algorithm re-derives the identical value without
  consuming new PRNG state.

### `simulator_production_attempt` minimal fields

- `simulator_run_id` (FK to `simulator_run`)
- `point_id` (UUID)
- `source_sequence` (bigint)
- `measurement_id` (UUID, the derived stable identity)
- `mapping_id` (UUID)
- `mapping_version` (bigint)
- `configuration_id` (UUID)
- `configuration_version` (bigint)
- `algorithm_id` (varchar)
- `algorithm_version` (bigint)
- `source_timestamp` (timestamptz)
- `numeric_value` (float64)
- `unit_code` (varchar)
- `status` (varchar): `Pending`, `Completed`
- `telemetry_outcome` (varchar, nullable): `Accepted`, `Rejected`, `Duplicate`
- `original_classification` (varchar, nullable): `Accepted`, `Rejected` — populated when
  Duplicate returns an existing result
- `latest_advanced` (bool, nullable)
- `error_code` (varchar, nullable)
- `created_at` (timestamptz)
- `completed_at` (timestamptz, nullable)
- `version` (bigint, optimistic concurrency)

Primary/unique identity: `(simulator_run_id, point_id, source_sequence)`.

## Run-point state extended fields

`simulator_run_point_state` is extended to include the pinned snapshot:

- `point_id`
- `point_version_at_start`
- `mapping_id`
- `mapping_version`
- `metric_id`
- `unit_id`
- `unit_code`
- `source_version`
- `next_source_sequence`
- `prng_state` (bytea, 25 bytes)
- `cache_spare_valid`, `cache_spare_value` (derived from `prng_state`)
- `next_due_at`

## Events

SimulatorRunStateChanged.v1 is consumed by Operations, Audit and Source Status evaluation.
Catalog supplies DataSourceStatusChanged.v1 and SourcePointMappingChanged.v1. Events use outbox_event
and are deduplicated by inbox_message.
