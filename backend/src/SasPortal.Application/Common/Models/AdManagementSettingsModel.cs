namespace SasPortal.Application.Common.Models;

public sealed record AdManagementSettingsModel(
    bool IsConfigured,
    bool IsEnabled,
    string? DomainFqdn,
    string? DefaultUserCreationUpnSuffix,
    string? NetbiosDomainName,
    string? DefaultNamingContext,
    string? BaseDn,
    string? UsersRootOu,
    string? DisabledUsersOu,
    string? GroupsSearchBase,
    string? ComputersSearchBase,
    IReadOnlyList<string> PreferredDomainControllers,
    bool UseSsl,
    int LdapPort,
    string? ServiceAccountUserName,
    bool HasServiceAccountPassword,
    bool PowerShellHealthEnabled,
    int PowerShellTimeoutSeconds,
    DateTime? LastValidatedAt,
    string? LastValidationStatus,
    string? LastValidationMessage);
