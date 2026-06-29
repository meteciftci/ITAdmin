using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Persistence.Configurations;

public sealed class LicenseRequestItemUserConfiguration : IEntityTypeConfiguration<LicenseRequestItemUser>
{
    public void Configure(EntityTypeBuilder<LicenseRequestItemUser> builder)
    {
        builder.ToTable("license_request_item_users");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.RequestItemId).HasColumnName("request_item_id").IsRequired();
        builder.Property(x => x.AdObjectId).HasColumnName("ad_object_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.SamAccountName).HasColumnName("sam_account_name").HasMaxLength(100);
        builder.Property(x => x.UserPrincipalName).HasColumnName("user_principal_name").HasMaxLength(250);
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        builder.Property(x => x.Department).HasColumnName("department").HasMaxLength(200);
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(x => x.Mail).HasColumnName("mail").HasMaxLength(250);
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(50);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);

        builder.HasOne(x => x.RequestItem)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.RequestItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.RequestItemId);
        builder.HasIndex(x => x.AdObjectId);
        builder.HasIndex(x => new { x.RequestItemId, x.AdObjectId }).IsUnique();
    }
}
