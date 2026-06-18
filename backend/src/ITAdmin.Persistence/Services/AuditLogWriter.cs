using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;

namespace ITAdmin.Persistence.Services;

public sealed class AuditLogWriter(AppDbContext context) : IAuditLogWriter
{
    private const int ActionMaxLength = 64;
    private const int EntityNameMaxLength = 128;
    private const int EntityIdMaxLength = 128;
    private const int DescriptionMaxLength = 2000;
    private const int ActorUserNameMaxLength = 100;
    private const int IpAddressMaxLength = 64;
    private const int UserAgentMaxLength = 1024;

    public async Task WriteAsync(AuditLogWriteRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                Action = TruncateRequired(request.Action, ActionMaxLength),
                EntityName = TruncateRequired(request.EntityName, EntityNameMaxLength),
                EntityId = TruncateNullable(request.EntityId, EntityIdMaxLength),
                Description = TruncateNullable(request.Description, DescriptionMaxLength),
                ActorUserId = request.ActorUserId,
                ActorUserName = TruncateNullable(request.ActorUserName, ActorUserNameMaxLength),
                IpAddress = TruncateNullable(request.IpAddress, IpAddressMaxLength),
                UserAgent = TruncateNullable(request.UserAgent, UserAgentMaxLength),
                CreatedAt = new DateTimeOffset(now, TimeSpan.Zero),
            },
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string TruncateRequired(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? TruncateNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
