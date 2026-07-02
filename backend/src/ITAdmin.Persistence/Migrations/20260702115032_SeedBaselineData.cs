using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedBaselineData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Baseline reference-data seed consolidated from the original per-feature seed
            // migrations: pgcrypto, portal permissions (+ the ManageAcquisitions->ManagePurchases
            // rename), administrator-role grants (no-op until the Administrator role exists),
            // the default product category, and notification templates. All statements are
            // idempotent (guarded by WHERE NOT EXISTS / code lookups).
            migrationBuilder.Sql("""
CREATE EXTENSION IF NOT EXISTS pgcrypto;

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'AdManagement', 'AdManagement.Settings.View', 'View AD management settings.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'AdManagement.Settings.View'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'AdManagement', 'AdManagement.Settings.Update', 'Update AD management settings.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'AdManagement.Settings.Update'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'AdOperationLogs', 'AdOperationLogs.View', 'View AD operation logs.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'AdOperationLogs.View'
);

INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
FROM portal_roles r
JOIN portal_permissions p ON p.code = 'AdManagement.Settings.View'
WHERE r.code = 'Administrator'
  AND NOT EXISTS (
      SELECT 1 FROM portal_role_permissions rp
      WHERE rp.portal_role_id = r.id AND rp.portal_permission_id = p.id
  );

INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
FROM portal_roles r
JOIN portal_permissions p ON p.code = 'AdManagement.Settings.Update'
WHERE r.code = 'Administrator'
  AND NOT EXISTS (
      SELECT 1 FROM portal_role_permissions rp
      WHERE rp.portal_role_id = r.id AND rp.portal_permission_id = p.id
  );

INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
FROM portal_roles r
JOIN portal_permissions p ON p.code = 'AdOperationLogs.View'
WHERE r.code = 'Administrator'
  AND NOT EXISTS (
      SELECT 1 FROM portal_role_permissions rp
      WHERE rp.portal_role_id = r.id AND rp.portal_permission_id = p.id
  );

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'AdManagement', 'AdManagement.Settings.View', 'View AD management settings.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'AdManagement.Settings.View'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'AdManagement', 'AdManagement.Settings.Update', 'Update AD management settings.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'AdManagement.Settings.Update'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'AdManagement', 'AdManagement.Users.View', 'View AD management directory users.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'AdManagement.Users.View'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'AdOperationLogs', 'AdOperationLogs.View', 'View AD operation logs.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'AdOperationLogs.View'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'AdManagement', 'AdManagement.Users.Create', 'Create AD management directory users.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'AdManagement.Users.Create'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'NotificationProviders', 'NotificationProviders.View', 'View notification provider settings.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'NotificationProviders.View');

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'NotificationProviders', 'NotificationProviders.Update', 'Update notification provider settings.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'NotificationProviders.Update');

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'NotificationProviders', 'NotificationProviders.Test', 'Send notification provider test messages.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'NotificationProviders.Test');

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'NotificationOutbox', 'NotificationOutbox.View', 'View notification outbox.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'NotificationOutbox.View');

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'NotificationOutbox', 'NotificationOutbox.Retry', 'Retry notification outbox items.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'NotificationOutbox.Retry');

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'NotificationOutbox', 'NotificationOutbox.Cancel', 'Cancel notification outbox items.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'NotificationOutbox.Cancel');

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'NotificationTemplates', 'NotificationTemplates.View', 'View notification templates.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'NotificationTemplates.View');

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'NotificationTemplates', 'NotificationTemplates.Update', 'Update notification templates.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'NotificationTemplates.Update');

INSERT INTO notification_templates (
    id, module_key, event_key, channel, name, is_enabled, subject_template, body_template, description, created_at, created_by)
SELECT gen_random_uuid(), 'System', 'GenericTest', 'Sms', 'Generic SMS Test', TRUE, NULL,
    'Test message: {{message}}', 'Default system SMS test template.', NOW() AT TIME ZONE 'UTC', 'migration'
WHERE NOT EXISTS (
    SELECT 1 FROM notification_templates
    WHERE module_key = 'System' AND event_key = 'GenericTest' AND channel = 'Sms');

INSERT INTO notification_templates (
    id, module_key, event_key, channel, name, is_enabled, subject_template, body_template, description, created_at, created_by)
SELECT gen_random_uuid(), 'System', 'GenericTest', 'Email', 'Generic Email Test', TRUE, 'Test: {{subject}}',
    'Test message: {{message}}', 'Default system email test template.', NOW() AT TIME ZONE 'UTC', 'migration'
WHERE NOT EXISTS (
    SELECT 1 FROM notification_templates
    WHERE module_key = 'System' AND event_key = 'GenericTest' AND channel = 'Email');

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'AdManagement', 'AdManagement.Users.Enable', 'Enable AD management directory user accounts.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'AdManagement.Users.Enable'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'AdManagement', 'AdManagement.Users.Disable', 'Disable AD management directory user accounts.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'AdManagement.Users.Disable'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'AdManagement', 'AdManagement.Users.Unlock', 'Unlock AD management directory user accounts.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'AdManagement.Users.Unlock'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'AdManagement', 'AdManagement.Users.Groups.View', 'View AD user direct group memberships.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'AdManagement.Users.Groups.View'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'AdManagement', 'AdManagement.Users.Groups.Add', 'Add AD users to groups.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'AdManagement.Users.Groups.Add'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'AdManagement', 'AdManagement.Users.Groups.Remove', 'Remove AD users from groups.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'AdManagement.Users.Groups.Remove'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'LicenseManagement', 'LicenseManagement.View', 'View license management.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'LicenseManagement.View'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'LicenseManagement', 'LicenseManagement.ManageCatalog', 'Manage license catalog (companies and products).', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'LicenseManagement.ManageCatalog'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'LicenseManagement', 'LicenseManagement.ManagePurchases', 'Manage license purchases and packages.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'LicenseManagement.ManagePurchases'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'LicenseManagement', 'LicenseManagement.ViewReports', 'View license management reports.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'LicenseManagement.ViewReports'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'LicenseManagement', 'LicenseManagement.ManageSettings', 'Manage license management settings.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'LicenseManagement.ManageSettings'
);

INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
FROM portal_roles r
JOIN portal_permissions p ON p.code = 'LicenseManagement.View'
WHERE r.code = 'Administrator'
  AND NOT EXISTS (
      SELECT 1 FROM portal_role_permissions rp
      WHERE rp.portal_role_id = r.id AND rp.portal_permission_id = p.id
  );

INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
FROM portal_roles r
JOIN portal_permissions p ON p.code = 'LicenseManagement.ManageCatalog'
WHERE r.code = 'Administrator'
  AND NOT EXISTS (
      SELECT 1 FROM portal_role_permissions rp
      WHERE rp.portal_role_id = r.id AND rp.portal_permission_id = p.id
  );

INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
FROM portal_roles r
JOIN portal_permissions p ON p.code = 'LicenseManagement.ManagePurchases'
WHERE r.code = 'Administrator'
  AND NOT EXISTS (
      SELECT 1 FROM portal_role_permissions rp
      WHERE rp.portal_role_id = r.id AND rp.portal_permission_id = p.id
  );

INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
FROM portal_roles r
JOIN portal_permissions p ON p.code = 'LicenseManagement.ViewReports'
WHERE r.code = 'Administrator'
  AND NOT EXISTS (
      SELECT 1 FROM portal_role_permissions rp
      WHERE rp.portal_role_id = r.id AND rp.portal_permission_id = p.id
  );

INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
FROM portal_roles r
JOIN portal_permissions p ON p.code = 'LicenseManagement.ManageSettings'
WHERE r.code = 'Administrator'
  AND NOT EXISTS (
      SELECT 1 FROM portal_role_permissions rp
      WHERE rp.portal_role_id = r.id AND rp.portal_permission_id = p.id
  );

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'LicenseManagement', 'LicenseManagement.ManageRequests', 'Manage license requests.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'LicenseManagement.ManageRequests'
);

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'Directory', 'Directory.Users.Lookup', 'Lookup directory users for read-only selection.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'Directory.Users.Lookup'
);

INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
FROM portal_roles r
JOIN portal_permissions p ON p.code = 'LicenseManagement.ManageRequests'
WHERE r.code = 'Administrator'
  AND NOT EXISTS (
      SELECT 1 FROM portal_role_permissions rp
      WHERE rp.portal_role_id = r.id AND rp.portal_permission_id = p.id
  );

INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
FROM portal_roles r
JOIN portal_permissions p ON p.code = 'Directory.Users.Lookup'
WHERE r.code = 'Administrator'
  AND NOT EXISTS (
      SELECT 1 FROM portal_role_permissions rp
      WHERE rp.portal_role_id = r.id AND rp.portal_permission_id = p.id
  );

INSERT INTO license_product_categories (id, name, description, is_active, created_at, created_by)
VALUES ('a1b2c3d4-e5f6-7890-abcd-ef1234567890', 'Genel', 'Varsayılan ürün kategorisi', TRUE, NOW() AT TIME ZONE 'UTC', 'migration');

UPDATE portal_permissions
SET code = 'LicenseManagement.ManagePurchases',
    description = 'Manage license purchases and packages.'
WHERE code = 'LicenseManagement.ManageAcquisitions';

INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
SELECT gen_random_uuid(), 'Directory', 'Directory.OrganizationalUnits.Lookup', 'Lookup directory organizational units for read-only selection.', TRUE, NOW() AT TIME ZONE 'UTC', 'migration', FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM portal_permissions WHERE code = 'Directory.OrganizationalUnits.Lookup'
);

INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
SELECT gen_random_uuid(), r.id, p.id, NOW() AT TIME ZONE 'UTC', 'migration'
FROM portal_roles r
JOIN portal_permissions p ON p.code = 'Directory.OrganizationalUnits.Lookup'
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
            migrationBuilder.Sql("DELETE FROM portal_role_permissions WHERE created_by = 'migration';");
            migrationBuilder.Sql("DELETE FROM portal_permissions WHERE created_by = 'migration';");
            migrationBuilder.Sql("DELETE FROM notification_templates;");
            migrationBuilder.Sql("DELETE FROM license_product_categories;");
        }
    }
}
