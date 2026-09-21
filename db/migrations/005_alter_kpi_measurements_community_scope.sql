-- =============================================================================
-- Migration 005: Community-scope KPI measurements
-- Purpose: KPIs are now delivered per community (no meter_id) and individual
--          value fields may be NULL. Change primary key to (community_id, timestamp)
--          and drop the meter_id column plus its foreign key.
-- =============================================================================

BEGIN;

-- Drop dependent indexes/constraints first
ALTER TABLE kpi_measurements DROP CONSTRAINT IF EXISTS pk_kpi_measurements;
ALTER TABLE kpi_measurements DROP CONSTRAINT IF EXISTS kpi_measurements_meter_id_fkey;

DROP INDEX IF EXISTS idx_kpi_measurements_meter_ts;
DROP INDEX IF EXISTS idx_kpi_measurements_timestamp;
-- EF Core auto-generated index name from previous model
DROP INDEX IF EXISTS "IX_kpi_measurements_meter_id_timestamp";
DROP INDEX IF EXISTS "IX_kpi_measurements_timestamp";

-- Drop meter_id column
ALTER TABLE kpi_measurements DROP COLUMN IF EXISTS meter_id;

-- Make value columns nullable
ALTER TABLE kpi_measurements ALTER COLUMN autarky           DROP NOT NULL;
ALTER TABLE kpi_measurements ALTER COLUMN own_consumption   DROP NOT NULL;
ALTER TABLE kpi_measurements ALTER COLUMN community         DROP NOT NULL;
ALTER TABLE kpi_measurements ALTER COLUMN feed              DROP NOT NULL;
ALTER TABLE kpi_measurements ALTER COLUMN remaining_demand  DROP NOT NULL;

-- Establish new community-scoped primary key
ALTER TABLE kpi_measurements
    ADD CONSTRAINT pk_kpi_measurements PRIMARY KEY (community_id, timestamp);

-- Recreate indexes
CREATE INDEX IF NOT EXISTS idx_kpi_measurements_community_ts
    ON kpi_measurements (community_id, timestamp DESC);

CREATE INDEX IF NOT EXISTS idx_kpi_measurements_timestamp
    ON kpi_measurements (timestamp DESC);

COMMENT ON TABLE kpi_measurements IS 'Community-scoped KPI values per timestamp.';
COMMENT ON COLUMN kpi_measurements.community_id IS 'External community identifier (PK part)';
COMMENT ON COLUMN kpi_measurements.timestamp IS 'KPI timestamp in UTC (PK part)';
COMMENT ON COLUMN kpi_measurements.autarky IS 'Autarky value (nullable)';
COMMENT ON COLUMN kpi_measurements.own_consumption IS 'Own consumption value (nullable)';
COMMENT ON COLUMN kpi_measurements.community IS 'Community value (nullable)';
COMMENT ON COLUMN kpi_measurements.feed IS 'Feed value (nullable)';
COMMENT ON COLUMN kpi_measurements.remaining_demand IS 'Remaining demand value (nullable)';

COMMIT;
