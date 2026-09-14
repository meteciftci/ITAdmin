using ITAdmin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITAdmin.Persistence.Configurations;

public sealed class DnsManagementSettingsConfiguration : IEntityTypeConfiguration<DnsManagementSettings>
{
    public void Configure(EntityTypeBuilder<DnsManagementSettings> builder)
    {
        builder.ToTable("dns_management_settings", table =>
        {
            table.HasCheckConstraint("ck_dns_settings_sync_interval", "default_sync_interval_minutes BETWEEN 1 AND 1440");
            table.HasCheckConstraint("ck_dns_settings_health_interval", "health_check_interval_minutes BETWEEN 1 AND 1440");
            table.HasCheckConstraint("ck_dns_settings_command_timeout", "command_timeout_seconds BETWEEN 5 AND 300");
            table.HasCheckConstraint("ck_dns_settings_parallel_servers", "max_parallel_servers BETWEEN 1 AND 20");
            table.HasCheckConstraint("ck_dns_settings_retention", "snapshot_retention_days BETWEEN 1 AND 3650");
            table.HasCheckConstraint("ck_dns_settings_stale_after", "comparison_snapshot_stale_after_minutes BETWEEN 1 AND 10080");
        });
        ConfigureAuditable(builder);
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.Property(x => x.AutomaticSyncEnabled).HasColumnName("automatic_sync_enabled");
        builder.Property(x => x.DefaultSyncIntervalMinutes).HasColumnName("default_sync_interval_minutes");
        builder.Property(x => x.HealthCheckIntervalMinutes).HasColumnName("health_check_interval_minutes");
        builder.Property(x => x.CommandTimeoutSeconds).HasColumnName("command_timeout_seconds");
        builder.Property(x => x.MaxParallelServers).HasColumnName("max_parallel_servers");
        builder.Property(x => x.SnapshotRetentionDays).HasColumnName("snapshot_retention_days");
        builder.Property(x => x.SyncRecordInventory).HasColumnName("sync_record_inventory");
        builder.Property(x => x.PromptForFullSyncOnComparisonOpen).HasColumnName("prompt_for_full_sync_on_comparison_open");
        builder.Property(x => x.ComparisonSnapshotStaleAfterMinutes).HasColumnName("comparison_snapshot_stale_after_minutes");
    }

    private static void ConfigureAuditable(EntityTypeBuilder<DnsManagementSettings> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
    }
}

public sealed class DnsCredentialProfileConfiguration : IEntityTypeConfiguration<DnsCredentialProfile>
{
    public void Configure(EntityTypeBuilder<DnsCredentialProfile> builder)
    {
        builder.ToTable("dns_credential_profiles");
        ConfigureAuditable(builder);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.AuthenticationMode).HasColumnName("authentication_mode").HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.UserName).HasColumnName("user_name").HasMaxLength(256).IsRequired();
        builder.Property(x => x.EncryptedPassword).HasColumnName("encrypted_password").IsRequired();
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.Property(x => x.LastValidatedAt).HasColumnName("last_validated_at");
        builder.Property(x => x.LastValidationStatus).HasColumnName("last_validation_status").HasMaxLength(32);
        builder.Property(x => x.LastValidationMessage).HasColumnName("last_validation_message").HasMaxLength(2000);
        builder.HasIndex(x => x.Name).IsUnique();
    }

    private static void ConfigureAuditable(EntityTypeBuilder<DnsCredentialProfile> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
    }
}

public sealed class DnsServerConfiguration : IEntityTypeConfiguration<DnsServer>
{
    public void Configure(EntityTypeBuilder<DnsServer> builder)
    {
        builder.ToTable("dns_servers", table =>
        {
            table.HasCheckConstraint("ck_dns_servers_port", "port BETWEEN 1 AND 65535");
            table.HasCheckConstraint("ck_dns_servers_sync_interval", "sync_interval_minutes IS NULL OR sync_interval_minutes BETWEEN 1 AND 1440");
        });
        ConfigureAuditable(builder);
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.HostName).HasColumnName("host_name").HasMaxLength(253).IsRequired();
        builder.Property(x => x.Port).HasColumnName("port");
        builder.Property(x => x.Transport).HasColumnName("transport").HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Environment).HasColumnName("environment").HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.DnsCredentialProfileId).HasColumnName("dns_credential_profile_id");
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.Property(x => x.SyncIntervalMinutes).HasColumnName("sync_interval_minutes");
        builder.Property(x => x.TlsCertificateThumbprint).HasColumnName("tls_certificate_thumbprint").HasMaxLength(128);
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(x => x.OperatingSystemVersion).HasColumnName("operating_system_version").HasMaxLength(128);
        builder.Property(x => x.DnsServerVersion).HasColumnName("dns_server_version").HasMaxLength(128);
        builder.Property(x => x.CapabilitiesJson).HasColumnName("capabilities_json");
        builder.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
        builder.Property(x => x.LastSuccessfulSyncAt).HasColumnName("last_successful_sync_at");
        builder.Property(x => x.LastSyncStatus).HasColumnName("last_sync_status").HasMaxLength(32);
        builder.Property(x => x.LastSyncMessage).HasColumnName("last_sync_message").HasMaxLength(2000);
        builder.HasOne(x => x.CredentialProfile).WithMany(x => x.Servers)
            .HasForeignKey(x => x.DnsCredentialProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.DisplayName).IsUnique();
        builder.HasIndex(x => new { x.HostName, x.Port }).IsUnique();
        builder.HasIndex(x => x.IsEnabled);
    }

    private static void ConfigureAuditable(EntityTypeBuilder<DnsServer> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
    }
}

