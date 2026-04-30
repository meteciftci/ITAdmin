using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SasPortal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLdapBindUserFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "bind_dn",
                table: "ldap_settings",
                newName: "bind_user_name");

            migrationBuilder.AlterColumn<string>(
                name: "bind_user_name",
                table: "ldap_settings",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "bind_user_domain",
                table: "ldap_settings",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bind_user_domain",
                table: "ldap_settings");

            migrationBuilder.AlterColumn<string>(
                name: "bind_user_name",
                table: "ldap_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldMaxLength: 250);

            migrationBuilder.RenameColumn(
                name: "bind_user_name",
                table: "ldap_settings",
                newName: "bind_dn");
        }
    }
}
