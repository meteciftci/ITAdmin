namespace ITAdmin.Application.Common.Models;

public sealed record LdapBindValidationRequest
{
    public string Host { get; init; } = string.Empty;
    public string BindUserName { get; init; } = string.Empty;
    public string? BindUserDomain { get; init; }
    public string BindPassword { get; init; } = string.Empty;
}
