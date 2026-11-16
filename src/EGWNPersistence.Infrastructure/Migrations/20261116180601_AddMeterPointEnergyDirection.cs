using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EGWNPersistence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMeterPointEnergyDirection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "energy_direction",
                table: "meter_points",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_meter_points_energy_direction",
                table: "meter_points",
                sql: "energy_direction IN ('consumption', 'generation')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_meter_points_energy_direction",
                table: "meter_points");

            migrationBuilder.DropColumn(
                name: "energy_direction",
                table: "meter_points");
        }
    }
}
