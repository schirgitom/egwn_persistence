-- Run against an isolated, EF-migrated test database after
-- db/grafana_customer_access.sql. The transaction rolls back all fixtures.
BEGIN;

CREATE ROLE grafana_test_admin NOLOGIN CREATEROLE;
DO $$
BEGIN
    EXECUTE format('GRANT CREATE ON DATABASE %I TO grafana_test_admin', current_database());
END $$;
GRANT USAGE ON SCHEMA grafana_access TO grafana_test_admin;
GRANT SELECT, INSERT ON grafana_access.customers TO grafana_test_admin;
GRANT SELECT, INSERT, DELETE ON grafana_access.communities, grafana_access.meters TO grafana_test_admin;
GRANT SELECT ON public.meter_points, public.meter_point_communities,
    public.raw_measurements, public.kpi_measurements TO grafana_test_admin;
GRANT EXECUTE ON FUNCTION grafana_access.provision_customer(uuid, text),
    grafana_access.install_community_hourly_view(uuid, name, name),
    grafana_access.replace_entitlements(uuid, text[], jsonb),
    grafana_access.disable_customer(uuid) TO grafana_test_admin;

SET ROLE grafana_test_admin;
SELECT grafana_access.provision_customer(
    '00000000-0000-0000-0000-000000000003', repeat('c', 32));
SELECT grafana_access.provision_customer(
    '00000000-0000-0000-0000-000000000003', repeat('d', 32));
SELECT grafana_access.replace_entitlements(
    '00000000-0000-0000-0000-000000000003', ARRAY['test-c1'], '[]'::jsonb);
SELECT grafana_access.disable_customer('00000000-0000-0000-0000-000000000003');
RESET ROLE;

SELECT grafana_access.provision_customer(
    '00000000-0000-0000-0000-000000000001', repeat('a', 32));
SELECT grafana_access.provision_customer(
    '00000000-0000-0000-0000-000000000002', repeat('b', 32));

INSERT INTO meter_points("Id", meter_point_number, energy_direction, created_at, updated_at)
VALUES (99101, 'TEST-OWN', 'consumption', now(), now()),
       (99102, 'TEST-SHARED', 'generation', now(), now()),
       (99103, 'TEST-OTHER', 'consumption', now(), now()),
       (99104, 'TEST-UNSET', NULL, now(), now());
INSERT INTO meter_point_communities(meter_id, community_id, created_at)
VALUES (99101, 'test-c1', now()), (99102, 'test-c1', now()),
       (99102, 'test-c2', now()), (99103, 'test-c1', now()),
       (99103, 'test-c2', now()), (99104, 'test-c1', now());

INSERT INTO raw_measurements(
    meter_id, community_id, timestamp, total_consumption, grid_share,
    community_share, grid_share_percent, community_share_percent,
    source, ingested_at, created_at, updated_at
)
SELECT m.meter_id, m.community_id, now(),
       CASE m.meter_id WHEN 99101 THEN 2 WHEN 99102 THEN 3 WHEN 99103 THEN 5 ELSE 11 END,
       CASE m.meter_id WHEN 99101 THEN 1 WHEN 99102 THEN 2 WHEN 99103 THEN 4 ELSE 10 END,
       1,
       CASE m.meter_id WHEN 99101 THEN 0.5 WHEN 99102 THEN 2.0/3 WHEN 99103 THEN 0.8 ELSE 10.0/11 END,
       CASE m.meter_id WHEN 99101 THEN 0.5 WHEN 99102 THEN 1.0/3 WHEN 99103 THEN 0.2 ELSE 1.0/11 END,
       'test', now(), now(), now()
FROM meter_point_communities m WHERE m.meter_id BETWEEN 99101 AND 99104;
INSERT INTO raw_measurements(
    meter_id, community_id, timestamp, total_consumption, grid_share,
    community_share, grid_share_percent, community_share_percent,
    source, ingested_at, created_at, updated_at
) VALUES (99101, 'test-c2', now(), 100, 100, 0, 1, 0, 'test', now(), now(), now());
INSERT INTO kpi_measurements(
    community_id, timestamp, ingested_at, created_at, updated_at
) VALUES ('test-c1', now(), now(), now(), now()), ('test-c2', now(), now(), now(), now());

SELECT grafana_access.replace_entitlements(
    '00000000-0000-0000-0000-000000000001',
    ARRAY['test-c1', 'test-c2'],
    '[{"communityId":"test-c1","meterPointNumber":"TEST-OWN"},
      {"communityId":"test-c1","meterPointNumber":"TEST-SHARED"}]'::jsonb);
SELECT grafana_access.replace_entitlements(
    '00000000-0000-0000-0000-000000000002',
    ARRAY['test-c2'],
    '[{"communityId":"test-c2","meterPointNumber":"TEST-SHARED"},
      {"communityId":"test-c2","meterPointNumber":"TEST-OTHER"}]'::jsonb);

