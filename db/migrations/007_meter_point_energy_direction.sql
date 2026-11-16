-- Optional direction supplied by the measurement producer.
-- For installations using SQL migrations instead of EF Core migrations; do not apply both.
ALTER TABLE meter_points
    ADD COLUMN energy_direction VARCHAR(20);

ALTER TABLE meter_points
    ADD CONSTRAINT ck_meter_points_energy_direction
        CHECK (energy_direction IN ('consumption', 'generation'));
