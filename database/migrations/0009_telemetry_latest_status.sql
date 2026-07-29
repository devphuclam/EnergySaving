-- 0009_telemetry_latest_status.sql
-- Telemetry-owned Latest and Source Health projections.
-- Organization, Catalog, Acquisition and Operations identifiers are logical
-- references; this migration intentionally creates no cross-schema FK.
CREATE SCHEMA IF NOT EXISTS telemetry;

CREATE TABLE IF NOT EXISTS telemetry.point_latest (
    point_id uuid PRIMARY KEY,
    measurement_id uuid NOT NULL,
    source_id uuid NOT NULL,
    simulator_run_id uuid NOT NULL,
    mapping_id uuid NOT NULL,
    mapping_version bigint NOT NULL,
    numeric_value double precision NOT NULL,
    unit_code text NOT NULL,
    quality_code text NOT NULL,
    reason_code text,
    source_timestamp_utc timestamptz NOT NULL,
    source_sequence bigint,
    received_at_utc timestamptz NOT NULL,
    processing_at_utc timestamptz NOT NULL,
    ordering_source_timestamp_utc timestamptz NOT NULL,
    ordering_source_sequence bigint,
    ordering_processing_at_utc timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    updated_at_utc timestamptz NOT NULL,
    CONSTRAINT ck_point_latest_mapping_version_positive
        CHECK (mapping_version > 0),
    CONSTRAINT ck_point_latest_sequence_nonnegative
        CHECK (source_sequence IS NULL OR source_sequence >= 0),
    CONSTRAINT ck_point_latest_ordering_sequence_nonnegative
        CHECK (ordering_source_sequence IS NULL OR ordering_source_sequence >= 0),
    CONSTRAINT ck_point_latest_quality_eligible
        CHECK (quality_code IN ('Good', 'Uncertain')),
    CONSTRAINT ck_point_latest_numeric_finite
        CHECK (
            numeric_value = numeric_value
            AND numeric_value <> 'Infinity'::double precision
            AND numeric_value <> '-Infinity'::double precision
        ),
    CONSTRAINT ck_point_latest_unit_nonblank
        CHECK (length(btrim(unit_code)) > 0),
    CONSTRAINT ck_point_latest_version_positive
        CHECK (version > 0)
);

CREATE INDEX IF NOT EXISTS ix_point_latest_current
    ON telemetry.point_latest (point_id, source_timestamp_utc DESC,
        source_sequence DESC, processing_at_utc DESC, measurement_id);
CREATE INDEX IF NOT EXISTS ix_point_latest_measurement
    ON telemetry.point_latest (measurement_id);

CREATE TABLE IF NOT EXISTS telemetry.point_source_status (
    point_id uuid PRIMARY KEY,
    source_id uuid NOT NULL,
    health_status text NOT NULL,
    last_accepted_received_at_utc timestamptz,
    expected_interval_seconds integer NOT NULL,
    no_data_after_seconds integer NOT NULL,
    source_version bigint NOT NULL,
    point_version bigint NOT NULL,
    provider_version bigint NOT NULL,
    run_status text,
    generated_count bigint NOT NULL DEFAULT 0,
    accepted_count bigint NOT NULL DEFAULT 0,
    rejected_count bigint NOT NULL DEFAULT 0,
    version bigint NOT NULL DEFAULT 1,
    evaluated_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz NOT NULL,
    CONSTRAINT ck_point_source_status_health
        CHECK (health_status IN ('Online', 'Stale', 'NoData', 'Suspended', 'Decommissioned')),
    CONSTRAINT ck_point_source_status_thresholds
        CHECK (expected_interval_seconds > 0
            AND no_data_after_seconds > expected_interval_seconds),
    CONSTRAINT ck_point_source_status_versions_positive
        CHECK (source_version > 0 AND point_version > 0 AND provider_version > 0
            AND version > 0),
    CONSTRAINT ck_point_source_status_run
        CHECK (run_status IS NULL OR run_status IN ('Running', 'Paused', 'Stopped')),
    CONSTRAINT ck_point_source_status_counters
        CHECK (generated_count >= 0 AND accepted_count >= 0 AND rejected_count >= 0)
);

CREATE INDEX IF NOT EXISTS ix_point_source_status_current
    ON telemetry.point_source_status (point_id, health_status, evaluated_at_utc DESC);
CREATE INDEX IF NOT EXISTS ix_point_source_status_source
    ON telemetry.point_source_status (source_id, health_status, evaluated_at_utc DESC);

COMMENT ON TABLE telemetry.point_latest IS
    'Current eligible Good/Uncertain observation; NoData is never stored here.';
COMMENT ON TABLE telemetry.point_source_status IS
    'Derived source health projection; it contains no synthetic numeric observation.';
