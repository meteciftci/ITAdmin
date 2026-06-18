using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdManagementOrganizationalUnitsPermissions : Migration
    {
        private const string PermOrganizationalUnitsView = "AdManagement.OrganizationalUnits.View";
        private const string PermOrganizationalUnitsCreate = "AdManagement.OrganizationalUnits.Create";
        private const string PermOrganizationalUnitsUpdate = "AdManagement.OrganizationalUnits.Update";
        private const string PermOrganizationalUnitsMove = "AdManagement.OrganizationalUnits.Move";
        private const string PermOrganizationalUnitsDelete = "AdManagement.OrganizationalUnits.Delete";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            migrationBuilder.Sql($"""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'AdManagement', '{PermOrganizationalUnitsView}', 'View AD management organizational units.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (
                    SELECT 1 FROM portal_permissions WHERE code = '{PermOrganizationalUnitsView}'
                );
                """);

            migrationBuilder.Sql($"""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'AdManagement', '{PermOrganizationalUnitsCreate}', 'Create AD management organizational units.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (
                    SELECT 1 FROM portal_permissions WHERE code = '{PermOrganizationalUnitsCreate}'
                );
                """);

            migrationBuilder.Sql($"""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'AdManagement', '{PermOrganizationalUnitsUpdate}', 'Rename AD management organizational units.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (
                    SELECT 1 FROM portal_permissions WHERE code = '{PermOrganizationalUnitsUpdate}'
                );
                """);

            migrationBuilder.Sql($"""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'AdManagement', '{PermOrganizationalUnitsMove}', 'Move AD management organizational units.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (
                    SELECT 1 FROM portal_permissions WHERE code = '{PermOrganizationalUnitsMove}'
                );
                """);

            migrationBuilder.Sql($"""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'AdManagement', '{PermOrganizationalUnitsDelete}', 'Delete AD management organizational units.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (
                    SELECT 1 FROM portal_permissions WHERE code = '{PermOrganizationalUnitsDelete}'
                );
                """);

            migrationBuilder.Sql($"""
                INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
                SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
                FROM portal_roles r
                JOIN portal_permissions p ON p.code IN (
                    '{PermOrganizationalUnitsView}',
                    '{PermOrganizationalUnitsCreate}',
                    '{PermOrganizationalUnitsUpdate}',
                    '{PermOrganizationalUnitsMove}',
                    '{PermOrganizationalUnitsDelete}'
                )
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
                    SELECT id FROM portal_permissions WHERE code IN (
                        '{PermOrganizationalUnitsView}',
                        '{PermOrganizationalUnitsCreate}',
                        '{PermOrganizationalUnitsUpdate}',
                        '{PermOrganizationalUnitsMove}',
                        '{PermOrganizationalUnitsDelete}'
                    )
                );
                """);

            migrationBuilder.Sql($"""
                DELETE FROM portal_permissions
                WHERE code IN (
                    '{PermOrganizationalUnitsView}',
                    '{PermOrganizationalUnitsCreate}',
                    '{PermOrganizationalUnitsUpdate}',
                    '{PermOrganizationalUnitsMove}',
                    '{PermOrganizationalUnitsDelete}'
                );
                """);
        }
    }
}
