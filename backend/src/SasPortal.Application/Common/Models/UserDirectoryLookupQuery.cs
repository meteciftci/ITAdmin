namespace SasPortal.Application.Common.Models;

public sealed record UserDirectoryLookupQuery(
    string Search,
    int MaxResults);