public sealed class DnsInventorySnapshotConfiguration : IEntityTypeConfiguration<DnsInventorySnapshot>
{
    public void Configure(EntityTypeBuilder<DnsInventorySnapshot> builder)
    {
        builder.ToTable("dns_inventory_snapshots", table =>
        {
            table.HasCheckConstraint("ck_dns_snapshots_active_completed", "NOT is_active OR (status = 'Completed' AND completed_at IS NOT NULL)");
            table.HasCheckConstraint("ck_dns_snapshots_counts", "zone_count >= 0 AND record_count >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.DnsServerId).HasColumnName("dns_server_id");
        builder.Property(x => x.Version).HasColumnName("version");
        builder.Property(x => x.Scope).HasColumnName("scope").HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Trigger).HasColumnName("trigger").HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.ZoneCount).HasColumnName("zone_count");
        builder.Property(x => x.RecordCount).HasColumnName("record_count");
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(64);
        builder.Property(x => x.Message).HasColumnName("message").HasMaxLength(2000);
        builder.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(x => x.RequestedByUserName).HasColumnName("requested_by_user_name").HasMaxLength(100);
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64);
        builder.HasOne(x => x.DnsServer).WithMany(x => x.InventorySnapshots)
            .HasForeignKey(x => x.DnsServerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.Version).IsUnique();
        builder.HasIndex(x => new { x.DnsServerId, x.StartedAt });
        builder.HasIndex(x => x.DnsServerId).IsUnique().HasFilter("is_active");
    }
}

public sealed class DnsZoneSnapshotConfiguration : IEntityTypeConfiguration<DnsZoneSnapshot>
{
    public void Configure(EntityTypeBuilder<DnsZoneSnapshot> builder)
    {
        builder.ToTable("dns_zone_snapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.DnsInventorySnapshotId).HasColumnName("dns_inventory_snapshot_id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(253).IsRequired();
        builder.Property(x => x.ZoneType).HasColumnName("zone_type").HasMaxLength(64).IsRequired();
        builder.Property(x => x.IsReverseLookupZone).HasColumnName("is_reverse_lookup_zone");
        builder.Property(x => x.IsDsIntegrated).HasColumnName("is_ds_integrated");
        builder.Property(x => x.IsSigned).HasColumnName("is_signed");
        builder.Property(x => x.IsPaused).HasColumnName("is_paused");
        builder.Property(x => x.DynamicUpdate).HasColumnName("dynamic_update").HasMaxLength(64);
        builder.Property(x => x.ReplicationScope).HasColumnName("replication_scope").HasMaxLength(64);
        builder.Property(x => x.DirectoryPartitionName).HasColumnName("directory_partition_name").HasMaxLength(512);
        builder.Property(x => x.ZoneFile).HasColumnName("zone_file").HasMaxLength(512);
        builder.Property(x => x.VirtualizationInstance).HasColumnName("virtualization_instance").HasMaxLength(128);
        builder.Property(x => x.PropertiesJson).HasColumnName("properties_json");
        builder.HasOne(x => x.InventorySnapshot).WithMany(x => x.Zones)
            .HasForeignKey(x => x.DnsInventorySnapshotId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.DnsInventorySnapshotId, x.Name });
    }
}

