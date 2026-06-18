namespace ITAdmin.Api.Contracts.Settings;

public sealed record ValidateLdapSettingsRequest
{
    public string Name { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public string BaseDn { get; init; } = string.Empty;
    public string UserSearchBase { get; init; } = string.Empty;
    public string UserSearchFilter { get; init; } = string.Empty;
    public string BindUserName { get; init; } = string.Empty;
    public string? BindUserDomain { get; init; }
    public string? BindPassword { get; init; }
    public string? TestUserName { get; init; }
    public string? TestPassword { get; init; }
}
