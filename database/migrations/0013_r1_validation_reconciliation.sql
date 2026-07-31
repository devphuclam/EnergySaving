-- 0013_r1_validation_reconciliation.sql
-- Read-only R1 validation and reconciliation evidence. Every query reports
-- inconsistencies; this file performs no repair and changes no business data.
BEGIN TRANSACTION READ ONLY;

-- Owner state: database/schema/table ownership plus Point Data Owner references.
SELECT n.nspname AS schema_name, c.relname AS relation_name,
       pg_get_userbyid(c.relowner) AS owner_name
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname IN ('iam','catalog','organization','acquisition','telemetry','integration','operations','audit')
  AND c.relkind IN ('r','p')
ORDER BY n.nspname, c.relname;

SELECT p.id AS point_id, p.data_owner_user_id
FROM organization.measurement_points p
LEFT JOIN iam.user_account u ON u.user_id::text = p.data_owner_user_id
WHERE u.user_id IS NULL OR u.status <> 'Active'
ORDER BY p.id;

-- Command registry terminal-shape and expiry reconciliation.
SELECT command_idempotency_id, status, attempt_count, version
FROM integration.command_idempotency
WHERE (status = 'Pending' AND (pending_owner IS NULL OR pending_until IS NULL))
   OR (status = 'Completed' AND (completed_at IS NULL OR original_http_status IS NULL OR original_result_payload IS NULL))
   OR attempt_count < 0 OR version <= 0
ORDER BY command_idempotency_id;

-- Telemetry terminal/raw/Latest consistency, including Bad exclusion.
SELECT i.measurement_id, i.final_classification,
       count(r.measurement_id) AS raw_count
FROM telemetry.measurement_identity i
LEFT JOIN telemetry.measurement_raw r ON r.measurement_id = i.measurement_id
GROUP BY i.measurement_id, i.final_classification
HAVING (i.final_classification = 'Accepted' AND count(r.measurement_id) <> 1)
    OR (i.final_classification = 'Rejected' AND count(r.measurement_id) <> 0)
ORDER BY i.measurement_id;

SELECT l.point_id, l.measurement_id, l.quality_code
FROM telemetry.point_latest l
LEFT JOIN telemetry.measurement_raw r ON r.measurement_id = l.measurement_id
WHERE r.measurement_id IS NULL
   OR l.quality_code NOT IN ('Good','Uncertain')
   OR r.point_id <> l.point_id
ORDER BY l.point_id;

-- Source Health projection shape and counter reconciliation.
SELECT point_id, source_id, health_status, generated_count, accepted_count, rejected_count
FROM telemetry.point_source_status
WHERE expected_interval_seconds <= 0
   OR no_data_after_seconds <= expected_interval_seconds
   OR accepted_count + rejected_count > generated_count
   OR (health_status = 'NoData' AND last_accepted_received_at_utc IS NOT NULL
       AND evaluated_at_utc <= last_accepted_received_at_utc)
ORDER BY point_id;

-- Outbox/inbox delivery and Published-without-Audit evidence.
SELECT o.event_id, o.status, o.published_at,
       count(i.consumer_name) FILTER (WHERE i.status = 'Completed') AS completed_consumers
FROM integration.outbox_event o
LEFT JOIN integration.inbox_message i ON i.event_id = o.event_id
GROUP BY o.event_id, o.status, o.published_at
HAVING (o.status = 'Published' AND o.published_at IS NULL)
    OR (o.status <> 'Published' AND o.published_at IS NOT NULL)
ORDER BY o.event_id;

SELECT o.event_id, o.event_type, o.published_at
FROM integration.outbox_event o
LEFT JOIN audit.audit_event a ON a.source_event_id = o.event_id
WHERE o.status = 'Published'
  AND o.event_type IN (
      'Organization.SiteChanged.v1','Organization.AreaChanged.v1',
      'Organization.AssetChanged.v1','Organization.PointChanged.v1',
      'Catalog.SourceChanged.v1','Catalog.MappingChanged.v1',
      'Acquisition.SimulatorRunChanged.v1')
  AND a.audit_event_id IS NULL
ORDER BY o.event_id;

-- Operations lease/job terminal-shape checks.
SELECT job_id, job_type, status, attempt_count, lease_owner, lease_until, completed_at
FROM operations.job
WHERE attempt_count < 0
   OR (status = 'Leased' AND (lease_owner IS NULL OR lease_until IS NULL))
   OR (status = 'Completed' AND completed_at IS NULL)
   OR (status <> 'Completed' AND completed_at IS NOT NULL)
ORDER BY job_id;

-- Immutable Audit evidence completeness and payload identity.
SELECT audit_event_id, source_event_id, event_type
FROM audit.audit_event
WHERE octet_length(payload_hash) <> 32
   OR length(btrim(correlation_id)) = 0
   OR length(btrim(object_type)) = 0
   OR length(btrim(object_id)) = 0
ORDER BY audit_event_id;

-- Logical-reference orphans; cross-schema foreign keys are intentionally absent.
SELECT 'mapping-point' AS reference_type, m.mapping_id::text AS source_id, m.point_id AS missing_id
FROM catalog.source_point_mapping m
LEFT JOIN organization.measurement_points p ON p.id::text = m.point_id
WHERE p.id IS NULL
UNION ALL
SELECT 'telemetry-point', r.measurement_id::text, r.point_id::text
FROM telemetry.measurement_raw r
LEFT JOIN organization.measurement_points p ON p.id = r.point_id
WHERE p.id IS NULL
UNION ALL
SELECT 'telemetry-source', r.measurement_id::text, r.source_id::text
FROM telemetry.measurement_raw r
LEFT JOIN catalog.data_sources s ON s.id = r.source_id
WHERE s.id IS NULL
ORDER BY reference_type, source_id;

-- Migration order/schema-signature evidence. The expected sequence is explicit;
-- the checksum is derived from the actual catalog shape, not presented as a file checksum.
WITH expected_migrations(ordinal, filename) AS (
    VALUES
      (1,'0001_r0_foundation.sql'), (2,'0002_iam_foundation.sql'),
      (3,'0003_catalog_foundation.sql'), (4,'0004_organization_hierarchy.sql'),
      (5,'0005_acquisition_configuration.sql'), (6,'0006_catalog_source_mapping.sql'),
      (7,'0007_acquisition_run.sql'), (8,'0008_telemetry_measurement.sql'),
      (9,'0009_telemetry_latest_status.sql'), (10,'0010_audit_event.sql'),
      (11,'0011_r1_infrastructure_expand.sql'), (12,'0012_r1_idempotent_seeds.sql'),
      (13,'0013_r1_validation_reconciliation.sql'), (14,'0014_operational_workspace_scope.sql'),
      (15,'0015_acquisition_simulator_configuration_receipts.sql')
), schema_signature AS (
    SELECT md5(string_agg(
        n.nspname || '.' || c.relname || ':' || a.attname || ':' ||
        pg_catalog.format_type(a.atttypid, a.atttypmod),
        '|' ORDER BY n.nspname, c.relname, a.attnum)) AS catalog_checksum
    FROM pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    JOIN pg_attribute a ON a.attrelid = c.oid
    WHERE n.nspname IN ('iam','catalog','organization','acquisition','telemetry','integration','operations','audit')
      AND c.relkind IN ('r','p') AND a.attnum > 0 AND NOT a.attisdropped
)
SELECT e.ordinal, e.filename, s.catalog_checksum
FROM expected_migrations e CROSS JOIN schema_signature s
ORDER BY e.ordinal;

COMMIT;
