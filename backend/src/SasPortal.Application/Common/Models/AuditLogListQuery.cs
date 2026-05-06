namespace SasPortal.Application.Common.Models;

public sealed record AuditLogListQuery(
    string? Search,
    string? Action,
    IReadOnlyList<string>? Actions,
    string? EntityName,
    IReadOnlyList<string>? EntityNames,
    Guid? ActorUserId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int PageNumber,
    int PageSize);
