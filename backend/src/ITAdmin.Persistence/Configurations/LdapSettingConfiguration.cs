using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;

namespace ITAdmin.Persistence.Configurations;

public class LdapSettingConfiguration : IEntityTypeConfiguration<LdapSetting>
{
    public void Configure(EntityTypeBuilder<LdapSetting> builder)
    {
        builder.ToTable("ldap_settings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Host)
            .HasColumnName("host")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.BaseDn)
            .HasColumnName("base_dn")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.UserSearchBase)
            .HasColumnName("user_search_base")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.UserSearchFilter)
            .HasColumnName("user_search_filter")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.BindUserName)
            .HasColumnName("bind_user_name")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.BindUserDomain)
            .HasColumnName("bind_user_domain")
            .HasMaxLength(250);

        builder.Property(x => x.EncryptedBindPassword)
            .HasColumnName("encrypted_bind_password")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");

        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.IsActive);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
