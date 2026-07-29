-- 0012_r1_idempotent_seeds.sql
-- Deterministic R1 bootstrap configuration. No user, credential, password hash,
-- token, session, root Site, user scope, or operational evidence is created.
BEGIN;

INSERT INTO iam.role (role_id, code, name) VALUES
    ('a0000000-0000-0000-0000-000000000001', 'Administrator', 'Administrator'),
    ('a0000000-0000-0000-0000-000000000002', 'Engineer', 'Engineer'),
    ('a0000000-0000-0000-0000-000000000003', 'Operator', 'Operator'),
    ('a0000000-0000-0000-0000-000000000004', 'Manager', 'Manager'),
    ('a0000000-0000-0000-0000-000000000005', 'Viewer', 'Viewer')
ON CONFLICT DO NOTHING;

INSERT INTO iam.capability (capability_id, code, name) VALUES
    ('a1000000-0000-0000-0000-000000000001', 'AUDIT_READ', 'Audit Review')
ON CONFLICT DO NOTHING;

INSERT INTO catalog.metrics (id, code, name, status, version) VALUES
    ('00000000-0000-0000-0000-000000000001', 'ELECTRIC_POWER', 'Electric Power', 'Active', 1),
    ('00000000-0000-0000-0000-000000000002', 'ELECTRICAL_ENERGY', 'Electrical Energy', 'Active', 1)
ON CONFLICT DO NOTHING;

INSERT INTO catalog.units (id, code, symbol, status, version) VALUES
    ('00000000-0000-0000-0000-000000000011', 'KW', 'kW', 'Active', 1),
    ('00000000-0000-0000-0000-000000000012', 'KWH', 'kWh', 'Active', 1)
ON CONFLICT DO NOTHING;

INSERT INTO catalog.metric_unit_compatibilities (metric_id, unit_id, is_canonical, version) VALUES
    ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000011', true, 1),
    ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000012', true, 1)
ON CONFLICT DO NOTHING;

-- Fail closed if a natural key or fixed ID was previously assigned a different
-- meaning. This validates existing mutable bootstrap configuration; it never
-- updates immutable operational or Audit evidence.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM (VALUES
            ('a0000000-0000-0000-0000-000000000001'::uuid, 'Administrator'),
            ('a0000000-0000-0000-0000-000000000002'::uuid, 'Engineer'),
            ('a0000000-0000-0000-0000-000000000003'::uuid, 'Operator'),
            ('a0000000-0000-0000-0000-000000000004'::uuid, 'Manager'),
            ('a0000000-0000-0000-0000-000000000005'::uuid, 'Viewer')
        ) expected(id, code)
        LEFT JOIN iam.role actual ON actual.role_id = expected.id AND actual.code = expected.code
        WHERE actual.role_id IS NULL
    ) THEN RAISE EXCEPTION 'R1_ROLE_SEED_CONFLICT'; END IF;

    IF NOT EXISTS (
        SELECT 1 FROM iam.capability
        WHERE capability_id = 'a1000000-0000-0000-0000-000000000001'
          AND code = 'AUDIT_READ'
    ) THEN RAISE EXCEPTION 'R1_CAPABILITY_SEED_CONFLICT'; END IF;

    IF EXISTS (
        SELECT 1
        FROM (VALUES
            ('00000000-0000-0000-0000-000000000001'::uuid, 'ELECTRIC_POWER'),
            ('00000000-0000-0000-0000-000000000002'::uuid, 'ELECTRICAL_ENERGY')
        ) expected(id, code)
        LEFT JOIN catalog.metrics actual ON actual.id = expected.id AND actual.code = expected.code
        WHERE actual.id IS NULL
    ) THEN RAISE EXCEPTION 'R1_METRIC_SEED_CONFLICT'; END IF;

    IF EXISTS (
        SELECT 1
        FROM (VALUES
            ('00000000-0000-0000-0000-000000000011'::uuid, 'KW'),
            ('00000000-0000-0000-0000-000000000012'::uuid, 'KWH')
        ) expected(id, code)
        LEFT JOIN catalog.units actual ON actual.id = expected.id AND actual.code = expected.code
        WHERE actual.id IS NULL
    ) THEN RAISE EXCEPTION 'R1_UNIT_SEED_CONFLICT'; END IF;
END $$;

COMMIT;
