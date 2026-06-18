namespace ITAdmin.Application.Abstractions.Services;

public sealed class AuditLogWriteRequest
{
    public string Action { get; init; } = string.Empty;
    public string EntityName { get; init; } = string.Empty;
    public string? EntityId { get; init; }
    public string Description { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }
    public string? ActorUserName { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}

public interface IAuditLogWriter
{
    Task WriteAsync(AuditLogWriteRequest request, CancellationToken cancellationToken = default);
}
