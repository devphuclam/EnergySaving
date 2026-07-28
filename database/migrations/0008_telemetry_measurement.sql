-- 0008_telemetry_measurement.sql
-- Telemetry-owned immutable terminal result registry and Accepted-only raw Measurement history.
-- Organization, Catalog and Acquisition identifiers are logical references; no cross-schema FK.
CREATE SCHEMA IF NOT EXISTS telemetry;

CREATE TABLE IF NOT EXISTS telemetry.measurement_identity (
    measurement_id uuid PRIMARY KEY,
    source_id uuid NOT NULL,
    simulator_run_id uuid NOT NULL,
    point_id uuid NOT NULL,
    mapping_id uuid NOT NULL,
    mapping_version bigint NOT NULL,
    source_sequence bigint NOT NULL,
    algorithm_id text NOT NULL,
    algorithm_version integer NOT NULL,
    simulator_configuration_id uuid NOT NULL,
    configuration_version bigint NOT NULL,
    request_fingerprint bytea NOT NULL,
    final_classification text NOT NULL,
    measurement_persisted boolean NOT NULL,
    persisted_measurement_id uuid,
    quality_code text,
    reason_code text,
    rejection_code text,
    latest_advanced boolean,
    completed_at_utc timestamptz NOT NULL,
    original_correlation_id text NOT NULL,
    original_lineage_id text NOT NULL,
    CONSTRAINT uq_measurement_identity_slot
        UNIQUE (simulator_run_id, point_id, source_sequence),
    CONSTRAINT ck_measurement_identity_sequence_nonnegative
        CHECK (source_sequence >= 0),
    CONSTRAINT ck_measurement_identity_versions_positive
        CHECK (
            mapping_version > 0
            AND algorithm_version > 0
            AND configuration_version > 0
        ),
    CONSTRAINT ck_measurement_identity_fingerprint_length
        CHECK (octet_length(request_fingerprint) = 32),
    CONSTRAINT ck_measurement_identity_classification
        CHECK (final_classification IN ('Accepted', 'Rejected')),
    CONSTRAINT ck_measurement_identity_correlation
        CHECK (
            length(btrim(original_correlation_id)) > 0
            AND length(btrim(original_lineage_id)) > 0
        ),
    CONSTRAINT ck_measurement_identity_terminal_shape CHECK (
        (
            final_classification = 'Accepted'
            AND measurement_persisted = true
            AND persisted_measurement_id = measurement_id
            AND quality_code IN ('Good', 'Uncertain', 'Bad')
            AND rejection_code IS NULL
            AND latest_advanced IS NOT NULL
        )
        OR
        (
            final_classification = 'Rejected'
            AND measurement_persisted = false
            AND persisted_measurement_id IS NULL
            AND quality_code IS NULL
            AND reason_code IS NULL
            AND rejection_code IS NOT NULL
            AND length(btrim(rejection_code)) > 0
            AND latest_advanced IS NULL
        )
    ),
    CONSTRAINT ck_measurement_identity_quality_reason CHECK (
        (quality_code = 'Good' AND reason_code IS NULL)
        OR (quality_code = 'Uncertain' AND reason_code = 'SOURCE_TIMESTAMP_FUTURE')
        OR (quality_code = 'Bad' AND reason_code = 'VALUE_OUT_OF_RANGE'
            AND latest_advanced = false)
        OR quality_code IS NULL
    )
);

CREATE INDEX IF NOT EXISTS ix_measurement_identity_source_completed
    ON telemetry.measurement_identity (source_id, completed_at_utc DESC, measurement_id);
CREATE INDEX IF NOT EXISTS ix_measurement_identity_point_completed
    ON telemetry.measurement_identity (point_id, completed_at_utc DESC, measurement_id);
CREATE INDEX IF NOT EXISTS ix_measurement_identity_reconciliation
    ON telemetry.measurement_identity (
        final_classification, completed_at_utc, simulator_run_id, point_id, source_sequence
    );

CREATE TABLE IF NOT EXISTS telemetry.measurement_raw (
    measurement_id uuid PRIMARY KEY,
    source_id uuid NOT NULL,
    simulator_run_id uuid NOT NULL,
    point_id uuid NOT NULL,
    mapping_id uuid NOT NULL,
    mapping_version bigint NOT NULL,
    source_sequence bigint NOT NULL,
    source_timestamp_utc timestamptz NOT NULL,
    received_at_utc timestamptz NOT NULL,
    processing_at_utc timestamptz NOT NULL,
    numeric_value double precision NOT NULL,
    unit_code text NOT NULL,
    quality_code text NOT NULL,
    reason_code text,
    correlation_id text NOT NULL,
    lineage_id text NOT NULL,
    CONSTRAINT fk_measurement_raw_identity
        FOREIGN KEY (measurement_id)
        REFERENCES telemetry.measurement_identity (measurement_id)
        DEFERRABLE INITIALLY DEFERRED,
    CONSTRAINT ck_measurement_raw_versions_positive CHECK (mapping_version > 0),
    CONSTRAINT ck_measurement_raw_sequence_nonnegative CHECK (source_sequence >= 0),
    CONSTRAINT ck_measurement_raw_numeric_finite CHECK (
        numeric_value = numeric_value
        AND numeric_value <> 'Infinity'::double precision
        AND numeric_value <> '-Infinity'::double precision
    ),
    CONSTRAINT ck_measurement_raw_unit CHECK (length(btrim(unit_code)) > 0),
    CONSTRAINT ck_measurement_raw_quality CHECK (
        (quality_code = 'Good' AND reason_code IS NULL)
        OR (quality_code = 'Uncertain' AND reason_code = 'SOURCE_TIMESTAMP_FUTURE')
        OR (quality_code = 'Bad' AND reason_code = 'VALUE_OUT_OF_RANGE')
    ),
    CONSTRAINT ck_measurement_raw_time_order CHECK (
        processing_at_utc >= received_at_utc
    ),
    CONSTRAINT ck_measurement_raw_provenance CHECK (
        length(btrim(correlation_id)) > 0
        AND length(btrim(lineage_id)) > 0
    )
);

