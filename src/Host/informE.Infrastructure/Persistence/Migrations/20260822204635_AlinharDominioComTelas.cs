using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace informE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlinharDominioComTelas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "source_script",
                table: "tasks",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            // defaultValue escrito à mão: o EF gerava "" nas colunas de enum, que
            // não é valor válido de MachineActionKind/ScriptKind e estouraria na
            // leitura de qualquer linha pré-existente.
            // Tasks anteriores ao catálogo não têm ação equivalente — LimpezaDeDisco
            // é só um valor legível para não deixar a coluna inconsistente.
            migrationBuilder.AddColumn<string>(
                name: "action",
                table: "tasks",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "LimpezaDeDisco");

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "tasks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PowerShell");

            migrationBuilder.AlterColumn<string>(
                name: "output_log",
                table: "task_execution_logs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "duration_ms",
                table: "task_execution_logs",
                type: "integer",
                nullable: true);

            // NÃO é mudança desta feature: Software.DetectedAt foi adicionado pelo
            // time e nunca migrado — o banco estava fora de sincronia com o modelo.
            // defaultValueSql "now()" em vez do DateTimeOffset.MinValue que o EF
            // gerava, que marcaria todo software como detectado no ano 1.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "detected_at",
                table: "softwares",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "mother_board",
                table: "info_devices",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "groups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            // Mesmo motivo: "" não é HealthStatus nem DeviceRole válido.
            // Erro/Aluno são exatamente os defaults da entidade Device.
            migrationBuilder.AddColumn<string>(
                name: "health",
                table: "devices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Erro");

            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "devices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Aluno");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_active",
                table: "users");

            migrationBuilder.DropColumn(
                name: "action",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "duration_ms",
                table: "task_execution_logs");

            migrationBuilder.DropColumn(
                name: "detected_at",
                table: "softwares");

            migrationBuilder.DropColumn(
                name: "mother_board",
                table: "info_devices");

            migrationBuilder.DropColumn(
                name: "health",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "role",
                table: "devices");

            migrationBuilder.AlterColumn<string>(
                name: "source_script",
                table: "tasks",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "output_log",
                table: "task_execution_logs",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "groups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
