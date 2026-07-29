-- 0011_r1_infrastructure_expand.sql
-- Additive Integration infrastructure. Existing outbox_event, inbox_message and job tables are reused.
CREATE SCHEMA IF NOT EXISTS integration;

CREATE TABLE IF NOT EXISTS integration.command_idempotency (
    command_idempotency_id uuid PRIMARY KEY,
    caller_user_id uuid NOT NULL,
    operation_code text NOT NULL,
    idempotency_key text NOT NULL,
    request_fingerprint bytea NOT NULL,
    target_scope_type text,
    target_scope_id uuid,
    target_aggregate_type text,
    target_aggregate_id uuid,
    expected_version bigint,
    status text NOT NULL,
    pending_owner text,
    pending_until timestamptz,
    attempt_count integer NOT NULL DEFAULT 0,
    original_http_status integer,
    original_result_payload jsonb,
    stable_result_reference text,
    original_location text,
    original_etag text,
    resource_id uuid,
    resource_version bigint,
    error_code text,
    last_error text,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    completed_at timestamptz,
    expires_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    CONSTRAINT uq_command_idempotency_identity UNIQUE (caller_user_id, operation_code, idempotency_key),
    CONSTRAINT ck_command_idempotency_fingerprint CHECK (octet_length(request_fingerprint) = 32),
    CONSTRAINT ck_command_idempotency_status CHECK (status IN ('Pending', 'Completed')),
    CONSTRAINT ck_command_idempotency_pending_shape CHECK (
        (status = 'Pending'
            AND original_http_status IS NULL AND original_result_payload IS NULL AND stable_result_reference IS NULL
            AND original_location IS NULL AND original_etag IS NULL AND completed_at IS NULL)
        OR (status = 'Completed' AND pending_owner IS NULL AND pending_until IS NULL
            AND original_http_status BETWEEN 100 AND 599 AND original_result_payload IS NOT NULL
            AND completed_at IS NOT NULL)
    ),
    CONSTRAINT ck_command_idempotency_attempts CHECK (attempt_count >= 0 AND version > 0)
);

CREATE INDEX IF NOT EXISTS ix_command_idempotency_pending_recovery
    ON integration.command_idempotency (pending_until, created_at)
    WHERE status = 'Pending';
CREATE INDEX IF NOT EXISTS ix_command_idempotency_retention
    ON integration.command_idempotency (expires_at);
CREATE INDEX IF NOT EXISTS ix_command_idempotency_target
    ON integration.command_idempotency (target_aggregate_id)
    WHERE target_aggregate_id IS NOT NULL;

ALTER TABLE integration.inbox_message
    ADD COLUMN IF NOT EXISTS pending_owner text,
    ADD COLUMN IF NOT EXISTS pending_until timestamptz,
    ADD COLUMN IF NOT EXISTS attempt_count integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS next_attempt_at timestamptz,
    ADD COLUMN IF NOT EXISTS last_error text,
    ADD COLUMN IF NOT EXISTS retention_until timestamptz,
    ADD COLUMN IF NOT EXISTS result_json jsonb,
    ADD COLUMN IF NOT EXISTS result_hash bytea;

-- Existing R0 inbox identity remains (consumer_name,event_id,payload_hash). The additive
-- expansion supplies a Pending/Completed lease state, retry metadata and a safe result without
-- recreating the table or introducing cross-schema FKs.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_inbox_payload_hash_nonblank') THEN
        ALTER TABLE integration.inbox_message ADD CONSTRAINT ck_inbox_payload_hash_nonblank CHECK (length(btrim(payload_hash)) > 0);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_inbox_attempts_nonnegative') THEN
        ALTER TABLE integration.inbox_message ADD CONSTRAINT ck_inbox_attempts_nonnegative CHECK (attempt_count >= 0);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_inbox_result_hash') THEN
        ALTER TABLE integration.inbox_message ADD CONSTRAINT ck_inbox_result_hash CHECK (result_hash IS NULL OR octet_length(result_hash) = 32);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_inbox_pending_completed_shape') THEN
        ALTER TABLE integration.inbox_message ADD CONSTRAINT ck_inbox_pending_completed_shape CHECK (
            (status = 'Processing' AND completed_at IS NULL)
            OR (status = 'Completed' AND completed_at IS NOT NULL)
            OR status = 'Failed'
        );
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_inbox_message_consumer_status
    ON integration.inbox_message (consumer_name, status, next_attempt_at);
CREATE INDEX IF NOT EXISTS ix_inbox_message_retention
    ON integration.inbox_message (retention_until)
    WHERE status = 'Completed';

CREATE OR REPLACE FUNCTION integration.prevent_completed_inbox_mutation() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' OR OLD.status = 'Completed' THEN
        RAISE EXCEPTION 'INBOX_COMPLETED_IMMUTABLE';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_inbox_completed_immutable ON integration.inbox_message;
CREATE TRIGGER trg_inbox_completed_immutable
    BEFORE UPDATE OR DELETE ON integration.inbox_message
    FOR EACH ROW EXECUTE FUNCTION integration.prevent_completed_inbox_mutation();

CREATE INDEX IF NOT EXISTS ix_inbox_message_recovery
    ON integration.inbox_message (pending_until, next_attempt_at);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_command_idempotency_pending_shape') THEN
        ALTER TABLE integration.command_idempotency DROP CONSTRAINT ck_command_idempotency_pending_shape;
    END IF;
    ALTER TABLE integration.command_idempotency ADD CONSTRAINT ck_command_idempotency_pending_shape CHECK (
        (status = 'Pending'
            AND original_http_status IS NULL AND original_result_payload IS NULL AND stable_result_reference IS NULL
            AND original_location IS NULL AND original_etag IS NULL AND completed_at IS NULL)
        OR (status = 'Completed' AND pending_owner IS NULL AND pending_until IS NULL
            AND original_http_status BETWEEN 100 AND 599 AND original_result_payload IS NOT NULL
            AND completed_at IS NOT NULL)
    );
END $$;

CREATE OR REPLACE FUNCTION integration.prevent_completed_command_mutation() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.status = 'Completed' THEN
        RAISE EXCEPTION 'COMMAND_COMPLETED_IMMUTABLE';
    END IF;
    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_command_completed_immutable ON integration.command_idempotency;
CREATE TRIGGER trg_command_completed_immutable
    BEFORE UPDATE OR DELETE ON integration.command_idempotency
    FOR EACH ROW EXECUTE FUNCTION integration.prevent_completed_command_mutation();
