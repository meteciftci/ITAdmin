using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260910020000_ProtectLicenseKeysAndSeedSensitivePermission")]
public sealed class ProtectLicenseKeysAndSeedSensitivePermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "license_key",
            table: "license_packages",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(2000)",
            oldMaxLength: 2000,
            oldNullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "license_key_is_encrypted",
            table: "license_packages",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
        migrationBuilder.Sql("""
            INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
            SELECT gen_random_uuid(), 'LicenseManagement', 'LicenseManagement.ViewSensitiveData', 'View confidential license package data.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
            WHERE NOT EXISTS (
                SELECT 1 FROM portal_permissions WHERE code = 'LicenseManagement.ViewSensitiveData'
            );

            INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
            SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
            FROM portal_roles r
            JOIN portal_permissions p ON p.code = 'LicenseManagement.ViewSensitiveData'
            WHERE r.code = 'Administrator'
              AND NOT EXISTS (
                  SELECT 1 FROM portal_role_permissions rp
                  WHERE rp.portal_role_id = r.id AND rp.portal_permission_id = p.id
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM portal_role_permissions
            WHERE portal_permission_id IN (
                SELECT id FROM portal_permissions
                WHERE code = 'LicenseManagement.ViewSensitiveData'
            );

            DELETE FROM portal_permissions
            WHERE code = 'LicenseManagement.ViewSensitiveData';
            """);

        migrationBuilder.DropColumn(
            name: "license_key_is_encrypted",
            table: "license_packages");

        migrationBuilder.AlterColumn<string>(
            name: "license_key",
            table: "license_packages",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(4000)",
            oldMaxLength: 4000,
            oldNullable: true);
    }
}
