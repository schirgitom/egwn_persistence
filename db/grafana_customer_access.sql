-- Install after the EF migrations, as a database administrator. This script
-- provisions the Persistence side only; CentralAPI needs its own read boundary.
BEGIN;
CREATE SCHEMA IF NOT EXISTS grafana_access;
REVOKE ALL ON SCHEMA grafana_access FROM PUBLIC;

CREATE TABLE IF NOT EXISTS grafana_access.customers (
    customer_id uuid PRIMARY KEY,
    role_name name NOT NULL UNIQUE,
    schema_name name NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS grafana_access.communities (
    customer_id uuid NOT NULL REFERENCES grafana_access.customers(customer_id) ON DELETE CASCADE,
    community_id varchar(100) NOT NULL,
    PRIMARY KEY (customer_id, community_id)
);

CREATE TABLE IF NOT EXISTS grafana_access.meters (
    customer_id uuid NOT NULL,
    community_id varchar(100) NOT NULL,
    meter_point_number varchar(100) NOT NULL,
    PRIMARY KEY (customer_id, community_id, meter_point_number),
    FOREIGN KEY (customer_id, community_id)
        REFERENCES grafana_access.communities(customer_id, community_id) ON DELETE CASCADE
);

REVOKE ALL ON ALL TABLES IN SCHEMA grafana_access FROM PUBLIC;

-- Only a trusted provisioning DB role with CREATE ROLE and CREATE on the
-- database may invoke these functions. Never grant them to a Grafana login.
CREATE OR REPLACE FUNCTION grafana_access.install_community_hourly_view(
    p_customer_id uuid,
    p_schema name,
    p_role name
) RETURNS void LANGUAGE plpgsql SET search_path = pg_catalog, public AS $$
BEGIN
    EXECUTE format($view$
        CREATE OR REPLACE VIEW %I.community_hourly_measurements WITH (security_barrier = true) AS
        SELECT r.community_id, mp.energy_direction, r.timestamp,
               SUM(r.total_consumption) AS total_consumption,
               SUM(r.grid_share) AS grid_share,
               SUM(r.community_share) AS community_share
        FROM public.raw_measurements r
        JOIN public.meter_points mp ON mp."Id" = r.meter_id
        JOIN public.meter_point_communities m
          ON m.meter_id = r.meter_id AND m.community_id = r.community_id
        JOIN grafana_access.communities a
          ON a.customer_id = %L::uuid AND a.community_id = r.community_id
        WHERE mp.energy_direction IN ('consumption', 'generation')
        GROUP BY r.community_id, mp.energy_direction, r.timestamp
    $view$, p_schema, p_customer_id);
    EXECUTE format('REVOKE ALL ON %I.community_hourly_measurements FROM PUBLIC', p_schema);
    EXECUTE format('GRANT SELECT ON %I.community_hourly_measurements TO %I', p_schema, p_role);
END $$;

CREATE OR REPLACE FUNCTION grafana_access.provision_customer(
    p_customer_id uuid,
    p_password text
) RETURNS text LANGUAGE plpgsql SET search_path = pg_catalog, public AS $$
DECLARE
    v_name text;
    v_schema text;
    v_registered boolean;
    v_table text;
BEGIN
    IF p_customer_id IS NULL OR p_password IS NULL OR length(p_password) < 32 THEN
        RAISE EXCEPTION 'Customer ID and a generated password of at least 32 characters are required';
    END IF;

    PERFORM pg_advisory_xact_lock(hashtextextended(p_customer_id::text, 0));
    v_name := 'grafana_customer_' || replace(p_customer_id::text, '-', '');
    v_schema := v_name;
    SELECT EXISTS (
        SELECT 1 FROM grafana_access.customers
        WHERE customer_id = p_customer_id AND role_name = v_name AND schema_name = v_schema
    ) INTO v_registered;

    IF NOT v_registered AND (
        EXISTS (SELECT 1 FROM pg_roles WHERE rolname = v_name)
        OR EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = v_schema)
    ) THEN
        RAISE EXCEPTION 'Provisioning name already exists but is not registered: %', v_name;
    END IF;

    IF NOT v_registered THEN
        EXECUTE format(
            'CREATE ROLE %I LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L',
            v_name, p_password
        );
        EXECUTE format('CREATE SCHEMA %I', v_schema);
        INSERT INTO grafana_access.customers(customer_id, role_name, schema_name)
        VALUES (p_customer_id, v_name, v_schema);
    ELSE
        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = v_name) OR
           NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = v_schema) THEN
            RAISE EXCEPTION 'Registered customer role or schema missing: %', v_name;
        END IF;
        IF EXISTS (
            SELECT 1 FROM pg_roles
            WHERE rolname = v_name AND
                  (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)
        ) THEN
            RAISE EXCEPTION 'Grafana role % has unexpected privileges', v_name;
        END IF;
        EXECUTE format(
            'ALTER ROLE %I LOGIN NOINHERIT PASSWORD %L',
            v_name, p_password
        );
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_auth_members a
        JOIN pg_roles r ON r.oid = a.member
        WHERE r.rolname = v_name
    ) THEN
        RAISE EXCEPTION 'Grafana role % must not be a member of other roles', v_name;
    END IF;
    IF has_schema_privilege(v_name, 'public', 'CREATE') OR
       has_database_privilege(v_name, current_database(), 'CREATE') THEN
        RAISE EXCEPTION 'Grafana role % has unrestricted CREATE privileges', v_name;
    END IF;

    -- A role can inherit access via PUBLIC even if it has no direct grants.
    -- Fail closed instead of creating a Grafana datasource with unrestricted reads.
    FOREACH v_table IN ARRAY ARRAY[
        'meter_points', 'meter_point_communities', 'raw_measurements', 'kpi_measurements',
        'hourly_rollups', 'daily_rollups', 'monthly_rollups', 'yearly_rollups',
        'community_hourly_rollups', 'community_daily_rollups',
        'community_monthly_rollups', 'community_yearly_rollups'
    ] LOOP
        IF to_regclass(format('public.%I', v_table)) IS NOT NULL AND
           has_table_privilege(v_name, format('public.%I', v_table), 'SELECT') THEN
            RAISE EXCEPTION 'Grafana role % has unrestricted SELECT on public.%', v_name, v_table;
        END IF;
    END LOOP;

    EXECUTE format('REVOKE ALL ON SCHEMA %I FROM PUBLIC', v_schema);
    EXECUTE format('GRANT USAGE ON SCHEMA %I TO %I', v_schema, v_name);

    EXECUTE format($view$
        CREATE OR REPLACE VIEW %I.raw_measurements WITH (security_barrier = true) AS
        SELECT r.meter_id, r.community_id, r.timestamp, r.total_consumption,
               r.grid_share, r.community_share
        FROM public.raw_measurements r
        JOIN public.meter_points mp ON mp."Id" = r.meter_id
        JOIN grafana_access.meters a
          ON a.customer_id = %L::uuid AND a.community_id = r.community_id
         AND a.meter_point_number = mp.meter_point_number
        JOIN public.meter_point_communities m
          ON m.meter_id = r.meter_id AND m.community_id = r.community_id
    $view$, v_schema, p_customer_id);

    EXECUTE format($view$
        CREATE OR REPLACE VIEW %I.meter_point_communities WITH (security_barrier = true) AS
        SELECT m.meter_id, m.community_id
        FROM public.meter_point_communities m
        JOIN public.meter_points mp ON mp."Id" = m.meter_id
        JOIN grafana_access.meters a
          ON a.customer_id = %L::uuid AND a.community_id = m.community_id
         AND a.meter_point_number = mp.meter_point_number
    $view$, v_schema, p_customer_id);

    EXECUTE format($view$
        CREATE OR REPLACE VIEW %I.meter_points WITH (security_barrier = true) AS
        SELECT mp."Id", mp.meter_point_number, mp.energy_direction
        FROM public.meter_points mp
        WHERE EXISTS (
            SELECT 1 FROM public.meter_point_communities m
            JOIN grafana_access.meters a
              ON a.customer_id = %L::uuid AND a.community_id = m.community_id
             AND a.meter_point_number = mp.meter_point_number
            WHERE m.meter_id = mp."Id"
        )
    $view$, v_schema, p_customer_id);

    EXECUTE format($view$
        CREATE OR REPLACE VIEW %I.kpi_measurements WITH (security_barrier = true) AS
        SELECT k.community_id, k.timestamp, k.autarky, k.own_consumption,
               k.community, k.feed, k.remaining_demand
        FROM public.kpi_measurements k
        JOIN grafana_access.communities a
          ON a.customer_id = %L::uuid AND a.community_id = k.community_id
    $view$, v_schema, p_customer_id);

    EXECUTE format('REVOKE ALL ON ALL TABLES IN SCHEMA %I FROM PUBLIC', v_schema);
    EXECUTE format('REVOKE ALL ON ALL TABLES IN SCHEMA %I FROM %I', v_schema, v_name);
    EXECUTE format(
        'GRANT SELECT ON %1$I.raw_measurements, %1$I.meter_points, %1$I.meter_point_communities, %1$I.kpi_measurements TO %2$I',
        v_schema, v_name
    );
    PERFORM grafana_access.install_community_hourly_view(p_customer_id, v_schema::name, v_name::name);
    RETURN v_name;
