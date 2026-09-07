using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseSeatAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "license_seat_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad_object_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sam_account_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    user_principal_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    mail = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    national_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    assigned_date = table.Column<DateOnly>(type: "date", nullable: false),
                    released_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    replaces_assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_request_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_seat_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_license_seat_assignments_license_packages_package_id",
                        column: x => x.package_id,
                        principalTable: "license_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_license_seat_assignments_license_seat_assignments_replaces_~",
                        column: x => x.replaces_assignment_id,
                        principalTable: "license_seat_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_license_seat_assignments_ad_object_id",
                table: "license_seat_assignments",
                column: "ad_object_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_seat_assignments_national_id",
                table: "license_seat_assignments",
                column: "national_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_seat_assignments_package_id",
                table: "license_seat_assignments",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_seat_assignments_package_id_status",
                table: "license_seat_assignments",
                columns: new[] { "package_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_license_seat_assignments_replaces_assignment_id",
                table: "license_seat_assignments",
                column: "replaces_assignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_seat_assignments_status",
                table: "license_seat_assignments",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "license_seat_assignments");
        }
    }
}
