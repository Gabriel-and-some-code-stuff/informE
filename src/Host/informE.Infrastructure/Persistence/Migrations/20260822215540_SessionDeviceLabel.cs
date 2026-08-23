using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace informE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SessionDeviceLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "device_label",
                table: "sessions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "device_label",
                table: "sessions");
        }
    }
}
