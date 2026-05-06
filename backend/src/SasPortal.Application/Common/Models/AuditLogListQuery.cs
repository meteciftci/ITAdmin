namespace SasPortal.Application.Common.Models;

public sealed record AuditLogListQuery(
    string? Search,
    string? Action,
    string? EntityName,
    Guid? ActorUserId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int PageNumber,
    int PageSize);
