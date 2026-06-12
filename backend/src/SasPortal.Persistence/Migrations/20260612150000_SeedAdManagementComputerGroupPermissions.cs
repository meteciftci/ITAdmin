using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SasPortal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdManagementComputerGroupPermissions : Migration
    {
        private const string PermComputersGroupsView = "AdManagement.Computers.Groups.View";
        private const string PermComputersGroupsAdd = "AdManagement.Computers.Groups.Add";
        private const string PermComputersGroupsRemove = "AdManagement.Computers.Groups.Remove";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            migrationBuilder.Sql($"""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'AdManagement', '{PermComputersGroupsView}', 'View AD computer direct group memberships.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (
                    SELECT 1 FROM portal_permissions WHERE code = '{PermComputersGroupsView}'
                );
                """);

            migrationBuilder.Sql($"""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'AdManagement', '{PermComputersGroupsAdd}', 'Add AD computers to groups.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (
                    SELECT 1 FROM portal_permissions WHERE code = '{PermComputersGroupsAdd}'
                );
                """);

            migrationBuilder.Sql($"""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'AdManagement', '{PermComputersGroupsRemove}', 'Remove AD computers from groups.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (
                    SELECT 1 FROM portal_permissions WHERE code = '{PermComputersGroupsRemove}'
                );
                """);

            migrationBuilder.Sql($"""
                INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
                SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
                FROM portal_roles r
                JOIN portal_permissions p ON p.code IN ('{PermComputersGroupsView}', '{PermComputersGroupsAdd}', '{PermComputersGroupsRemove}')
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
                    SELECT id FROM portal_permissions WHERE code IN ('{PermComputersGroupsView}', '{PermComputersGroupsAdd}', '{PermComputersGroupsRemove}')
                );
                """);

            migrationBuilder.Sql($"""
                DELETE FROM portal_permissions
                WHERE code IN ('{PermComputersGroupsView}', '{PermComputersGroupsAdd}', '{PermComputersGroupsRemove}');
                """);
        }
    }
}
