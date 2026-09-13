-- =============================================================================
-- Timescale continuous aggregates and refresh policies
-- Single script for creating all rollups + policy setup.
-- Prerequisite: raw_measurements hypertable exists.
-- =============================================================================

-- Clean existing policies/views so script is re-runnable
DO $$
BEGIN
    PERFORM remove_continuous_aggregate_policy('hourly_rollups', if_exists => true);
    PERFORM remove_continuous_aggregate_policy('daily_rollups', if_exists => true);
    PERFORM remove_continuous_aggregate_policy('monthly_rollups', if_exists => true);
    PERFORM remove_continuous_aggregate_policy('yearly_rollups', if_exists => true);
    PERFORM remove_continuous_aggregate_policy('community_hourly_rollups', if_exists => true);
    PERFORM remove_continuous_aggregate_policy('community_daily_rollups', if_exists => true);
    PERFORM remove_continuous_aggregate_policy('community_monthly_rollups', if_exists => true);
    PERFORM remove_continuous_aggregate_policy('community_yearly_rollups', if_exists => true);
EXCEPTION WHEN undefined_function THEN
    NULL;
END $$;

DROP MATERIALIZED VIEW IF EXISTS hourly_rollups CASCADE;
DROP MATERIALIZED VIEW IF EXISTS daily_rollups CASCADE;
DROP MATERIALIZED VIEW IF EXISTS monthly_rollups CASCADE;
DROP MATERIALIZED VIEW IF EXISTS yearly_rollups CASCADE;
DROP MATERIALIZED VIEW IF EXISTS community_hourly_rollups CASCADE;
DROP MATERIALIZED VIEW IF EXISTS community_daily_rollups CASCADE;
DROP MATERIALIZED VIEW IF EXISTS community_monthly_rollups CASCADE;
DROP MATERIALIZED VIEW IF EXISTS community_yearly_rollups CASCADE;

-- -----------------------------------------------------------------------------
-- Hourly rollups
-- -----------------------------------------------------------------------------
CREATE MATERIALIZED VIEW hourly_rollups
WITH (timescaledb.continuous) AS
SELECT
    meter_id,
    time_bucket('1 hour', timestamp) AS bucket_hour,
    SUM(total_consumption) AS total_consumption,
    SUM(grid_share) AS grid_share,
    SUM(community_share) AS community_share,
    SUM(grid_share) / NULLIF(SUM(total_consumption), 0) AS avg_grid_share_percent,
    SUM(community_share) / NULLIF(SUM(total_consumption), 0) AS avg_community_share_percent,
    COUNT(*) AS measurement_count
FROM raw_measurements
GROUP BY meter_id, time_bucket('1 hour', timestamp)
WITH NO DATA;

CREATE INDEX IF NOT EXISTS idx_hourly_rollups_meter_bucket
    ON hourly_rollups (meter_id, bucket_hour DESC);

-- -----------------------------------------------------------------------------
-- Daily rollups
-- -----------------------------------------------------------------------------
CREATE MATERIALIZED VIEW daily_rollups
WITH (timescaledb.continuous) AS
SELECT
    meter_id,
    time_bucket('1 day', timestamp) AS bucket_day,
    SUM(total_consumption) AS total_consumption,
    SUM(grid_share) AS grid_share,
    SUM(community_share) AS community_share,
    SUM(grid_share) / NULLIF(SUM(total_consumption), 0) AS avg_grid_share_percent,
    SUM(community_share) / NULLIF(SUM(total_consumption), 0) AS avg_community_share_percent,
    COUNT(*) AS measurement_count
FROM raw_measurements
GROUP BY meter_id, time_bucket('1 day', timestamp)
WITH NO DATA;

CREATE INDEX IF NOT EXISTS idx_daily_rollups_meter_bucket
    ON daily_rollups (meter_id, bucket_day DESC);

-- -----------------------------------------------------------------------------
-- Monthly rollups
-- -----------------------------------------------------------------------------
CREATE MATERIALIZED VIEW monthly_rollups
WITH (timescaledb.continuous) AS
SELECT
    meter_id,
    time_bucket('1 month', timestamp) AS bucket_month,
    SUM(total_consumption) AS total_consumption,
    SUM(grid_share) AS grid_share,
    SUM(community_share) AS community_share,
    SUM(grid_share) / NULLIF(SUM(total_consumption), 0) AS avg_grid_share_percent,
    SUM(community_share) / NULLIF(SUM(total_consumption), 0) AS avg_community_share_percent,
    COUNT(*) AS measurement_count
