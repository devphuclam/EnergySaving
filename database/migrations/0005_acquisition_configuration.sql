-- 0005_acquisition_configuration.sql
-- Acquisition owns immutable Simulator configuration heads and versions.
-- SourceId is a logical Catalog reference: there is intentionally no cross-schema FK.
CREATE SCHEMA IF NOT EXISTS acquisition;

CREATE TABLE IF NOT EXISTS acquisition.simulator_configuration (
    configuration_id uuid PRIMARY KEY,
    source_id uuid NOT NULL,
    current_configuration_version bigint NOT NULL DEFAULT 1,
    version bigint NOT NULL DEFAULT 1,
    created_by_user_id text NOT NULL,
    created_by_username text NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_simulator_configuration_source UNIQUE (source_id),
    CONSTRAINT ck_simulator_configuration_current_positive CHECK (current_configuration_version > 0),
    CONSTRAINT ck_simulator_configuration_version_positive CHECK (version > 0),
    CONSTRAINT ck_simulator_configuration_actor_nonempty CHECK (length(btrim(created_by_user_id)) > 0 AND length(btrim(created_by_username)) > 0)
);
CREATE INDEX IF NOT EXISTS ix_simulator_configuration_source ON acquisition.simulator_configuration (source_id);
CREATE INDEX IF NOT EXISTS ix_simulator_configuration_current_version ON acquisition.simulator_configuration (current_configuration_version);

CREATE TABLE IF NOT EXISTS acquisition.simulator_configuration_version (
    configuration_id uuid NOT NULL,
    configuration_version bigint NOT NULL,
    interval_seconds integer NOT NULL,
    minimum_value double precision NOT NULL,
    maximum_value double precision NOT NULL,
    deterministic_seed text NOT NULL,
    scenario_type text NOT NULL,
    algorithm_id text NOT NULL,
    algorithm_version integer NOT NULL,
    created_by_user_id text NOT NULL,
    created_by_username text NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    correlation_id text,
    causation_id text,
    PRIMARY KEY (configuration_id, configuration_version),
    CONSTRAINT fk_simulator_configuration_version_head FOREIGN KEY (configuration_id)
        REFERENCES acquisition.simulator_configuration(configuration_id),
    CONSTRAINT ck_simulator_configuration_version_positive CHECK (configuration_version > 0),
    CONSTRAINT ck_simulator_configuration_interval_positive CHECK (interval_seconds > 0),
    CONSTRAINT ck_simulator_configuration_bounds_finite CHECK (
        minimum_value NOT IN ('Infinity'::double precision, '-Infinity'::double precision, 'NaN'::double precision)
        AND maximum_value NOT IN ('Infinity'::double precision, '-Infinity'::double precision, 'NaN'::double precision)
    ),
    CONSTRAINT ck_simulator_configuration_scenario CHECK (scenario_type IN ('Constant', 'Normal')),
    CONSTRAINT ck_simulator_configuration_scenario_bounds CHECK (
        (scenario_type = 'Constant' AND minimum_value = maximum_value)
        OR (scenario_type = 'Normal' AND minimum_value < maximum_value)
    ),
    CONSTRAINT ck_simulator_configuration_seed_nonempty CHECK (length(btrim(deterministic_seed)) > 0),
    CONSTRAINT ck_simulator_configuration_algorithm CHECK (algorithm_id = 'IUMP-DETERMINISTIC-V1' AND algorithm_version > 0),
    CONSTRAINT ck_simulator_configuration_version_actor_nonempty CHECK (length(btrim(created_by_user_id)) > 0 AND length(btrim(created_by_username)) > 0)
);
CREATE INDEX IF NOT EXISTS ix_simulator_configuration_version_lookup ON acquisition.simulator_configuration_version (configuration_id, configuration_version);
CREATE INDEX IF NOT EXISTS ix_simulator_configuration_version_created_at ON acquisition.simulator_configuration_version (created_at_utc);

-- Version rows are append-only by design. The application port exposes no update/delete operation.
CREATE OR REPLACE FUNCTION acquisition.reject_simulator_configuration_version_mutation()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'simulator configuration versions are immutable';
END;
$$;
DROP TRIGGER IF EXISTS trg_simulator_configuration_version_append_only ON acquisition.simulator_configuration_version;
CREATE TRIGGER trg_simulator_configuration_version_append_only
BEFORE UPDATE OR DELETE ON acquisition.simulator_configuration_version
FOR EACH ROW EXECUTE FUNCTION acquisition.reject_simulator_configuration_version_mutation();
