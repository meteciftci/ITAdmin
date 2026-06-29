using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Persistence.Configurations;

public sealed class LicensePurchaseConfiguration : IEntityTypeConfiguration<LicensePurchase>
{
    public void Configure(EntityTypeBuilder<LicensePurchase> builder)
    {
        builder.ToTable("license_purchases");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.PurchaseType)
            .HasColumnName("purchase_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(4000);
        builder.Property(x => x.PurchaseDate).HasColumnName("purchase_date");
        builder.Property(x => x.TenderNumber).HasColumnName("tender_number").HasMaxLength(100);
        builder.Property(x => x.TenderDate).HasColumnName("tender_date");
        builder.Property(x => x.DirectPurchaseNumber).HasColumnName("direct_purchase_number").HasMaxLength(100);
        builder.Property(x => x.DmoOrderNumber).HasColumnName("dmo_order_number").HasMaxLength(100);
        builder.Property(x => x.EbysNumber).HasColumnName("ebys_number").HasMaxLength(100);
        builder.Property(x => x.EbysDate).HasColumnName("ebys_date");
        builder.Property(x => x.InvoiceNumber).HasColumnName("invoice_number").HasMaxLength(100);
        builder.Property(x => x.InvoiceDate).HasColumnName("invoice_date");
        builder.Property(x => x.ContractNumber).HasColumnName("contract_number").HasMaxLength(100);
        builder.Property(x => x.ContractStartDate).HasColumnName("contract_start_date");
        builder.Property(x => x.ContractEndDate).HasColumnName("contract_end_date");

        builder.Property(x => x.SupplierCompanyId).HasColumnName("supplier_company_id");
        builder.Property(x => x.SupportCompanyId).HasColumnName("support_company_id");

        builder.Property(x => x.ActualTotalCost)
            .HasColumnName("actual_total_cost")
            .HasPrecision(18, 2);

        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(10);
        builder.Property(x => x.VatIncluded).HasColumnName("vat_included");
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(4000);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);

        builder.HasOne(x => x.SupplierCompany)
            .WithMany(x => x.SupplierPurchases)
            .HasForeignKey(x => x.SupplierCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SupportCompany)
            .WithMany(x => x.SupportPurchases)
            .HasForeignKey(x => x.SupportCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PurchaseType);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.SupplierCompanyId);
        builder.HasIndex(x => x.SupportCompanyId);
    }
}
