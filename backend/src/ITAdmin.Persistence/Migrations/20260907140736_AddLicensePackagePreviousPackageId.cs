using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLicensePackagePreviousPackageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "previous_package_id",
                table: "license_packages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_license_packages_previous_package_id",
                table: "license_packages",
                column: "previous_package_id");

            migrationBuilder.AddForeignKey(
                name: "FK_license_packages_license_packages_previous_package_id",
                table: "license_packages",
                column: "previous_package_id",
                principalTable: "license_packages",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_license_packages_license_packages_previous_package_id",
                table: "license_packages");

            migrationBuilder.DropIndex(
                name: "IX_license_packages_previous_package_id",
                table: "license_packages");

            migrationBuilder.DropColumn(
                name: "previous_package_id",
                table: "license_packages");
        }
    }
}
