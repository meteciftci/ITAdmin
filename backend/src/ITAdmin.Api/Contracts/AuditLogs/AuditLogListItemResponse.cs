namespace ITAdmin.Api.Contracts.AuditLogs;

public sealed record AuditLogListItemResponse(
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
