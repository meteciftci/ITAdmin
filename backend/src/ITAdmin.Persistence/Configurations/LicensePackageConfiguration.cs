using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Persistence.Configurations;

public sealed class LicensePackageConfiguration : IEntityTypeConfiguration<LicensePackage>
{
    public void Configure(EntityTypeBuilder<LicensePackage> builder)
    {
        builder.ToTable("license_packages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.PurchaseId).HasColumnName("purchase_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();

        builder.Property(x => x.LicenseType)
            .HasColumnName("license_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();

        builder.Property(x => x.StartDate).HasColumnName("start_date");
        builder.Property(x => x.EndDate).HasColumnName("end_date");
        builder.Property(x => x.IsPerpetual).HasColumnName("is_perpetual").HasDefaultValue(false);
        builder.Property(x => x.RenewalRequired).HasColumnName("renewal_required").HasDefaultValue(false);
        builder.Property(x => x.RenewalDate).HasColumnName("renewal_date");

        builder.Property(x => x.SerialNumber).HasColumnName("serial_number").HasMaxLength(200);
        builder.Property(x => x.LicenseKey).HasColumnName("license_key").HasMaxLength(2000);
        builder.Property(x => x.LicenseAccountEmail).HasColumnName("license_account_email").HasMaxLength(250);
        builder.Property(x => x.LicensePortalUrl).HasColumnName("license_portal_url").HasMaxLength(1000);
        builder.Property(x => x.LicenseNotes).HasColumnName("license_notes").HasMaxLength(4000);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);

        builder.HasOne(x => x.Purchase)
            .WithMany(x => x.LicensePackages)
            .HasForeignKey(x => x.PurchaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.LicensePackages)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PurchaseId);
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.IsActive);
    }
}
