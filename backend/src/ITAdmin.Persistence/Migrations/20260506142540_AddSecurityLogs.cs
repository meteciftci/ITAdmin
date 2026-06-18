using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_security_logs_user_name",
                table: "security_logs");

            migrationBuilder.DropColumn(
                name: "is_success",
                table: "security_logs");

            migrationBuilder.DropColumn(
                name: "message",
                table: "security_logs");

            migrationBuilder.RenameColumn(
                name: "portal_user_id",
                table: "security_logs",
                newName: "user_id");

            migrationBuilder.AlterColumn<string>(
                name: "user_agent",
                table: "security_logs",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ip_address",
                table: "security_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "event_type",
                table: "security_logs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "security_logs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "severity",
                table: "security_logs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_security_logs_event_type",
                table: "security_logs",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "IX_security_logs_severity",
                table: "security_logs",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "IX_security_logs_user_id",
                table: "security_logs",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_security_logs_event_type",
                table: "security_logs");

            migrationBuilder.DropIndex(
                name: "IX_security_logs_severity",
                table: "security_logs");

            migrationBuilder.DropIndex(
                name: "IX_security_logs_user_id",
                table: "security_logs");

            migrationBuilder.DropColumn(
                name: "description",
                table: "security_logs");

            migrationBuilder.DropColumn(
                name: "severity",
                table: "security_logs");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "security_logs",
                newName: "portal_user_id");

            migrationBuilder.AlterColumn<string>(
                name: "user_agent",
                table: "security_logs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ip_address",
                table: "security_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "event_type",
                table: "security_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AddColumn<bool>(
                name: "is_success",
                table: "security_logs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "message",
                table: "security_logs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_security_logs_user_name",
                table: "security_logs",
                column: "user_name");
        }
    }
}