CREATE INDEX IF NOT EXISTS ix_measurement_raw_point_source_time
    ON telemetry.measurement_raw (
        point_id, source_timestamp_utc DESC, source_sequence DESC, measurement_id
    );
CREATE INDEX IF NOT EXISTS ix_measurement_raw_source_received
    ON telemetry.measurement_raw (source_id, received_at_utc DESC, measurement_id);

CREATE OR REPLACE FUNCTION telemetry.reject_immutable_measurement()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION '% rows are immutable', TG_TABLE_NAME;
END;
$$;

DROP TRIGGER IF EXISTS trg_measurement_identity_immutable
    ON telemetry.measurement_identity;
CREATE TRIGGER trg_measurement_identity_immutable
BEFORE UPDATE OR DELETE ON telemetry.measurement_identity
FOR EACH ROW EXECUTE FUNCTION telemetry.reject_immutable_measurement();

DROP TRIGGER IF EXISTS trg_measurement_raw_immutable
    ON telemetry.measurement_raw;
CREATE TRIGGER trg_measurement_raw_immutable
BEFORE UPDATE OR DELETE ON telemetry.measurement_raw
FOR EACH ROW EXECUTE FUNCTION telemetry.reject_immutable_measurement();

CREATE OR REPLACE FUNCTION telemetry.assert_measurement_terminal_consistency()
RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE
    target_id uuid := COALESCE(NEW.measurement_id, OLD.measurement_id);
    identity_row telemetry.measurement_identity%ROWTYPE;
    raw_row telemetry.measurement_raw%ROWTYPE;
    raw_count bigint;
BEGIN
    SELECT *
      INTO identity_row
      FROM telemetry.measurement_identity
     WHERE measurement_id = target_id;
    SELECT count(*)
      INTO raw_count
      FROM telemetry.measurement_raw
     WHERE measurement_id = target_id;

    IF identity_row.final_classification = 'Accepted' AND raw_count <> 1 THEN
        RAISE EXCEPTION 'Accepted terminal result requires exactly one raw Measurement';
    END IF;
    IF identity_row.final_classification = 'Rejected' AND raw_count <> 0 THEN
        RAISE EXCEPTION 'Rejected terminal result cannot have a raw Measurement';
    END IF;
    IF raw_count > 0 AND identity_row.final_classification IS DISTINCT FROM 'Accepted' THEN
        RAISE EXCEPTION 'raw Measurement requires an Accepted terminal result';
    END IF;
    IF identity_row.final_classification = 'Accepted' AND raw_count = 1 THEN
        SELECT *
          INTO raw_row
          FROM telemetry.measurement_raw
         WHERE measurement_id = target_id;
        IF identity_row.persisted_measurement_id IS DISTINCT FROM raw_row.measurement_id
           OR identity_row.source_id IS DISTINCT FROM raw_row.source_id
           OR identity_row.simulator_run_id IS DISTINCT FROM raw_row.simulator_run_id
           OR identity_row.point_id IS DISTINCT FROM raw_row.point_id
           OR identity_row.mapping_id IS DISTINCT FROM raw_row.mapping_id
           OR identity_row.mapping_version IS DISTINCT FROM raw_row.mapping_version
           OR identity_row.source_sequence IS DISTINCT FROM raw_row.source_sequence
           OR identity_row.quality_code IS DISTINCT FROM raw_row.quality_code
           OR identity_row.reason_code IS DISTINCT FROM raw_row.reason_code
           OR identity_row.original_correlation_id IS DISTINCT FROM raw_row.correlation_id
           OR identity_row.original_lineage_id IS DISTINCT FROM raw_row.lineage_id
        THEN
            RAISE EXCEPTION 'Accepted terminal and raw Measurement provenance must match';
        END IF;
    END IF;
    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS trg_measurement_identity_consistency
    ON telemetry.measurement_identity;
CREATE CONSTRAINT TRIGGER trg_measurement_identity_consistency
AFTER INSERT ON telemetry.measurement_identity
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION telemetry.assert_measurement_terminal_consistency();

DROP TRIGGER IF EXISTS trg_measurement_raw_consistency
    ON telemetry.measurement_raw;
CREATE CONSTRAINT TRIGGER trg_measurement_raw_consistency
AFTER INSERT ON telemetry.measurement_raw
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION telemetry.assert_measurement_terminal_consistency();
