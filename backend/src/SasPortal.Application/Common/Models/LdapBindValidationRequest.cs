namespace SasPortal.Application.Common.Models;

public sealed record LdapBindValidationRequest
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public bool UseSsl { get; init; }
    public string BindUserName { get; init; } = string.Empty;
    public string? BindUserDomain { get; init; }
    public string BindPassword { get; init; } = string.Empty;
}
