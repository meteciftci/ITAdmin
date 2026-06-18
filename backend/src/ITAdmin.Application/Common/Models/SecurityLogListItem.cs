namespace ITAdmin.Application.Common.Models;

public sealed record SecurityLogListItem(
    Guid Id,
    string EventType,
    string Severity,
    Guid? UserId,
    string? UserName,
    string? IpAddress,
    string? UserAgent,
    string? Description,
    DateTimeOffset CreatedAt);
