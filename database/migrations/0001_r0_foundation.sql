BEGIN;

CREATE SCHEMA IF NOT EXISTS integration;
CREATE SCHEMA IF NOT EXISTS operations;

CREATE TABLE IF NOT EXISTS integration.outbox_event (
    event_id uuid PRIMARY KEY,
    event_type text NOT NULL,
    schema_version integer NOT NULL CHECK (schema_version > 0),
    occurred_at timestamptz NOT NULL,
    payload_json jsonb NOT NULL,
    status text NOT NULL CHECK (status IN ('Pending', 'Leased', 'Published', 'Failed')),
    attempt_count integer NOT NULL DEFAULT 0 CHECK (attempt_count >= 0),
    next_attempt_at timestamptz NOT NULL,
    lease_owner text NULL,
    lease_until timestamptz NULL,
    last_error text NULL,
    published_at timestamptz NULL
);

CREATE INDEX IF NOT EXISTS ix_outbox_event_dispatch
    ON integration.outbox_event (status, next_attempt_at);

CREATE TABLE IF NOT EXISTS integration.inbox_message (
    consumer_name text NOT NULL,
    event_id uuid NOT NULL,
    payload_hash text NOT NULL,
    status text NOT NULL CHECK (status IN ('Processing', 'Completed', 'Failed')),
    received_at timestamptz NOT NULL,
    completed_at timestamptz NULL,
    last_error text NULL,
    PRIMARY KEY (consumer_name, event_id)
);

CREATE TABLE IF NOT EXISTS operations.job (
    job_id uuid PRIMARY KEY,
    job_type text NOT NULL,
    payload_json jsonb NOT NULL,
    payload_version integer NOT NULL CHECK (payload_version > 0),
    status text NOT NULL CHECK (status IN ('Pending', 'Leased', 'Completed', 'Failed')),
    idempotency_key text NOT NULL,
    available_at timestamptz NOT NULL,
    lease_owner text NULL,
    lease_until timestamptz NULL,
    attempt_count integer NOT NULL DEFAULT 0 CHECK (attempt_count >= 0),
    created_at timestamptz NOT NULL,
    completed_at timestamptz NULL,
    last_error text NULL,
    UNIQUE (job_type, idempotency_key)
);

CREATE INDEX IF NOT EXISTS ix_job_dispatch
    ON operations.job (status, available_at);

COMMIT;
