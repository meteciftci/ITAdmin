using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SasPortal.Domain.Entities;

namespace SasPortal.Persistence.Configurations;

public class AdOperationLogConfiguration : IEntityTypeConfiguration<AdOperationLog>
{
    public void Configure(EntityTypeBuilder<AdOperationLog> builder)
    {
        builder.ToTable("ad_operation_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.OperationType)
            .HasColumnName("operation_type")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.TargetObjectType)
            .HasColumnName("target_object_type")
            .HasMaxLength(64);

        builder.Property(x => x.TargetDistinguishedName)
            .HasColumnName("target_distinguished_name")
            .HasMaxLength(1000);

        builder.Property(x => x.TargetObjectGuid)
            .HasColumnName("target_object_guid")
            .HasMaxLength(64);

        builder.Property(x => x.TargetSamAccountName)
            .HasColumnName("target_sam_account_name")
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ErrorCode)
            .HasColumnName("error_code")
            .HasMaxLength(64);

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(2000);

        builder.Property(x => x.DomainController)
            .HasColumnName("domain_controller")
            .HasMaxLength(250);

        builder.Property(x => x.RequestSummaryJson).HasColumnName("request_summary_json");

        builder.Property(x => x.BeforeSnapshotJson).HasColumnName("before_snapshot_json");

        builder.Property(x => x.AfterSnapshotJson).HasColumnName("after_snapshot_json");

        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");

        builder.Property(x => x.ActorUserName)
            .HasColumnName("actor_user_name")
            .HasMaxLength(100);

        builder.Property(x => x.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(64);

        builder.Property(x => x.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(1024);

        builder.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(64);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.OperationType);
        builder.HasIndex(x => x.ActorUserId);
        builder.HasIndex(x => x.TargetSamAccountName);
    }
}
