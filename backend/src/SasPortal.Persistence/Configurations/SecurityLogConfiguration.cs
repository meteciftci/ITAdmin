using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SasPortal.Domain.Entities;

namespace SasPortal.Persistence.Configurations;

public class SecurityLogConfiguration : IEntityTypeConfiguration<SecurityLog>
{
    public void Configure(EntityTypeBuilder<SecurityLog> builder)
    {
        builder.ToTable("security_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Severity)
            .HasColumnName("severity")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.UserId).HasColumnName("user_id");

        builder.Property(x => x.UserName)
            .HasColumnName("user_name")
            .HasMaxLength(100);

        builder.Property(x => x.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(64);

        builder.Property(x => x.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(1024);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.EventType);
        builder.HasIndex(x => x.Severity);
        builder.HasIndex(x => x.UserId);
    }
}
