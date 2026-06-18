using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdManagementComputersDeletePermission : Migration
    {
        private const string PermComputersDelete = "AdManagement.Computers.Delete";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            migrationBuilder.Sql($"""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'AdManagement', '{PermComputersDelete}', 'Delete AD management directory computer accounts.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (
                    SELECT 1 FROM portal_permissions WHERE code = '{PermComputersDelete}'
                );
                """);

            migrationBuilder.Sql($"""
                INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
                SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
                FROM portal_roles r
                JOIN portal_permissions p ON p.code = '{PermComputersDelete}'
                WHERE r.code = 'Administrator'
                  AND NOT EXISTS (
                      SELECT 1 FROM portal_role_permissions rp
                      WHERE rp.portal_role_id = r.id AND rp.portal_permission_id = p.id
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DELETE FROM portal_role_permissions
                WHERE portal_permission_id IN (
                    SELECT id FROM portal_permissions WHERE code = '{PermComputersDelete}'
                );
                """);

            migrationBuilder.Sql($"""
                DELETE FROM portal_permissions
                WHERE code = '{PermComputersDelete}';
                """);
        }
    }
}
