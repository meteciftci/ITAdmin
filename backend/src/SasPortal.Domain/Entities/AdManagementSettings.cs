using SasPortal.Domain.Common;

namespace SasPortal.Domain.Entities;

public class AdManagementSettings : AuditableEntity
{
    public bool IsEnabled { get; set; }
    public string? DomainFqdn { get; set; }
    public string? DefaultUserCreationUpnSuffix { get; set; }
    public string? NetbiosDomainName { get; set; }
    public string? DefaultNamingContext { get; set; }
    public string? BaseDn { get; set; }
    public string? UsersRootOu { get; set; }
    public string? DisabledUsersOu { get; set; }
    public string? GroupsSearchBase { get; set; }
    public string? ComputersSearchBase { get; set; }
    public string? PreferredDomainControllersJson { get; set; }
    public string? ServiceAccountUserName { get; set; }
    public string? EncryptedServiceAccountPassword { get; set; }
    public bool PowerShellHealthEnabled { get; set; }
    public int PowerShellTimeoutSeconds { get; set; } = 30;
    public DateTime? LastValidatedAt { get; set; }
    public string? LastValidationStatus { get; set; }
    public string? LastValidationMessage { get; set; }
    public string? NotificationSettingsJson { get; set; }
}
