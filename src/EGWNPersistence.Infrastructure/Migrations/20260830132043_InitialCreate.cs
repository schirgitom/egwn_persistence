using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EGWNPersistence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kpi_measurements",
                columns: table => new
                {
                    meter_id = table.Column<long>(type: "bigint", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    community_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    autarky = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    own_consumption = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    community = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    feed = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    remaining_demand = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    ingested_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kpi_measurements", x => new { x.meter_id, x.timestamp });
                });

            migrationBuilder.CreateTable(
                name: "meter_points",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    meter_point_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meter_points", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "raw_measurements",
                columns: table => new
                {
                    meter_id = table.Column<long>(type: "bigint", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    total_consumption = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    grid_share = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    community_share = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    grid_share_percent = table.Column<decimal>(type: "numeric(7,6)", nullable: false),
                    community_share_percent = table.Column<decimal>(type: "numeric(7,6)", nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ingested_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raw_measurements", x => new { x.meter_id, x.timestamp });
                });

            migrationBuilder.CreateIndex(
                name: "IX_kpi_measurements_meter_id_timestamp",
                table: "kpi_measurements",
                columns: new[] { "meter_id", "timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kpi_measurements_timestamp",
                table: "kpi_measurements",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_meter_points_meter_point_number",
                table: "meter_points",
                column: "meter_point_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_raw_measurements_meter_id_timestamp",
                table: "raw_measurements",
                columns: new[] { "meter_id", "timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_raw_measurements_timestamp",
                table: "raw_measurements",
                column: "timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kpi_measurements");

            migrationBuilder.DropTable(
                name: "meter_points");

            migrationBuilder.DropTable(
                name: "raw_measurements");
        }
    }
}
