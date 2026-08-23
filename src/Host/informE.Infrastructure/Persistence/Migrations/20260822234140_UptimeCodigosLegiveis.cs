using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace informE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UptimeCodigosLegiveis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "task_code_seq",
                startValue: 1000L);

            migrationBuilder.CreateSequence<int>(
                name: "user_code_seq");

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'USR-' || lpad(nextval('user_code_seq')::text, 4, '0')");

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "tasks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'EX-' || nextval('task_code_seq')");

            migrationBuilder.AddColumn<int>(
                name: "uptime_seconds",
                table: "devices",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_code",
                table: "users",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tasks_code",
                table: "tasks",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_code",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_tasks_code",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "code",
                table: "users");

            migrationBuilder.DropColumn(
                name: "code",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "uptime_seconds",
                table: "devices");

            migrationBuilder.DropSequence(
                name: "task_code_seq");

            migrationBuilder.DropSequence(
                name: "user_code_seq");
        }
    }
}