public sealed class DnsRecordSnapshotConfiguration : IEntityTypeConfiguration<DnsRecordSnapshot>
{
    public void Configure(EntityTypeBuilder<DnsRecordSnapshot> builder)
    {
        builder.ToTable("dns_record_snapshots", table =>
            table.HasCheckConstraint("ck_dns_record_snapshots_ttl", "time_to_live_seconds >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.DnsZoneSnapshotId).HasColumnName("dns_zone_snapshot_id");
        builder.Property(x => x.RelativeName).HasColumnName("relative_name").HasMaxLength(253).IsRequired();
        builder.Property(x => x.FullyQualifiedName).HasColumnName("fully_qualified_name").HasMaxLength(512).IsRequired();
        builder.Property(x => x.RecordType).HasColumnName("record_type").HasMaxLength(32).IsRequired();
        builder.Property(x => x.CanonicalValue).HasColumnName("canonical_value").IsRequired();
        builder.Property(x => x.RecordDataJson).HasColumnName("record_data_json").IsRequired();
        builder.Property(x => x.TimeToLiveSeconds).HasColumnName("time_to_live_seconds");
        builder.Property(x => x.Timestamp).HasColumnName("timestamp");
        builder.Property(x => x.ZoneScope).HasColumnName("zone_scope").HasMaxLength(128);
        builder.Property(x => x.VirtualizationInstance).HasColumnName("virtualization_instance").HasMaxLength(128);
        builder.Property(x => x.RecordHash).HasColumnName("record_hash").HasMaxLength(64).IsRequired();
        builder.HasOne(x => x.ZoneSnapshot).WithMany(x => x.Records)
            .HasForeignKey(x => x.DnsZoneSnapshotId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.DnsZoneSnapshotId, x.RelativeName, x.RecordType });
        builder.HasIndex(x => x.RecordHash);
    }
}

public sealed class DnsSyncJobConfiguration : IEntityTypeConfiguration<DnsSyncJob>
{
    public void Configure(EntityTypeBuilder<DnsSyncJob> builder)
    {
        builder.ToTable("dns_sync_jobs", table =>
        {
            table.HasCheckConstraint("ck_dns_sync_jobs_attempt_count", "attempt_count >= 0");
            table.HasCheckConstraint("ck_dns_sync_jobs_priority", "priority >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.BatchId).HasColumnName("batch_id");
        builder.Property(x => x.DnsServerId).HasColumnName("dns_server_id");
        builder.Property(x => x.Scope).HasColumnName("scope").HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Trigger).HasColumnName("trigger").HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ZoneName).HasColumnName("zone_name").HasMaxLength(253);
        builder.Property(x => x.DedupeKey).HasColumnName("dedupe_key").HasMaxLength(600).IsRequired();
        builder.Property(x => x.Priority).HasColumnName("priority");
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        builder.Property(x => x.RequestedAt).HasColumnName("requested_at");
        builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.LeaseExpiresAt).HasColumnName("lease_expires_at");
        builder.Property(x => x.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(128);
        builder.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(x => x.RequestedByUserName).HasColumnName("requested_by_user_name").HasMaxLength(100);
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64);
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(64);
        builder.Property(x => x.Message).HasColumnName("message").HasMaxLength(2000);
        builder.HasOne(x => x.DnsServer).WithMany(x => x.SyncJobs)
            .HasForeignKey(x => x.DnsServerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.BatchId);
        builder.HasIndex(x => new { x.Status, x.Priority, x.RequestedAt });
        builder.HasIndex(x => x.DedupeKey).IsUnique()
            .HasFilter("status IN ('Pending', 'Running')");
    }
}

public sealed class DnsOperationLogConfiguration : IEntityTypeConfiguration<DnsOperationLog>
{
    public void Configure(EntityTypeBuilder<DnsOperationLog> builder)
    {
        builder.ToTable("dns_operation_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.DnsServerId).HasColumnName("dns_server_id");
        builder.Property(x => x.ServerDisplayName).HasColumnName("server_display_name").HasMaxLength(150);
        builder.Property(x => x.OperationType).HasColumnName("operation_type").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.ZoneName).HasColumnName("zone_name").HasMaxLength(253);
        builder.Property(x => x.RecordName).HasColumnName("record_name").HasMaxLength(512);
        builder.Property(x => x.RecordType).HasColumnName("record_type").HasMaxLength(32);
        builder.Property(x => x.RequestSummaryJson).HasColumnName("request_summary_json");
        builder.Property(x => x.BeforeSnapshotJson).HasColumnName("before_snapshot_json");
        builder.Property(x => x.AfterSnapshotJson).HasColumnName("after_snapshot_json");
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(64);
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(x => x.ActorUserName).HasColumnName("actor_user_name").HasMaxLength(100);
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(1024);
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasOne(x => x.DnsServer).WithMany().HasForeignKey(x => x.DnsServerId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.DnsServerId, x.CreatedAt });
        builder.HasIndex(x => x.OperationType);
        builder.HasIndex(x => x.ActorUserId);
    }
}