FROM raw_measurements
GROUP BY meter_id, time_bucket('1 month', timestamp)
WITH NO DATA;

CREATE INDEX IF NOT EXISTS idx_monthly_rollups_meter_bucket
    ON monthly_rollups (meter_id, bucket_month DESC);

-- -----------------------------------------------------------------------------
-- Yearly rollups
-- -----------------------------------------------------------------------------
CREATE MATERIALIZED VIEW yearly_rollups
WITH (timescaledb.continuous) AS
SELECT
    meter_id,
    time_bucket('1 year', timestamp) AS bucket_year,
    SUM(total_consumption) AS total_consumption,
    SUM(grid_share) AS grid_share,
    SUM(community_share) AS community_share,
    SUM(grid_share) / NULLIF(SUM(total_consumption), 0) AS avg_grid_share_percent,
    SUM(community_share) / NULLIF(SUM(total_consumption), 0) AS avg_community_share_percent,
    COUNT(*) AS measurement_count
FROM raw_measurements
GROUP BY meter_id, time_bucket('1 year', timestamp)
WITH NO DATA;

CREATE INDEX IF NOT EXISTS idx_yearly_rollups_meter_bucket
    ON yearly_rollups (meter_id, bucket_year DESC);

-- -----------------------------------------------------------------------------
-- Community-wide hourly rollups
-- -----------------------------------------------------------------------------
CREATE MATERIALIZED VIEW community_hourly_rollups
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 hour', timestamp) AS bucket_hour,
    SUM(total_consumption) AS total_consumption,
    SUM(grid_share) AS grid_share,
    SUM(community_share) AS community_share,
    SUM(grid_share) / NULLIF(SUM(total_consumption), 0) AS avg_grid_share_percent,
    SUM(community_share) / NULLIF(SUM(total_consumption), 0) AS avg_community_share_percent,
    COUNT(*) AS measurement_count
FROM raw_measurements
GROUP BY time_bucket('1 hour', timestamp)
WITH NO DATA;

CREATE INDEX IF NOT EXISTS idx_community_hourly_rollups_bucket
    ON community_hourly_rollups (bucket_hour DESC);

-- -----------------------------------------------------------------------------
-- Community-wide daily rollups
-- -----------------------------------------------------------------------------
CREATE MATERIALIZED VIEW community_daily_rollups
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 day', timestamp) AS bucket_day,
    SUM(total_consumption) AS total_consumption,
    SUM(grid_share) AS grid_share,
    SUM(community_share) AS community_share,
    SUM(grid_share) / NULLIF(SUM(total_consumption), 0) AS avg_grid_share_percent,
    SUM(community_share) / NULLIF(SUM(total_consumption), 0) AS avg_community_share_percent,
    COUNT(*) AS measurement_count
FROM raw_measurements
GROUP BY time_bucket('1 day', timestamp)
WITH NO DATA;

CREATE INDEX IF NOT EXISTS idx_community_daily_rollups_bucket
    ON community_daily_rollups (bucket_day DESC);

-- -----------------------------------------------------------------------------
-- Community-wide monthly rollups
-- -----------------------------------------------------------------------------
CREATE MATERIALIZED VIEW community_monthly_rollups
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 month', timestamp) AS bucket_month,
    SUM(total_consumption) AS total_consumption,
    SUM(grid_share) AS grid_share,
    SUM(community_share) AS community_share,
    SUM(grid_share) / NULLIF(SUM(total_consumption), 0) AS avg_grid_share_percent,
    SUM(community_share) / NULLIF(SUM(total_consumption), 0) AS avg_community_share_percent,
    COUNT(*) AS measurement_count
FROM raw_measurements
GROUP BY time_bucket('1 month', timestamp)
WITH NO DATA;

CREATE INDEX IF NOT EXISTS idx_community_monthly_rollups_bucket
    ON community_monthly_rollups (bucket_month DESC);

-- -----------------------------------------------------------------------------
-- Community-wide yearly rollups
-- -----------------------------------------------------------------------------
CREATE MATERIALIZED VIEW community_yearly_rollups
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 year', timestamp) AS bucket_year,
    SUM(total_consumption) AS total_consumption,
    SUM(grid_share) AS grid_share,
    SUM(community_share) AS community_share,
    SUM(grid_share) / NULLIF(SUM(total_consumption), 0) AS avg_grid_share_percent,
    SUM(community_share) / NULLIF(SUM(total_consumption), 0) AS avg_community_share_percent,
    COUNT(*) AS measurement_count
FROM raw_measurements
GROUP BY time_bucket('1 year', timestamp)
WITH NO DATA;

