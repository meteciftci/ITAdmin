using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SasPortal.Domain.Entities;

namespace SasPortal.Persistence.Configurations;

public sealed class NotificationOutboxConfiguration : IEntityTypeConfiguration<NotificationOutbox>
{
    public void Configure(EntityTypeBuilder<NotificationOutbox> builder)
    {
        builder.ToTable("notification_outbox");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Channel)
            .HasColumnName("channel")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ProviderKey)
            .HasColumnName("provider_key")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Recipient)
            .HasColumnName("recipient")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.RecipientMasked)
            .HasColumnName("recipient_masked")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Subject)
            .HasColumnName("subject")
            .HasMaxLength(500);

        builder.Property(x => x.Body)
            .HasColumnName("body")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Priority).HasColumnName("priority").HasDefaultValue(0);
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
        builder.Property(x => x.MaxAttempts).HasColumnName("max_attempts").HasDefaultValue(3);

        builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(x => x.LastAttemptAt).HasColumnName("last_attempt_at");
        builder.Property(x => x.SentAt).HasColumnName("sent_at");
        builder.Property(x => x.LockedAt).HasColumnName("locked_at");
        builder.Property(x => x.LockedBy)
            .HasColumnName("locked_by")
            .HasMaxLength(200);

        builder.Property(x => x.LastErrorMessage)
            .HasColumnName("last_error_message")
            .HasMaxLength(2000);

        builder.Property(x => x.ProviderSummary)
            .HasColumnName("provider_summary")
            .HasMaxLength(500);

        builder.Property(x => x.RelatedModule)
            .HasColumnName("related_module")
            .HasMaxLength(100);

        builder.Property(x => x.RelatedEvent)
            .HasColumnName("related_event")
            .HasMaxLength(100);

        builder.Property(x => x.RelatedEntityType)
            .HasColumnName("related_entity_type")
            .HasMaxLength(100);

        builder.Property(x => x.RelatedEntityId)
            .HasColumnName("related_entity_id")
            .HasMaxLength(500);

        builder.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(200);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(200);

        builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
        builder.HasIndex(x => new { x.Channel, x.Status });
        builder.HasIndex(x => new { x.RelatedModule, x.RelatedEvent });
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.CorrelationId);
    }
}
