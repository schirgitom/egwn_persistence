-- =============================================================================
-- Retention & Compression Policies for raw_measurements
-- Run after 001_initial_schema.sql.
-- Adjust retention_period and compress_after to match your requirements.
-- =============================================================================

-- COMPRESSION POLICY
-- Compresses chunks older than 7 days. Compressed chunks use ~10x less storage.
-- Timescale uses columnar compression internally.
ALTER TABLE raw_measurements SET (
    timescaledb.compress,
    timescaledb.compress_orderby      = 'timestamp DESC',
    timescaledb.compress_segmentby    = 'meter_point_id'
);

SELECT add_compression_policy(
    'raw_measurements',
    compress_after  => INTERVAL '7 days',
    if_not_exists   => TRUE
);

-- RETENTION POLICY
-- Drops chunks older than 2 years.
-- Adjust the interval (e.g. '5 years') for regulatory requirements.
SELECT add_retention_policy(
    'raw_measurements',
    drop_after    => INTERVAL '2 years',
    if_not_exists => TRUE
);
