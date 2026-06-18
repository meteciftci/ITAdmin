using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;

namespace ITAdmin.Persistence.Configurations;

public class AdAttributeMappingConfiguration : IEntityTypeConfiguration<AdAttributeMapping>
{
    public void Configure(EntityTypeBuilder<AdAttributeMapping> builder)
    {
        builder.ToTable("ad_attribute_mappings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.LogicalField)
            .HasColumnName("logical_field")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.AttributeName)
            .HasColumnName("attribute_name")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.IsEnabled)
            .HasColumnName("is_enabled")
            .HasDefaultValue(true);

        builder.Property(x => x.IsEditable)
            .HasColumnName("is_editable")
            .HasDefaultValue(true);

        builder.Property(x => x.IsSensitive)
            .HasColumnName("is_sensitive")
            .HasDefaultValue(false);

        builder.Property(x => x.IsSearchable)
            .HasColumnName("is_searchable")
            .HasDefaultValue(false);

        builder.Property(x => x.ValidationType)
            .HasColumnName("validation_type")
            .HasMaxLength(32)
            .IsRequired()
            .HasDefaultValue("None");

        builder.Property(x => x.MaskingStrategy)
            .HasColumnName("masking_strategy")
            .HasMaxLength(32)
            .IsRequired()
            .HasDefaultValue("None");

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order")
            .HasDefaultValue(0);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => x.LogicalField).IsUnique();
        builder.HasIndex(x => x.SortOrder);
    }
}
