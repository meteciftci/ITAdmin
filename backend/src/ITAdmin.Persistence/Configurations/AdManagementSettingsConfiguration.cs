using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;

namespace ITAdmin.Persistence.Configurations;

public class AdManagementSettingsConfiguration : IEntityTypeConfiguration<AdManagementSettings>
{
    public void Configure(EntityTypeBuilder<AdManagementSettings> builder)
    {
        builder.ToTable("ad_management_settings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.IsEnabled)
            .HasColumnName("is_enabled")
            .HasDefaultValue(false);

        builder.Property(x => x.DomainFqdn)
            .HasColumnName("domain_fqdn")
            .HasMaxLength(250);

        builder.Property(x => x.DefaultUserCreationUpnSuffix)
            .HasColumnName("default_user_creation_upn_suffix")
            .HasMaxLength(250);

        builder.Property(x => x.NetbiosDomainName)
            .HasColumnName("netbios_domain_name")
            .HasMaxLength(64);

        builder.Property(x => x.DefaultNamingContext)
            .HasColumnName("default_naming_context")
            .HasMaxLength(500);

        builder.Property(x => x.BaseDn)
            .HasColumnName("base_dn")
            .HasMaxLength(500);

        builder.Property(x => x.UsersRootOu)
            .HasColumnName("users_root_ou")
            .HasMaxLength(500);

        builder.Property(x => x.DisabledUsersOu)
            .HasColumnName("disabled_users_ou")
            .HasMaxLength(500);

        builder.Property(x => x.DefaultUserOu)
            .HasColumnName("default_user_ou")
            .HasMaxLength(500);

        builder.Property(x => x.DefaultGroupOu)
            .HasColumnName("default_group_ou")
            .HasMaxLength(500);

        builder.Property(x => x.DefaultComputerOu)
            .HasColumnName("default_computer_ou")
            .HasMaxLength(500);

        builder.Property(x => x.DeletedObjectsEnabled)
            .HasColumnName("deleted_objects_enabled")
            .HasDefaultValue(false);

        builder.Property(x => x.GroupsSearchBase)
            .HasColumnName("groups_search_base")
            .HasMaxLength(500);

        builder.Property(x => x.ComputersSearchBase)
            .HasColumnName("computers_search_base")
            .HasMaxLength(500);

        builder.Property(x => x.PreferredDomainControllersJson)
            .HasColumnName("preferred_domain_controllers_json");

        builder.Property(x => x.ServiceAccountUserName)
            .HasColumnName("service_account_user_name")
            .HasMaxLength(250);

        builder.Property(x => x.EncryptedServiceAccountPassword)
            .HasColumnName("encrypted_service_account_password")
            .HasMaxLength(2000);

        builder.Property(x => x.PowerShellHealthEnabled)
            .HasColumnName("powershell_health_enabled")
            .HasDefaultValue(false);

        builder.Property(x => x.PowerShellTimeoutSeconds)
            .HasColumnName("powershell_timeout_seconds")
            .HasDefaultValue(30);

        builder.Property(x => x.LastValidatedAt).HasColumnName("last_validated_at");

        builder.Property(x => x.LastValidationStatus)
            .HasColumnName("last_validation_status")
            .HasMaxLength(32);

        builder.Property(x => x.LastValidationMessage)
            .HasColumnName("last_validation_message")
            .HasMaxLength(2000);

        builder.Property(x => x.NotificationSettingsJson)
            .HasColumnName("notification_settings_json");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
    }
}
