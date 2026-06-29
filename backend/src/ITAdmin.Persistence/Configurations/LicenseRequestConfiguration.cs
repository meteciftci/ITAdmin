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

        builder.Property(x => x.RequestedByAdObjectId)
            .HasColumnName("requested_by_ad_object_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.RequestedBySamAccountName).HasColumnName("requested_by_sam_account_name").HasMaxLength(100);
        builder.Property(x => x.RequestedByUserPrincipalName).HasColumnName("requested_by_user_principal_name").HasMaxLength(250);
        builder.Property(x => x.RequestedByDisplayName).HasColumnName("requested_by_display_name").HasMaxLength(200);
        builder.Property(x => x.RequestedByDepartment).HasColumnName("requested_by_department").HasMaxLength(200);
        builder.Property(x => x.RequestedByTitle).HasColumnName("requested_by_title").HasMaxLength(200);
        builder.Property(x => x.RequestedByMail).HasColumnName("requested_by_mail").HasMaxLength(250);
        builder.Property(x => x.RequestedByPhone).HasColumnName("requested_by_phone").HasMaxLength(50);
        builder.Property(x => x.RequestedByManagerName).HasColumnName("requested_by_manager_name").HasMaxLength(200);
        builder.Property(x => x.RequesterUnit).HasColumnName("requester_unit").HasMaxLength(200);
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
        builder.HasIndex(x => x.RequestedByAdObjectId);
    }
}
