using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;

namespace ITAdmin.Persistence.Configurations;

public sealed class NotificationProviderSettingsConfiguration : IEntityTypeConfiguration<NotificationProviderSettings>
{
    public void Configure(EntityTypeBuilder<NotificationProviderSettings> builder)
    {
        builder.ToTable("notification_provider_settings");

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

        builder.HasIndex(x => new { x.Channel, x.ProviderKey }).IsUnique();

        builder.Property(x => x.IsEnabled)
            .HasColumnName("is_enabled")
            .HasDefaultValue(false);

        builder.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200);

        builder.Property(x => x.PublicSettingsJson)
            .HasColumnName("public_settings_json")
            .HasColumnType("text");

        builder.Property(x => x.EncryptedSecretSettingsJson)
            .HasColumnName("encrypted_secret_settings_json")
            .HasColumnType("text");

        builder.Property(x => x.LastValidatedAt).HasColumnName("last_validated_at");
        builder.Property(x => x.LastValidationStatus)
            .HasColumnName("last_validation_status")
            .HasMaxLength(32);
        builder.Property(x => x.LastValidationMessage)
            .HasColumnName("last_validation_message")
            .HasMaxLength(2000);

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