DO $$
DECLARE rejected boolean := false;
BEGIN
    BEGIN
        PERFORM grafana_access.replace_entitlements(
            '00000000-0000-0000-0000-000000000001',
            ARRAY['test-c1'],
            '[{"communityId":"test-c2","meterPointNumber":"TEST-SHARED"}]'::jsonb);
    EXCEPTION WHEN raise_exception THEN
        rejected := true;
    END;
    IF NOT rejected OR
       has_function_privilege(
           'grafana_customer_00000000000000000000000000000001',
           'grafana_access.replace_entitlements(uuid, text[], jsonb)', 'EXECUTE') THEN
        RAISE EXCEPTION 'Invalid entitlement or function privilege was accepted';
    END IF;
END $$;

SET ROLE grafana_customer_00000000000000000000000000000001;
DO $$
DECLARE s text := 'grafana_customer_00000000000000000000000000000001';
BEGIN
    IF (SELECT count(*) FROM grafana_customer_00000000000000000000000000000001.raw_measurements) <> 2
       OR (SELECT count(*) FROM grafana_customer_00000000000000000000000000000001.meter_point_communities) <> 2
       OR (SELECT count(*) FROM grafana_customer_00000000000000000000000000000001.meter_points) <> 2
       OR (SELECT count(*) FROM grafana_customer_00000000000000000000000000000001.kpi_measurements) <> 2
       OR (SELECT count(*) FROM grafana_customer_00000000000000000000000000000001.community_hourly_measurements) <> 4
       OR NOT EXISTS (
           SELECT 1 FROM grafana_customer_00000000000000000000000000000001.community_hourly_measurements
           WHERE community_id = 'test-c1' AND energy_direction = 'consumption'
             AND total_consumption = 7 AND grid_share = 5 AND community_share = 2)
       OR NOT EXISTS (
           SELECT 1 FROM grafana_customer_00000000000000000000000000000001.community_hourly_measurements
           WHERE community_id = 'test-c2' AND energy_direction = 'generation'
             AND total_consumption = 3)
       OR NOT EXISTS (
           SELECT 1 FROM grafana_customer_00000000000000000000000000000001.community_hourly_measurements
           WHERE community_id = 'test-c2' AND energy_direction = 'consumption'
             AND total_consumption = 5)
       OR EXISTS (
           SELECT 1 FROM information_schema.columns
           WHERE table_schema = s AND table_name = 'community_hourly_measurements'
             AND column_name IN ('meter_id', 'meter_point_number'))
       OR has_table_privilege(s, 'public.raw_measurements', 'SELECT')
       OR (to_regclass('public.community_hourly_rollups') IS NOT NULL AND
           has_table_privilege(s, to_regclass('public.community_hourly_rollups'), 'SELECT'))
       OR has_table_privilege(
           s, (SELECT oid FROM pg_class WHERE relname = 'meters'
               AND relnamespace = (SELECT oid FROM pg_namespace WHERE nspname = 'grafana_access')),
           'SELECT')
       OR has_schema_privilege(s, 'grafana_customer_00000000000000000000000000000002', 'USAGE') THEN
        RAISE EXCEPTION 'Customer 1 isolation failed';
    END IF;
END $$;
RESET ROLE;

SET ROLE grafana_customer_00000000000000000000000000000002;
DO $$
BEGIN
    IF (SELECT count(*) FROM grafana_customer_00000000000000000000000000000002.raw_measurements) <> 2
       OR (SELECT count(*) FROM grafana_customer_00000000000000000000000000000002.kpi_measurements) <> 1
       OR (SELECT count(*) FROM grafana_customer_00000000000000000000000000000002.community_hourly_measurements) <> 2
       OR EXISTS (
           SELECT 1 FROM grafana_customer_00000000000000000000000000000002.community_hourly_measurements
           WHERE community_id = 'test-c1')
       OR EXISTS (
           SELECT 1 FROM grafana_customer_00000000000000000000000000000002.meter_points
           WHERE meter_point_number = 'TEST-OWN'
       ) THEN
        RAISE EXCEPTION 'Customer 2 isolation failed';
    END IF;
END $$;
RESET ROLE;

SELECT grafana_access.replace_entitlements(
    '00000000-0000-0000-0000-000000000001',
    ARRAY['test-c2'], '[]'::jsonb);
SET ROLE grafana_customer_00000000000000000000000000000001;
DO $$
BEGIN
    IF (SELECT count(*) FROM grafana_customer_00000000000000000000000000000001.raw_measurements) <> 0
       OR (SELECT count(*) FROM grafana_customer_00000000000000000000000000000001.kpi_measurements) <> 1
       OR (SELECT count(*) FROM grafana_customer_00000000000000000000000000000001.community_hourly_measurements) <> 2 THEN
        RAISE EXCEPTION 'Authorization replacement failed';
    END IF;
END $$;
RESET ROLE;

SELECT grafana_access.disable_customer('00000000-0000-0000-0000-000000000001');
SET ROLE grafana_customer_00000000000000000000000000000001;
DO $$
BEGIN
    IF (SELECT count(*) FROM grafana_customer_00000000000000000000000000000001.kpi_measurements) <> 0
       OR (SELECT count(*) FROM grafana_customer_00000000000000000000000000000001.community_hourly_measurements) <> 0 THEN
        RAISE EXCEPTION 'Disabled customer still sees KPIs';
    END IF;
END $$;
RESET ROLE;

ROLLBACK;
