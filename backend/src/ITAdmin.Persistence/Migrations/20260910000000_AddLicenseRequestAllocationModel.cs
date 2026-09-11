using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260910000000_AddLicenseRequestAllocationModel")]
public partial class AddLicenseRequestAllocationModel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "license_type",
            table: "license_request_items",
            type: "character varying(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "NamedUser");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "license_type",
            table: "license_request_items");
    }
}
