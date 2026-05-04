namespace SasPortal.Api.Contracts.Users;

public sealed record UserDirectoryLookupItemResponse(
    string DirectoryObjectId,
    string UserName,
    string DisplayName,
    string? Email,
    string? NationalIdMasked,
    bool IsAlreadyPortalUser);
