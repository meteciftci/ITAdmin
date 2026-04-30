namespace SasPortal.Api.Contracts.Setup;

public sealed record ValidateLdapRequest
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public bool UseSsl { get; init; }
    public string BaseDn { get; init; } = string.Empty;
    public string UserSearchBase { get; init; } = string.Empty;
    public string UserSearchFilter { get; init; } = string.Empty;
    public string BindDn { get; init; } = string.Empty;
    public string BindPassword { get; init; } = string.Empty;
    public string TestUserName { get; init; } = string.Empty;
    public string TestPassword { get; init; } = string.Empty;
}
