namespace SasPortal.Application.Common.Models;

public sealed record SecurityLogListQuery(
    string? Search,
    IReadOnlyList<string>? EventTypes,
    IReadOnlyList<string>? Severities,
    Guid? UserId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int PageNumber,
    int PageSize);
