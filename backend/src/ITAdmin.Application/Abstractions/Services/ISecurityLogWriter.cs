namespace ITAdmin.Application.Abstractions.Services;

public sealed class SecurityLogWriteRequest
{
    public string EventType { get; init; } = string.Empty;
    public string Severity { get; init; } = "Warning";
    public Guid? UserId { get; init; }
    public string? UserName { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? Description { get; init; }
}

public interface ISecurityLogWriter
{
    Task TryWriteAsync(SecurityLogWriteRequest request, CancellationToken cancellationToken = default);
}
