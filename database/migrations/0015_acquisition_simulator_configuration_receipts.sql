-- 0015_acquisition_simulator_configuration_receipts.sql
-- Acquisition-owned, Draft-version-bound relationship review and validation receipts.
-- Receipts are intentionally not a generic workflow store and never contain secrets.
BEGIN;

CREATE TABLE IF NOT EXISTS acquisition.simulator_configuration_receipt (
    configuration_id uuid NOT NULL,
    draft_configuration_version bigint NOT NULL,
    source_id uuid NOT NULL,
    relationship_fingerprint text NOT NULL,
    reviewed_by_user_id text NOT NULL,
    reviewed_at_utc timestamptz NOT NULL,
    validated_payload_fingerprint text NULL,
    validated_by_user_id text NULL,
    validated_at_utc timestamptz NULL,
    PRIMARY KEY (configuration_id, draft_configuration_version),
    CONSTRAINT fk_simulator_configuration_receipt_head
        FOREIGN KEY (configuration_id)
        REFERENCES acquisition.simulator_configuration(configuration_id),
    CONSTRAINT ck_simulator_configuration_receipt_version_positive
        CHECK (draft_configuration_version > 0),
    CONSTRAINT ck_simulator_configuration_receipt_review_actor_nonempty
        CHECK (length(btrim(reviewed_by_user_id)) > 0),
    CONSTRAINT ck_simulator_configuration_receipt_validation_pair
        CHECK ((validated_payload_fingerprint IS NULL AND validated_by_user_id IS NULL AND validated_at_utc IS NULL)
            OR (validated_payload_fingerprint IS NOT NULL AND length(btrim(validated_payload_fingerprint)) > 0
                AND validated_by_user_id IS NOT NULL AND length(btrim(validated_by_user_id)) > 0
                AND validated_at_utc IS NOT NULL))
);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_simulator_configuration_receipt_version'
          AND conrelid = 'acquisition.simulator_configuration_receipt'::regclass
    ) THEN
        ALTER TABLE acquisition.simulator_configuration_receipt
            ADD CONSTRAINT fk_simulator_configuration_receipt_version
            FOREIGN KEY (configuration_id, draft_configuration_version)
            REFERENCES acquisition.simulator_configuration_version(configuration_id, configuration_version);
    END IF;
END
$$;

CREATE INDEX IF NOT EXISTS ix_simulator_configuration_receipt_source
    ON acquisition.simulator_configuration_receipt (source_id, draft_configuration_version);

COMMIT;
