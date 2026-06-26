using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;

namespace ITAdmin.Persistence.Services.LicenseManagement;

internal static class LicenseManagementServiceHelpers
{
    internal const int AuditDescriptionMaxLength = 2000;
    internal const int AuditIpAddressMaxLength = 64;
    internal const int AuditUserAgentMaxLength = 1024;

    internal static (int PageNumber, int PageSize) NormalizePaging(int pageNumber, int pageSize) =>
        (
            pageNumber < 1 ? 1 : pageNumber,
            pageSize switch
            {
                < 1 => 20,
                > 100 => 100,
                _ => pageSize
            });

    internal static string BuildILikeContainsPattern(string search) =>
        $"%{search.Trim().Replace("%", "\\%").Replace("_", "\\_")}%";

    internal static async Task WriteAuditAsync(
        AppDbContext context,
        string action,
        string entityName,
        Guid entityId,
        string description,
        Guid? actorUserId,
        string? actorUserName,
        string? actorIpAddress,
        string? actorUserAgent,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                ActorUserId = actorUserId,
                ActorUserName = actorUserName,
                Action = action,
                EntityName = entityName,
                EntityId = entityId.ToString(),
                Description = Truncate(description, AuditDescriptionMaxLength),
                IpAddress = Truncate(actorIpAddress, AuditIpAddressMaxLength),
                UserAgent = Truncate(actorUserAgent, AuditUserAgentMaxLength),
                CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
            },
            cancellationToken);
    }

    internal static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
