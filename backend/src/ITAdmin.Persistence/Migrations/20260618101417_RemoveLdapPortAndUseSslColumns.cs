using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLdapPortAndUseSslColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "port",
                table: "ldap_settings");

            migrationBuilder.DropColumn(
                name: "use_ssl",
                table: "ldap_settings");

            migrationBuilder.DropColumn(
                name: "ldap_port",
                table: "ad_management_settings");

            migrationBuilder.DropColumn(
                name: "use_ssl",
                table: "ad_management_settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "port",
                table: "ldap_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "use_ssl",
                table: "ldap_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ldap_port",
                table: "ad_management_settings",
                type: "integer",
                nullable: false,
                defaultValue: 636);

            migrationBuilder.AddColumn<bool>(
                name: "use_ssl",
                table: "ad_management_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }
    }
}
