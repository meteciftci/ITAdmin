using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SasPortal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectoryIdentityToPortalUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "directory_object_id",
                table: "portal_users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "directory_source",
                table: "portal_users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "national_id_encrypted",
                table: "portal_users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "national_id_masked",
                table: "portal_users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_portal_users_directory_object_id",
                table: "portal_users",
                column: "directory_object_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_portal_users_directory_object_id",
                table: "portal_users");

            migrationBuilder.DropColumn(
                name: "directory_object_id",
                table: "portal_users");

            migrationBuilder.DropColumn(
                name: "directory_source",
                table: "portal_users");

            migrationBuilder.DropColumn(
                name: "national_id_encrypted",
                table: "portal_users");

            migrationBuilder.DropColumn(
                name: "national_id_masked",
                table: "portal_users");
        }
    }
}
