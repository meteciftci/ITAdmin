namespace SasPortal.Application.Common.Models;

public sealed record LdapUserProfile(
    string DirectoryObjectId,
    string UserName,
    string DisplayName,
    string? Email,
    string? NationalId);
