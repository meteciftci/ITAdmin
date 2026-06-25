namespace ITAdmin.Api.Contracts.AdManagement;

public sealed record AdManagementSettingsUpdateRequest
{
    public bool IsEnabled { get; init; }
    public string? DomainFqdn { get; init; }
    public string? DefaultUserCreationUpnSuffix { get; init; }
    public string? DefaultUserOu { get; init; }
    public string? DefaultGroupOu { get; init; }
    public string? DefaultComputerOu { get; init; }
    public string? NetbiosDomainName { get; init; }
    public string? DefaultNamingContext { get; init; }
    public string? BaseDn { get; init; }
    public string? UsersRootOu { get; init; }
    public string? DisabledUsersOu { get; init; }
    public string? GroupsSearchBase { get; init; }
    public string? ComputersSearchBase { get; init; }
    public IReadOnlyList<string>? PreferredDomainControllers { get; init; }
    public string? ServiceAccountUserName { get; init; }
    public string? ServiceAccountPassword { get; init; }
    public bool ClearServiceAccountPassword { get; init; }
    public bool PowerShellHealthEnabled { get; init; }
    public int PowerShellTimeoutSeconds { get; init; } = 30;
    public AdManagementNotificationSettingsRequest NotificationSettings { get; init; } = new();
}
