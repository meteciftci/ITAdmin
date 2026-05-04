namespace SasPortal.Api.Contracts.Users;

public sealed record UserDetailResponse(
    Guid Id,
    string DirectorySource,
    string DirectoryObjectId,
    string UserName,
    string DisplayName,
    string? NationalIdMasked,
    string? Email,
    bool IsActive,
    DateTime? LastLoginAt,
    IReadOnlyCollection<string> Roles,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);
