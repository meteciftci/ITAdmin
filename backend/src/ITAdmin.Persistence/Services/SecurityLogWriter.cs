using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;

namespace ITAdmin.Persistence.Services;

public sealed class SecurityLogWriter(
    AppDbContext context,
    ILogger<SecurityLogWriter> logger) : ISecurityLogWriter
{
    private const int EventTypeMaxLength = 128;
    private const int SeverityMaxLength = 32;
    private const int UserNameMaxLength = 100;
    private const int IpAddressMaxLength = 64;
    private const int UserAgentMaxLength = 1024;
    private const int DescriptionMaxLength = 2000;

    public async Task TryWriteAsync(SecurityLogWriteRequest request, CancellationToken cancellationToken = default)
    {
        var securityLog = new SecurityLog
        {
            EventType = TruncateRequired(request.EventType, EventTypeMaxLength),
            Severity = TruncateRequired(request.Severity, SeverityMaxLength),
            UserId = request.UserId,
            UserName = TruncateNullable(request.UserName, UserNameMaxLength),
            IpAddress = TruncateNullable(request.IpAddress, IpAddressMaxLength),
            UserAgent = TruncateNullable(request.UserAgent, UserAgentMaxLength),
            Description = TruncateNullable(request.Description, DescriptionMaxLength),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        try
        {
            await context.SecurityLogs.AddAsync(securityLog, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            try
            {
                context.Entry(securityLog).State = EntityState.Detached;
            }
            catch (ObjectDisposedException)
            {
                // Context may already be disposed when logging from a failing scope.
            }

            logger.LogError(ex, "Failed to write security log for event {EventType}", request.EventType);
        }
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
