using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseRequestItemFulfillments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "license_request_item_fulfillments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_request_item_fulfillments", x => x.id);
                    table.ForeignKey(
                        name: "FK_license_request_item_fulfillments_license_packages_package_~",
                        column: x => x.package_id,
                        principalTable: "license_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_license_request_item_fulfillments_license_request_items_req~",
                        column: x => x.request_item_id,
                        principalTable: "license_request_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_license_request_item_fulfillments_package_id",
                table: "license_request_item_fulfillments",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_request_item_fulfillments_request_item_id",
                table: "license_request_item_fulfillments",
                column: "request_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "license_request_item_fulfillments");
        }
    }
}
