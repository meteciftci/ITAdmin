using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdManagementUserAccountOperationPermissions : Migration
    {
        private const string PermUsersEnable = "AdManagement.Users.Enable";
        private const string PermUsersDisable = "AdManagement.Users.Disable";
        private const string PermUsersUnlock = "AdManagement.Users.Unlock";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            migrationBuilder.Sql($"""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'AdManagement', '{PermUsersEnable}', 'Enable AD management directory user accounts.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (
                    SELECT 1 FROM portal_permissions WHERE code = '{PermUsersEnable}'
                );
                """);

            migrationBuilder.Sql($"""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'AdManagement', '{PermUsersDisable}', 'Disable AD management directory user accounts.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (
                    SELECT 1 FROM portal_permissions WHERE code = '{PermUsersDisable}'
                );
                """);

            migrationBuilder.Sql($"""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'AdManagement', '{PermUsersUnlock}', 'Unlock AD management directory user accounts.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (
                    SELECT 1 FROM portal_permissions WHERE code = '{PermUsersUnlock}'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DELETE FROM portal_permissions
                WHERE code IN ('{PermUsersEnable}', '{PermUsersDisable}', '{PermUsersUnlock}');
                """);
        }
    }
}
