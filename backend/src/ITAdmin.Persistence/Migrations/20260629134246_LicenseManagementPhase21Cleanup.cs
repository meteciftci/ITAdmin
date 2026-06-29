using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LicenseManagementPhase21Cleanup : Migration
    {
        private static readonly Guid DefaultCategoryId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "license_product_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_product_categories", x => x.id);
                });

            migrationBuilder.Sql($@"
INSERT INTO license_product_categories (id, name, description, is_active, created_at, created_by)
VALUES ('{DefaultCategoryId}', 'Genel', 'Varsayılan ürün kategorisi', TRUE, NOW() AT TIME ZONE 'UTC', 'migration');
");

            migrationBuilder.CreateIndex(
                name: "IX_license_product_categories_is_active",
                table: "license_product_categories",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_license_product_categories_name",
                table: "license_product_categories",
                column: "name");

            migrationBuilder.DropForeignKey(
                name: "FK_licensed_products_license_companies_vendor_company_id",
                table: "licensed_products");

            migrationBuilder.DropIndex(
                name: "IX_licensed_products_vendor_company_id",
                table: "licensed_products");

            migrationBuilder.AddColumn<string>(
                name: "brand",
                table: "licensed_products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                table: "licensed_products",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql($@"
UPDATE licensed_products
SET category_id = '{DefaultCategoryId}'
WHERE category_id IS NULL;
");

            migrationBuilder.AlterColumn<Guid>(
                name: "category_id",
                table: "licensed_products",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "category",
                table: "licensed_products");

            migrationBuilder.DropColumn(
                name: "default_license_type",
                table: "licensed_products");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "licensed_products");

            migrationBuilder.DropColumn(
                name: "vendor_company_id",
                table: "licensed_products");

            migrationBuilder.DropColumn(
                name: "address",
                table: "license_companies");

            migrationBuilder.DropColumn(
                name: "support_email",
                table: "license_companies");

            migrationBuilder.DropColumn(
                name: "support_phone",
                table: "license_companies");

            migrationBuilder.DropColumn(
                name: "tax_number",
                table: "license_companies");

            migrationBuilder.CreateIndex(
                name: "IX_licensed_products_category_id",
                table: "licensed_products",
                column: "category_id");

            migrationBuilder.AddForeignKey(
                name: "FK_licensed_products_license_product_categories_category_id",
                table: "licensed_products",
                column: "category_id",
                principalTable: "license_product_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
UPDATE portal_permissions
SET code = 'LicenseManagement.ManagePurchases',
    description = 'Manage license purchases and packages.'
WHERE code = 'LicenseManagement.ManageAcquisitions';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE portal_permissions
SET code = 'LicenseManagement.ManageAcquisitions',
    description = 'Manage license acquisitions and packages.'
WHERE code = 'LicenseManagement.ManagePurchases';
");

            migrationBuilder.DropForeignKey(
                name: "FK_licensed_products_license_product_categories_category_id",
                table: "licensed_products");

            migrationBuilder.DropTable(
                name: "license_product_categories");

            migrationBuilder.DropIndex(
                name: "IX_licensed_products_category_id",
                table: "licensed_products");

            migrationBuilder.DropColumn(
                name: "brand",
                table: "licensed_products");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "licensed_products");

            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "licensed_products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "default_license_type",
                table: "licensed_products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "licensed_products",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "vendor_company_id",
                table: "licensed_products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "license_companies",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "support_email",
                table: "license_companies",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "support_phone",
                table: "license_companies",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_number",
                table: "license_companies",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_licensed_products_vendor_company_id",
                table: "licensed_products",
                column: "vendor_company_id");

            migrationBuilder.AddForeignKey(
                name: "FK_licensed_products_license_companies_vendor_company_id",
                table: "licensed_products",
                column: "vendor_company_id",
                principalTable: "license_companies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
