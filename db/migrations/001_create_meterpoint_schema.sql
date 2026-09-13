-- =============================================================================
-- Migration 001: Create meter_points and raw_measurements tables
-- Target: TimescaleDB/PostgreSQL
-- Purpose: BigInt-based meter point metadata and append-only raw hourly measurements
-- =============================================================================

CREATE EXTENSION IF NOT EXISTS timescaledb;
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ----------------------------------------------------------------------------
-- 1) Meter point metadata table
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS meter_points (
    id                 BIGSERIAL PRIMARY KEY,
    meter_point_number TEXT      NOT NULL UNIQUE,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at         TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_meter_points_meter_point_number
    ON meter_points (meter_point_number);

COMMENT ON TABLE meter_points IS 'Metadata for meter points. Internal surrogate key is BIGINT, while meter_point_number is the unique external identifier.';

-- ----------------------------------------------------------------------------
-- 2) Raw measurement hypertable
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS raw_measurements (
    meter_id               BIGINT      NOT NULL REFERENCES meter_points(id),
    timestamp              TIMESTAMPTZ NOT NULL,
    total_consumption      NUMERIC(18,6) NOT NULL,
    grid_share             NUMERIC(18,6) NOT NULL,
    community_share        NUMERIC(18,6) NOT NULL,
    grid_share_percent     NUMERIC(7,6)  NOT NULL CHECK (grid_share_percent BETWEEN 0 AND 1),
    community_share_percent NUMERIC(7,6) NOT NULL CHECK (community_share_percent BETWEEN 0 AND 1),
    source                 TEXT        NOT NULL,
    ingested_at            TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT pk_raw_measurements PRIMARY KEY (meter_id, timestamp)
);

SELECT create_hypertable(
    'raw_measurements',
    'timestamp',
    chunk_time_interval => INTERVAL '1 week',
    if_not_exists       => TRUE
);

CREATE INDEX IF NOT EXISTS idx_raw_measurements_meter_ts
    ON raw_measurements (meter_id, timestamp DESC);

CREATE INDEX IF NOT EXISTS idx_raw_measurements_ingested_at
    ON raw_measurements (ingested_at DESC);

COMMENT ON TABLE raw_measurements IS 'Raw hourly measurements. Values can be corrected within a 3-week window; after that they are treated as fixed historical data.';
COMMENT ON COLUMN raw_measurements.meter_id IS 'Internal BIGINT FK to meter_points.id';
COMMENT ON COLUMN raw_measurements.timestamp IS 'Measurement period start timestamp in UTC';
COMMENT ON COLUMN raw_measurements.total_consumption IS 'Total consumption in kWh for the hour';
COMMENT ON COLUMN raw_measurements.grid_share IS 'Grid share in kWh';
COMMENT ON COLUMN raw_measurements.community_share IS 'Community share in kWh';
COMMENT ON COLUMN raw_measurements.grid_share_percent IS 'Grid share as fraction of total (0..1)';
COMMENT ON COLUMN raw_measurements.community_share_percent IS 'Community share as fraction of total (0..1)';
COMMENT ON COLUMN raw_measurements.source IS 'Data source identifier, e.g. EDA';
COMMENT ON COLUMN raw_measurements.ingested_at IS 'When ingestion inserted the record in the database';
COMMENT ON COLUMN raw_measurements.created_at IS 'Original creation timestamp';
COMMENT ON COLUMN raw_measurements.updated_at IS 'Timestamp of the latest correction within the 3-week correction window';
