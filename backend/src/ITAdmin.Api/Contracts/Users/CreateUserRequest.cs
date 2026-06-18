namespace ITAdmin.Api.Contracts.Users;

public sealed record CreateUserRequest(
    string DirectoryObjectId,
    bool IsActive);
