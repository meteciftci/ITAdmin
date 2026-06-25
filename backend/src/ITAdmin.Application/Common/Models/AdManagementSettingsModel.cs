using ITAdmin.Application.Common.AdManagement;

namespace ITAdmin.Application.Common.Models;

public sealed record AdManagementSettingsModel(
    bool IsConfigured,
    bool IsEnabled,
    string? DomainFqdn,
    string? DefaultUserCreationUpnSuffix,
    string? DefaultUserOu,
    string? DefaultGroupOu,
    string? DefaultComputerOu,
    string? NetbiosDomainName,
    string? DefaultNamingContext,
    string? BaseDn,
    string? UsersRootOu,
    string? DisabledUsersOu,
    string? GroupsSearchBase,
    string? ComputersSearchBase,
    IReadOnlyList<string> PreferredDomainControllers,
    string? ServiceAccountUserName,
    bool HasServiceAccountPassword,
    bool PowerShellHealthEnabled,
    int PowerShellTimeoutSeconds,
    DateTime? LastValidatedAt,
    string? LastValidationStatus,
    string? LastValidationMessage,
    AdManagementNotificationSettings NotificationSettings);
