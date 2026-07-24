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
deterministic locks in the required flow order: Organization Point/ancestors -> Catalog
Source/Mapping -> Acquisition Run/Run-Point rows -> Integration outbox.

## Immutable configuration and algorithm

`simulator_configuration` is a head with `current_version`. Every edit creates an immutable
`simulator_configuration_version` row. A Run stores the exact configuration ID/version and later
edits affect only future Start commands.

### IUMP-DETERMINISTIC-V1 normative specification

Algorithm ID: `IUMP-DETERMINISTIC-V1`. Every Run stores the exact `algorithm_version` as part
of the immutable configuration.

#### PRNG core

Use the fixed 32-bit projection of the PCG RXS-M-XS 64-bit permutation, named
`pcg_output_rxs_m_xs_64_32` by this contract:

- state multiplier: `6364136223846793005` (`0x5851F42D4C957F2D`);
- increment/stream 0: `1442695040888963407` (`0x14057B7EF767814F`);
- output multiplier: `12605985483714917081` (`0xAEF17502108EF2D9`);
- arithmetic: unsigned 64-bit with truncation to 64 bits after every operation;
- output: low 32 bits of the final 64-bit RXS-M-XS permutation.

#### Canonical initialization

Initialization is implementation-independent. All arithmetic marked `u64` or `u32` is truncated
immediately after the operation.

```text
MASK64 = 0xffffffffffffffff
MASK32 = 0xffffffff
STATE_MULTIPLIER = 0x5851f42d4c957f2d
INCREMENT_STREAM_0 = 0x14057b7ef767814f
OUTPUT_MULTIPLIER = 0xaef17502108ef2d9
FNV_OFFSET = 0xcbf29ce484222325
FNV_PRIME = 0x00000100000001b3

u64(x) = x AND MASK64
u32(x) = x AND MASK32
step(s) = u64(u64(s * STATE_MULTIPLIER) + INCREMENT_STREAM_0)
```

Normalize inputs as follows:

- `deterministic_seed`: unsigned 64-bit integer rendered as exactly 16 lowercase hexadecimal
  digits, without `0x`;
- Point and configuration UUIDs: canonical lowercase dashed 36-character form; UUID fields are
  characters in the seed material and are not decoded, byte-swapped or interpreted as integers;
- `configuration_version` and `algorithm_version`: unsigned base-10 with no leading zero except
  the value zero itself.

The canonical seed material is the following exact string, with literal `|` and `=` characters,
encoded as UTF-8 in display order with no BOM, terminator, whitespace or newline:

```text
IUMP-DETERMINISTIC-V1|seed={seed16hex}|point_id={point_uuid}|configuration_id={configuration_uuid}|configuration_version={configuration_version_decimal}|algorithm_version={algorithm_version_decimal}
```

Hash and initialize:

```text
h = FNV_OFFSET
for each UTF-8 byte b in canonical seed material:
    h = u64(h XOR u64(b))
    h = u64(h * FNV_PRIME)

state = 0
state = step(state)
state = u64(state + h)
state = step(state)
increment = INCREMENT_STREAM_0
spare_valid = false
spare_value = positive float64 zero
```

The resulting `state` is the initial state for the Run + Point stream. Run ID is intentionally
absent, so separate Runs with identical immutable inputs reproduce the same values.

Each PRNG draw uses the pre-transition state for output and stores the transitioned state:

```text
old = state
state = step(old)
shift = u32(old >> 59) + 5
word = u64(((old >> shift) XOR old) * OUTPUT_MULTIPLIER)
permuted = u64((word >> 43) XOR word)
draw = u32(permuted)
```

All shifts are logical unsigned shifts. `word` is truncated before the final XOR and `draw` is
the low 32 bits after the final XOR.

#### PRNG draws and Normal conversion

Normal generation uses the standard trigonometric Box-Muller transform:

1. If `spare_valid=true`, use persisted `spare_value`, set `spare_valid=false` and
   `spare_value=+0.0`, and consume zero PRNG draws.
