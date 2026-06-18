namespace ITAdmin.Application.Common.Models;

public sealed record UserDirectoryLookupItem(
    string DirectoryObjectId,
    string UserName,
    string DisplayName,
    string? Email,
    string? NationalIdMasked,
    bool IsAlreadyPortalUser);
