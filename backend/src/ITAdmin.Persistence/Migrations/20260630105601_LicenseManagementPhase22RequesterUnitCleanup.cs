using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LicenseManagementPhase22RequesterUnitCleanup : Migration
    {
        private const string PermDirectoryOuLookup = "Directory.OrganizationalUnits.Lookup";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_license_requests_requested_by_ad_object_id",
                table: "license_requests");

            migrationBuilder.DropColumn(
                name: "requested_by_ad_object_id",
                table: "license_requests");

            migrationBuilder.DropColumn(
                name: "requested_by_department",
                table: "license_requests");

            migrationBuilder.DropColumn(
                name: "requested_by_display_name",
                table: "license_requests");

            migrationBuilder.DropColumn(
                name: "requested_by_mail",
                table: "license_requests");

            migrationBuilder.DropColumn(
                name: "requested_by_phone",
                table: "license_requests");

            migrationBuilder.DropColumn(
                name: "requested_by_sam_account_name",
                table: "license_requests");

            migrationBuilder.DropColumn(
                name: "requested_by_title",
                table: "license_requests");

            migrationBuilder.DropColumn(
                name: "requested_by_user_principal_name",
                table: "license_requests");

            migrationBuilder.RenameColumn(
                name: "requester_unit",
                table: "license_requests",
                newName: "requester_unit_display_name");

            migrationBuilder.RenameColumn(
                name: "requested_by_manager_name",
                table: "license_requests",
                newName: "requester_manager_name");

            migrationBuilder.AlterColumn<string>(
                name: "requester_unit_display_name",
                table: "license_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "requester_unit_distinguished_name",
                table: "license_requests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "requester_unit_object_guid",
                table: "license_requests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_license_requests_requester_unit_object_guid",
                table: "license_requests",
                column: "requester_unit_object_guid");

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            migrationBuilder.Sql($@"
INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'Directory', '{PermDirectoryOuLookup}', 'Lookup directory organizational units for read-only selection.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = '{PermDirectoryOuLookup}'
);
");

            migrationBuilder.Sql($@"
INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
FROM portal_roles r
JOIN portal_permissions p ON p.code = '{PermDirectoryOuLookup}'
WHERE r.code = 'Administrator'
  AND NOT EXISTS (
      SELECT 1 FROM portal_role_permissions rp
      WHERE rp.portal_role_id = r.id AND rp.portal_permission_id = p.id
  );
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
DELETE FROM portal_role_permissions
WHERE portal_permission_id IN (
    SELECT id FROM portal_permissions
    WHERE code = '{PermDirectoryOuLookup}'
);
");

            migrationBuilder.Sql($@"
DELETE FROM portal_permissions
WHERE code = '{PermDirectoryOuLookup}';
");

            migrationBuilder.DropIndex(
                name: "IX_license_requests_requester_unit_object_guid",
                table: "license_requests");

            migrationBuilder.DropColumn(
                name: "requester_unit_distinguished_name",
                table: "license_requests");

            migrationBuilder.DropColumn(
                name: "requester_unit_object_guid",
                table: "license_requests");

            migrationBuilder.RenameColumn(
                name: "requester_manager_name",
                table: "license_requests",
                newName: "requested_by_manager_name");

            migrationBuilder.RenameColumn(
                name: "requester_unit_display_name",
                table: "license_requests",
                newName: "requester_unit");

            migrationBuilder.AlterColumn<string>(
                name: "requester_unit",
                table: "license_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "requested_by_ad_object_id",
                table: "license_requests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "requested_by_department",
                table: "license_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "requested_by_display_name",
                table: "license_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "requested_by_mail",
                table: "license_requests",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "requested_by_phone",
                table: "license_requests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "requested_by_sam_account_name",
                table: "license_requests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "requested_by_title",
                table: "license_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "requested_by_user_principal_name",
                table: "license_requests",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_license_requests_requested_by_ad_object_id",
                table: "license_requests",
                column: "requested_by_ad_object_id");
        }
    }
}