2. Otherwise draw two uint32 values and convert them to `u1`, `u2` with
   `(draw + 1.0) / 4294967296.0`.
3. Compute `radius = sqrt(-2.0 * ln(u1))`,
   `z0 = radius * cos(2.0 * pi * u2)` and
   `z1 = radius * sin(2.0 * pi * u2)`, using the IEEE 754 binary64 value of pi whose bits are
   `0x400921fb54442d18`.
4. Use `z0` for the current value and persist `z1` with `spare_valid=true`.
5. Scale and shift selected `z`:
   `raw = midpoint + z * sigma`,
   `midpoint = (maximum_value + minimum_value) / 2.0`, and
   `sigma = (maximum_value - minimum_value) / 6.0`.
6. Round `raw` to four decimal places with ties-to-even, then deterministically clamp the rounded
   float64 to inclusive `[minimum_value, maximum_value]`.

A fresh Box-Muller pair consumes exactly two draws and its first value uses `z0`. The next Normal
value may use cached `z1` and consume zero new draws. Implementations MUST use IEEE 754 binary64
after every operation, without fused multiply-add or extended-precision retention.

#### Constant scenario

Constant emits exactly `minimum_value` (which equals `maximum_value`). It consumes no PRNG draws and
does not change serialized PRNG state. A newly reserved Constant production slot still advances
`source_sequence` exactly once under the common reservation rule.

#### State and spare serialization

The serialized state (`simulator_run_point_state.prng_state`) contains:

- `state` (uint64, 8 bytes, little-endian);
- `increment` (uint64, 8 bytes, little-endian);
- `spare_valid` (0 or 1, 1 byte);
- `spare_value` (float64, 8 bytes, IEEE 754 little-endian).

Total: 25 bytes. When `spare_valid=false`, `spare_value` MUST be canonical positive zero.

#### Numeric precision and bounding

All computation uses IEEE 754 float64. The single normative bounding policy is:

1. round the raw float64 to four decimal places using ties-to-even;
2. if rounded value is below `minimum_value`, return `minimum_value`;
3. if rounded value is above `maximum_value`, return `maximum_value`;
4. otherwise return the rounded value.

There is no output rejection sampling and no clamp before rounding.

#### Restart behavior

On Worker restart, deserialize the persisted Run-Point state and continue at stored
`next_source_sequence`; do not repeat initial-state derivation. An existing Pending production
attempt is not generator restart input: retry uses its persisted authoritative payload and does not
deserialize or advance generator state.

#### Golden test vectors

These three vectors are normative. Plan, research, quickstart and future TDD tasks reference these
literal values; implementation must not invent replacements.

Common inputs:

- `algorithm_id`: `IUMP-DETERMINISTIC-V1`
- `algorithm_version`: `1`
- `deterministic_seed`: `42` (canonical seed hex `000000000000002a`)
- Point UUID: `11111111-2222-4333-8444-555555555555`
- configuration UUID: `aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee`
- `configuration_version`: `7`
- canonical material FNV-1a-64: `0x4d97328a5ba8ffb5`
- initial serialized state hex:
  `032ba308f46f1f8e4f8167f77e7b0514000000000000000000`

| Vector | Scenario/bounds | Attempt `source_sequence` | Output | Resulting serialized state hex | Cached spare after attempt | Stored `next_source_sequence` |
|---|---|---:|---:|---|---|---:|
| 1 - Constant first slot | Constant, min=max=`12.5000` | 0 | `12.5000` | `032ba308f46f1f8e4f8167f77e7b0514000000000000000000` | invalid, `+0.0` | 1 |
| 2 - Normal first slot | Normal, min=`10.0000`, max=`20.0000` | 0 | `11.6519` | `ed99faae39338fb74f8167f77e7b0514013f80c23bc5fbfb3f` | valid, `1.7489673933378211` | 1 |
| 3 - Normal persisted-state restart | Normal, min=`10.0000`, max=`20.0000` | 1 | `17.9149` | `ed99faae39338fb74f8167f77e7b0514000000000000000000` | invalid, `+0.0` | 2 |

