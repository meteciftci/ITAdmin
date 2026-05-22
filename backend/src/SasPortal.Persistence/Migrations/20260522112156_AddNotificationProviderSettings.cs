using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SasPortal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationProviderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_provider_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    public_settings_json = table.Column<string>(type: "text", nullable: true),
                    encrypted_secret_settings_json = table.Column<string>(type: "text", nullable: true),
                    last_validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_validation_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    last_validation_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_provider_settings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_provider_settings_channel_provider_key",
                table: "notification_provider_settings",
                columns: new[] { "channel", "provider_key" },
                unique: true);

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            migrationBuilder.Sql("""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'NotificationProviders', 'NotificationProviders.View', 'View notification provider settings.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'NotificationProviders.View');
                """);

            migrationBuilder.Sql("""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'NotificationProviders', 'NotificationProviders.Update', 'Update notification provider settings.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'NotificationProviders.Update');
                """);

            migrationBuilder.Sql("""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'NotificationProviders', 'NotificationProviders.Test', 'Send notification provider test messages.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'NotificationProviders.Test');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM portal_permissions
                WHERE code IN (
                    'NotificationProviders.View',
                    'NotificationProviders.Update',
                    'NotificationProviders.Test');
                """);

            migrationBuilder.DropTable(
                name: "notification_provider_settings");
        }
    }
}
