using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260911000000_OptimizeLicenseManagementQueries")]
public sealed class OptimizeLicenseManagementQueries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_license_packages_status_created_at",
            table: "license_packages",
            columns: new[] { "status", "created_at" });

        migrationBuilder.CreateIndex(
            name: "IX_license_purchases_status_purchase_date",
            table: "license_purchases",
            columns: new[] { "status", "purchase_date" });

        migrationBuilder.CreateIndex(
            name: "IX_license_request_items_status",
            table: "license_request_items",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "IX_license_request_items_status_product_id",
            table: "license_request_items",
            columns: new[] { "status", "product_id" });

        migrationBuilder.CreateIndex(
            name: "IX_license_requests_status_request_date",
            table: "license_requests",
            columns: new[] { "status", "request_date" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_license_packages_status_created_at",
            table: "license_packages");

        migrationBuilder.DropIndex(
            name: "IX_license_purchases_status_purchase_date",
            table: "license_purchases");

        migrationBuilder.DropIndex(
            name: "IX_license_request_items_status",
            table: "license_request_items");

        migrationBuilder.DropIndex(
            name: "IX_license_request_items_status_product_id",
            table: "license_request_items");

        migrationBuilder.DropIndex(
            name: "IX_license_requests_status_request_date",
            table: "license_requests");
    }
}
