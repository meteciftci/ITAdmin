namespace SasPortal.Application.Common.Models;

public sealed record AuditLogListItem(
    Guid Id,
    string Action,
    string EntityName,
    string? EntityId,
    string? Description,
    Guid? ActorUserId,
    string? ActorUserName,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset CreatedAt);