Vector 2 draws are `123721628` (`0x075fd79c`) and `1657810024` (`0x62d02c68`);
`z0=-2.0088412749224465` and persisted `z1=1.7489673933378211`. Vector 3 deserializes
Vector 2 state, consumes cached `z1`, and makes zero fresh draws. The table distinguishes the
sequence used by the current attempt from the next sequence stored after reservation.

### Fixed UUID namespace

Repository namespace UUID for deterministic identity derivation:

```text
IUMP_NAMESPACE_UUID = 02e993bb-c767-5ff6-963f-530e1dfdff6b
```

Derivation: UUIDv5(DNS namespace `6ba7b810-9dad-11d1-80b4-00c04fd430c8`,
name = `"iump.idea-technology.com"`).

### Measurement identity derivation

```text
namespace = IUMP_NAMESPACE_UUID (02e993bb-c767-5ff6-963f-530e1dfdff6b)
name = "IUMP:SIMULATOR:V1|source_id|run_id|point_id|mapping_id|source_sequence|algorithm_version"
```

Rules:

- all UUIDs in the name string are lowercase canonical format with dashes;
- `source_sequence` is decimal with no leading zeros unless zero itself;
- separator is literal `|` (U+007C);
- encode the full name string as UTF-8;
- use SHA-1 and UUIDv5 byte-order/version/variant rules per RFC 4122;
- algorithm and configuration serialization version `V1` is fixed.

Acquisition generates `measurement_id` before calling Telemetry. Telemetry MUST NOT generate a
replacement Measurement ID.

## Run lifecycle and Worker

Start creates a new Running Run and new per-Point state whose `next_source_sequence=0`. Running ->
Paused/Stopped; Paused -> Running/Stopped; Stopped is terminal for that Run. Resume continues the
existing sequence and generator state. Only persisted Running Runs auto-recover after Worker
restart. Lease/version prevents two workers producing one slot.

### Input snapshot at Start

At Start, `simulator_run_point_state` pins:

- `point_id` - Measurement Point identity;
- `point_version_at_start` - optimistic concurrency version at Start;
- `mapping_id` and `mapping_version` - active Catalog mapping identity/version;
- `metric_id`, `unit_id`, `unit_code` - resolved Metric/Unit identity;
- `source_version` - Catalog Data Source version at Start;
- `next_source_sequence` - zero-based cursor for the next new production slot; starts at 0;
- `prng_state` - initial PCG state and cached spare (25 bytes);
- `next_due_at` - calculated next due timestamp.

These pinned identities bind the Run to exact Catalog and Organization state at creation. Changing,
superseding or inactivating a Mapping after Start MUST NOT change the identity of an already
reserved production attempt.

### Owner state change behavior during Running Run

- Already-reserved Pending attempts retain their pinned Mapping identity, persisted value and
  `measurement_id`.
- Future production stops safely when a non-terminal owner change is detected: if Source, Mapping,
  Point or any ancestor is no longer Active, the Worker faults the next new production cycle with
  `SOURCE_INACTIVE`, `MAPPING_INACTIVE`, `POINT_INACTIVE` or `ANCESTOR_INACTIVE`.
- The Run does not silently switch to a new Mapping.
- A replacement Mapping requires an explicit new Start and new Run.
- Resulting Run status is Stopped with error code and is audited.
- Reconciliation flags stale pinned-identity references for operator review.

## Source sequence semantics

`source_sequence` is the zero-based ordinal of a production slot within one Simulator Run +
Measurement Point stream.

- `next_source_sequence` starts at 0.
- A new reservation uses `source_sequence = next_source_sequence`.
- After the Pending attempt insert succeeds, store
  `next_source_sequence = source_sequence + 1`.
- Sequence advances exactly once for each new Constant or Normal production attempt.
- Retrying an existing Pending attempt does not advance sequence.
- Pause/Resume continues the stored sequence; a new Start creates a new Run whose per-Point cursor
  starts at 0.

