namespace ITAdmin.Api.Contracts.Users;

public sealed record UserListItemResponse(
    Guid Id,
    string DirectorySource,
    string DirectoryObjectId,
    string UserName,
    string DisplayName,
    string? NationalIdMasked,
    string? Email,
    bool IsActive,
    DateTime? LastLoginAt,
    IReadOnlyCollection<string> Roles);
