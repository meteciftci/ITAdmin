namespace SasPortal.Application.Common.Models;

public sealed record CreateUserRequest(
    string DirectoryObjectId,
    bool IsActive,
    string? ActorUserName);
