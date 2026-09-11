using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ITAdmin.Persistence.Context;

#nullable disable

namespace ITAdmin.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260910010000_EnforceSinglePackageRenewal")]
public partial class EnforceSinglePackageRenewal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE license_packages
            SET is_active = CASE WHEN status = 'Active' THEN TRUE ELSE FALSE END
            WHERE is_active <> (status = 'Active');
            """);

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT previous_package_id
                    FROM license_packages
                    WHERE previous_package_id IS NOT NULL
                    GROUP BY previous_package_id
                    HAVING COUNT(*) > 1
                ) THEN
                    RAISE EXCEPTION USING
                        MESSAGE = 'Cannot enforce a single renewal per license package because duplicate renewal links exist.',
                        HINT = 'Find duplicate previous_package_id values in license_packages, resolve them explicitly, and run the migration again.';
                END IF;
            END
            $$;
            """);

        // The original EF migration created this index with a quoted, uppercase
        // "IX_" prefix. PostgreSQL identifiers are case-sensitive when quoted,
        // so dropping the lowercase name breaks clean-database migrations.
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS "IX_license_packages_previous_package_id";
            DROP INDEX IF EXISTS ix_license_packages_previous_package_id;
            """);

        migrationBuilder.CreateIndex(
            name: "ix_license_packages_previous_package_id",
            table: "license_packages",
            column: "previous_package_id",
            unique: true,
            filter: "previous_package_id IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS ix_license_packages_previous_package_id;
            DROP INDEX IF EXISTS "IX_license_packages_previous_package_id";
            """);

        migrationBuilder.CreateIndex(
            name: "IX_license_packages_previous_package_id",
            table: "license_packages",
            column: "previous_package_id");
    }
}
