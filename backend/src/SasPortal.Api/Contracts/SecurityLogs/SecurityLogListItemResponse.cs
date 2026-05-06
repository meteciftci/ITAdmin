namespace SasPortal.Api.Contracts.SecurityLogs;

public sealed record SecurityLogListItemResponse(
    Guid Id,
    string EventType,
    string Severity,
    Guid? UserId,
    string? UserName,
    string? IpAddress,
    string? UserAgent,
    string? Description,
    DateTimeOffset CreatedAt);
