namespace ITAdmin.Application.Common.Models;

public sealed record UserDirectoryLookupResult(
    IReadOnlyCollection<UserDirectoryLookupItem> Items);
