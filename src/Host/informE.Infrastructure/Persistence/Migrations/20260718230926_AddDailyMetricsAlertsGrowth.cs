using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace informE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyMetricsAlertsGrowth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    message = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alerts", x => x.id);
                    table.ForeignKey(
                        name: "fk_alerts_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_daily_metrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    uptime_seconds = table.Column<int>(type: "integer", nullable: false),
                    peak_cpu_percent = table.Column<float>(type: "real", nullable: false),
                    peak_ram_percent = table.Column<float>(type: "real", nullable: false),
                    peak_disk_percent = table.Column<float>(type: "real", nullable: false),
                    active_users_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_daily_metrics", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_daily_metrics_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "network_growth_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_devices = table.Column<int>(type: "integer", nullable: false),
                    total_groups = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_network_growth_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alerts_device_id_occurred_at",
                table: "alerts",
                columns: new[] { "device_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_device_daily_metrics_device_id_date",
                table: "device_daily_metrics",
                columns: new[] { "device_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_network_growth_snapshots_date",
                table: "network_growth_snapshots",
                column: "date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "device_daily_metrics");

            migrationBuilder.DropTable(
                name: "network_growth_snapshots");
        }
    }
}
