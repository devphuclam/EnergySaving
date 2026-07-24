BEGIN;

CREATE SCHEMA IF NOT EXISTS iam;

CREATE TABLE IF NOT EXISTS iam.role (
    role_id uuid PRIMARY KEY,
    code text NOT NULL UNIQUE CHECK (code ~ '^[A-Z][A-Za-z_]+$'),
    name text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

INSERT INTO iam.role (role_id, code, name) VALUES
    ('a0000000-0000-0000-0000-000000000001', 'Administrator', 'Administrator'),
    ('a0000000-0000-0000-0000-000000000002', 'Engineer', 'Engineer'),
    ('a0000000-0000-0000-0000-000000000003', 'Operator', 'Operator'),
    ('a0000000-0000-0000-0000-000000000004', 'Manager', 'Manager'),
    ('a0000000-0000-0000-0000-000000000005', 'Viewer', 'Viewer');

CREATE TABLE IF NOT EXISTS iam.user_account (
    user_id uuid PRIMARY KEY,
    username text NOT NULL UNIQUE CHECK (char_length(username) BETWEEN 3 AND 64),
    password_hash text NOT NULL,
    status text NOT NULL CHECK (status IN ('Active', 'Disabled')),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0)
);

CREATE INDEX IF NOT EXISTS ix_user_account_username ON iam.user_account (username);

CREATE TABLE IF NOT EXISTS iam.user_role (
    user_role_id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES iam.user_account (user_id),
    role_id uuid NOT NULL REFERENCES iam.role (role_id),
    assigned_by uuid NULL REFERENCES iam.user_account (user_id),
    assigned_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (user_id, role_id)
);

CREATE INDEX IF NOT EXISTS ix_user_role_user_id ON iam.user_role (user_id);

CREATE TABLE IF NOT EXISTS iam.user_scope (
    scope_id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES iam.user_account (user_id),
    site_id uuid NOT NULL,
    area_id uuid NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (user_id, site_id, area_id)
);

CREATE INDEX IF NOT EXISTS ix_user_scope_user_id ON iam.user_scope (user_id);

CREATE TABLE IF NOT EXISTS iam.capability (
    capability_id uuid PRIMARY KEY,
    code text NOT NULL UNIQUE CHECK (code ~ '^[A-Z][A-Za-z_]+$'),
    name text NOT NULL
);

CREATE TABLE IF NOT EXISTS iam.user_capability (
    user_capability_id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES iam.user_account (user_id),
    capability_id uuid NOT NULL REFERENCES iam.capability (capability_id),
    assigned_by uuid NOT NULL REFERENCES iam.user_account (user_id),
    assigned_at timestamptz NOT NULL DEFAULT now(),
    revoked_at timestamptz NULL,
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    UNIQUE (user_id, capability_id)
);

CREATE INDEX IF NOT EXISTS ix_user_capability_user_id ON iam.user_capability (user_id);

CREATE TABLE IF NOT EXISTS iam.user_session (
    session_id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES iam.user_account (user_id),
    token_hash text NOT NULL,
    issued_at timestamptz NOT NULL,
    idle_expires_at timestamptz NOT NULL,
    absolute_expires_at timestamptz NOT NULL,
    revoked_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_user_session_token_hash ON iam.user_session (token_hash);
CREATE INDEX IF NOT EXISTS ix_user_session_user_id ON iam.user_session (user_id);

INSERT INTO iam.capability (capability_id, code, name) VALUES
    ('a1000000-0000-0000-0000-000000000001', 'AUDIT_READ', 'Audit Review');

COMMIT;
