-- 0004_organization_hierarchy.sql
-- Organization owns Site, Area, Asset, and Measurement Point hierarchy with lifecycle.
-- This migration is intentionally independent of IAM and Catalog schemas.
CREATE SCHEMA IF NOT EXISTS organization;

CREATE TABLE IF NOT EXISTS organization.sites (
    id uuid PRIMARY KEY,
    code text NOT NULL,
    name text NOT NULL,
    description text,
    timezone text NOT NULL,
    status text NOT NULL DEFAULT 'Draft',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_sites_code_nonempty CHECK (length(btrim(code)) > 0 AND code = upper(code)),
    CONSTRAINT ck_sites_name_nonempty CHECK (length(btrim(name)) > 0),
    CONSTRAINT ck_sites_timezone_nonempty CHECK (length(btrim(timezone)) > 0),
    CONSTRAINT ck_sites_status CHECK (status IN ('Draft', 'Active', 'Inactive')),
    CONSTRAINT ck_sites_version_positive CHECK (version > 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_sites_code ON organization.sites (upper(code));
CREATE INDEX IF NOT EXISTS ix_sites_status ON organization.sites (status);

CREATE TABLE IF NOT EXISTS organization.areas (
    id uuid PRIMARY KEY,
    site_id uuid NOT NULL REFERENCES organization.sites(id),
    code text NOT NULL,
    name text NOT NULL,
    description text,
    status text NOT NULL DEFAULT 'Draft',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_areas_id_site UNIQUE (id, site_id),
    CONSTRAINT ck_areas_code_nonempty CHECK (length(btrim(code)) > 0 AND code = upper(code)),
    CONSTRAINT ck_areas_name_nonempty CHECK (length(btrim(name)) > 0),
    CONSTRAINT ck_areas_status CHECK (status IN ('Draft', 'Active', 'Inactive')),
    CONSTRAINT ck_areas_version_positive CHECK (version > 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_areas_site_code ON organization.areas (site_id, upper(code));
CREATE INDEX IF NOT EXISTS ix_areas_site_id ON organization.areas (site_id);
CREATE INDEX IF NOT EXISTS ix_areas_status ON organization.areas (status);

CREATE TABLE IF NOT EXISTS organization.assets (
    id uuid PRIMARY KEY,
    site_id uuid NOT NULL REFERENCES organization.sites(id),
    area_id uuid NOT NULL,
    code text NOT NULL,
    name text NOT NULL,
    description text,
    status text NOT NULL DEFAULT 'Draft',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_assets_id_site_area UNIQUE (id, site_id, area_id),
    CONSTRAINT fk_assets_area_site FOREIGN KEY (area_id, site_id)
        REFERENCES organization.areas (id, site_id),
    CONSTRAINT ck_assets_code_nonempty CHECK (length(btrim(code)) > 0 AND code = upper(code)),
    CONSTRAINT ck_assets_name_nonempty CHECK (length(btrim(name)) > 0),
    CONSTRAINT ck_assets_status CHECK (status IN ('Draft', 'Active', 'Inactive', 'Decommissioned')),
    CONSTRAINT ck_assets_version_positive CHECK (version > 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_assets_area_code ON organization.assets (area_id, upper(code));
CREATE INDEX IF NOT EXISTS ix_assets_site_id ON organization.assets (site_id);
CREATE INDEX IF NOT EXISTS ix_assets_area_id ON organization.assets (area_id);
CREATE INDEX IF NOT EXISTS ix_assets_status ON organization.assets (status);

CREATE TABLE IF NOT EXISTS organization.measurement_points (
    id uuid PRIMARY KEY,
    site_id uuid NOT NULL REFERENCES organization.sites(id),
    area_id uuid NOT NULL,
    asset_id uuid NOT NULL,
    code text NOT NULL,
    description text,
    metric_id text NOT NULL,
    unit_id text NOT NULL,
    data_owner_user_id text NOT NULL,
    expected_interval_seconds integer NOT NULL,
    no_data_after_seconds integer NOT NULL,
    status text NOT NULL DEFAULT 'Draft',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_points_asset_ancestry FOREIGN KEY (asset_id, area_id, site_id)
        REFERENCES organization.assets (id, area_id, site_id),
    CONSTRAINT ck_measurement_points_code_nonempty CHECK (length(btrim(code)) > 0 AND code = upper(code)),
    CONSTRAINT ck_measurement_points_status CHECK (status IN ('Draft', 'Active', 'Inactive', 'Decommissioned')),
    CONSTRAINT ck_measurement_points_version_positive CHECK (version > 0),
    CONSTRAINT ck_measurement_points_expected_interval_positive CHECK (expected_interval_seconds > 0),
    CONSTRAINT ck_measurement_points_no_data_after_greater CHECK (no_data_after_seconds > expected_interval_seconds)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_measurement_points_site_code ON organization.measurement_points (site_id, upper(code));
CREATE INDEX IF NOT EXISTS ix_measurement_points_site_id ON organization.measurement_points (site_id);
CREATE INDEX IF NOT EXISTS ix_measurement_points_area_id ON organization.measurement_points (area_id);
CREATE INDEX IF NOT EXISTS ix_measurement_points_asset_id ON organization.measurement_points (asset_id);
CREATE INDEX IF NOT EXISTS ix_measurement_points_status ON organization.measurement_points (status);

CREATE TABLE IF NOT EXISTS organization.point_lifecycle_history (
    id uuid PRIMARY KEY,
    point_id uuid NOT NULL REFERENCES organization.measurement_points(id),
    point_version bigint NOT NULL,
    old_status text NOT NULL,
    new_status text NOT NULL,
    actor_id text NOT NULL,
    actor_username text,
    reason text,
    occurred_at timestamptz NOT NULL DEFAULT now(),
    correlation_id text,
    causation_id text,
    CONSTRAINT ck_point_history_version_positive CHECK (point_version > 0),
    CONSTRAINT ck_point_history_statuses CHECK (
        old_status IN ('Draft', 'Active', 'Inactive', 'Decommissioned')
        AND new_status IN ('Draft', 'Active', 'Inactive', 'Decommissioned')
        AND old_status <> new_status
    )
);
CREATE INDEX IF NOT EXISTS ix_point_lifecycle_history_point_id ON organization.point_lifecycle_history (point_id);
CREATE INDEX IF NOT EXISTS ix_point_lifecycle_history_occurred_at ON organization.point_lifecycle_history (occurred_at);
CREATE UNIQUE INDEX IF NOT EXISTS ux_point_lifecycle_history_point_version
    ON organization.point_lifecycle_history (point_id, point_version);

CREATE OR REPLACE FUNCTION organization.reject_point_history_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'point_lifecycle_history is append-only';
END;
$$;

DROP TRIGGER IF EXISTS trg_point_lifecycle_history_append_only ON organization.point_lifecycle_history;
CREATE TRIGGER trg_point_lifecycle_history_append_only
BEFORE UPDATE OR DELETE ON organization.point_lifecycle_history
FOR EACH ROW EXECUTE FUNCTION organization.reject_point_history_mutation();
