using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace informE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MetricasCorrentesNoDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "cpu_percent",
                table: "devices",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "disk_percent",
                table: "devices",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "ram_percent",
                table: "devices",
                type: "real",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cpu_percent",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "disk_percent",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "ram_percent",
                table: "devices");
        }
    }
}
