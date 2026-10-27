-- =============================================================================
-- Migration 003: Retention and compression policy for raw_measurements
-- =============================================================================

ALTER TABLE raw_measurements SET (
    timescaledb.compress,
    timescaledb.compress_orderby = 'timestamp DESC',
    timescaledb.compress_segmentby = 'community_id, meter_id'
);

SELECT add_compression_policy(
    'raw_measurements',
    compress_after => INTERVAL '7 days',
    if_not_exists => TRUE
);

SELECT add_retention_policy(
    'raw_measurements',
    drop_after => INTERVAL '2 years',
    if_not_exists => TRUE
);