END $$;

CREATE OR REPLACE FUNCTION grafana_access.replace_entitlements(
    p_customer_id uuid,
    p_communities text[],
    p_meters jsonb
) RETURNS void LANGUAGE plpgsql SET search_path = pg_catalog, public AS $$
BEGIN
    IF p_customer_id IS NULL THEN
        RAISE EXCEPTION USING ERRCODE = 'P0002', MESSAGE = 'Customer is not provisioned';
    END IF;
    PERFORM pg_advisory_xact_lock(hashtextextended(p_customer_id::text, 0));
    IF NOT EXISTS (SELECT 1 FROM grafana_access.customers WHERE customer_id = p_customer_id) THEN
        RAISE EXCEPTION USING ERRCODE = 'P0002', MESSAGE = 'Customer is not provisioned';
    END IF;
    IF p_communities IS NULL OR p_meters IS NULL OR jsonb_typeof(p_meters) <> 'array' OR
       EXISTS (SELECT 1 FROM unnest(p_communities) c
               WHERE c IS NULL OR length(trim(c)) = 0 OR length(c) > 100) OR
       EXISTS (SELECT 1 FROM jsonb_to_recordset(p_meters) AS m("communityId" text, "meterPointNumber" text)
               WHERE m."communityId" IS NULL OR length(trim(m."communityId")) = 0 OR length(m."communityId") > 100
                  OR m."meterPointNumber" IS NULL OR length(trim(m."meterPointNumber")) = 0 OR length(m."meterPointNumber") > 100
                  OR NOT m."communityId" = ANY(p_communities)) THEN
        RAISE EXCEPTION 'Invalid community or meter entitlements';
    END IF;

    DELETE FROM grafana_access.meters WHERE customer_id = p_customer_id;
    DELETE FROM grafana_access.communities WHERE customer_id = p_customer_id;

    INSERT INTO grafana_access.communities(customer_id, community_id)
    SELECT p_customer_id, c FROM unnest(p_communities) c GROUP BY c;

    INSERT INTO grafana_access.meters(customer_id, community_id, meter_point_number)
    SELECT DISTINCT p_customer_id, m."communityId", m."meterPointNumber"
    FROM jsonb_to_recordset(p_meters) AS m("communityId" text, "meterPointNumber" text);
