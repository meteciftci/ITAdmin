namespace ITAdmin.Application.Common.Models;

public sealed record CreateUserRequest(
    string DirectoryObjectId,
    bool IsActive,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
