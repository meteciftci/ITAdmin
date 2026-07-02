using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                    is_searchable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                    default_user_creation_upn_suffix = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    netbios_domain_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    default_naming_context = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    base_dn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    users_root_ou = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    disabled_users_ou = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    default_user_ou = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    default_group_ou = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    default_computer_ou = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    deleted_objects_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    groups_search_base = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    computers_search_base = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    preferred_domain_controllers_json = table.Column<string>(type: "text", nullable: true),
                    service_account_user_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    encrypted_service_account_password = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    powershell_health_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    powershell_timeout_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    last_validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_validation_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    last_validation_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    notification_settings_json = table.Column<string>(type: "text", nullable: true),
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

            migrationBuilder.CreateTable(
                name: "application_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "text", nullable: true),
                    value_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_encrypted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_user_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ldap_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    host = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    base_dn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    user_search_base = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    user_search_filter = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    bind_user_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    bind_user_domain = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    encrypted_bind_password = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ldap_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "license_companies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    website = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                name: "license_management_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "TRY"),
                    default_vat_included = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    default_renewal_reminder_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    default_renewal_recipients = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    default_renewal_cc_recipients = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_management_settings", x => x.id);
                });

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

            migrationBuilder.CreateTable(
                name: "license_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    request_date = table.Column<DateOnly>(type: "date", nullable: false),
                    external_request_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ebys_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ebys_date = table.Column<DateOnly>(type: "date", nullable: true),
                    requester_unit_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    requester_unit_distinguished_name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    requester_unit_object_guid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    requester_manager_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                name: "notification_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    recipient = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    recipient_masked = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    max_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    provider_summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    related_module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    related_event = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    related_entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    related_entity_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_provider_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    public_settings_json = table.Column<string>(type: "text", nullable: true),
                    encrypted_secret_settings_json = table.Column<string>(type: "text", nullable: true),
                    last_validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_validation_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    last_validation_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_provider_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    subject_template = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    body_template = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "portal_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portal_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "portal_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portal_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "portal_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    directory_source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    directory_object_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    preferred_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "tr"),
                    national_id_encrypted = table.Column<string>(type: "text", nullable: true),
                    national_id_masked = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    user_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portal_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "security_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "license_purchases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    purchase_date = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_license_purchases", x => x.id);
                    table.ForeignKey(
                        name: "FK_license_purchases_license_companies_supplier_company_id",
                        column: x => x.supplier_company_id,
                        principalTable: "license_companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_license_purchases_license_companies_support_company_id",
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
                    brand = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_licensed_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_licensed_products_license_product_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "license_product_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "portal_role_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    portal_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    portal_permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portal_role_permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_portal_role_permissions_portal_permissions_portal_permissio~",
                        column: x => x.portal_permission_id,
                        principalTable: "portal_permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_portal_role_permissions_portal_roles_portal_role_id",
                        column: x => x.portal_role_id,
                        principalTable: "portal_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "portal_user_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    portal_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    portal_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portal_user_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_portal_user_roles_portal_roles_portal_role_id",
                        column: x => x.portal_role_id,
                        principalTable: "portal_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_portal_user_roles_portal_users_portal_user_id",
                        column: x => x.portal_user_id,
                        principalTable: "portal_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    portal_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_by_ip = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    revoked_by_ip = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_persistent = table.Column<bool>(type: "boolean", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_portal_users_portal_user_id",
                        column: x => x.portal_user_id,
                        principalTable: "portal_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "license_packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                        name: "FK_license_packages_license_purchases_purchase_id",
                        column: x => x.purchase_id,
                        principalTable: "license_purchases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_license_packages_licensed_products_product_id",
                        column: x => x.product_id,
                        principalTable: "licensed_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateIndex(
                name: "IX_application_settings_key",
                table: "application_settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_actor_user_id",
                table: "audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_created_at",
                table: "audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity_name_entity_id",
                table: "audit_logs",
                columns: new[] { "entity_name", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ldap_settings_is_active",
                table: "ldap_settings",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_ldap_settings_name",
                table: "ldap_settings",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_license_companies_is_active",
                table: "license_companies",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_license_companies_name",
                table: "license_companies",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_license_packages_is_active",
                table: "license_packages",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_license_packages_product_id",
                table: "license_packages",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_packages_purchase_id",
                table: "license_packages",
                column: "purchase_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_packages_status",
                table: "license_packages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_license_product_categories_is_active",
                table: "license_product_categories",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_license_product_categories_name",
                table: "license_product_categories",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_license_purchases_purchase_type",
                table: "license_purchases",
                column: "purchase_type");

            migrationBuilder.CreateIndex(
                name: "IX_license_purchases_status",
                table: "license_purchases",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_license_purchases_supplier_company_id",
                table: "license_purchases",
                column: "supplier_company_id");

            migrationBuilder.CreateIndex(
                name: "IX_license_purchases_support_company_id",
                table: "license_purchases",
                column: "support_company_id");

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
                name: "IX_license_requests_request_source",
                table: "license_requests",
                column: "request_source");

            migrationBuilder.CreateIndex(
                name: "IX_license_requests_requester_unit_object_guid",
                table: "license_requests",
                column: "requester_unit_object_guid");

            migrationBuilder.CreateIndex(
                name: "IX_license_requests_status",
                table: "license_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_licensed_products_category_id",
                table: "licensed_products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_licensed_products_is_active",
                table: "licensed_products",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_licensed_products_name",
                table: "licensed_products",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_channel_status",
                table: "notification_outbox",
                columns: new[] { "channel", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_correlation_id",
                table: "notification_outbox",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_created_at",
                table: "notification_outbox",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_related_module_related_event",
                table: "notification_outbox",
                columns: new[] { "related_module", "related_event" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_status_next_attempt_at",
                table: "notification_outbox",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_provider_settings_channel_provider_key",
                table: "notification_provider_settings",
                columns: new[] { "channel", "provider_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_templates_module_key_event_key_channel",
                table: "notification_templates",
                columns: new[] { "module_key", "event_key", "channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_portal_permissions_code",
                table: "portal_permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_portal_role_permissions_portal_permission_id",
                table: "portal_role_permissions",
                column: "portal_permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_portal_role_permissions_portal_role_id_portal_permission_id",
                table: "portal_role_permissions",
                columns: new[] { "portal_role_id", "portal_permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_portal_roles_code",
                table: "portal_roles",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_portal_user_roles_portal_role_id",
                table: "portal_user_roles",
                column: "portal_role_id");

            migrationBuilder.CreateIndex(
                name: "IX_portal_user_roles_portal_user_id_portal_role_id",
                table: "portal_user_roles",
                columns: new[] { "portal_user_id", "portal_role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_portal_users_directory_object_id",
                table: "portal_users",
                column: "directory_object_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_portal_users_email",
                table: "portal_users",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_portal_users_user_name",
                table: "portal_users",
                column: "user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_portal_user_id",
                table: "refresh_tokens",
                column: "portal_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_security_logs_created_at",
                table: "security_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_security_logs_event_type",
                table: "security_logs",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "IX_security_logs_severity",
                table: "security_logs",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "IX_security_logs_user_id",
                table: "security_logs",
                column: "user_id");
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

            migrationBuilder.DropTable(
                name: "application_settings");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "ldap_settings");

            migrationBuilder.DropTable(
                name: "license_management_settings");

            migrationBuilder.DropTable(
                name: "license_packages");

            migrationBuilder.DropTable(
                name: "license_request_item_users");

            migrationBuilder.DropTable(
                name: "notification_outbox");

            migrationBuilder.DropTable(
                name: "notification_provider_settings");

            migrationBuilder.DropTable(
                name: "notification_templates");

            migrationBuilder.DropTable(
                name: "portal_role_permissions");

            migrationBuilder.DropTable(
                name: "portal_user_roles");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "security_logs");

            migrationBuilder.DropTable(
                name: "license_purchases");

            migrationBuilder.DropTable(
                name: "license_request_items");

            migrationBuilder.DropTable(
                name: "portal_permissions");

            migrationBuilder.DropTable(
                name: "portal_roles");

            migrationBuilder.DropTable(
                name: "portal_users");

            migrationBuilder.DropTable(
                name: "license_companies");

            migrationBuilder.DropTable(
                name: "license_requests");

            migrationBuilder.DropTable(
                name: "licensed_products");

            migrationBuilder.DropTable(
                name: "license_product_categories");
        }
    }
}
