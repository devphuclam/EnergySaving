-- 0014_operational_workspace_scope.sql
-- Persist pre-Mapping Data Source ownership for scoped setup resume and close
-- the nullable root-Site-scope uniqueness gap.
BEGIN;

ALTER TABLE catalog.data_sources
    ADD COLUMN IF NOT EXISTS site_id uuid NULL;

CREATE INDEX IF NOT EXISTS ix_data_sources_site_status
    ON catalog.data_sources (site_id, status);

CREATE UNIQUE INDEX IF NOT EXISTS ux_user_scope_root_site
    ON iam.user_scope (user_id, site_id)
    WHERE area_id IS NULL;

COMMIT;
