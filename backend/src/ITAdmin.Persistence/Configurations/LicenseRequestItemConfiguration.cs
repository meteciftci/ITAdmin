using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Persistence.Configurations;

public sealed class LicenseRequestItemConfiguration : IEntityTypeConfiguration<LicenseRequestItem>
{
    public void Configure(EntityTypeBuilder<LicenseRequestItem> builder)
    {
        builder.ToTable("license_request_items");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.RequestId).HasColumnName("request_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.RequestedQuantity).HasColumnName("requested_quantity").IsRequired();
        builder.Property(x => x.ApprovedQuantity).HasColumnName("approved_quantity");
        builder.Property(x => x.FulfilledQuantity).HasColumnName("fulfilled_quantity").HasDefaultValue(0);

        builder.Property(x => x.EstimatedUnitCost)
            .HasColumnName("estimated_unit_cost")
            .HasPrecision(18, 2);

        builder.Property(x => x.EstimatedTotalCost)
            .HasColumnName("estimated_total_cost")
            .HasPrecision(18, 2);

        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(10);
        builder.Property(x => x.VatIncluded).HasColumnName("vat_included");
        builder.Property(x => x.Justification).HasColumnName("justification").HasMaxLength(2000);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);

        builder.HasOne(x => x.Request)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RequestId);
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => new { x.RequestId, x.ProductId }).IsUnique();
    }
}
