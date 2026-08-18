using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260818120000_SeedSystemUpdatePermissions")]
public sealed class SeedSystemUpdatePermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
        migrationBuilder.Sql("""
            INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
            SELECT gen_random_uuid(), 'System', value.code, value.description, TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
            FROM (VALUES
                ('System.Updates.View', 'View application installation and update status.'),
                ('System.Updates.Manage', 'Install verified ITAdmin releases from the configured repository.')
            ) AS value(code, description)
            WHERE NOT EXISTS (SELECT 1 FROM portal_permissions p WHERE p.code = value.code);

            INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
            SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
            FROM portal_roles r
            CROSS JOIN portal_permissions p
            WHERE r.code = 'Administrator'
              AND p.code IN ('System.Updates.View', 'System.Updates.Manage')
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
                WHERE code IN ('System.Updates.View', 'System.Updates.Manage')
            );

            DELETE FROM portal_permissions
            WHERE code IN ('System.Updates.View', 'System.Updates.Manage');
            """);
    }
}
