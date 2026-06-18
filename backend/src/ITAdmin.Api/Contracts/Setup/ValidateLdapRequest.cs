namespace ITAdmin.Api.Contracts.Setup;

public sealed record ValidateLdapRequest
{
    public string Host { get; init; } = string.Empty;
    public string BaseDn { get; init; } = string.Empty;
    public string UserSearchBase { get; init; } = string.Empty;
    public string UserSearchFilter { get; init; } = string.Empty;
    public string BindUserName { get; init; } = string.Empty;
    public string? BindUserDomain { get; init; }
    public string BindPassword { get; init; } = string.Empty;
    public string TestUserName { get; init; } = string.Empty;
    public string TestPassword { get; init; } = string.Empty;
}
