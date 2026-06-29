using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseRequestTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "license_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    request_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    request_date = table.Column<DateOnly>(type: "date", nullable: false),
                    external_request_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ebys_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ebys_date = table.Column<DateOnly>(type: "date", nullable: true),
                    requested_by_ad_object_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    requested_by_sam_account_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    requested_by_user_principal_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    requested_by_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requested_by_department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requested_by_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requested_by_mail = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    requested_by_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    requested_by_manager_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requester_unit = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    estimated_total_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    vat_included = table.Column<bool>(type: "boolean", nullable: true),
                    cost_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "license_request_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_quantity = table.Column<int>(type: "integer", nullable: false),
                    approved_quantity = table.Column<int>(type: "integer", nullable: true),
                    fulfilled_quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    estimated_unit_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    estimated_total_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    vat_included = table.Column<bool>(type: "boolean", nullable: true),
                    justification = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_request_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_license_request_items_license_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "license_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_license_request_items_licensed_products_product_id",
                        column: x => x.product_id,
                        principalTable: "licensed_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "license_request_item_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad_object_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sam_account_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    user_principal_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    mail = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_request_item_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_license_request_item_users_license_request_items_request_it~",
                        column: x => x.request_item_id,
                        principalTable: "license_request_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_license_request_item_users_ad_object_id",
                table: "license_request_item_users",
                column: "ad_object_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_request_item_users_request_item_id",
                table: "license_request_item_users",
                column: "request_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_request_item_users_request_item_id_ad_object_id",
                table: "license_request_item_users",
                columns: new[] { "request_item_id", "ad_object_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_license_request_items_product_id",
                table: "license_request_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_request_items_request_id",
                table: "license_request_items",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_request_items_request_id_product_id",
                table: "license_request_items",
                columns: new[] { "request_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_license_requests_request_date",
                table: "license_requests",
                column: "request_date");

            migrationBuilder.CreateIndex(
                name: "IX_license_requests_request_number",
                table: "license_requests",
                column: "request_number");

            migrationBuilder.CreateIndex(
                name: "IX_license_requests_request_source",
                table: "license_requests",
                column: "request_source");

            migrationBuilder.CreateIndex(
                name: "IX_license_requests_requested_by_ad_object_id",
                table: "license_requests",
                column: "requested_by_ad_object_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_requests_status",
                table: "license_requests",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "license_request_item_users");

            migrationBuilder.DropTable(
                name: "license_request_items");

            migrationBuilder.DropTable(
                name: "license_requests");
        }
    }
}
