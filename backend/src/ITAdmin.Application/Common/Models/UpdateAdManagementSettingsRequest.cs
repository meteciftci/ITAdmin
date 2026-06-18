using ITAdmin.Application.Common.AdManagement;

namespace ITAdmin.Application.Common.Models;

public sealed record UpdateAdManagementSettingsRequest(
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
    IReadOnlyList<string>? PreferredDomainControllers,
    string? ServiceAccountUserName,
    string? ServiceAccountPassword,
    bool ClearServiceAccountPassword,
    bool PowerShellHealthEnabled,
    int PowerShellTimeoutSeconds,
    AdManagementNotificationSettings NotificationSettings,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
