using SasPortal.Application.Abstractions.Services;
using SasPortal.Domain.Entities;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class AuditLogWriter(AppDbContext context) : IAuditLogWriter
{
    private const int DescriptionMaxLength = 2000;
    private const int IpAddressMaxLength = 64;
    private const int UserAgentMaxLength = 1024;

    public async Task WriteAsync(AuditLogWriteRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                Action = request.Action,
                EntityName = request.EntityName,
                EntityId = request.EntityId,
                Description = Truncate(request.Description, DescriptionMaxLength),
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = TruncateNullable(request.IpAddress, IpAddressMaxLength),
                UserAgent = TruncateNullable(request.UserAgent, UserAgentMaxLength),
                CreatedAt = new DateTimeOffset(now, TimeSpan.Zero),
            },
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

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
