-- =============================================================================
-- Migration: 001_initial_schema.sql
-- Creates TimescaleDB extension, meter_points metadata table, and the
-- raw_measurements hypertable. Run once against the target database.
-- =============================================================================

-- Enable TimescaleDB extension
CREATE EXTENSION IF NOT EXISTS timescaledb;

-- Enable gen_random_uuid() (pgcrypto or pg_crypto)
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- =============================================================================
-- Table: meter_points
-- Stores metadata for each metering point (Zählpunkt).
-- This is a standard Postgres table — NOT a hypertable.
-- =============================================================================
CREATE TABLE IF NOT EXISTS meter_points (
    id                 UUID        NOT NULL DEFAULT gen_random_uuid(),
    meter_point_number TEXT        NOT NULL,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT pk_meter_points PRIMARY KEY (id),
    CONSTRAINT uq_meter_points_number UNIQUE (meter_point_number)
);

CREATE INDEX IF NOT EXISTS idx_meter_points_number
    ON meter_points (meter_point_number);

COMMENT ON TABLE  meter_points IS 'Registry of EDA metering points (Zählpunkte).';
COMMENT ON COLUMN meter_points.meter_point_number IS 'The EDA-assigned metering point number (e.g. AT001234567890). Globally unique.';

-- =============================================================================
-- Table: raw_measurements (TimescaleDB hypertable)
-- Append-only store for hourly energy measurements.
-- Never updated — historical data is immutable.
-- =============================================================================
CREATE TABLE IF NOT EXISTS raw_measurements (
    meter_point_id         UUID         NOT NULL REFERENCES meter_points (id),
    timestamp              TIMESTAMPTZ  NOT NULL,
    id                     UUID         NOT NULL DEFAULT gen_random_uuid(),
    total_consumption      NUMERIC(18, 6) NOT NULL,
    grid_share             NUMERIC(18, 6) NOT NULL,
    community_share        NUMERIC(18, 6) NOT NULL,
    grid_share_percent     NUMERIC(7, 6)  NOT NULL CHECK (grid_share_percent BETWEEN 0 AND 1),
    community_share_percent NUMERIC(7, 6) NOT NULL CHECK (community_share_percent BETWEEN 0 AND 1),
    source                 TEXT         NOT NULL,
    ingested_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    created_at             TIMESTAMPTZ  NOT NULL DEFAULT NOW(),

    CONSTRAINT pk_raw_measurements PRIMARY KEY (meter_point_id, timestamp)
);

-- Convert to hypertable — partition by timestamp, 1-week chunks (168 rows/chunk per meter point)
SELECT create_hypertable(
    'raw_measurements',
    'timestamp',
    chunk_time_interval => INTERVAL '1 week',
    if_not_exists       => TRUE
);

-- Primary performance index: per-meter-point time-series lookups
CREATE INDEX IF NOT EXISTS idx_raw_meas_mp_ts
    ON raw_measurements (meter_point_id, timestamp DESC);

-- Index for ingestion monitoring / audit queries
CREATE INDEX IF NOT EXISTS idx_raw_meas_ingested_at
    ON raw_measurements (ingested_at DESC);

COMMENT ON TABLE  raw_measurements IS 'Append-only hourly energy measurement records from EDA. Never updated.';
COMMENT ON COLUMN raw_measurements.timestamp IS 'Start of the measurement period (UTC, hourly granularity).';
COMMENT ON COLUMN raw_measurements.total_consumption IS 'Total energy consumption in kWh for this hour.';
COMMENT ON COLUMN raw_measurements.grid_share IS 'Grid-sourced portion of consumption in kWh.';
COMMENT ON COLUMN raw_measurements.community_share IS 'Energy-community-sourced portion of consumption in kWh.';
COMMENT ON COLUMN raw_measurements.grid_share_percent IS 'Grid share as a fraction of total (0–1).';
COMMENT ON COLUMN raw_measurements.community_share_percent IS 'Community share as a fraction of total (0–1).';
COMMENT ON COLUMN raw_measurements.source IS 'Data source identifier, e.g. ''EDA''.';
COMMENT ON COLUMN raw_measurements.ingested_at IS 'Server-side timestamp when this record was written (immutable).';
COMMENT ON COLUMN raw_measurements.created_at IS 'Logical creation timestamp (immutable). Same as ingested_at for new records.';