CREATE INDEX IF NOT EXISTS idx_community_yearly_rollups_bucket
    ON community_yearly_rollups (bucket_year DESC);

-- -----------------------------------------------------------------------------
-- Initial backfill (long history)
-- -----------------------------------------------------------------------------
CALL refresh_continuous_aggregate('hourly_rollups', NOW() - INTERVAL '10 years', NOW());
CALL refresh_continuous_aggregate('daily_rollups', NOW() - INTERVAL '10 years', NOW());
CALL refresh_continuous_aggregate('monthly_rollups', NOW() - INTERVAL '10 years', NOW());
CALL refresh_continuous_aggregate('yearly_rollups', NOW() - INTERVAL '10 years', NOW());
CALL refresh_continuous_aggregate('community_hourly_rollups', NOW() - INTERVAL '10 years', NOW());
CALL refresh_continuous_aggregate('community_daily_rollups', NOW() - INTERVAL '10 years', NOW());
CALL refresh_continuous_aggregate('community_monthly_rollups', NOW() - INTERVAL '10 years', NOW());
CALL refresh_continuous_aggregate('community_yearly_rollups', NOW() - INTERVAL '10 years', NOW());

-- -----------------------------------------------------------------------------
-- Ongoing refresh policies: every 5 minutes
-- -----------------------------------------------------------------------------
SELECT add_continuous_aggregate_policy(
    'hourly_rollups',
    start_offset      => INTERVAL '30 days',
    end_offset        => INTERVAL '5 minutes',
    schedule_interval => INTERVAL '5 minutes',
    if_not_exists     => TRUE
);

SELECT add_continuous_aggregate_policy(
    'daily_rollups',
    start_offset      => INTERVAL '60 days',
    end_offset        => INTERVAL '5 minutes',
    schedule_interval => INTERVAL '5 minutes',
    if_not_exists     => TRUE
);

SELECT add_continuous_aggregate_policy(
    'monthly_rollups',
    start_offset      => INTERVAL '2 years',
    end_offset        => INTERVAL '5 minutes',
    schedule_interval => INTERVAL '5 minutes',
    if_not_exists     => TRUE
);

SELECT add_continuous_aggregate_policy(
    'yearly_rollups',
    start_offset      => INTERVAL '10 years',
    end_offset        => INTERVAL '5 minutes',
    schedule_interval => INTERVAL '5 minutes',
    if_not_exists     => TRUE
);

SELECT add_continuous_aggregate_policy(
    'community_hourly_rollups',
    start_offset      => INTERVAL '30 days',
    end_offset        => INTERVAL '5 minutes',
    schedule_interval => INTERVAL '5 minutes',
    if_not_exists     => TRUE
);

SELECT add_continuous_aggregate_policy(
    'community_daily_rollups',
    start_offset      => INTERVAL '60 days',
    end_offset        => INTERVAL '5 minutes',
    schedule_interval => INTERVAL '5 minutes',
    if_not_exists     => TRUE
);

SELECT add_continuous_aggregate_policy(
    'community_monthly_rollups',
    start_offset      => INTERVAL '2 years',
    end_offset        => INTERVAL '5 minutes',
    schedule_interval => INTERVAL '5 minutes',
    if_not_exists     => TRUE
);

SELECT add_continuous_aggregate_policy(
    'community_yearly_rollups',
    start_offset      => INTERVAL '10 years',
    end_offset        => INTERVAL '5 minutes',
    schedule_interval => INTERVAL '5 minutes',
    if_not_exists     => TRUE
);

COMMENT ON MATERIALIZED VIEW hourly_rollups IS 'Continuous aggregate for hourly per-meter metrics.';
COMMENT ON MATERIALIZED VIEW daily_rollups IS 'Continuous aggregate for daily per-meter metrics.';
COMMENT ON MATERIALIZED VIEW monthly_rollups IS 'Continuous aggregate for monthly per-meter metrics.';
COMMENT ON MATERIALIZED VIEW yearly_rollups IS 'Continuous aggregate for yearly per-meter metrics.';
COMMENT ON MATERIALIZED VIEW community_hourly_rollups IS 'Continuous aggregate for hourly community-wide metrics.';
COMMENT ON MATERIALIZED VIEW community_daily_rollups IS 'Continuous aggregate for daily community-wide metrics.';
COMMENT ON MATERIALIZED VIEW community_monthly_rollups IS 'Continuous aggregate for monthly community-wide metrics.';
COMMENT ON MATERIALIZED VIEW community_yearly_rollups IS 'Continuous aggregate for yearly community-wide metrics.';