The production-attempt key and Measurement identity both use the attempt's `source_sequence`, never
the post-reservation `next_source_sequence`.

## Production-attempt checkpoint

Acquisition owns `acquisition.simulator_production_attempt`. For each due Run + Point, the Worker:

1. Acquires the Run-Point lease.
2. Loads an existing Pending attempt before invoking the generator.
3. If Pending exists, treats it as the authoritative retry payload and proceeds to Telemetry. It
   does not invoke the generator, deserialize or advance PRNG state, change `next_source_sequence`,
   or increment Generated.
4. Otherwise opens an Acquisition REPEATABLE READ transaction and reserves one new slot:
   - read `source_sequence = next_source_sequence`;
   - generate the value only for that new slot and derive `measurement_id`;
   - insert the complete Pending payload for `(run_id, point_id, source_sequence)`;
   - only after insert success, persist resulting PRNG state, set
     `next_source_sequence = source_sequence + 1`, and increment Generated exactly once;
   - commit. A uniqueness race reloads the winning Pending row and applies none of these state or
     counter changes.
5. Calls Telemetry outside the transaction using the persisted Pending payload:
   `measurement_id, source_id, simulator_run_id, point_id, mapping_id, mapping_version,
   source_sequence, algorithm_id, algorithm_version, simulator_configuration_id,
   configuration_version, source_timestamp, numeric_value, unit_code, producer_identity,
   correlation_id, lineage_id`.
6. Finalizes attempt and counter change atomically:
   - copy response disposition (`Accepted`, `Rejected` or `Duplicate`) and the stable original
     result into the attempt;
   - on the first Pending -> Completed transition only, increment exactly one run counter according
     to stored `final_classification`: Accepted or Rejected;
   - if already Completed with the same result, make no mutation and no counter increment;
   - a different terminal result is an invariant conflict for reconciliation.

### Crash recovery

- Crash after Telemetry persistence but before finalization: retry returns Duplicate plus the exact
  stored original result, allowing the Pending attempt to finalize correctly.
- Crash before Telemetry: load the existing Pending attempt and reuse its persisted payload. Do not
  invoke the generator, deserialize or advance PRNG state, increment sequence/Generated, or
  derive any retry field; generator state is irrelevant to this retry.

### `simulator_production_attempt` minimal fields

- `simulator_run_id` (FK to `simulator_run`)
- `source_id` (UUID)
- `point_id` (UUID)
- `source_sequence` (bigint)
- `measurement_id` (UUID, derived stable identity)
- `mapping_id`, `mapping_version`
- `configuration_id`, `configuration_version`
- `algorithm_id`, `algorithm_version`
- `source_timestamp`
- `numeric_value` (float64)
- `unit_code`
- `producer_identity` (safe trusted-producer reference)
- `correlation_id`
- `lineage_id` (UUID or reproducible immutable reference)
- `status`: Pending or Completed
- `telemetry_outcome`: Accepted, Rejected or Duplicate
- `final_classification`: Accepted or Rejected once terminal result is known
- `latest_advanced` (nullable)
- `error_code`/`rejection_code` (nullable)
- `created_at`, `completed_at`
- `version` (optimistic concurrency)

Primary/unique identity: `(simulator_run_id, point_id, source_sequence)`. `measurement_id` is also
unique. The persisted attempt payload is immutable after insertion except for terminal finalization
fields.

## Run-point state extended fields

`simulator_run_point_state` includes:

- `point_id`
- `point_version_at_start`
- `mapping_id`, `mapping_version`
- `metric_id`
- `unit_id`, `unit_code`
- `source_version`
- `next_source_sequence`
- `prng_state` (bytea, 25 bytes)
- `next_due_at`

## Events

`SimulatorRunStateChanged.v1` is consumed by Operations, Audit and Source Status evaluation.
Catalog supplies `DataSourceStatusChanged.v1` and `SourcePointMappingChanged.v1`. Events use
`outbox_event` and are deduplicated by `inbox_message`.
