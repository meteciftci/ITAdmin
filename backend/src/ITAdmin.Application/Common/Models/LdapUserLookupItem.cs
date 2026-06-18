namespace ITAdmin.Application.Common.Models;

public sealed record LdapUserLookupItem(
    string DirectoryObjectId,
    string UserName,
    string DisplayName,
    string? Email,
    string? NationalId);
