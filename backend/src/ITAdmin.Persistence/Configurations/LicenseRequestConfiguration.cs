using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Persistence.Configurations;

public sealed class LicenseRequestConfiguration : IEntityTypeConfiguration<LicenseRequest>
{
    public void Configure(EntityTypeBuilder<LicenseRequest> builder)
    {
        builder.ToTable("license_requests");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.RequestNumber)
            .HasColumnName("request_number")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.RequestSource)
            .HasColumnName("request_source")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.RequestDate).HasColumnName("request_date").IsRequired();
        builder.Property(x => x.ExternalRequestNumber).HasColumnName("external_request_number").HasMaxLength(100);
        builder.Property(x => x.EbysNumber).HasColumnName("ebys_number").HasMaxLength(100);
        builder.Property(x => x.EbysDate).HasColumnName("ebys_date");

        builder.Property(x => x.RequesterUnitDisplayName)
            .HasColumnName("requester_unit_display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.RequesterUnitDistinguishedName)
            .HasColumnName("requester_unit_distinguished_name")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.RequesterUnitObjectGuid)
            .HasColumnName("requester_unit_object_guid")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.RequesterManagerName).HasColumnName("requester_manager_name").HasMaxLength(200);
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(4000);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.EstimatedTotalCost)
            .HasColumnName("estimated_total_cost")
            .HasPrecision(18, 2);

        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(10);
        builder.Property(x => x.VatIncluded).HasColumnName("vat_included");
        builder.Property(x => x.CostNote).HasColumnName("cost_note").HasMaxLength(2000);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);

        builder.HasIndex(x => x.RequestNumber);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.RequestDate);
        builder.HasIndex(x => x.RequestSource);
        builder.HasIndex(x => x.RequesterUnitObjectGuid);
    }
}
