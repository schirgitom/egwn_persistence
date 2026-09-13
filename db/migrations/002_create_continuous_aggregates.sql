-- =============================================================================
-- Migration 002: Continuous aggregates for Hour, Day, Month, Year
-- Applies to raw_measurements hypertable.
-- =============================================================================

-- ----------------------------------------------------------------------------
-- Hourly rollups
-- ----------------------------------------------------------------------------
CREATE MATERIALIZED VIEW IF NOT EXISTS hourly_rollups
WITH (timescaledb.continuous) AS
SELECT
    meter_id,
    time_bucket('1 hour', timestamp) AS bucket_hour,
    SUM(total_consumption) AS total_consumption,
    SUM(grid_share) AS grid_share,
    SUM(community_share) AS community_share,
    AVG(grid_share_percent) AS avg_grid_share_percent,
    AVG(community_share_percent) AS avg_community_share_percent,
    COUNT(*) AS measurement_count
FROM raw_measurements
GROUP BY meter_id, time_bucket('1 hour', timestamp)
WITH NO DATA;

SELECT add_continuous_aggregate_policy(
    'hourly_rollups',
    start_offset => INTERVAL '30 days',
    end_offset => INTERVAL '1 hour',
    schedule_interval => INTERVAL '1 hour',
    if_not_exists => TRUE
);

CREATE INDEX IF NOT EXISTS idx_hourly_rollups_meter_bucket
    ON hourly_rollups (meter_id, bucket_hour DESC);

-- ----------------------------------------------------------------------------
-- Daily rollups
-- ----------------------------------------------------------------------------
CREATE MATERIALIZED VIEW IF NOT EXISTS daily_rollups
WITH (timescaledb.continuous) AS
SELECT
    meter_id,
    time_bucket('1 day', timestamp) AS bucket_day,
    SUM(total_consumption) AS total_consumption,
    SUM(grid_share) AS grid_share,
    SUM(community_share) AS community_share,
    AVG(grid_share_percent) AS avg_grid_share_percent,
    AVG(community_share_percent) AS avg_community_share_percent,
    COUNT(*) AS measurement_count
FROM raw_measurements
GROUP BY meter_id, time_bucket('1 day', timestamp)
WITH NO DATA;

SELECT add_continuous_aggregate_policy(
    'daily_rollups',
    start_offset => INTERVAL '90 days',
    end_offset => INTERVAL '1 day',
    schedule_interval => INTERVAL '1 day',
    if_not_exists => TRUE
);

CREATE INDEX IF NOT EXISTS idx_daily_rollups_meter_bucket
    ON daily_rollups (meter_id, bucket_day DESC);

-- ----------------------------------------------------------------------------
-- Monthly rollups
-- ----------------------------------------------------------------------------
CREATE MATERIALIZED VIEW IF NOT EXISTS monthly_rollups
WITH (timescaledb.continuous) AS
SELECT
    meter_id,
    time_bucket('1 month', timestamp) AS bucket_month,
    SUM(total_consumption) AS total_consumption,
    SUM(grid_share) AS grid_share,
    SUM(community_share) AS community_share,
    AVG(grid_share_percent) AS avg_grid_share_percent,
    AVG(community_share_percent) AS avg_community_share_percent,
    COUNT(*) AS measurement_count
FROM raw_measurements
GROUP BY meter_id, time_bucket('1 month', timestamp)
WITH NO DATA;

SELECT add_continuous_aggregate_policy(
    'monthly_rollups',
    start_offset => INTERVAL '12 months',
    end_offset => INTERVAL '1 month',
    schedule_interval => INTERVAL '1 day',
    if_not_exists => TRUE
);

CREATE INDEX IF NOT EXISTS idx_monthly_rollups_meter_bucket
    ON monthly_rollups (meter_id, bucket_month DESC);

-- ----------------------------------------------------------------------------
-- Yearly rollups
-- ----------------------------------------------------------------------------
CREATE MATERIALIZED VIEW IF NOT EXISTS yearly_rollups
WITH (timescaledb.continuous) AS
SELECT
    meter_id,
    time_bucket('1 year', timestamp) AS bucket_year,
    SUM(total_consumption) AS total_consumption,
    SUM(grid_share) AS grid_share,
    SUM(community_share) AS community_share,
    AVG(grid_share_percent) AS avg_grid_share_percent,
    AVG(community_share_percent) AS avg_community_share_percent,
    COUNT(*) AS measurement_count
FROM raw_measurements
GROUP BY meter_id, time_bucket('1 year', timestamp)
WITH NO DATA;

SELECT add_continuous_aggregate_policy(
    'yearly_rollups',
    start_offset => INTERVAL '10 years',
    end_offset => INTERVAL '1 month',
    schedule_interval => INTERVAL '1 month',
    if_not_exists => TRUE
);

CREATE INDEX IF NOT EXISTS idx_yearly_rollups_meter_bucket
    ON yearly_rollups (meter_id, bucket_year DESC);

COMMENT ON MATERIALIZED VIEW hourly_rollups IS 'Continuous aggregate for hourly per-meter metrics.';
COMMENT ON MATERIALIZED VIEW daily_rollups IS 'Continuous aggregate for daily per-meter metrics.';
COMMENT ON MATERIALIZED VIEW monthly_rollups IS 'Continuous aggregate for monthly per-meter metrics.';
COMMENT ON MATERIALIZED VIEW yearly_rollups IS 'Continuous aggregate for yearly per-meter metrics.';
