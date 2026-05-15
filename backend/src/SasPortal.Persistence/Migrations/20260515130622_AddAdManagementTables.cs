using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SasPortal.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdManagementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ad_attribute_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    logical_field = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    attribute_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_editable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_sensitive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    validation_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "None"),
                    masking_strategy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "None"),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_attribute_mappings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ad_management_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    domain_fqdn = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    netbios_domain_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    default_naming_context = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    base_dn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    users_root_ou = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    disabled_users_ou = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    groups_search_base = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    computers_search_base = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    preferred_domain_controllers_json = table.Column<string>(type: "text", nullable: true),
                    use_ssl = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ldap_port = table.Column<int>(type: "integer", nullable: false, defaultValue: 636),
                    service_account_user_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    encrypted_service_account_password = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    powershell_health_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    powershell_timeout_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    last_validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_validation_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    last_validation_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_management_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ad_operation_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_object_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    target_distinguished_name = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    target_object_guid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    target_sam_account_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    domain_controller = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    request_summary_json = table.Column<string>(type: "text", nullable: true),
                    before_snapshot_json = table.Column<string>(type: "text", nullable: true),
                    after_snapshot_json = table.Column<string>(type: "text", nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_user_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_operation_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ad_attribute_mappings_logical_field",
                table: "ad_attribute_mappings",
                column: "logical_field",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ad_attribute_mappings_sort_order",
                table: "ad_attribute_mappings",
                column: "sort_order");

            migrationBuilder.CreateIndex(
                name: "IX_ad_operation_logs_actor_user_id",
                table: "ad_operation_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ad_operation_logs_created_at",
                table: "ad_operation_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_ad_operation_logs_operation_type",
                table: "ad_operation_logs",
                column: "operation_type");

            migrationBuilder.CreateIndex(
                name: "IX_ad_operation_logs_target_sam_account_name",
                table: "ad_operation_logs",
                column: "target_sam_account_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ad_attribute_mappings");

            migrationBuilder.DropTable(
                name: "ad_management_settings");

            migrationBuilder.DropTable(
                name: "ad_operation_logs");
        }
    }
}
