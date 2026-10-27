-- =============================================================================
-- Migration: 002_rollup_aggregates.sql
-- Creates TimescaleDB continuous aggregates for daily and monthly rollups.
-- Run after 001_initial_schema.sql.
-- =============================================================================

-- =============================================================================
-- Continuous Aggregate: daily_rollups
-- Aggregates raw measurements to daily totals per meter point.
-- Refreshed automatically by TimescaleDB.
-- =============================================================================
CREATE MATERIALIZED VIEW IF NOT EXISTS daily_rollups
WITH (timescaledb.continuous) AS
SELECT
    community_id,
    meter_id,
    time_bucket('1 day', timestamp) AS day,
    SUM(total_consumption)       AS total_consumption,
    SUM(grid_share)              AS grid_share,
    SUM(community_share)         AS community_share,
    AVG(grid_share_percent)      AS avg_grid_share_percent,
    AVG(community_share_percent) AS avg_community_share_percent,
    COUNT(*)                     AS measurement_count
FROM raw_measurements
GROUP BY community_id, meter_id, time_bucket('1 day', timestamp)
WITH NO DATA;

-- Refresh policy: refresh trailing 3 days, run every hour
SELECT add_continuous_aggregate_policy(
    'daily_rollups',
    start_offset  => INTERVAL '3 days',
    end_offset    => INTERVAL '1 hour',
    schedule_interval => INTERVAL '1 hour',
    if_not_exists => TRUE
);

COMMENT ON MATERIALIZED VIEW daily_rollups IS
    'Daily energy consumption rollup per meter point. Managed by TimescaleDB continuous aggregate.';

-- =============================================================================
-- Continuous Aggregate: monthly_rollups
-- Aggregates daily rollups to monthly totals per meter point.
-- =============================================================================
CREATE MATERIALIZED VIEW IF NOT EXISTS monthly_rollups
WITH (timescaledb.continuous) AS
SELECT
    community_id,
    meter_id,
    time_bucket('1 month', timestamp) AS month,
    SUM(total_consumption)       AS total_consumption,
    SUM(grid_share)              AS grid_share,
    SUM(community_share)         AS community_share,
    AVG(grid_share_percent)      AS avg_grid_share_percent,
    AVG(community_share_percent) AS avg_community_share_percent,
    COUNT(*)                     AS measurement_count
FROM raw_measurements
GROUP BY community_id, meter_id, time_bucket('1 month', timestamp)
WITH NO DATA;

-- Refresh policy: refresh trailing 2 months, run every day
SELECT add_continuous_aggregate_policy(
    'monthly_rollups',
    start_offset  => INTERVAL '2 months',
    end_offset    => INTERVAL '1 day',
    schedule_interval => INTERVAL '1 day',
    if_not_exists => TRUE
);

COMMENT ON MATERIALIZED VIEW monthly_rollups IS
    'Monthly energy consumption rollup per meter point. Managed by TimescaleDB continuous aggregate.';

-- =============================================================================
-- Indexes on continuous aggregates
-- =============================================================================
CREATE INDEX IF NOT EXISTS idx_daily_rollups_mp_day
    ON daily_rollups (community_id, meter_id, day DESC);

CREATE INDEX IF NOT EXISTS idx_monthly_rollups_mp_month
    ON monthly_rollups (community_id, meter_id, month DESC);
