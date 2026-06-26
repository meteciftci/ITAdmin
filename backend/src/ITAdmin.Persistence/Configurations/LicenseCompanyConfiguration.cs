using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;

namespace ITAdmin.Persistence.Configurations;

public sealed class LicenseCompanyConfiguration : IEntityTypeConfiguration<LicenseCompany>
{
    public void Configure(EntityTypeBuilder<LicenseCompany> builder)
    {
        builder.ToTable("license_companies");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.TaxNumber).HasColumnName("tax_number").HasMaxLength(50);
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(50);
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(250);
        builder.Property(x => x.Website).HasColumnName("website").HasMaxLength(1000);
        builder.Property(x => x.Address).HasColumnName("address").HasMaxLength(2000);
        builder.Property(x => x.SupportPhone).HasColumnName("support_phone").HasMaxLength(50);
        builder.Property(x => x.SupportEmail).HasColumnName("support_email").HasMaxLength(250);
        builder.Property(x => x.ContactPersonName).HasColumnName("contact_person_name").HasMaxLength(200);
        builder.Property(x => x.ContactPersonPhone).HasColumnName("contact_person_phone").HasMaxLength(50);
        builder.Property(x => x.ContactPersonEmail).HasColumnName("contact_person_email").HasMaxLength(250);
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(4000);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);

        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.IsActive);
    }
}
