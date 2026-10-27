-- =============================================================================
-- Migration 006: Scope meter measurements to communities.
-- Assigns pre-existing measurements and meter points to the existing community.
-- Run during a maintenance window, then recreate rollups by running
-- db/timescale_continuous_aggregates.sql before using community dashboards.
-- =============================================================================

BEGIN;

DO $$
DECLARE chunk RECORD;
BEGIN
    FOR chunk IN
        SELECT format('%I.%I', chunk_schema, chunk_name)::regclass AS chunk_table
        FROM timescaledb_information.chunks
        WHERE hypertable_name = 'raw_measurements' AND is_compressed
    LOOP
        PERFORM decompress_chunk(chunk.chunk_table, if_compressed => true);
    END LOOP;
END $$;

CREATE TABLE meter_point_communities (
    meter_id BIGINT NOT NULL,
    community_id VARCHAR(100) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_meter_point_communities PRIMARY KEY (meter_id, community_id)
);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'meter_points'
          AND column_name = 'id'
    ) THEN
        EXECUTE 'ALTER TABLE meter_point_communities
            ADD CONSTRAINT fk_meter_point_communities_meter_points_meter_id
            FOREIGN KEY (meter_id) REFERENCES meter_points (id) ON DELETE CASCADE';
        EXECUTE 'INSERT INTO meter_point_communities (meter_id, community_id)
            SELECT id, ''2OWxVjZ6MMjZzyRB'' FROM meter_points
            ON CONFLICT (meter_id, community_id) DO NOTHING';
    ELSE
        EXECUTE 'ALTER TABLE meter_point_communities
            ADD CONSTRAINT fk_meter_point_communities_meter_points_meter_id
            FOREIGN KEY (meter_id) REFERENCES meter_points ("Id") ON DELETE CASCADE';
        EXECUTE 'INSERT INTO meter_point_communities (meter_id, community_id)
            SELECT "Id", ''2OWxVjZ6MMjZzyRB'' FROM meter_points
            ON CONFLICT (meter_id, community_id) DO NOTHING';
    END IF;
END $$;

DO $$
DECLARE constraint_name TEXT;
BEGIN
    FOR constraint_name IN
        SELECT conname
        FROM pg_constraint
        WHERE conrelid = 'raw_measurements'::regclass AND contype = 'p'
    LOOP
        EXECUTE format('ALTER TABLE raw_measurements DROP CONSTRAINT %I', constraint_name);
    END LOOP;
END $$;

DROP INDEX IF EXISTS "IX_raw_measurements_meter_id_timestamp";
DROP INDEX IF EXISTS "IX_raw_measurements_timestamp";
DROP INDEX IF EXISTS idx_raw_meas_mp_ts;
DROP INDEX IF EXISTS idx_raw_measurements_meter_ts;

ALTER TABLE raw_measurements
    ADD COLUMN community_id VARCHAR(100);

UPDATE raw_measurements
SET community_id = '2OWxVjZ6MMjZzyRB'
WHERE community_id IS NULL;

ALTER TABLE raw_measurements
    ALTER COLUMN community_id SET NOT NULL,
    ADD CONSTRAINT pk_raw_measurements
        PRIMARY KEY (meter_id, community_id, timestamp);

CREATE INDEX idx_raw_measurements_community_meter_ts
    ON raw_measurements (community_id, meter_id, timestamp DESC);

CREATE INDEX idx_meter_point_communities_community
    ON meter_point_communities (community_id);

ALTER TABLE raw_measurements SET (
    timescaledb.compress,
    timescaledb.compress_orderby = 'timestamp DESC',
    timescaledb.compress_segmentby = 'community_id, meter_id'
);

COMMENT ON COLUMN raw_measurements.community_id IS 'External energy community identifier (part of the raw measurement key).';
COMMENT ON TABLE meter_point_communities IS 'Many-to-many membership of globally identified meter points in energy communities.';

DROP MATERIALIZED VIEW IF EXISTS hourly_rollups CASCADE;
DROP MATERIALIZED VIEW IF EXISTS daily_rollups CASCADE;
DROP MATERIALIZED VIEW IF EXISTS monthly_rollups CASCADE;
DROP MATERIALIZED VIEW IF EXISTS yearly_rollups CASCADE;
DROP MATERIALIZED VIEW IF EXISTS community_hourly_rollups CASCADE;
DROP MATERIALIZED VIEW IF EXISTS community_daily_rollups CASCADE;
DROP MATERIALIZED VIEW IF EXISTS community_monthly_rollups CASCADE;
DROP MATERIALIZED VIEW IF EXISTS community_yearly_rollups CASCADE;

COMMIT;
