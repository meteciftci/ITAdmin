using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;

namespace ITAdmin.Persistence.Configurations;

public sealed class LicenseRequestItemFulfillmentConfiguration
    : IEntityTypeConfiguration<LicenseRequestItemFulfillment>
{
    public void Configure(EntityTypeBuilder<LicenseRequestItemFulfillment> builder)
    {
        builder.ToTable("license_request_item_fulfillments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.RequestItemId).HasColumnName("request_item_id").IsRequired();
        builder.Property(x => x.PackageId).HasColumnName("package_id").IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);

        // Restrict deletes so the fulfillment audit trail survives package/item lifecycle changes.
        builder.HasOne(x => x.RequestItem)
            .WithMany(x => x.Fulfillments)
            .HasForeignKey(x => x.RequestItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Package)
            .WithMany(x => x.Fulfillments)
            .HasForeignKey(x => x.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RequestItemId);
        builder.HasIndex(x => x.PackageId);
    }
}