END $$;

CREATE OR REPLACE FUNCTION grafana_access.disable_customer(p_customer_id uuid)
RETURNS void LANGUAGE plpgsql SET search_path = pg_catalog, public AS $$
DECLARE
    v_name name;
BEGIN
    IF p_customer_id IS NULL THEN
        RAISE EXCEPTION USING ERRCODE = 'P0002', MESSAGE = 'Customer is not provisioned';
    END IF;
    PERFORM pg_advisory_xact_lock(hashtextextended(p_customer_id::text, 0));
    SELECT role_name INTO v_name FROM grafana_access.customers WHERE customer_id = p_customer_id;
    IF v_name IS NULL THEN
        RAISE EXCEPTION USING ERRCODE = 'P0002', MESSAGE = 'Customer is not provisioned';
    END IF;
    DELETE FROM grafana_access.communities WHERE customer_id = p_customer_id;
    EXECUTE format('ALTER ROLE %I NOLOGIN', v_name);
END $$;

REVOKE ALL ON FUNCTION grafana_access.provision_customer(uuid, text) FROM PUBLIC;
REVOKE ALL ON FUNCTION grafana_access.replace_entitlements(uuid, text[], jsonb) FROM PUBLIC;
REVOKE ALL ON FUNCTION grafana_access.disable_customer(uuid) FROM PUBLIC;
REVOKE ALL ON FUNCTION grafana_access.install_community_hourly_view(uuid, name, name) FROM PUBLIC;

DO $$
DECLARE
    c record;
BEGIN
    FOR c IN SELECT customer_id, schema_name, role_name FROM grafana_access.customers LOOP
        PERFORM grafana_access.install_community_hourly_view(
            c.customer_id, c.schema_name, c.role_name);
    END LOOP;
END $$;
COMMIT;
