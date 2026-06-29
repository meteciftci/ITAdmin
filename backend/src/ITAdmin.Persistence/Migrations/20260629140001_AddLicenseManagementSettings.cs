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
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS license_management_settings (
                    id uuid NOT NULL,
                    default_currency character varying(10) NOT NULL DEFAULT 'TRY',
                    default_vat_included boolean NOT NULL DEFAULT FALSE,
                    default_renewal_reminder_days integer NOT NULL DEFAULT 60,
                    default_renewal_recipients character varying(4000),
                    default_renewal_cc_recipients character varying(4000),
                    notes character varying(4000),
                    created_at timestamp with time zone NOT NULL,
                    created_by character varying(200),
                    updated_at timestamp with time zone,
                    updated_by character varying(200),
                    CONSTRAINT "PK_license_management_settings" PRIMARY KEY (id)
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "license_management_settings");
        }
    }
}
