using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;

namespace ITAdmin.Persistence.Configurations;

public sealed class LicenseManagementSettingsConfiguration : IEntityTypeConfiguration<LicenseManagementSettings>
{
    public void Configure(EntityTypeBuilder<LicenseManagementSettings> builder)
    {
        builder.ToTable("license_management_settings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.DefaultCurrency)
            .HasColumnName("default_currency")
            .HasMaxLength(10)
            .HasDefaultValue("TRY")
            .IsRequired();

        builder.Property(x => x.DefaultVatIncluded)
            .HasColumnName("default_vat_included")
            .HasDefaultValue(false);

        builder.Property(x => x.DefaultRenewalReminderDays)
            .HasColumnName("default_renewal_reminder_days")
            .HasDefaultValue(60);

        builder.Property(x => x.DefaultRenewalRecipients)
            .HasColumnName("default_renewal_recipients")
            .HasMaxLength(4000);

        builder.Property(x => x.DefaultRenewalCcRecipients)
            .HasColumnName("default_renewal_cc_recipients")
            .HasMaxLength(4000);

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(4000);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);
    }
}
