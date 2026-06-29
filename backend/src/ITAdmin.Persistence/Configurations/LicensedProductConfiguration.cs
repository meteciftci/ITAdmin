using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;

namespace ITAdmin.Persistence.Configurations;

public sealed class LicensedProductConfiguration : IEntityTypeConfiguration<LicensedProduct>
{
    public void Configure(EntityTypeBuilder<LicensedProduct> builder)
    {
        builder.ToTable("licensed_products");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Brand)
            .HasColumnName("brand")
            .HasMaxLength(200);

        builder.Property(x => x.CategoryId)
            .HasColumnName("category_id")
            .IsRequired();

        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.CategoryId);
    }
}
