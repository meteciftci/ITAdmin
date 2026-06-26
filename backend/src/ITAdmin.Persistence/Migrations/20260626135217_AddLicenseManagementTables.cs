using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseManagementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "license_companies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tax_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    website = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    address = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    support_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    support_email = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    contact_person_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    contact_person_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    contact_person_email = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "license_acquisitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    acquisition_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    acquisition_date = table.Column<DateOnly>(type: "date", nullable: true),
                    tender_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tender_date = table.Column<DateOnly>(type: "date", nullable: true),
                    direct_purchase_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    dmo_order_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ebys_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ebys_date = table.Column<DateOnly>(type: "date", nullable: true),
                    invoice_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: true),
                    contract_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    contract_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    contract_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    supplier_company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    support_company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actual_total_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    vat_included = table.Column<bool>(type: "boolean", nullable: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_acquisitions", x => x.id);
                    table.ForeignKey(
                        name: "FK_license_acquisitions_license_companies_supplier_company_id",
                        column: x => x.supplier_company_id,
                        principalTable: "license_companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_license_acquisitions_license_companies_support_company_id",
                        column: x => x.support_company_id,
                        principalTable: "license_companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "licensed_products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    vendor_company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    default_license_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_licensed_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_licensed_products_license_companies_vendor_company_id",
                        column: x => x.vendor_company_id,
                        principalTable: "license_companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "license_packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    acquisition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    license_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_perpetual = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    renewal_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    renewal_date = table.Column<DateOnly>(type: "date", nullable: true),
                    serial_number = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    license_key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    license_account_email = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    license_portal_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    license_notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_packages", x => x.id);
                    table.ForeignKey(
                        name: "FK_license_packages_license_acquisitions_acquisition_id",
                        column: x => x.acquisition_id,
                        principalTable: "license_acquisitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_license_packages_licensed_products_product_id",
                        column: x => x.product_id,
                        principalTable: "licensed_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_license_acquisitions_acquisition_type",
                table: "license_acquisitions",
                column: "acquisition_type");

            migrationBuilder.CreateIndex(
                name: "IX_license_acquisitions_status",
                table: "license_acquisitions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_license_acquisitions_supplier_company_id",
                table: "license_acquisitions",
                column: "supplier_company_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_acquisitions_support_company_id",
                table: "license_acquisitions",
                column: "support_company_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_companies_is_active",
                table: "license_companies",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_license_companies_name",
                table: "license_companies",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_license_packages_acquisition_id",
                table: "license_packages",
                column: "acquisition_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_packages_is_active",
                table: "license_packages",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_license_packages_product_id",
                table: "license_packages",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_packages_status",
                table: "license_packages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_licensed_products_is_active",
                table: "licensed_products",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_licensed_products_name",
                table: "licensed_products",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_licensed_products_vendor_company_id",
                table: "licensed_products",
                column: "vendor_company_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "license_packages");

            migrationBuilder.DropTable(
                name: "license_acquisitions");

            migrationBuilder.DropTable(
                name: "licensed_products");

            migrationBuilder.DropTable(
                name: "license_companies");
        }
    }
}
