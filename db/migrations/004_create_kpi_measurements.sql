-- =============================================================================
-- Migration 004: Create KPI measurements table
-- Purpose: Store KPI values per meter and timestamp.
-- =============================================================================

CREATE TABLE IF NOT EXISTS kpi_measurements (
    meter_id          BIGINT      NOT NULL REFERENCES meter_points(id),
    community_id      TEXT        NOT NULL,
    timestamp         TIMESTAMPTZ NOT NULL,
    autarky           NUMERIC(18,6) NOT NULL,
    own_consumption   NUMERIC(18,6) NOT NULL,
    community         NUMERIC(18,6) NOT NULL,
    feed              NUMERIC(18,6) NOT NULL,
    remaining_demand  NUMERIC(18,6) NOT NULL,
    ingested_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT pk_kpi_measurements PRIMARY KEY (meter_id, timestamp)
);

CREATE INDEX IF NOT EXISTS idx_kpi_measurements_meter_ts
    ON kpi_measurements (meter_id, timestamp DESC);

CREATE INDEX IF NOT EXISTS idx_kpi_measurements_timestamp
    ON kpi_measurements (timestamp DESC);

COMMENT ON TABLE kpi_measurements IS 'KPI values for each meter and timestamp.';
COMMENT ON COLUMN kpi_measurements.meter_id IS 'Internal BIGINT FK to meter_points.id';
COMMENT ON COLUMN kpi_measurements.community_id IS 'External community identifier';
COMMENT ON COLUMN kpi_measurements.timestamp IS 'KPI timestamp in UTC';
COMMENT ON COLUMN kpi_measurements.autarky IS 'Autarky value';
COMMENT ON COLUMN kpi_measurements.own_consumption IS 'Own consumption value';
COMMENT ON COLUMN kpi_measurements.community IS 'Community value';
COMMENT ON COLUMN kpi_measurements.feed IS 'Feed value';
COMMENT ON COLUMN kpi_measurements.remaining_demand IS 'Remaining demand value';
COMMENT ON COLUMN kpi_measurements.ingested_at IS 'When the KPI row was inserted';
COMMENT ON COLUMN kpi_measurements.created_at IS 'Original creation timestamp';
COMMENT ON COLUMN kpi_measurements.updated_at IS 'Last update timestamp';
