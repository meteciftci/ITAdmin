using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdManagementDefaultOuSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "default_computer_ou",
                table: "ad_management_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "default_group_ou",
                table: "ad_management_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "default_user_ou",
                table: "ad_management_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "deleted_objects_enabled",
                table: "ad_management_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "default_computer_ou",
                table: "ad_management_settings");

            migrationBuilder.DropColumn(
                name: "default_group_ou",
                table: "ad_management_settings");

            migrationBuilder.DropColumn(
                name: "default_user_ou",
                table: "ad_management_settings");

            migrationBuilder.DropColumn(
                name: "deleted_objects_enabled",
                table: "ad_management_settings");
        }
    }
}
