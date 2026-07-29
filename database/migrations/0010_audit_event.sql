-- 0010_audit_event.sql
-- Audit owns immutable evidence. Logical business identifiers intentionally have no cross-schema FKs.
CREATE SCHEMA IF NOT EXISTS audit;

CREATE TABLE IF NOT EXISTS audit.audit_event (
    audit_event_id uuid PRIMARY KEY,
    source_event_id uuid NOT NULL UNIQUE,
    event_type text NOT NULL,
    schema_version integer NOT NULL DEFAULT 1,
    source_producer text NOT NULL,
    payload_hash bytea NOT NULL,
    object_type text NOT NULL,
    object_id text NOT NULL,
    action text NOT NULL,
    actor_id text,
    actor_username text,
    before_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    after_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    summary text NOT NULL,
    site_id text,
    area_id text,
    occurred_at_utc timestamptz NOT NULL,
    recorded_at_utc timestamptz NOT NULL,
    correlation_id text NOT NULL,
    causation_id text,
    CONSTRAINT ck_audit_event_type_versioned CHECK (event_type LIKE '%.v1'),
    CONSTRAINT ck_audit_event_schema_version CHECK (schema_version = 1),
    CONSTRAINT ck_audit_event_payload_hash CHECK (octet_length(payload_hash) = 32),
    CONSTRAINT ck_audit_event_nonblank CHECK (length(btrim(object_type)) > 0 AND length(btrim(object_id)) > 0
        AND length(btrim(action)) > 0 AND length(btrim(summary)) > 0 AND length(btrim(correlation_id)) > 0)
);

CREATE INDEX IF NOT EXISTS ix_audit_event_keyset
    ON audit.audit_event (occurred_at_utc DESC, audit_event_id DESC);
CREATE INDEX IF NOT EXISTS ix_audit_event_object
    ON audit.audit_event (object_type, object_id, occurred_at_utc DESC);
CREATE INDEX IF NOT EXISTS ix_audit_event_scope
    ON audit.audit_event (site_id, area_id, occurred_at_utc DESC);
CREATE INDEX IF NOT EXISTS ix_audit_event_actor_action
    ON audit.audit_event (actor_id, action, occurred_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_audit_event_source_hash
    ON audit.audit_event (source_event_id, payload_hash);

CREATE OR REPLACE FUNCTION audit.prevent_audit_mutation() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'AUDIT_APPEND_ONLY';
END;
$$;

DROP TRIGGER IF EXISTS trg_audit_event_append_only ON audit.audit_event;
CREATE TRIGGER trg_audit_event_append_only
    BEFORE UPDATE OR DELETE ON audit.audit_event
    FOR EACH ROW EXECUTE FUNCTION audit.prevent_audit_mutation();

REVOKE UPDATE, DELETE ON audit.audit_event FROM PUBLIC;

COMMENT ON TABLE audit.audit_event IS 'Append-only immutable Audit evidence; source identity, schema version and SHA-256 payload hash make delivery idempotent. JSON snapshots are safe/redacted and indexed for keyset and scope queries.';
