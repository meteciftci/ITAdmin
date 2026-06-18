using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;

namespace ITAdmin.Persistence.Configurations;

public class PortalUserConfiguration : IEntityTypeConfiguration<PortalUser>
{
    public void Configure(EntityTypeBuilder<PortalUser> builder)
    {
        builder.ToTable("portal_users");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.DirectorySource)
            .HasColumnName("directory_source")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DirectoryObjectId)
            .HasColumnName("directory_object_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PreferredLanguage)
            .HasColumnName("preferred_language")
            .HasMaxLength(10)
            .HasDefaultValue("tr")
            .IsRequired();

        builder.Property(x => x.NationalIdEncrypted)
            .HasColumnName("national_id_encrypted");

        builder.Property(x => x.NationalIdMasked)
            .HasColumnName("national_id_masked")
            .HasMaxLength(50);

        builder.Property(x => x.UserName)
            .HasColumnName("user_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(250);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(x => x.LastLoginAt).HasColumnName("last_login_at");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");

        builder.HasIndex(x => x.UserName).IsUnique();
        builder.HasIndex(x => x.DirectoryObjectId).IsUnique();
        builder.HasIndex(x => x.Email);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
