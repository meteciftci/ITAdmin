using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SasPortal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdManagementPermissions : Migration
    {
        private const string PermAdManagementSettingsView = "AdManagement.Settings.View";
        private const string PermAdManagementSettingsUpdate = "AdManagement.Settings.Update";
        private const string PermAdOperationLogsView = "AdOperationLogs.View";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            UpsertPermission(
                migrationBuilder,
                module: "AdManagement",
                code: PermAdManagementSettingsView,
                description: "View AD management settings.");

            UpsertPermission(
                migrationBuilder,
                module: "AdManagement",
                code: PermAdManagementSettingsUpdate,
                description: "Update AD management settings.");

            UpsertPermission(
                migrationBuilder,
                module: "AdOperationLogs",
                code: PermAdOperationLogsView,
                description: "View AD operation logs.");

            GrantToAdministratorRole(migrationBuilder, PermAdManagementSettingsView);
            GrantToAdministratorRole(migrationBuilder, PermAdManagementSettingsUpdate);
            GrantToAdministratorRole(migrationBuilder, PermAdOperationLogsView);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove role grants first to satisfy FK restrictions, then permissions themselves.
            migrationBuilder.Sql($@"
DELETE FROM portal_role_permissions
WHERE portal_permission_id IN (
    SELECT id FROM portal_permissions
    WHERE code IN ('{PermAdManagementSettingsView}', '{PermAdManagementSettingsUpdate}', '{PermAdOperationLogsView}')
);
");

            migrationBuilder.Sql($@"
DELETE FROM portal_permissions
WHERE code IN ('{PermAdManagementSettingsView}', '{PermAdManagementSettingsUpdate}', '{PermAdOperationLogsView}');
");
        }

        private static void UpsertPermission(
            MigrationBuilder migrationBuilder,
            string module,
            string code,
            string description)
        {
            // Idempotent insert: relies on the unique index on portal_permissions.code.
            migrationBuilder.Sql($@"
INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), '{module}', '{code}', '{description}', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = '{code}'
);
");
        }

        private static void GrantToAdministratorRole(MigrationBuilder migrationBuilder, string permissionCode)
        {
            // Idempotent grant: relies on unique (portal_role_id, portal_permission_id) index.
            // Skips silently if Administrator role does not yet exist (fresh DB before setup).
            migrationBuilder.Sql($@"
INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
FROM portal_roles r
JOIN portal_permissions p ON p.code = '{permissionCode}'
WHERE r.code = 'Administrator'
  AND NOT EXISTS (
      SELECT 1 FROM portal_role_permissions rp
      WHERE rp.portal_role_id = r.id AND rp.portal_permission_id = p.id
  );
");
        }
    }
}
