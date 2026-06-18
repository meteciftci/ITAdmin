namespace ITAdmin.Application.Common.Models;

public sealed record UserListItem(
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
