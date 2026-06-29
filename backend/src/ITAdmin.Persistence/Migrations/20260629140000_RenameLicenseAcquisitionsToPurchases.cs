using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameLicenseAcquisitionsToPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_license_packages_license_acquisitions_acquisition_id",
                table: "license_packages");

            migrationBuilder.RenameTable(
                name: "license_acquisitions",
                newName: "license_purchases");

            migrationBuilder.RenameColumn(
                name: "acquisition_type",
                table: "license_purchases",
                newName: "purchase_type");

            migrationBuilder.RenameColumn(
                name: "acquisition_date",
                table: "license_purchases",
                newName: "purchase_date");

            migrationBuilder.RenameColumn(
                name: "acquisition_id",
                table: "license_packages",
                newName: "purchase_id");

            migrationBuilder.RenameIndex(
                name: "IX_license_acquisitions_acquisition_type",
                table: "license_purchases",
                newName: "IX_license_purchases_purchase_type");

            migrationBuilder.RenameIndex(
                name: "IX_license_acquisitions_status",
                table: "license_purchases",
                newName: "IX_license_purchases_status");

            migrationBuilder.RenameIndex(
                name: "IX_license_acquisitions_supplier_company_id",
                table: "license_purchases",
                newName: "IX_license_purchases_supplier_company_id");

            migrationBuilder.RenameIndex(
                name: "IX_license_acquisitions_support_company_id",
                table: "license_purchases",
                newName: "IX_license_purchases_support_company_id");

            migrationBuilder.RenameIndex(
                name: "IX_license_packages_acquisition_id",
                table: "license_packages",
                newName: "IX_license_packages_purchase_id");

            migrationBuilder.Sql(
                """
                ALTER TABLE license_purchases RENAME CONSTRAINT "PK_license_acquisitions" TO "PK_license_purchases";
                ALTER TABLE license_purchases RENAME CONSTRAINT "FK_license_acquisitions_license_companies_supplier_company_id" TO "FK_license_purchases_license_companies_supplier_company_id";
                ALTER TABLE license_purchases RENAME CONSTRAINT "FK_license_acquisitions_license_companies_support_company_id" TO "FK_license_purchases_license_companies_support_company_id";
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_license_packages_license_purchases_purchase_id",
                table: "license_packages",
                column: "purchase_id",
                principalTable: "license_purchases",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_license_packages_license_purchases_purchase_id",
                table: "license_packages");

            migrationBuilder.Sql(
                """
                ALTER TABLE license_purchases RENAME CONSTRAINT "PK_license_purchases" TO "PK_license_acquisitions";
                ALTER TABLE license_purchases RENAME CONSTRAINT "FK_license_purchases_license_companies_supplier_company_id" TO "FK_license_acquisitions_license_companies_supplier_company_id";
                ALTER TABLE license_purchases RENAME CONSTRAINT "FK_license_purchases_license_companies_support_company_id" TO "FK_license_acquisitions_license_companies_support_company_id";
                """);

            migrationBuilder.RenameIndex(
                name: "IX_license_packages_purchase_id",
                table: "license_packages",
                newName: "IX_license_packages_acquisition_id");

            migrationBuilder.RenameIndex(
                name: "IX_license_purchases_support_company_id",
                table: "license_purchases",
                newName: "IX_license_acquisitions_support_company_id");

            migrationBuilder.RenameIndex(
                name: "IX_license_purchases_supplier_company_id",
                table: "license_purchases",
                newName: "IX_license_acquisitions_supplier_company_id");

            migrationBuilder.RenameIndex(
                name: "IX_license_purchases_status",
                table: "license_purchases",
                newName: "IX_license_acquisitions_status");

            migrationBuilder.RenameIndex(
                name: "IX_license_purchases_purchase_type",
                table: "license_purchases",
                newName: "IX_license_acquisitions_acquisition_type");

            migrationBuilder.RenameColumn(
                name: "purchase_id",
                table: "license_packages",
                newName: "acquisition_id");

            migrationBuilder.RenameColumn(
                name: "purchase_date",
                table: "license_purchases",
                newName: "acquisition_date");

            migrationBuilder.RenameColumn(
                name: "purchase_type",
                table: "license_purchases",
                newName: "acquisition_type");

            migrationBuilder.RenameTable(
                name: "license_purchases",
                newName: "license_acquisitions");

            migrationBuilder.AddForeignKey(
                name: "FK_license_packages_license_acquisitions_acquisition_id",
                table: "license_packages",
                column: "acquisition_id",
                principalTable: "license_acquisitions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
