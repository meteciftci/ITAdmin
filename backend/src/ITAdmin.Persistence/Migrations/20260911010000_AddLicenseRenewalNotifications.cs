using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260911010000_AddLicenseRenewalNotifications")]
public sealed class AddLicenseRenewalNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "last_renewal_reminder_run_at",
            table: "license_management_settings",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "last_renewal_reminder_status",
            table: "license_management_settings",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "last_renewal_reminder_due_count",
            table: "license_management_settings",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "last_renewal_reminder_queued_count",
            table: "license_management_settings",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "last_renewal_reminder_message",
            table: "license_management_settings",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX ix_notification_outbox_license_renewal_dedupe
            ON notification_outbox (correlation_id)
            WHERE related_module = 'LicenseManagement'
              AND related_event = 'RenewalDue'
              AND correlation_id IS NOT NULL;
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO notification_templates
                (id, module_key, event_key, channel, name, is_enabled, subject_template,
                 body_template, description, created_at, created_by)
            VALUES
                (gen_random_uuid(), 'LicenseManagement', 'RenewalDue', 'Email',
                 'License renewal due', TRUE,
                 'License renewal due: {{productName}} ({{renewalDate}})',
                 E'The {{productName}} license package for "{{purchaseTitle}}" is due for renewal on {{renewalDate}}.\n\nDays remaining: {{daysRemaining}}\nQuantity: {{quantity}}\nLicense type: {{licenseType}}\n\nThis is an automated message from {{applicationName}}.',
                 'Sent once per package, renewal date, and configured recipient when the renewal threshold is reached.',
                 NOW() AT TIME ZONE 'UTC', 'migration')
            ON CONFLICT (module_key, event_key, channel) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM notification_templates
            WHERE module_key = 'LicenseManagement'
              AND event_key = 'RenewalDue'
              AND channel = 'Email'
              AND created_by = 'migration';
            """);

        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_notification_outbox_license_renewal_dedupe;");

        migrationBuilder.DropColumn(name: "last_renewal_reminder_run_at", table: "license_management_settings");
        migrationBuilder.DropColumn(name: "last_renewal_reminder_status", table: "license_management_settings");
        migrationBuilder.DropColumn(name: "last_renewal_reminder_due_count", table: "license_management_settings");
        migrationBuilder.DropColumn(name: "last_renewal_reminder_queued_count", table: "license_management_settings");
        migrationBuilder.DropColumn(name: "last_renewal_reminder_message", table: "license_management_settings");
    }
}
