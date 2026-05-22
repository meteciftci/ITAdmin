using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SasPortal.Domain.Entities;

namespace SasPortal.Persistence.Configurations;

public sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notification_templates");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.ModuleKey)
            .HasColumnName("module_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.EventKey)
            .HasColumnName("event_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Channel)
            .HasColumnName("channel")
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(x => new { x.ModuleKey, x.EventKey, x.Channel }).IsUnique();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.IsEnabled)
            .HasColumnName("is_enabled")
            .HasDefaultValue(true);

        builder.Property(x => x.SubjectTemplate)
            .HasColumnName("subject_template")
            .HasMaxLength(500);

        builder.Property(x => x.BodyTemplate)
            .HasColumnName("body_template")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(200);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(200);
    }
}
