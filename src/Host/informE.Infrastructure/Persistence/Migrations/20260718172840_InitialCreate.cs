using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace informE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "enrollment_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    redeemed_by_device_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enrollment_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "softwares",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    version = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_softwares", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    source_script = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tasks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    username = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    email = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    hostname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_ip = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    mac_address = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    os = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    os_user = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    agent_key_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    key_rotated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_devices", x => x.id);
                    table.ForeignKey(
                        name: "fk_devices_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ip_address = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    refresh_token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "devices_softwares",
                columns: table => new
                {
                    devices_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installed_softwares_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_devices_softwares", x => new { x.devices_id, x.installed_softwares_id });
                    table.ForeignKey(
                        name: "fk_devices_softwares_devices_devices_id",
                        column: x => x.devices_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_devices_softwares_softwares_installed_softwares_id",
                        column: x => x.installed_softwares_id,
                        principalTable: "softwares",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "devices_tasks",
                columns: table => new
                {
                    target_devices_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tasks_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_devices_tasks", x => new { x.target_devices_id, x.tasks_id });
                    table.ForeignKey(
                        name: "fk_devices_tasks_devices_target_devices_id",
                        column: x => x.target_devices_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_devices_tasks_tasks_tasks_id",
                        column: x => x.tasks_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "info_devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    cpu = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    gpu = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ram_gb = table.Column<int>(type: "integer", nullable: false),
                    ram_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    storage_gb = table.Column<int>(type: "integer", nullable: false),
                    storage_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    bios = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_info_devices", x => x.id);
                    table.ForeignKey(
                        name: "fk_info_devices_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_execution_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    action_type = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    output_log = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    machine_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_execution_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_execution_logs_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_execution_logs_machine_tasks_machine_task_id",
                        column: x => x.machine_task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_devices_group_id",
                table: "devices",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_devices_hostname",
                table: "devices",
                column: "hostname",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_devices_mac_address",
                table: "devices",
                column: "mac_address",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_devices_softwares_installed_softwares_id",
                table: "devices_softwares",
                column: "installed_softwares_id");

            migrationBuilder.CreateIndex(
                name: "ix_devices_tasks_tasks_id",
                table: "devices_tasks",
                column: "tasks_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollment_tokens_token",
                table: "enrollment_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_groups_name",
                table: "groups",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_info_devices_device_id",
                table: "info_devices",
                column: "device_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sessions_user_id_is_active",
                table: "sessions",
                columns: new[] { "user_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_softwares_name",
                table: "softwares",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_execution_logs_device_id",
                table: "task_execution_logs",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_execution_logs_machine_task_id",
                table: "task_execution_logs",
                column: "machine_task_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "devices_softwares");

            migrationBuilder.DropTable(
                name: "devices_tasks");

            migrationBuilder.DropTable(
                name: "enrollment_tokens");

            migrationBuilder.DropTable(
                name: "info_devices");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "task_execution_logs");

            migrationBuilder.DropTable(
                name: "softwares");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "groups");
        }
    }
}
