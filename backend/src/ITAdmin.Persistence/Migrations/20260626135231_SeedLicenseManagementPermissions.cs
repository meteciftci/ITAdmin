using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedLicenseManagementPermissions : Migration
    {
        private const string PermView = "LicenseManagement.View";
        private const string PermManageCatalog = "LicenseManagement.ManageCatalog";
        private const string PermManagePurchases = "LicenseManagement.ManagePurchases";
        private const string PermViewReports = "LicenseManagement.ViewReports";
        private const string PermManageSettings = "LicenseManagement.ManageSettings";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            UpsertPermission(migrationBuilder, "LicenseManagement", PermView, "View license management.");
            UpsertPermission(migrationBuilder, "LicenseManagement", PermManageCatalog, "Manage license catalog (companies and products).");
            UpsertPermission(migrationBuilder, "LicenseManagement", PermManagePurchases, "Manage license purchases and packages.");
            UpsertPermission(migrationBuilder, "LicenseManagement", PermViewReports, "View license management reports.");
            UpsertPermission(migrationBuilder, "LicenseManagement", PermManageSettings, "Manage license management settings.");

            GrantToAdministratorRole(migrationBuilder, PermView);
            GrantToAdministratorRole(migrationBuilder, PermManageCatalog);
            GrantToAdministratorRole(migrationBuilder, PermManagePurchases);
            GrantToAdministratorRole(migrationBuilder, PermViewReports);
            GrantToAdministratorRole(migrationBuilder, PermManageSettings);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
DELETE FROM portal_role_permissions
WHERE portal_permission_id IN (
    SELECT id FROM portal_permissions
    WHERE code IN ('{PermView}', '{PermManageCatalog}', '{PermManagePurchases}', '{PermViewReports}', '{PermManageSettings}')
);
");

            migrationBuilder.Sql($@"
DELETE FROM portal_permissions
WHERE code IN ('{PermView}', '{PermManageCatalog}', '{PermManagePurchases}', '{PermViewReports}', '{PermManageSettings}');
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

        private static void GrantToAdministratorRole(MigrationBuilder migrationBuilder, string permissionCode)
        {
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
