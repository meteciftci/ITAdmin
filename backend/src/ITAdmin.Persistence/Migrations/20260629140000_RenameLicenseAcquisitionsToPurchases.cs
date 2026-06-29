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
            migrationBuilder.Sql(
                """
                DO $migration$
                DECLARE
                    package_fk RECORD;
                BEGIN
                    IF to_regclass('public.license_packages') IS NOT NULL THEN
                        FOR package_fk IN
                            SELECT c.conname
                            FROM pg_constraint c
                            JOIN pg_class t ON c.conrelid = t.oid
                            JOIN pg_namespace n ON t.relnamespace = n.oid
                            WHERE n.nspname = 'public'
                              AND t.relname = 'license_packages'
                              AND c.contype = 'f'
                              AND (
                                  c.conname LIKE 'FK_license_packages_license_acquisitions%'
                                  OR c.conname LIKE 'FK_license_packages_license_purchases%'
                              )
                        LOOP
                            EXECUTE format(
                                'ALTER TABLE license_packages DROP CONSTRAINT IF EXISTS %I',
                                package_fk.conname);
                        END LOOP;
                    END IF;

                    IF to_regclass('public.license_acquisitions') IS NOT NULL THEN
                        ALTER TABLE license_acquisitions RENAME TO license_purchases;
                    END IF;

                    IF to_regclass('public.license_purchases') IS NOT NULL THEN
                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_schema = 'public'
                              AND table_name = 'license_purchases'
                              AND column_name = 'acquisition_type'
                        ) THEN
                            ALTER TABLE license_purchases RENAME COLUMN acquisition_type TO purchase_type;
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_schema = 'public'
                              AND table_name = 'license_purchases'
                              AND column_name = 'acquisition_date'
                        ) THEN
                            ALTER TABLE license_purchases RENAME COLUMN acquisition_date TO purchase_date;
                        END IF;

                        IF EXISTS (
                            SELECT 1 FROM pg_constraint
                            WHERE conname = 'PK_license_acquisitions'
                        ) THEN
                            ALTER TABLE license_purchases
                                RENAME CONSTRAINT "PK_license_acquisitions" TO "PK_license_purchases";
                        END IF;

                        IF EXISTS (
                            SELECT 1 FROM pg_constraint
                            WHERE conname = 'FK_license_acquisitions_license_companies_supplier_company_id'
                        ) THEN
                            ALTER TABLE license_purchases
                                RENAME CONSTRAINT "FK_license_acquisitions_license_companies_supplier_company_id"
                                TO "FK_license_purchases_license_companies_supplier_company_id";
                        END IF;

                        IF EXISTS (
                            SELECT 1 FROM pg_constraint
                            WHERE conname = 'FK_license_acquisitions_license_companies_support_company_id'
                        ) THEN
                            ALTER TABLE license_purchases
                                RENAME CONSTRAINT "FK_license_acquisitions_license_companies_support_company_id"
                                TO "FK_license_purchases_license_companies_support_company_id";
                        END IF;
                    END IF;

                    IF to_regclass('public.license_packages') IS NOT NULL THEN
                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_schema = 'public'
                              AND table_name = 'license_packages'
                              AND column_name = 'acquisition_id'
                        ) THEN
                            ALTER TABLE license_packages RENAME COLUMN acquisition_id TO purchase_id;
                        END IF;

                        IF NOT EXISTS (
                            SELECT 1 FROM pg_constraint
                            WHERE conname = 'FK_license_packages_license_purchases_purchase_id'
                        ) THEN
                            ALTER TABLE license_packages
                                ADD CONSTRAINT "FK_license_packages_license_purchases_purchase_id"
                                FOREIGN KEY (purchase_id)
                                REFERENCES license_purchases (id)
                                ON DELETE RESTRICT;
                        END IF;
                    END IF;
                END
                $migration$;
                """);

            migrationBuilder.Sql(
                """
                ALTER INDEX IF EXISTS "IX_license_acquisitions_acquisition_type"
                    RENAME TO "IX_license_purchases_purchase_type";
                ALTER INDEX IF EXISTS "IX_license_acquisitions_status"
                    RENAME TO "IX_license_purchases_status";
                ALTER INDEX IF EXISTS "IX_license_acquisitions_supplier_company_id"
                    RENAME TO "IX_license_purchases_supplier_company_id";
                ALTER INDEX IF EXISTS "IX_license_acquisitions_support_company_id"
                    RENAME TO "IX_license_purchases_support_company_id";
                ALTER INDEX IF EXISTS "IX_license_packages_acquisition_id"
                    RENAME TO "IX_license_packages_purchase_id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $migration$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_license_packages_license_purchases_purchase_id'
                    ) THEN
                        ALTER TABLE license_packages
                            DROP CONSTRAINT "FK_license_packages_license_purchases_purchase_id";
                    END IF;

                    IF to_regclass('public.license_purchases') IS NOT NULL THEN
                        IF EXISTS (
                            SELECT 1 FROM pg_constraint
                            WHERE conname = 'PK_license_purchases'
                        ) THEN
                            ALTER TABLE license_purchases
                                RENAME CONSTRAINT "PK_license_purchases" TO "PK_license_acquisitions";
                        END IF;

                        IF EXISTS (
                            SELECT 1 FROM pg_constraint
                            WHERE conname = 'FK_license_purchases_license_companies_supplier_company_id'
                        ) THEN
                            ALTER TABLE license_purchases
                                RENAME CONSTRAINT "FK_license_purchases_license_companies_supplier_company_id"
                                TO "FK_license_acquisitions_license_companies_supplier_company_id";
                        END IF;

                        IF EXISTS (
                            SELECT 1 FROM pg_constraint
                            WHERE conname = 'FK_license_purchases_license_companies_support_company_id'
                        ) THEN
                            ALTER TABLE license_purchases
                                RENAME CONSTRAINT "FK_license_purchases_license_companies_support_company_id"
                                TO "FK_license_acquisitions_license_companies_support_company_id";
                        END IF;
                    END IF;
                END
                $migration$;
                """);

            migrationBuilder.Sql(
                """
                ALTER INDEX IF EXISTS "IX_license_packages_purchase_id"
                    RENAME TO "IX_license_packages_acquisition_id";
                ALTER INDEX IF EXISTS "IX_license_purchases_support_company_id"
                    RENAME TO "IX_license_acquisitions_support_company_id";
                ALTER INDEX IF EXISTS "IX_license_purchases_supplier_company_id"
                    RENAME TO "IX_license_acquisitions_supplier_company_id";
                ALTER INDEX IF EXISTS "IX_license_purchases_status"
                    RENAME TO "IX_license_acquisitions_status";
                ALTER INDEX IF EXISTS "IX_license_purchases_purchase_type"
                    RENAME TO "IX_license_acquisitions_acquisition_type";
                """);

            migrationBuilder.Sql(
                """
                DO $migration$
                BEGIN
                    IF to_regclass('public.license_packages') IS NOT NULL
                       AND EXISTS (
                           SELECT 1
                           FROM information_schema.columns
                           WHERE table_schema = 'public'
                             AND table_name = 'license_packages'
                             AND column_name = 'purchase_id'
                       ) THEN
                        ALTER TABLE license_packages RENAME COLUMN purchase_id TO acquisition_id;
                    END IF;

                    IF to_regclass('public.license_purchases') IS NOT NULL THEN
                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_schema = 'public'
                              AND table_name = 'license_purchases'
                              AND column_name = 'purchase_date'
                        ) THEN
                            ALTER TABLE license_purchases RENAME COLUMN purchase_date TO acquisition_date;
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_schema = 'public'
                              AND table_name = 'license_purchases'
                              AND column_name = 'purchase_type'
                        ) THEN
                            ALTER TABLE license_purchases RENAME COLUMN purchase_type TO acquisition_type;
                        END IF;

                        ALTER TABLE license_purchases RENAME TO license_acquisitions;
                    END IF;

                    IF to_regclass('public.license_packages') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1 FROM pg_constraint
                           WHERE conname = 'FK_license_packages_license_acquisitions_acquisition_id'
                       ) THEN
                        ALTER TABLE license_packages
                            ADD CONSTRAINT "FK_license_packages_license_acquisitions_acquisition_id"
                            FOREIGN KEY (acquisition_id)
                            REFERENCES license_acquisitions (id)
                            ON DELETE RESTRICT;
                    END IF;
                END
                $migration$;
                """);
        }
    }
}
