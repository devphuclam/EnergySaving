-- 0003_catalog_foundation.sql
-- Schema: catalog
-- Tables: metrics, units, metric_unit_compatibilities, data_sources, source_point_mappings

CREATE SCHEMA IF NOT EXISTS catalog;

-- metrics
CREATE TABLE catalog.metrics (
    id            uuid PRIMARY KEY,
    code          text NOT NULL,
    name          text NOT NULL,
    status        text NOT NULL DEFAULT 'Active',
    version       bigint NOT NULL DEFAULT 1,
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX idx_metrics_code ON catalog.metrics (lower(code));

-- units
CREATE TABLE catalog.units (
    id            uuid PRIMARY KEY,
    code          text NOT NULL,
    symbol        text NOT NULL,
    status        text NOT NULL DEFAULT 'Active',
    version       bigint NOT NULL DEFAULT 1,
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX idx_units_code ON catalog.units (lower(code));

-- metric_unit_compatibilities
CREATE TABLE catalog.metric_unit_compatibilities (
    metric_id     uuid NOT NULL REFERENCES catalog.metrics(id),
    unit_id       uuid NOT NULL REFERENCES catalog.units(id),
    is_canonical  boolean NOT NULL DEFAULT false,
    version       bigint NOT NULL DEFAULT 1,
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (metric_id, unit_id)
);

-- data_sources
CREATE TABLE catalog.data_sources (
    id            uuid PRIMARY KEY,
    code          text NOT NULL,
    name          text NOT NULL,
    source_type   text NOT NULL,
    status        text NOT NULL DEFAULT 'Draft',
    version       bigint NOT NULL DEFAULT 1,
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX idx_data_sources_code ON catalog.data_sources (lower(code));

-- source_point_mappings
CREATE TABLE catalog.source_point_mappings (
    id                uuid PRIMARY KEY,
    data_source_id    uuid NOT NULL REFERENCES catalog.data_sources(id),
    point_id          text NOT NULL,
    status            text NOT NULL DEFAULT 'Draft',
    effective_from    timestamptz NOT NULL,
    effective_to      timestamptz,
    version           bigint NOT NULL DEFAULT 1,
    created_at        timestamptz NOT NULL DEFAULT now(),
    updated_at        timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX idx_mappings_point ON catalog.source_point_mappings (point_id);
CREATE INDEX idx_mappings_source ON catalog.source_point_mappings (data_source_id);

-- seed data: electric power (kW) and electrical energy (kWh)
INSERT INTO catalog.metrics (id, code, name, status, version) VALUES
    ('00000000-0000-0000-0000-000000000001', 'ELECTRIC_POWER', 'Electric Power', 'Active', 1),
    ('00000000-0000-0000-0000-000000000002', 'ELECTRICAL_ENERGY', 'Electrical Energy', 'Active', 1);

INSERT INTO catalog.units (id, code, symbol, status, version) VALUES
    ('00000000-0000-0000-0000-000000000011', 'KW', 'kW', 'Active', 1),
    ('00000000-0000-0000-0000-000000000012', 'KWH', 'kWh', 'Active', 1);

INSERT INTO catalog.metric_unit_compatibilities (metric_id, unit_id, is_canonical, version) VALUES
    ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000011', true, 1),
    ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000012', true, 1);
