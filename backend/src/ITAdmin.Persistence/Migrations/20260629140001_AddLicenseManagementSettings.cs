using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseManagementSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "license_management_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "TRY"),
                    default_vat_included = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    default_renewal_reminder_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    default_renewal_recipients = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    default_renewal_cc_recipients = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_management_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "license_management_settings");
        }
    }
}
