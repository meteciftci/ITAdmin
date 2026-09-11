using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAdmin.Persistence.Migrations;

public partial class AddDnsManagementFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE EXTENSION IF NOT EXISTS pgcrypto;

            CREATE TABLE dns_credential_profiles (
                id uuid PRIMARY KEY,
                name varchar(150) NOT NULL,
                authentication_mode varchar(32) NOT NULL,
                user_name varchar(256) NOT NULL,
                encrypted_password text NOT NULL,
                is_enabled boolean NOT NULL,
                last_validated_at timestamptz NULL,
                last_validation_status varchar(32) NULL,
                last_validation_message varchar(2000) NULL,
                created_at timestamptz NOT NULL,
                created_by varchar(100) NULL,
                updated_at timestamptz NULL,
                updated_by varchar(100) NULL
            );

            CREATE TABLE dns_management_settings (
                id uuid PRIMARY KEY,
                is_enabled boolean NOT NULL,
                automatic_sync_enabled boolean NOT NULL,
                default_sync_interval_minutes integer NOT NULL,
                health_check_interval_minutes integer NOT NULL,
                command_timeout_seconds integer NOT NULL,
                max_parallel_servers integer NOT NULL,
                snapshot_retention_days integer NOT NULL,
                sync_record_inventory boolean NOT NULL,
                prompt_for_full_sync_on_comparison_open boolean NOT NULL,
                comparison_snapshot_stale_after_minutes integer NOT NULL,
                created_at timestamptz NOT NULL,
                created_by varchar(100) NULL,
                updated_at timestamptz NULL,
                updated_by varchar(100) NULL,
                CONSTRAINT ck_dns_settings_sync_interval CHECK (default_sync_interval_minutes BETWEEN 1 AND 1440),
                CONSTRAINT ck_dns_settings_health_interval CHECK (health_check_interval_minutes BETWEEN 1 AND 1440),
                CONSTRAINT ck_dns_settings_command_timeout CHECK (command_timeout_seconds BETWEEN 5 AND 300),
                CONSTRAINT ck_dns_settings_parallel_servers CHECK (max_parallel_servers BETWEEN 1 AND 20),
                CONSTRAINT ck_dns_settings_retention CHECK (snapshot_retention_days BETWEEN 1 AND 3650),
                CONSTRAINT ck_dns_settings_stale_after CHECK (comparison_snapshot_stale_after_minutes BETWEEN 1 AND 10080)
            );

            CREATE TABLE dns_servers (
                id uuid PRIMARY KEY,
                display_name varchar(150) NOT NULL,
                host_name varchar(253) NOT NULL,
                port integer NOT NULL,
                environment varchar(32) NOT NULL,
                dns_credential_profile_id uuid NOT NULL,
                is_enabled boolean NOT NULL,
                sync_interval_minutes integer NULL,
                tls_certificate_thumbprint varchar(128) NULL,
                notes varchar(2000) NULL,
                operating_system_version varchar(128) NULL,
                dns_server_version varchar(128) NULL,
                capabilities_json text NULL,
                last_seen_at timestamptz NULL,
                last_successful_sync_at timestamptz NULL,
                last_sync_status varchar(32) NULL,
                last_sync_message varchar(2000) NULL,
                created_at timestamptz NOT NULL,
                created_by varchar(100) NULL,
                updated_at timestamptz NULL,
                updated_by varchar(100) NULL,
                CONSTRAINT fk_dns_servers_credentials FOREIGN KEY (dns_credential_profile_id)
                    REFERENCES dns_credential_profiles (id) ON DELETE RESTRICT,
                CONSTRAINT ck_dns_servers_port CHECK (port BETWEEN 1 AND 65535),
                CONSTRAINT ck_dns_servers_sync_interval CHECK (
                    sync_interval_minutes IS NULL OR sync_interval_minutes BETWEEN 1 AND 1440)
            );

            CREATE TABLE dns_inventory_snapshots (
                id uuid PRIMARY KEY,
                dns_server_id uuid NOT NULL,
                version uuid NOT NULL,
                scope varchar(32) NOT NULL,
                trigger varchar(32) NOT NULL,
                status varchar(32) NOT NULL,
                is_active boolean NOT NULL,
                started_at timestamptz NOT NULL,
                completed_at timestamptz NULL,
                zone_count integer NOT NULL,
                record_count integer NOT NULL,
                error_code varchar(64) NULL,
                message varchar(2000) NULL,
                requested_by_user_id uuid NULL,
                requested_by_user_name varchar(100) NULL,
                correlation_id varchar(64) NULL,
                CONSTRAINT fk_dns_inventory_snapshots_server FOREIGN KEY (dns_server_id)
                    REFERENCES dns_servers (id) ON DELETE RESTRICT,
                CONSTRAINT ck_dns_snapshots_active_completed CHECK (
                    NOT is_active OR (status = 'Completed' AND completed_at IS NOT NULL)),
                CONSTRAINT ck_dns_snapshots_counts CHECK (zone_count >= 0 AND record_count >= 0)
            );

            CREATE TABLE dns_operation_logs (
                id uuid PRIMARY KEY,
                dns_server_id uuid NULL,
                server_display_name varchar(150) NULL,
                operation_type varchar(64) NOT NULL,
                status varchar(32) NOT NULL,
                zone_name varchar(253) NULL,
                record_name varchar(512) NULL,
                record_type varchar(32) NULL,
                request_summary_json text NULL,
                before_snapshot_json text NULL,
                after_snapshot_json text NULL,
                error_code varchar(64) NULL,
                error_message varchar(2000) NULL,
                actor_user_id uuid NULL,
                actor_user_name varchar(100) NULL,
                ip_address varchar(64) NULL,
                user_agent varchar(1024) NULL,
                correlation_id varchar(64) NULL,
                created_at timestamptz NOT NULL,
                CONSTRAINT fk_dns_operation_logs_server FOREIGN KEY (dns_server_id)
                    REFERENCES dns_servers (id) ON DELETE SET NULL
            );

            CREATE TABLE dns_sync_jobs (
                id uuid PRIMARY KEY,
                batch_id uuid NOT NULL,
                dns_server_id uuid NOT NULL,
                scope varchar(32) NOT NULL,
                trigger varchar(32) NOT NULL,
                status varchar(32) NOT NULL,
                zone_name varchar(253) NULL,
                dedupe_key varchar(600) NOT NULL,
                priority integer NOT NULL,
                attempt_count integer NOT NULL,
                requested_at timestamptz NOT NULL,
                started_at timestamptz NULL,
                completed_at timestamptz NULL,
                lease_expires_at timestamptz NULL,
                lease_owner varchar(128) NULL,
                requested_by_user_id uuid NULL,
                requested_by_user_name varchar(100) NULL,
                correlation_id varchar(64) NULL,
                error_code varchar(64) NULL,
                message varchar(2000) NULL,
                CONSTRAINT fk_dns_sync_jobs_server FOREIGN KEY (dns_server_id)
                    REFERENCES dns_servers (id) ON DELETE RESTRICT,
                CONSTRAINT ck_dns_sync_jobs_attempt_count CHECK (attempt_count >= 0),
                CONSTRAINT ck_dns_sync_jobs_priority CHECK (priority >= 0)
            );

            CREATE TABLE dns_zone_snapshots (
                id uuid PRIMARY KEY,
                dns_inventory_snapshot_id uuid NOT NULL,
                name varchar(253) NOT NULL,
                zone_type varchar(64) NOT NULL,
                is_reverse_lookup_zone boolean NOT NULL,
                is_ds_integrated boolean NOT NULL,
                is_signed boolean NOT NULL,
                is_paused boolean NOT NULL,
                dynamic_update varchar(64) NULL,
                replication_scope varchar(64) NULL,
                directory_partition_name varchar(512) NULL,
                zone_file varchar(512) NULL,
                virtualization_instance varchar(128) NULL,
                properties_json text NULL,
                CONSTRAINT fk_dns_zone_snapshots_inventory FOREIGN KEY (dns_inventory_snapshot_id)
                    REFERENCES dns_inventory_snapshots (id) ON DELETE CASCADE
            );

            CREATE TABLE dns_record_snapshots (
                id uuid PRIMARY KEY,
                dns_zone_snapshot_id uuid NOT NULL,
                relative_name varchar(253) NOT NULL,
                fully_qualified_name varchar(512) NOT NULL,
                record_type varchar(32) NOT NULL,
                canonical_value text NOT NULL,
                record_data_json text NOT NULL,
                time_to_live_seconds integer NOT NULL,
                timestamp timestamptz NULL,
                zone_scope varchar(128) NULL,
                virtualization_instance varchar(128) NULL,
                record_hash varchar(64) NOT NULL,
                CONSTRAINT fk_dns_record_snapshots_zone FOREIGN KEY (dns_zone_snapshot_id)
                    REFERENCES dns_zone_snapshots (id) ON DELETE CASCADE,
                CONSTRAINT ck_dns_record_snapshots_ttl CHECK (time_to_live_seconds >= 0)
            );

            CREATE UNIQUE INDEX "IX_dns_credential_profiles_name" ON dns_credential_profiles (name);
            CREATE UNIQUE INDEX "IX_dns_servers_display_name" ON dns_servers (display_name);
            CREATE INDEX "IX_dns_servers_dns_credential_profile_id" ON dns_servers (dns_credential_profile_id);
            CREATE UNIQUE INDEX "IX_dns_servers_host_name_port" ON dns_servers (host_name, port);
            CREATE INDEX "IX_dns_servers_is_enabled" ON dns_servers (is_enabled);
            CREATE UNIQUE INDEX "IX_dns_inventory_snapshots_dns_server_id" ON dns_inventory_snapshots (dns_server_id) WHERE is_active;
            CREATE INDEX "IX_dns_inventory_snapshots_dns_server_id_started_at" ON dns_inventory_snapshots (dns_server_id, started_at);
            CREATE UNIQUE INDEX "IX_dns_inventory_snapshots_version" ON dns_inventory_snapshots (version);
            CREATE INDEX "IX_dns_operation_logs_actor_user_id" ON dns_operation_logs (actor_user_id);
            CREATE INDEX "IX_dns_operation_logs_created_at" ON dns_operation_logs (created_at);
            CREATE INDEX "IX_dns_operation_logs_dns_server_id_created_at" ON dns_operation_logs (dns_server_id, created_at);
            CREATE INDEX "IX_dns_operation_logs_operation_type" ON dns_operation_logs (operation_type);
            CREATE INDEX "IX_dns_sync_jobs_batch_id" ON dns_sync_jobs (batch_id);
            CREATE UNIQUE INDEX "IX_dns_sync_jobs_dedupe_key" ON dns_sync_jobs (dedupe_key) WHERE status IN ('Pending', 'Running');
            CREATE INDEX "IX_dns_sync_jobs_dns_server_id" ON dns_sync_jobs (dns_server_id);
            CREATE INDEX "IX_dns_sync_jobs_status_priority_requested_at" ON dns_sync_jobs (status, priority, requested_at);
            CREATE INDEX "IX_dns_zone_snapshots_dns_inventory_snapshot_id_name" ON dns_zone_snapshots (dns_inventory_snapshot_id, name);
            CREATE INDEX "IX_dns_record_snapshots_dns_zone_snapshot_id_relative_name_record_type" ON dns_record_snapshots (dns_zone_snapshot_id, relative_name, record_type);
            CREATE INDEX "IX_dns_record_snapshots_record_hash" ON dns_record_snapshots (record_hash);

            INSERT INTO dns_management_settings (
                id, is_enabled, automatic_sync_enabled, default_sync_interval_minutes,
                health_check_interval_minutes, command_timeout_seconds, max_parallel_servers,
                snapshot_retention_days, sync_record_inventory,
                prompt_for_full_sync_on_comparison_open,
                comparison_snapshot_stale_after_minutes, created_at, created_by)
            VALUES (gen_random_uuid(), FALSE, TRUE, 15, 5, 30, 3, 30, TRUE, TRUE, 15, NOW(), 'migration');

            INSERT INTO portal_permissions (
                id, module, code, description, is_active, created_at, created_by, is_deleted)
            SELECT gen_random_uuid(), 'DnsManagement', value.code, value.description,
                   TRUE, NOW(), 'migration', FALSE
            FROM (VALUES
                ('DnsManagement.View', 'View DNS management.'),
                ('DnsManagement.ManageSettings', 'Manage DNS management settings.'),
                ('DnsManagement.Servers.View', 'View registered DNS servers.'),
                ('DnsManagement.Servers.Manage', 'Manage registered DNS servers and credential profiles.'),
                ('DnsManagement.Servers.TestConnection', 'Test DNS server connections.'),
                ('DnsManagement.Zones.View', 'View DNS zones.'),
                ('DnsManagement.Zones.Create', 'Create DNS zones.'),
                ('DnsManagement.Zones.Update', 'Update DNS zones.'),
                ('DnsManagement.Zones.Delete', 'Delete DNS zones.'),
                ('DnsManagement.Records.View', 'View DNS records.'),
                ('DnsManagement.Records.Create', 'Create DNS records.'),
                ('DnsManagement.Records.Update', 'Update DNS records.'),
                ('DnsManagement.Records.Delete', 'Delete DNS records.'),
                ('DnsManagement.Compare', 'Compare DNS inventory snapshots across servers.'),
                ('DnsManagement.Export', 'Export DNS inventory and comparison results.'),
                ('DnsManagement.Synchronize', 'Synchronize DNS server inventory.'),
                ('DnsManagement.ServerSettings.Manage', 'Manage DNS server-wide settings.'),
                ('DnsManagement.Dnssec.Manage', 'Manage DNSSEC configuration.'),
                ('DnsManagement.Policies.Manage', 'Manage DNS policies, scopes, and client subnets.'),
                ('DnsManagement.Cache.Clear', 'Clear DNS server cache.'),
                ('DnsManagement.OperationLogs.View', 'View DNS operation logs.')
            ) AS value(code, description)
            WHERE NOT EXISTS (
                SELECT 1 FROM portal_permissions permission WHERE permission.code = value.code
            );

            INSERT INTO portal_role_permissions (
                id, portal_role_id, portal_permission_id, created_at, created_by)
            SELECT gen_random_uuid(), role.id, permission.id, NOW(), 'migration'
            FROM portal_roles role
            CROSS JOIN portal_permissions permission
            WHERE role.code = 'Administrator'
              AND permission.module = 'DnsManagement'
              AND NOT EXISTS (
                  SELECT 1 FROM portal_role_permissions role_permission
                  WHERE role_permission.portal_role_id = role.id
                    AND role_permission.portal_permission_id = permission.id
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM portal_role_permissions
            WHERE portal_permission_id IN (
                SELECT id FROM portal_permissions WHERE module = 'DnsManagement'
            );
            DELETE FROM portal_permissions WHERE module = 'DnsManagement';

            DROP TABLE dns_management_settings;
            DROP TABLE dns_operation_logs;
            DROP TABLE dns_record_snapshots;
            DROP TABLE dns_sync_jobs;
            DROP TABLE dns_zone_snapshots;
            DROP TABLE dns_inventory_snapshots;
            DROP TABLE dns_servers;
            DROP TABLE dns_credential_profiles;
            """);
    }
}
