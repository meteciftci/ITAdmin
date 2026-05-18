using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SasPortal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdManagementModulePermissions : Migration
    {
        private const string PermAdManagementSettingsView = "AdManagement.Settings.View";
        private const string PermAdManagementSettingsUpdate = "AdManagement.Settings.Update";
        private const string PermAdManagementUsersView = "AdManagement.Users.View";
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
                module: "AdManagement",
                code: PermAdManagementUsersView,
                description: "View AD management directory users.");

            UpsertPermission(
                migrationBuilder,
                module: "AdOperationLogs",
                code: PermAdOperationLogsView,
                description: "View AD operation logs.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
DELETE FROM portal_role_permissions
WHERE portal_permission_id IN (
    SELECT id FROM portal_permissions
    WHERE code IN (
        '{PermAdManagementSettingsView}',
        '{PermAdManagementSettingsUpdate}',
        '{PermAdManagementUsersView}',
        '{PermAdOperationLogsView}'
    )
);
");

            migrationBuilder.Sql($@"
DELETE FROM portal_permissions
WHERE code IN (
    '{PermAdManagementSettingsView}',
    '{PermAdManagementSettingsUpdate}',
    '{PermAdManagementUsersView}',
    '{PermAdOperationLogsView}'
);
");
        }

        private static void UpsertPermission(
            MigrationBuilder migrationBuilder,
            string module,
            string code,
            string description)
        {
            migrationBuilder.Sql($@"
INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), '{module}', '{code}', '{description}', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = '{code}'
);
");
        }
    }
}
