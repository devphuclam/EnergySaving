-- 0003_catalog_foundation.sql
-- Catalog owns Metric, Unit, compatibility and Data Source. Effective source associations are 0006.
CREATE SCHEMA IF NOT EXISTS catalog;

CREATE TABLE IF NOT EXISTS catalog.metrics (
    id uuid PRIMARY KEY,
    code text NOT NULL,
    name text NOT NULL,
    status text NOT NULL DEFAULT 'Active',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_metrics_code_nonempty CHECK (length(btrim(code)) > 0 AND code = upper(code)),
    CONSTRAINT ck_metrics_name_nonempty CHECK (length(btrim(name)) > 0),
    CONSTRAINT ck_metrics_status CHECK (status IN ('Active', 'Inactive')),
    CONSTRAINT ck_metrics_version_positive CHECK (version > 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_metrics_code ON catalog.metrics (code);
CREATE INDEX IF NOT EXISTS ix_metrics_status ON catalog.metrics (status);

CREATE TABLE IF NOT EXISTS catalog.units (
    id uuid PRIMARY KEY,
    code text NOT NULL,
    symbol text NOT NULL,
    status text NOT NULL DEFAULT 'Active',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_units_code_nonempty CHECK (length(btrim(code)) > 0 AND code = upper(code)),
    CONSTRAINT ck_units_symbol_nonempty CHECK (length(btrim(symbol)) > 0),
    CONSTRAINT ck_units_status CHECK (status IN ('Active', 'Inactive')),
    CONSTRAINT ck_units_version_positive CHECK (version > 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_units_code ON catalog.units (code);
CREATE INDEX IF NOT EXISTS ix_units_status ON catalog.units (status);

CREATE TABLE IF NOT EXISTS catalog.metric_unit_compatibilities (
    metric_id uuid NOT NULL REFERENCES catalog.metrics(id),
    unit_id uuid NOT NULL REFERENCES catalog.units(id),
    is_canonical boolean NOT NULL DEFAULT false,
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_metric_unit_compatibilities PRIMARY KEY (metric_id, unit_id),
    CONSTRAINT ck_metric_unit_compatibilities_version_positive CHECK (version > 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_metric_one_canonical_unit
    ON catalog.metric_unit_compatibilities (metric_id) WHERE is_canonical;
CREATE INDEX IF NOT EXISTS ix_metric_unit_compatibilities_unit ON catalog.metric_unit_compatibilities (unit_id);

CREATE TABLE IF NOT EXISTS catalog.data_sources (
    id uuid PRIMARY KEY,
    code text NOT NULL,
    name text NOT NULL,
    source_type text NOT NULL,
    status text NOT NULL DEFAULT 'Draft',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_data_sources_code_nonempty CHECK (length(btrim(code)) > 0 AND code = upper(code)),
    CONSTRAINT ck_data_sources_name_nonempty CHECK (length(btrim(name)) > 0),
    CONSTRAINT ck_data_sources_source_type CHECK (source_type = 'Simulator'),
    CONSTRAINT ck_data_sources_status CHECK (status IN ('Draft', 'Active', 'Suspended', 'Decommissioned')),
    CONSTRAINT ck_data_sources_version_positive CHECK (version > 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_data_sources_code ON catalog.data_sources (code);
CREATE INDEX IF NOT EXISTS ix_data_sources_status ON catalog.data_sources (status);

-- Idempotent development/POC seeds. They do not approve any measurement point.
INSERT INTO catalog.metrics (id, code, name, status, version) VALUES
    ('00000000-0000-0000-0000-000000000001', 'ELECTRIC_POWER', 'Electric Power', 'Active', 1),
    ('00000000-0000-0000-0000-000000000002', 'ELECTRICAL_ENERGY', 'Electrical Energy', 'Active', 1)
ON CONFLICT (id) DO NOTHING;

INSERT INTO catalog.units (id, code, symbol, status, version) VALUES
    ('00000000-0000-0000-0000-000000000011', 'KW', 'kW', 'Active', 1),
    ('00000000-0000-0000-0000-000000000012', 'KWH', 'kWh', 'Active', 1)
ON CONFLICT (id) DO NOTHING;

INSERT INTO catalog.metric_unit_compatibilities (metric_id, unit_id, is_canonical, version) VALUES
    ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000011', true, 1),
    ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000012', true, 1)
ON CONFLICT (metric_id, unit_id) DO NOTHING;
