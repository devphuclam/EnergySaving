-- 0006_catalog_source_mapping.sql
-- Catalog owns the Source -> Organization Point logical association.
-- PointId is intentionally logical: no cross-schema FK to organization.
CREATE SCHEMA IF NOT EXISTS catalog;

CREATE TABLE IF NOT EXISTS catalog.source_point_mapping (
    mapping_id uuid PRIMARY KEY,
    data_source_id uuid NOT NULL REFERENCES catalog.data_sources(id),
    point_id text NOT NULL,
    status text NOT NULL DEFAULT 'Draft',
    effective_from timestamptz NOT NULL,
    effective_to timestamptz,
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_source_point_mapping_point_nonempty CHECK (length(btrim(point_id)) > 0),
    CONSTRAINT ck_source_point_mapping_status CHECK (status IN ('Draft', 'Active', 'Inactive', 'Superseded')),
    CONSTRAINT ck_source_point_mapping_period_half_open CHECK (effective_to IS NULL OR effective_to > effective_from),
    CONSTRAINT ck_source_point_mapping_version_positive CHECK (version > 0)
);
CREATE INDEX IF NOT EXISTS ix_source_point_mapping_point_time ON catalog.source_point_mapping (point_id, effective_from, effective_to);
CREATE INDEX IF NOT EXISTS ix_source_point_mapping_source_status ON catalog.source_point_mapping (data_source_id, status);
CREATE INDEX IF NOT EXISTS ix_source_point_mapping_status ON catalog.source_point_mapping (status);

-- Active effective periods for one Point must not overlap. The reviewed PostgreSQL-native
-- exclusion constraint requires btree_gist, which is not approved for this invocation.
-- BLOCKED_CAPABILITY: provision btree_gist through the approved DBA/package path, then execute:
-- ALTER TABLE catalog.source_point_mapping ADD CONSTRAINT ex_source_point_mapping_active_period
-- EXCLUDE USING gist (point_id WITH =, tstzrange(effective_from, effective_to, '[)') WITH &&)
-- WHERE (status = 'Active');
-- Do not replace this invariant with a weaker unique index or provision the extension here.
