using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SasPortal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdManagementNotificationSettingsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "notification_settings_json",
                table: "ad_management_settings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "notification_settings_json",
                table: "ad_management_settings");
        }
    }
}
