-- 0007_acquisition_run.sql
-- Acquisition owns Simulator Run, pinned Run-Point state, and durable production attempts.
-- Catalog and Organization identifiers are logical references; no cross-schema FK is permitted.
CREATE SCHEMA IF NOT EXISTS acquisition;

CREATE TABLE IF NOT EXISTS acquisition.simulator_run (
    run_id uuid PRIMARY KEY,
    source_id uuid NOT NULL,
    source_version bigint NOT NULL,
    configuration_id uuid NOT NULL,
    configuration_version bigint NOT NULL,
    algorithm_id text NOT NULL,
    algorithm_version integer NOT NULL,
    status text NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    generated_count bigint NOT NULL DEFAULT 0,
    accepted_count bigint NOT NULL DEFAULT 0,
    rejected_count bigint NOT NULL DEFAULT 0,
    latest_error_code text,
    latest_error_message text,
    created_at_utc timestamptz NOT NULL,
    started_at_utc timestamptz NOT NULL,
    paused_at_utc timestamptz,
    resumed_at_utc timestamptz,
    stopped_at_utc timestamptz,
    actor_id text NOT NULL,
    actor_username text NOT NULL,
    correlation_id text NOT NULL,
    causation_id text,
    CONSTRAINT ck_simulator_run_source_version_positive CHECK (source_version > 0),
    CONSTRAINT ck_simulator_run_configuration_version_positive CHECK (configuration_version > 0),
    CONSTRAINT ck_simulator_run_algorithm CHECK (
        algorithm_id = 'IUMP-DETERMINISTIC-V1' AND algorithm_version = 1
    ),
    CONSTRAINT ck_simulator_run_status CHECK (status IN ('Running', 'Paused', 'Stopped')),
    CONSTRAINT ck_simulator_run_version_positive CHECK (version > 0),
    CONSTRAINT ck_simulator_run_counters_nonnegative CHECK (
        generated_count >= 0 AND accepted_count >= 0 AND rejected_count >= 0
    ),
    CONSTRAINT ck_simulator_run_final_not_above_generated CHECK (
        accepted_count + rejected_count <= generated_count
    ),
    CONSTRAINT ck_simulator_run_actor_nonempty CHECK (
        length(btrim(actor_id)) > 0 AND length(btrim(actor_username)) > 0
    ),
    CONSTRAINT ck_simulator_run_correlation_nonempty CHECK (length(btrim(correlation_id)) > 0),
    CONSTRAINT ck_simulator_run_lifecycle_time CHECK (
        started_at_utc >= created_at_utc
        AND (paused_at_utc IS NULL OR paused_at_utc >= started_at_utc)
        AND (resumed_at_utc IS NULL OR resumed_at_utc >= started_at_utc)
        AND (stopped_at_utc IS NULL OR stopped_at_utc >= started_at_utc)
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_simulator_run_current_source
    ON acquisition.simulator_run (source_id)
    WHERE status IN ('Running', 'Paused');
CREATE INDEX IF NOT EXISTS ix_simulator_run_status_id
    ON acquisition.simulator_run (status, run_id);
CREATE INDEX IF NOT EXISTS ix_simulator_run_source_created
    ON acquisition.simulator_run (source_id, created_at_utc DESC);

CREATE TABLE IF NOT EXISTS acquisition.simulator_run_point_state (
    run_id uuid NOT NULL,
    point_id uuid NOT NULL,
    point_version_at_start bigint NOT NULL,
    mapping_id uuid NOT NULL,
    mapping_version bigint NOT NULL,
    metric_id uuid NOT NULL,
    unit_id uuid NOT NULL,
    unit_code text NOT NULL,
    source_version bigint NOT NULL,
    next_source_sequence bigint NOT NULL DEFAULT 0,
    prng_state bytea NOT NULL,
    next_due_at_utc timestamptz NOT NULL,
    site_id text NOT NULL,
    area_id text,
    lease_owner text,
    lease_token uuid,
    lease_version bigint NOT NULL DEFAULT 0,
    lease_expires_at_utc timestamptz,
    version bigint NOT NULL DEFAULT 1,
    PRIMARY KEY (run_id, point_id),
    CONSTRAINT fk_simulator_run_point_run FOREIGN KEY (run_id)
        REFERENCES acquisition.simulator_run(run_id),
    CONSTRAINT ck_simulator_run_point_provider_versions CHECK (
        point_version_at_start > 0 AND mapping_version > 0 AND source_version > 0
    ),
    CONSTRAINT ck_simulator_run_point_sequence_nonnegative CHECK (next_source_sequence >= 0),
    CONSTRAINT ck_simulator_run_point_prng_state_length CHECK (octet_length(prng_state) = 25),
    CONSTRAINT ck_simulator_run_point_unit_nonempty CHECK (length(btrim(unit_code)) > 0),
    CONSTRAINT ck_simulator_run_point_site_nonempty CHECK (length(btrim(site_id)) > 0),
    CONSTRAINT ck_simulator_run_point_lease_version_nonnegative CHECK (lease_version >= 0),
    CONSTRAINT ck_simulator_run_point_version_positive CHECK (version > 0),
    CONSTRAINT ck_simulator_run_point_lease_consistent CHECK (
        (lease_owner IS NULL AND lease_token IS NULL AND lease_expires_at_utc IS NULL)
        OR
        (length(btrim(lease_owner)) > 0 AND lease_token IS NOT NULL
            AND lease_expires_at_utc IS NOT NULL)
    )
);

CREATE INDEX IF NOT EXISTS ix_simulator_run_point_due
    ON acquisition.simulator_run_point_state (next_due_at_utc, run_id, point_id);
CREATE INDEX IF NOT EXISTS ix_simulator_run_point_lease
    ON acquisition.simulator_run_point_state (lease_expires_at_utc, run_id, point_id);
CREATE INDEX IF NOT EXISTS ix_simulator_run_point_recovery
    ON acquisition.simulator_run_point_state (run_id, next_due_at_utc, point_id);

CREATE OR REPLACE FUNCTION acquisition.reject_simulator_run_point_pinned_mutation()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.run_id IS DISTINCT FROM OLD.run_id
       OR NEW.point_id IS DISTINCT FROM OLD.point_id
       OR NEW.point_version_at_start IS DISTINCT FROM OLD.point_version_at_start
       OR NEW.mapping_id IS DISTINCT FROM OLD.mapping_id
       OR NEW.mapping_version IS DISTINCT FROM OLD.mapping_version
       OR NEW.metric_id IS DISTINCT FROM OLD.metric_id
       OR NEW.unit_id IS DISTINCT FROM OLD.unit_id
       OR NEW.unit_code IS DISTINCT FROM OLD.unit_code
       OR NEW.source_version IS DISTINCT FROM OLD.source_version
       OR NEW.site_id IS DISTINCT FROM OLD.site_id
       OR NEW.area_id IS DISTINCT FROM OLD.area_id
    THEN
        RAISE EXCEPTION 'simulator Run-Point pinned state is immutable';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_simulator_run_point_pinned_immutable
    ON acquisition.simulator_run_point_state;
CREATE TRIGGER trg_simulator_run_point_pinned_immutable
BEFORE UPDATE ON acquisition.simulator_run_point_state
FOR EACH ROW EXECUTE FUNCTION acquisition.reject_simulator_run_point_pinned_mutation();

CREATE TABLE IF NOT EXISTS acquisition.simulator_production_attempt (
    run_id uuid NOT NULL,
    point_id uuid NOT NULL,
    source_sequence bigint NOT NULL,
    measurement_id uuid NOT NULL,
    source_id uuid NOT NULL,
    mapping_id uuid NOT NULL,
    mapping_version bigint NOT NULL,
    algorithm_id text NOT NULL,
    algorithm_version integer NOT NULL,
    configuration_id uuid NOT NULL,
    configuration_version bigint NOT NULL,
    source_timestamp_utc timestamptz NOT NULL,
    numeric_value double precision NOT NULL,
    unit_code text NOT NULL,
    producer_identity text NOT NULL,
    correlation_id text NOT NULL,
    lineage_id text NOT NULL,
    status text NOT NULL,
    telemetry_outcome text,
    final_classification text,
    measurement_persisted boolean,
    persisted_measurement_id uuid,
    quality_code text,
    reason_code text,
    latest_advanced boolean,
    error_code text,
    rejection_code text,
    created_at_utc timestamptz NOT NULL,
    completed_at_utc timestamptz,
    original_correlation_id text,
    original_lineage_id text,
    version bigint NOT NULL DEFAULT 1,
    PRIMARY KEY (run_id, point_id, source_sequence),
    CONSTRAINT uq_simulator_production_attempt_measurement UNIQUE (measurement_id),
    CONSTRAINT fk_simulator_production_attempt_run FOREIGN KEY (run_id)
        REFERENCES acquisition.simulator_run(run_id),
    CONSTRAINT fk_simulator_production_attempt_point FOREIGN KEY (run_id, point_id)
        REFERENCES acquisition.simulator_run_point_state(run_id, point_id),
    CONSTRAINT ck_simulator_production_attempt_sequence_nonnegative CHECK (source_sequence >= 0),
    CONSTRAINT ck_simulator_production_attempt_mapping_version CHECK (mapping_version > 0),
    CONSTRAINT ck_simulator_production_attempt_algorithm CHECK (
        algorithm_id = 'IUMP-DETERMINISTIC-V1' AND algorithm_version = 1
    ),
    CONSTRAINT ck_simulator_production_attempt_configuration_version CHECK (
        configuration_version > 0
    ),
    CONSTRAINT ck_simulator_production_attempt_numeric_finite CHECK (
        numeric_value NOT IN (
            'Infinity'::double precision,
            '-Infinity'::double precision,
            'NaN'::double precision
        )
    ),
    CONSTRAINT ck_simulator_production_attempt_payload_nonempty CHECK (
        length(btrim(unit_code)) > 0
        AND length(btrim(producer_identity)) > 0
        AND length(btrim(correlation_id)) > 0
        AND length(btrim(lineage_id)) > 0
    ),
    CONSTRAINT ck_simulator_production_attempt_status CHECK (status IN ('Pending', 'Completed')),
    CONSTRAINT ck_simulator_production_attempt_outcome CHECK (
        telemetry_outcome IS NULL OR telemetry_outcome IN ('Accepted', 'Rejected', 'Duplicate')
    ),
    CONSTRAINT ck_simulator_production_attempt_classification CHECK (
        final_classification IS NULL OR final_classification IN ('Accepted', 'Rejected')
    ),
    CONSTRAINT ck_simulator_production_attempt_terminal CHECK (
        (
            status = 'Pending'
            AND telemetry_outcome IS NULL
            AND final_classification IS NULL
            AND latest_advanced IS NULL
            AND error_code IS NULL
            AND rejection_code IS NULL
            AND completed_at_utc IS NULL
        )
        OR
        (
            status = 'Completed'
            AND telemetry_outcome IS NOT NULL
            AND final_classification IS NOT NULL
            AND latest_advanced IS NOT NULL
            AND completed_at_utc IS NOT NULL
            AND completed_at_utc >= created_at_utc
        )
    ),
    CONSTRAINT ck_simulator_production_attempt_terminal_pair CHECK (
        status = 'Pending'
        OR (
            telemetry_outcome = 'Accepted'
            AND final_classification = 'Accepted'
            AND measurement_persisted = true
            AND persisted_measurement_id IS NOT NULL
            AND quality_code IS NOT NULL
            AND rejection_code IS NULL
        )
        OR (
            telemetry_outcome = 'Rejected'
            AND final_classification = 'Rejected'
            AND measurement_persisted = false
            AND persisted_measurement_id IS NULL
            AND quality_code IS NULL
            AND reason_code IS NULL
            AND latest_advanced = false
            AND rejection_code IS NOT NULL
            AND length(btrim(rejection_code)) > 0
        )
        OR (
            telemetry_outcome = 'Duplicate'
            AND (
                (
                    final_classification = 'Accepted'
                    AND rejection_code IS NULL
                )
                OR
                (
                    final_classification = 'Rejected'
                    AND latest_advanced = false
                    AND rejection_code IS NOT NULL
                    AND length(btrim(rejection_code)) > 0
                )
            )
        )
    ),
    CONSTRAINT ck_simulator_production_attempt_original_provenance CHECK (
        (status = 'Pending')
        OR
        (original_correlation_id IS NOT NULL AND length(btrim(original_correlation_id)) > 0
         AND original_lineage_id IS NOT NULL AND length(btrim(original_lineage_id)) > 0)
    ),
    CONSTRAINT ck_simulator_production_attempt_version_positive CHECK (version > 0)
);

CREATE INDEX IF NOT EXISTS ix_simulator_production_attempt_pending
    ON acquisition.simulator_production_attempt (run_id, point_id, source_sequence)
    WHERE status = 'Pending';
CREATE INDEX IF NOT EXISTS ix_simulator_production_attempt_reconciliation
    ON acquisition.simulator_production_attempt (status, created_at_utc, run_id, point_id);

CREATE OR REPLACE FUNCTION acquisition.reject_simulator_attempt_payload_mutation()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.run_id IS DISTINCT FROM OLD.run_id
       OR NEW.point_id IS DISTINCT FROM OLD.point_id
       OR NEW.source_sequence IS DISTINCT FROM OLD.source_sequence
       OR NEW.measurement_id IS DISTINCT FROM OLD.measurement_id
       OR NEW.source_id IS DISTINCT FROM OLD.source_id
       OR NEW.mapping_id IS DISTINCT FROM OLD.mapping_id
       OR NEW.mapping_version IS DISTINCT FROM OLD.mapping_version
       OR NEW.algorithm_id IS DISTINCT FROM OLD.algorithm_id
       OR NEW.algorithm_version IS DISTINCT FROM OLD.algorithm_version
       OR NEW.configuration_id IS DISTINCT FROM OLD.configuration_id
       OR NEW.configuration_version IS DISTINCT FROM OLD.configuration_version
       OR NEW.source_timestamp_utc IS DISTINCT FROM OLD.source_timestamp_utc
       OR NEW.numeric_value IS DISTINCT FROM OLD.numeric_value
       OR NEW.unit_code IS DISTINCT FROM OLD.unit_code
       OR NEW.producer_identity IS DISTINCT FROM OLD.producer_identity
       OR NEW.correlation_id IS DISTINCT FROM OLD.correlation_id
       OR NEW.lineage_id IS DISTINCT FROM OLD.lineage_id
       OR NEW.created_at_utc IS DISTINCT FROM OLD.created_at_utc
    THEN
        RAISE EXCEPTION 'simulator production attempt payload is immutable';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_simulator_attempt_payload_immutable
    ON acquisition.simulator_production_attempt;
CREATE TRIGGER trg_simulator_attempt_payload_immutable
BEFORE UPDATE ON acquisition.simulator_production_attempt
FOR EACH ROW EXECUTE FUNCTION acquisition.reject_simulator_attempt_payload_mutation();
