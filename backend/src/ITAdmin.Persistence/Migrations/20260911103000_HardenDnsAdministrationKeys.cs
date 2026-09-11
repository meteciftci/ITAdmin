using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260911103000_HardenDnsAdministrationKeys")]
public sealed class HardenDnsAdministrationKeys : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX ux_dns_management_settings_singleton
                ON dns_management_settings ((true));
            CREATE UNIQUE INDEX ux_dns_credential_profiles_name_ci
                ON dns_credential_profiles (lower(name));
            CREATE UNIQUE INDEX ux_dns_servers_display_name_ci
                ON dns_servers (lower(display_name));
            CREATE UNIQUE INDEX ux_dns_servers_endpoint_ci
                ON dns_servers (lower(host_name), port);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS ux_dns_servers_endpoint_ci;
            DROP INDEX IF EXISTS ux_dns_servers_display_name_ci;
            DROP INDEX IF EXISTS ux_dns_credential_profiles_name_ci;
            DROP INDEX IF EXISTS ux_dns_management_settings_singleton;
            """);
    }
}
