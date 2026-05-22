using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SasPortal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationOutboxAndTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    recipient = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    recipient_masked = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    max_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    provider_summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    related_module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    related_event = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    related_entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    related_entity_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    subject_template = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    body_template = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_templates", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_channel_status",
                table: "notification_outbox",
                columns: new[] { "channel", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_correlation_id",
                table: "notification_outbox",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_created_at",
                table: "notification_outbox",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_related_module_related_event",
                table: "notification_outbox",
                columns: new[] { "related_module", "related_event" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_status_next_attempt_at",
                table: "notification_outbox",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_templates_module_key_event_key_channel",
                table: "notification_templates",
                columns: new[] { "module_key", "event_key", "channel" },
                unique: true);

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            migrationBuilder.Sql("""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'NotificationOutbox', 'NotificationOutbox.View', 'View notification outbox.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'NotificationOutbox.View');
                """);

            migrationBuilder.Sql("""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'NotificationOutbox', 'NotificationOutbox.Retry', 'Retry notification outbox items.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'NotificationOutbox.Retry');
                """);

            migrationBuilder.Sql("""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'NotificationOutbox', 'NotificationOutbox.Cancel', 'Cancel notification outbox items.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'NotificationOutbox.Cancel');
                """);

            migrationBuilder.Sql("""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'NotificationTemplates', 'NotificationTemplates.View', 'View notification templates.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'NotificationTemplates.View');
                """);

            migrationBuilder.Sql("""
                INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
                SELECT gen_random_uuid(), 'NotificationTemplates', 'NotificationTemplates.Update', 'Update notification templates.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
                WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'NotificationTemplates.Update');
                """);

            migrationBuilder.Sql("""
                INSERT INTO notification_templates (
                    id, module_key, event_key, channel, name, is_enabled, subject_template, body_template, description, created_at, created_by)
                SELECT gen_random_uuid(), 'System', 'GenericTest', 'Sms', 'Generic SMS Test', TRUE, NULL,
                    'Test message: {{message}}', 'Default system SMS test template.', NOW() AT TIME ZONE 'UTC', 'migration'
                WHERE NOT EXISTS (
                    SELECT 1 FROM notification_templates
                    WHERE module_key = 'System' AND event_key = 'GenericTest' AND channel = 'Sms');
                """);

            migrationBuilder.Sql("""
                INSERT INTO notification_templates (
                    id, module_key, event_key, channel, name, is_enabled, subject_template, body_template, description, created_at, created_by)
                SELECT gen_random_uuid(), 'System', 'GenericTest', 'Email', 'Generic Email Test', TRUE, 'Test: {{subject}}',
                    'Test message: {{message}}', 'Default system email test template.', NOW() AT TIME ZONE 'UTC', 'migration'
                WHERE NOT EXISTS (
                    SELECT 1 FROM notification_templates
                    WHERE module_key = 'System' AND event_key = 'GenericTest' AND channel = 'Email');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM notification_templates
                WHERE module_key = 'System' AND event_key = 'GenericTest' AND channel IN ('Sms', 'Email');
                """);

            migrationBuilder.Sql("""
                DELETE FROM portal_permissions
                WHERE code IN (
                    'NotificationOutbox.View',
                    'NotificationOutbox.Retry',
                    'NotificationOutbox.Cancel',
                    'NotificationTemplates.View',
                    'NotificationTemplates.Update');
                """);

            migrationBuilder.DropTable(
                name: "notification_outbox");

            migrationBuilder.DropTable(
                name: "notification_templates");
        }
    }
}
