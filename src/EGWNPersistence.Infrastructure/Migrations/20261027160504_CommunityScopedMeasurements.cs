using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EGWNPersistence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommunityScopedMeasurements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE chunk RECORD;
                BEGIN
                    FOR chunk IN
                        SELECT format('%I.%I', chunk_schema, chunk_name)::regclass AS chunk_table
                        FROM timescaledb_information.chunks
                        WHERE hypertable_name = 'raw_measurements' AND is_compressed
                    LOOP
                        PERFORM decompress_chunk(chunk.chunk_table, if_compressed => true);
                    END LOOP;
                END $$;
                """);

            migrationBuilder.CreateTable(
                name: "meter_point_communities",
                columns: table => new
                {
                    meter_id = table.Column<long>(type: "bigint", nullable: false),
                    community_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meter_point_communities", x => new { x.meter_id, x.community_id });
                    table.ForeignKey(
                        name: "FK_meter_point_communities_meter_points_meter_id",
                        column: x => x.meter_id,
                        principalTable: "meter_points",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE constraint_name text;
                BEGIN
                    FOR constraint_name IN
                        SELECT conname
                        FROM pg_constraint
                        WHERE conrelid = 'raw_measurements'::regclass AND contype = 'p'
                    LOOP
                        EXECUTE format('ALTER TABLE raw_measurements DROP CONSTRAINT %I', constraint_name);
                    END LOOP;
                END $$;
                DROP INDEX IF EXISTS "IX_raw_measurements_meter_id_timestamp";
                DROP INDEX IF EXISTS "IX_raw_measurements_timestamp";
                DROP INDEX IF EXISTS idx_raw_meas_mp_ts;
                DROP INDEX IF EXISTS idx_raw_measurements_meter_ts;
                """);

            migrationBuilder.AddColumn<string>(
                name: "community_id",
                table: "raw_measurements",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE raw_measurements
                SET community_id = '2OWxVjZ6MMjZzyRB'
                WHERE community_id IS NULL;

                INSERT INTO meter_point_communities (meter_id, community_id, created_at)
                SELECT "Id", '2OWxVjZ6MMjZzyRB', NOW()
                FROM meter_points
                ON CONFLICT (meter_id, community_id) DO NOTHING;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "community_id",
                table: "raw_measurements",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_raw_measurements",
                table: "raw_measurements",
                columns: new[] { "meter_id", "community_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_raw_measurements_community_id_meter_id_timestamp",
                table: "raw_measurements",
                columns: new[] { "community_id", "meter_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_meter_point_communities_community_id",
                table: "meter_point_communities",
                column: "community_id");

            migrationBuilder.Sql(
                """
                ALTER TABLE raw_measurements SET (
                    timescaledb.compress,
                    timescaledb.compress_orderby = 'timestamp DESC',
                    timescaledb.compress_segmentby = 'community_id, meter_id'
                );

                DROP MATERIALIZED VIEW IF EXISTS hourly_rollups CASCADE;
                DROP MATERIALIZED VIEW IF EXISTS daily_rollups CASCADE;
                DROP MATERIALIZED VIEW IF EXISTS monthly_rollups CASCADE;
                DROP MATERIALIZED VIEW IF EXISTS yearly_rollups CASCADE;
                DROP MATERIALIZED VIEW IF EXISTS community_hourly_rollups CASCADE;
                DROP MATERIALIZED VIEW IF EXISTS community_daily_rollups CASCADE;
                DROP MATERIALIZED VIEW IF EXISTS community_monthly_rollups CASCADE;
                DROP MATERIALIZED VIEW IF EXISTS community_yearly_rollups CASCADE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE chunk RECORD;
                BEGIN
                    FOR chunk IN
                        SELECT format('%I.%I', chunk_schema, chunk_name)::regclass AS chunk_table
                        FROM timescaledb_information.chunks
                        WHERE hypertable_name = 'raw_measurements' AND is_compressed
                    LOOP
                        PERFORM decompress_chunk(chunk.chunk_table, if_compressed => true);
                    END LOOP;
                END $$;
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM raw_measurements
                        GROUP BY meter_id, timestamp
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot revert community-scoped measurements while a meter/timestamp exists in multiple communities.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "meter_point_communities");

            migrationBuilder.DropPrimaryKey(
                name: "PK_raw_measurements",
                table: "raw_measurements");

            migrationBuilder.DropIndex(
                name: "IX_raw_measurements_community_id_meter_id_timestamp",
                table: "raw_measurements");

            migrationBuilder.DropColumn(
                name: "community_id",
                table: "raw_measurements");

            migrationBuilder.AddPrimaryKey(
                name: "PK_raw_measurements",
                table: "raw_measurements",
                columns: new[] { "meter_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_raw_measurements_meter_id_timestamp",
                table: "raw_measurements",
                columns: new[] { "meter_id", "timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_raw_measurements_timestamp",
                table: "raw_measurements",
                column: "timestamp");

            migrationBuilder.Sql(
                """
                ALTER TABLE raw_measurements SET (
                    timescaledb.compress,
                    timescaledb.compress_orderby = 'timestamp DESC',
                    timescaledb.compress_segmentby = 'meter_id'
                );

                DROP MATERIALIZED VIEW IF EXISTS hourly_rollups CASCADE;
                DROP MATERIALIZED VIEW IF EXISTS daily_rollups CASCADE;
                DROP MATERIALIZED VIEW IF EXISTS monthly_rollups CASCADE;
                DROP MATERIALIZED VIEW IF EXISTS yearly_rollups CASCADE;
                DROP MATERIALIZED VIEW IF EXISTS community_hourly_rollups CASCADE;
                DROP MATERIALIZED VIEW IF EXISTS community_daily_rollups CASCADE;
                DROP MATERIALIZED VIEW IF EXISTS community_monthly_rollups CASCADE;
                DROP MATERIALIZED VIEW IF EXISTS community_yearly_rollups CASCADE;
                """);
        }
    }
}
