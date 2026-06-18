namespace ITAdmin.Api.Contracts.Users;

public sealed record UserDirectoryLookupResponse(
    IReadOnlyCollection<UserDirectoryLookupItemResponse> Items);
