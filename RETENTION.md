# Data Retention & Aggregation Strategy

## Overview

`EGWNPersistence` stores hourly EDA meter measurements in a TimescaleDB hypertable (`raw_measurements`). This document explains the data lifecycle, compression, retention, and rollup strategy.

---

## Raw Measurements

### Storage (`raw_measurements` hypertable)

| Property | Value |
|---|---|
| Partition key | `timestamp` |
| Chunk interval | 1 week |
| Segment by | `community_id, meter_id` |
| Order by | `timestamp DESC` |
| Correction window | Measurements may be corrected for 21 days; older records are treated as immutable |

### Why TimescaleDB?
- Native time-series partitioning (chunks) avoids table bloat
- Columnar compression achieves ~90% storage reduction on cold data
- Continuous aggregates avoid expensive GROUP BY on raw data
- Built-in retention and compression background jobs

---

## Compression Policy

Raw data is compressed after **7 days**. TimescaleDB converts chunk data from row-based to columnar format, reducing storage by 10–20×.

```sql
ALTER TABLE raw_measurements SET (
    timescaledb.compress,
    timescaledb.compress_orderby   = 'timestamp DESC',
    timescaledb.compress_segmentby = 'community_id, meter_id'
);

SELECT add_compression_policy('raw_measurements', compress_after => INTERVAL '7 days');
```

**Effect:** Queries on compressed chunks are still fast — TimescaleDB decompresses on the fly and can skip entire chunks via chunk exclusion.

---

## Retention Policy

Raw data older than **2 years** is automatically dropped at the chunk level (no row-by-row deletes). Adjust the interval to match your regulatory requirements.

```sql
SELECT add_retention_policy('raw_measurements', drop_after => INTERVAL '2 years');
```

**Important:** Before retention drops raw chunks, ensure rollup aggregates have been fully materialized to preserve historical summaries.

---

## Continuous Aggregates (Rollups)

Per-meter and community-wide rollups are grouped by `community_id`; customer-facing queries must retain that filter.

### Daily Rollups (`daily_rollups`)

- Aggregates per community and meter point per day: SUM of consumption, average percentages
- Refreshed every **1 hour**, covering the trailing **3 days**
- Stored as a TimescaleDB continuous aggregate (materialized view)

### Monthly Rollups (`monthly_rollups`)

- Aggregates per community and meter point per calendar month
- Refreshed every **1 day**, covering the trailing **2 months**

Rollups are **not subject to the 2-year retention** on raw data — they persist indefinitely unless explicitly dropped.

---

## Idempotency

The service guarantees at-least-once delivery from RabbitMQ. Duplicate messages are handled at two levels:

1. **Application layer:** `GetExistingTimestampsAsync` filters already-persisted timestamps before attempting insert.
2. **Database layer:** `PRIMARY KEY (meter_id, community_id, timestamp)` enforces uniqueness for each community's meter readings.

Meter points are globally identified by their meter-point number and can belong to multiple communities through `meter_point_communities`.

---

## Index Strategy

| Table | Index | Purpose |
|---|---|---|
| `meter_points` | `uq_meter_points_number` | Fast global meter-point resolution |
| `meter_point_communities` | `(meter_id, community_id)` | Community membership lookup |
| `raw_measurements` | `pk_raw_measurements (meter_id, community_id, timestamp)` | Unique community-scoped measurement identity |
| `raw_measurements` | community/meter/timestamp index | Community-filtered time-range queries |
| `raw_measurements` | `idx_raw_meas_ingested_at (ingested_at DESC)` | Ingestion monitoring, late-arrival detection |
| `daily_rollups` | `idx_daily_rollups_mp_day` | Dashboard daily queries |
| `monthly_rollups` | `idx_monthly_rollups_mp_month` | Dashboard monthly queries |

---

## Operational Notes

- **Never backfill raw data directly** — use the RabbitMQ message pipeline to preserve idempotency checks.
- **Monitor chunk count:** `SELECT count(*) FROM timescaledb_information.chunks WHERE hypertable_name = 'raw_measurements';`
- **Monitor compression ratio:** `SELECT * FROM chunk_compression_stats('raw_measurements');`
- **Monitor retention:** `SELECT * FROM timescaledb_information.drop_chunks_policies;`
- **Continuous aggregate lag:** `SELECT * FROM timescaledb_information.continuous_aggregate_stats;`
- **Community migration:** apply `db/migrations/006_community_scoped_measurements.sql` (or its EF migration) in a maintenance window, then rerun `db/timescale_continuous_aggregates.sql`. The migration removes old rollups so they cannot return cross-community aggregates.
- **Legacy data:** existing raw measurements are assigned to community `2OWxVjZ6MMjZzyRB`.
- **Platform access:** this service does not manage customer accounts or authorization. The platform must authorize the user and filter every query by their allowed communities/meters.

---

## Configuration Variables

| Variable | Default | Location |
|---|---|---|
| `Database:ConnectionString` | (required) | `appsettings.json` / env |
| `RabbitMQ:QueueName` | `egwn.measurements` | `appsettings.json` / env |
| `RabbitMQ:PrefetchCount` | `10` | `appsettings.json` / env |
| Compression threshold | `7 days` | `db/policies/retention_compression.sql` |
| Retention period | `2 years` | `db/policies/retention_compression.sql` |
| Daily rollup refresh | `1 hour` | `db/migrations/002_rollup_aggregates.sql` |
| Monthly rollup refresh | `1 day` | `db/migrations/002_rollup_aggregates.sql` |
