using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EGWNPersistence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlterKpiMeasurementsCommunityScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop old PK on (meter_id, timestamp) and its unique index
            migrationBuilder.DropPrimaryKey(
                name: "PK_kpi_measurements",
                table: "kpi_measurements");

            migrationBuilder.DropIndex(
                name: "IX_kpi_measurements_meter_id_timestamp",
                table: "kpi_measurements");

            // Drop meter_id column
            migrationBuilder.DropColumn(
                name: "meter_id",
                table: "kpi_measurements");

            // Make value columns nullable
            migrationBuilder.AlterColumn<decimal>(
                name: "autarky",
                table: "kpi_measurements",
                type: "numeric(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "own_consumption",
                table: "kpi_measurements",
                type: "numeric(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "community",
                table: "kpi_measurements",
                type: "numeric(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "feed",
                table: "kpi_measurements",
                type: "numeric(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "remaining_demand",
                table: "kpi_measurements",
                type: "numeric(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");

            // New community-scoped primary key
            migrationBuilder.AddPrimaryKey(
                name: "PK_kpi_measurements",
                table: "kpi_measurements",
                columns: new[] { "community_id", "timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_kpi_measurements",
                table: "kpi_measurements");

            migrationBuilder.AddColumn<long>(
                name: "meter_id",
                table: "kpi_measurements",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<decimal>(
                name: "autarky",
                table: "kpi_measurements",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "own_consumption",
                table: "kpi_measurements",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "community",
                table: "kpi_measurements",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "feed",
                table: "kpi_measurements",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "remaining_demand",
                table: "kpi_measurements",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_kpi_measurements",
                table: "kpi_measurements",
                columns: new[] { "meter_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_kpi_measurements_meter_id_timestamp",
                table: "kpi_measurements",
                columns: new[] { "meter_id", "timestamp" },
                unique: true);
        }
    }
}
