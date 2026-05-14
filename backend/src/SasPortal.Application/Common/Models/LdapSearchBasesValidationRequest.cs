namespace SasPortal.Application.Common.Models;

public sealed record LdapSearchBasesValidationRequest
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public bool UseSsl { get; init; }
    public string BaseDn { get; init; } = string.Empty;
    public string UserSearchBase { get; init; } = string.Empty;
    public string BindUserName { get; init; } = string.Empty;
    public string? BindUserDomain { get; init; }
    public string BindPassword { get; init; } = string.Empty;
}
