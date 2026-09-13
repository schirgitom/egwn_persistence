# Plan

## Status
- Clean Architecture solution scaffolded and implemented for Domain, Application, Infrastructure, and Worker.
- EF Core is used for TimescaleDB persistence.
- `meter_points` stores business metadata with a BIGINT internal ID.
- `raw_measurements` is append-only and keyed by `(meter_id, timestamp)`.
- Serilog config is loaded from Consul with local fallback.
- RabbitMQ ingestion, validation, health checks, and repository/unit-of-work abstractions are in place.
- Tests pass (33/33).

## Current design decisions
- Raw measurement rows remain keyed by `(meter_id, timestamp)`, but they are still editable for a configurable correction window after the measurement timestamp.
- Duplicate events for the same meter/timestamp are treated as idempotent and ignored when the values are equal.
- Conflicting values for the same meter/timestamp are treated as corrections when they arrive within the configured window; outside that window, they are rejected or routed to a DLQ for inspection.
- The default window is 21 days and can be overridden via the `MeasurementCorrection:CorrectionWindowDays` configuration value.
- The RabbitMQ payload now matches the EDA JSON format with `meterPointNumber`, `communityId`, `createdAtUtc`, and `readings` arrays.
- A dedicated `kpi_measurements` table and `KpiMeasurement` entity were added for KPI time-series storage.

## Next steps
- Keep EF model and SQL migration files aligned.
- Validate against a live TimescaleDB instance.
- Add or refine real Consul/KV config values for deployment environments.
- Optionally generate and version EF migrations for operational database evolution.
