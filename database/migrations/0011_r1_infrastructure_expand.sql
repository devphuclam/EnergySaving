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
        (status = 'Pending' AND original_http_status IS NULL AND completed_at IS NULL)
        OR (status = 'Completed' AND original_http_status BETWEEN 100 AND 599 AND completed_at IS NOT NULL)
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
    ADD COLUMN IF NOT EXISTS payload_hash bytea,
    ADD COLUMN IF NOT EXISTS pending_owner text,
    ADD COLUMN IF NOT EXISTS pending_until timestamptz,
    ADD COLUMN IF NOT EXISTS attempt_count integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS next_attempt_at timestamptz,
    ADD COLUMN IF NOT EXISTS last_error text;

CREATE INDEX IF NOT EXISTS ix_inbox_message_recovery
    ON integration.inbox_message (pending_until, next_attempt_at);
