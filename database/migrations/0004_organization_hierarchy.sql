-- 0004_organization_hierarchy.sql
-- Organization owns Site, Area, Asset, and Measurement Point hierarchy with lifecycle.
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
    CONSTRAINT ck_sites_status CHECK (status IN ('Draft', 'Active', 'Inactive', 'Decommissioned')),
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
    CONSTRAINT ck_areas_code_nonempty CHECK (length(btrim(code)) > 0 AND code = upper(code)),
    CONSTRAINT ck_areas_name_nonempty CHECK (length(btrim(name)) > 0),
    CONSTRAINT ck_areas_status CHECK (status IN ('Draft', 'Active', 'Inactive', 'Decommissioned')),
    CONSTRAINT ck_areas_version_positive CHECK (version > 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_areas_site_code ON organization.areas (site_id, upper(code));
CREATE INDEX IF NOT EXISTS ix_areas_site_id ON organization.areas (site_id);
CREATE INDEX IF NOT EXISTS ix_areas_status ON organization.areas (status);

CREATE TABLE IF NOT EXISTS organization.assets (
    id uuid PRIMARY KEY,
    site_id uuid NOT NULL REFERENCES organization.sites(id),
    area_id uuid NOT NULL REFERENCES organization.areas(id),
    code text NOT NULL,
    name text NOT NULL,
    description text,
    status text NOT NULL DEFAULT 'Draft',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
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
    area_id uuid NOT NULL REFERENCES organization.areas(id),
    asset_id uuid NOT NULL REFERENCES organization.assets(id),
    code text NOT NULL,
    description text,
    metric_id text NOT NULL,
    unit_id text NOT NULL,
    data_owner_user_id text,
    expected_interval_seconds integer NOT NULL,
    no_data_after_seconds integer NOT NULL,
    status text NOT NULL DEFAULT 'Draft',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
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
    id text PRIMARY KEY,
    point_id text NOT NULL,
    point_version bigint NOT NULL,
    old_status text NOT NULL,
    new_status text NOT NULL,
    actor_id text NOT NULL,
    actor_username text,
    reason text,
    occurred_at timestamptz NOT NULL,
    correlation_id text,
    causation_id text
);
CREATE INDEX IF NOT EXISTS ix_point_lifecycle_history_point_id ON organization.point_lifecycle_history (point_id);
CREATE INDEX IF NOT EXISTS ix_point_lifecycle_history_occurred_at ON organization.point_lifecycle_history (occurred_at);
