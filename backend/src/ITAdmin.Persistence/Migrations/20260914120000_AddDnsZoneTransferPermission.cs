using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260914120000_AddDnsZoneTransferPermission")]
public sealed class AddDnsZoneTransferPermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO portal_permissions (id, module, code, description, is_active, created_at, created_by, is_deleted)
            SELECT gen_random_uuid(), 'DnsManagement', 'DnsManagement.ZoneTransfers.Manage',
                   'Manage DNS primary-zone transfers and notifications.', TRUE, NOW(), 'migration', FALSE
            WHERE NOT EXISTS (SELECT 1 FROM portal_permissions WHERE code = 'DnsManagement.ZoneTransfers.Manage');

            INSERT INTO portal_role_permissions (id, portal_role_id, portal_permission_id, created_at, created_by)
            SELECT gen_random_uuid(), r.id, p.id, NOW(), 'migration'
            FROM portal_roles r CROSS JOIN portal_permissions p
            WHERE r.code = 'Administrator' AND p.code = 'DnsManagement.ZoneTransfers.Manage'
              AND NOT EXISTS (SELECT 1 FROM portal_role_permissions rp
                              WHERE rp.portal_role_id = r.id AND rp.portal_permission_id = p.id);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM portal_role_permissions
            WHERE portal_permission_id IN (SELECT id FROM portal_permissions WHERE code = 'DnsManagement.ZoneTransfers.Manage');
            DELETE FROM portal_permissions WHERE code = 'DnsManagement.ZoneTransfers.Manage';
            """);
    }
}
