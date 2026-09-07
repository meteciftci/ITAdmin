using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsKindAndTeknomartProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sms_kind",
                table: "notification_templates",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sms_kind",
                table: "notification_outbox",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sms_kind",
                table: "notification_templates");

            migrationBuilder.DropColumn(
                name: "sms_kind",
                table: "notification_outbox");
        }
    }
}
